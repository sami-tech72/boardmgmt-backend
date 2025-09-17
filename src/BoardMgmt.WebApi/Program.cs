using BoardMgmt.Application;                     // AddApplication()
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Infrastructure;                  // AddInfrastructure(config)
using BoardMgmt.Infrastructure.Persistence;      // AppDbContext, DbSeeder
using BoardMgmt.WebApi.Common.Http;              // Api middleware + filters
using BoardMgmt.WebApi.Common.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;        // FormOptions
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ───────────────────────────────────────────────────────────────────────────────
// Services
// ───────────────────────────────────────────────────────────────────────────────

// Infrastructure (DbContext, Identity, JwtTokenService)
builder.Services.AddInfrastructure(config);

// Application (MediatR, Validators, Pipeline)
builder.Services.AddApplication();

// JWT auth
var issuer = config["Jwt:Issuer"] ?? "BoardMgmt";
var audience = config["Jwt:Audience"] ?? "BoardMgmt.Client";
var key = config["Jwt:Key"] ?? "super-secret-key-change-me";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
    });

builder.Services.AddAuthorization();

// CORS (named policy used by your UI)
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("ui", p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithOrigins(
            config.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200", "http://localhost:4000" }
        )
    );
});

// Controllers / Swagger + uniform response setup
builder.Services
    .AddControllers(o =>
    {
        // uniform ModelState response
        o.Filters.Add<InvalidModelStateFilter>();
    })
    .ConfigureApiBehaviorOptions(o => o.SuppressModelStateInvalidFilter = true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Global exception formatter (DI)
builder.Services.AddSingleton<ExceptionHandlingMiddleware>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Large upload support (e.g., document uploads up to 50 MB)
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
});

// ───────────────────────────────────────────────────────────────────────────────
// App
// ───────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Ensure wwwroot/uploads exists (so StaticFiles can serve uploaded files)
var webRoot = app.Environment.WebRootPath;
if (string.IsNullOrWhiteSpace(webRoot))
{
    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
}
Directory.CreateDirectory(Path.Combine(webRoot, "uploads"));

// Auto-migrate + seed demo data
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
    await DbSeeder.SeedAsync(sp, logger);
}

// Swagger in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global exception handler FIRST so it can catch downstream exceptions
app.UseMiddleware<ExceptionHandlingMiddleware>();

// IMPORTANT: No HTTPS redirect in Development (prevents status 0 on HTTP calls)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Serve files from wwwroot (including /uploads/*)
app.UseStaticFiles();

app.UseCors("ui");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
