using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Infrastructure.Auth;
using BoardMgmt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BoardMgmt.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("DefaultConnection")
                 ?? "Server=localhost\\SQLEXPRESS;Database=BoardMgmtDb;User=sa;Password=Admin@123;Trusted_Connection=True;TrustServerCertificate=True;";

        // DbContexts (concrete + mapping for Application's DbContext)
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(cs, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddDbContext<DbContext, AppDbContext>(opt =>
            opt.UseSqlServer(cs, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        // Identity
        services.AddIdentityCore<AppUser>(o =>
        {
            o.Password.RequiredLength = 6;
            o.Password.RequireDigit = true;
            o.Password.RequireNonAlphanumeric = false;
            o.Password.RequireUppercase = false;
            o.Password.RequireLowercase = false;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>();

        // JWT service
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
