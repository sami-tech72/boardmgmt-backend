using BoardMgmt.Infrastructure;                  // for AddInfrastructure
using BoardMgmt.Infrastructure.Persistence;      // AppDbContext (if you need the type later)
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------------- Infrastructure (DbContext, Identity, JwtTokenService)
builder.Services.AddInfrastructure(config);

// ---------------- JWT auth values
var issuer = config["Jwt:Issuer"] ?? "BoardMgmt";
var audience = config["Jwt:Audience"] ?? "BoardMgmt.Client";
var key = config["Jwt:Key"] ?? "super-secret-key-change-me";

// ---------------- Authentication / Authorization
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
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

// ---------------- CORS
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("ui", p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithOrigins(config.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:4200", "http://localhost:4000" }));
});

// ---------------- MVC / Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------------- DB migrate + seed (optional)
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var userMgr = sp.GetRequiredService<UserManager<AppUser>>();
    var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();

    if (!await roleMgr.RoleExistsAsync("Admin")) await roleMgr.CreateAsync(new IdentityRole("Admin"));
    if (!await roleMgr.RoleExistsAsync("BoardMember")) await roleMgr.CreateAsync(new IdentityRole("BoardMember"));

    var admin = await userMgr.FindByEmailAsync("admin@board.local");
    if (admin is null)
    {
        admin = new AppUser { UserName = "admin@board.local", Email = "admin@board.local" };
        await userMgr.CreateAsync(admin, "P@ssw0rd!");
        await userMgr.AddToRoleAsync(admin, "Admin");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ui");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
