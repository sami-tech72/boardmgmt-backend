using Azure.Identity;
using BoardMgmt.Application;
using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Email;
using BoardMgmt.Application.Common.Interfaces; // IFileStorage
using BoardMgmt.Domain.Identity;
using BoardMgmt.Infrastructure;
using BoardMgmt.Infrastructure.Calendars;
using BoardMgmt.Infrastructure.Email;
using BoardMgmt.Infrastructure.Graph;
using BoardMgmt.Application.Common.Options;
using BoardMgmt.Infrastructure.Persistence;
using BoardMgmt.Infrastructure.Persistence.Seed;
using BoardMgmt.WebApi.Auth;
using BoardMgmt.WebApi.Common.Http;
using BoardMgmt.WebApi.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions; // for RemoveAll
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ✅ Serilog logging
builder.Logging.ClearProviders();
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    // optional enrichers if you installed packages:
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
);


// ---- DI: Infrastructure then Application ----
builder.Services.AddInfrastructure(config);
builder.Services.AddApplication();

// ---- File storage provider selection ----
//builder.Services.RemoveAll<IFileStorage>(); // remove default if Infrastructure registered one
//var uploadsProvider = builder.Configuration["Uploads:Provider"] ?? "Local";

//if (uploadsProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
//{
//    builder.Services.AddSingleton<IFileStorage, BoardMgmt.Infrastructure.Storage.LocalFileStorage>();
//}
//else
//{
//    builder.Services.AddSingleton<IFileStorage, BoardMgmt.Infrastructure.Files.DiskFileStorage>();
//}

// ---- JWT ----
var issuer = config["Jwt:Issuer"] ?? "BoardMgmt";
var audience = config["Jwt:Audience"] ?? "BoardMgmt.Client";
var key = config["Jwt:Key"] ?? "super-secret-key-change-me";

builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false; // dev only
        o.SaveToken = true;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };

        // >>> allow SignalR to send token via access_token in query
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx => {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/chat"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };

    });

// ---- SignalR ----
builder.Services.AddSignalR();

// ---- Authorization / policies ----
builder.Services.AddAuthorization();


// Bind options
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("App"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

// Use SMTP mailer
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

var authz = builder.Services.AddAuthorizationBuilder();

void AddModulePolicies(string name, AppModule m)
{
    var key = ((int)m).ToString();

    authz.AddPolicy($"{name}.Page", p => p.Requirements.Add(new PermissionRequirement(key, Permission.Page)));
    authz.AddPolicy($"{name}.View", p => p.Requirements.Add(new PermissionRequirement(key, Permission.View)));
    authz.AddPolicy($"{name}.Create", p => p.Requirements.Add(new PermissionRequirement(key, Permission.Create)));
    authz.AddPolicy($"{name}.Update", p => p.Requirements.Add(new PermissionRequirement(key, Permission.Update)));
    authz.AddPolicy($"{name}.Delete", p => p.Requirements.Add(new PermissionRequirement(key, Permission.Delete)));
}

AddModulePolicies("Users", AppModule.Users);
AddModulePolicies("Meetings", AppModule.Meetings);
AddModulePolicies("Documents", AppModule.Documents);
AddModulePolicies("Folders", AppModule.Folders);
AddModulePolicies("Votes", AppModule.Votes);
AddModulePolicies("Dashboard", AppModule.Dashboard);
AddModulePolicies("Settings", AppModule.Settings);
AddModulePolicies("Reports", AppModule.Reports);
AddModulePolicies("Messages", AppModule.Messages);

builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();


builder.Services.AddCalendarIntegrations(builder.Configuration);
// ---- MVC + filters ----
builder.Services
    .AddControllers(o => o.Filters.Add<InvalidModelStateFilter>())
    .ConfigureApiBehaviorOptions(o => o.SuppressModelStateInvalidFilter = true);

// ---- CORS (SignalR needs credentials + explicit origins) ----
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("ui", p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithOrigins(
            config.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200", "https://localhost:4200" }
        )
    );
});

// ---- Swagger (fix name collisions + JWT) ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.CustomSchemaIds(t => t.FullName?.Replace('+', '.'));
    opt.SwaggerDoc("v1", new OpenApiInfo { Title = "BoardMgmt.WebApi", Version = "v1" });

    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT}"
    });
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xml);
    if (File.Exists(xmlPath))
        opt.IncludeXmlComments(xmlPath);
});

// ---- Misc ----
builder.Services.AddSingleton<ExceptionHandlingMiddleware>();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = 50 * 1024 * 1024; });

var app = builder.Build();

// Ensure /wwwroot/uploads exists
var webRoot = app.Environment.WebRootPath;
if (string.IsNullOrWhiteSpace(webRoot))
    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(Path.Combine(webRoot, "uploads"));

// auto-migrate + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DepartmentSeeder.SeedAsync(db);
}

await DbSeeder.SeedAsync(app.Services, app.Logger);

// ---- Swagger only in Development ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BoardMgmt.WebApi v1");
    });
}

// ---- Pipeline ----
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("ui");            // CORS BEFORE auth
app.UseAuthentication();      // then auth
app.UseAuthorization();       // then authorization

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");


app.Run();
