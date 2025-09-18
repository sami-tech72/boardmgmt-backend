using BoardMgmt.Application;
using BoardMgmt.Infrastructure;
using BoardMgmt.Infrastructure.Persistence;
using BoardMgmt.WebApi.Common.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Infrastructure then Application
builder.Services.AddInfrastructure(config);
builder.Services.AddApplication(); // only once

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

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("ui", p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithOrigins(config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:4200", "http://localhost:4000" }));
});

builder.Services
    .AddControllers(o => o.Filters.Add<InvalidModelStateFilter>())
    .ConfigureApiBehaviorOptions(o => o.SuppressModelStateInvalidFilter = true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ExceptionHandlingMiddleware>();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = 50 * 1024 * 1024; });

var app = builder.Build();

// Static files (uploads)
var webRoot = app.Environment.WebRootPath;
if (string.IsNullOrWhiteSpace(webRoot)) webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(Path.Combine(webRoot, "uploads"));

// auto-migrate + seed
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
    await DbSeeder.SeedAsync(sp, logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseCors("ui");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
