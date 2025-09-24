using BoardMgmt.Application;
using BoardMgmt.Application.Common.Interfaces; // IFileStorage
using BoardMgmt.Domain.Identity;
using BoardMgmt.Infrastructure;
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
);

/// Infrastructure then Application
builder.Services.AddInfrastructure(config);
builder.Services.AddApplication();

// ---- File storage provider selection ----
builder.Services.RemoveAll<IFileStorage>(); // remove default if Infrastructure registered one
var uploadsProvider = builder.Configuration["Uploads:Provider"] ?? "Local";

if (uploadsProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IFileStorage, BoardMgmt.Infrastructure.Storage.LocalFileStorage>();
}
else
{
    builder.Services.AddSingleton<IFileStorage, BoardMgmt.Infrastructure.Files.DiskFileStorage>();
}

// JWT
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
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("ui", p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowed(_ => true)
        .WithOrigins(
            config.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200", "https://localhost:4200" }
        ));
});

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

builder.Services
    .AddControllers(o => o.Filters.Add<InvalidModelStateFilter>())
    .ConfigureApiBehaviorOptions(o => o.SuppressModelStateInvalidFilter = true);

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BoardMgmt.WebApi v1");
    });
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseCors("ui");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<MessagesHub>("/hubs/messages");
app.Run();
