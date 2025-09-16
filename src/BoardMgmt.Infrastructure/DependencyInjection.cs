using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Infrastructure.Auth;
using BoardMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;


namespace BoardMgmt.Infrastructure;


public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("DefaultConnection")
        ?? "Server=SAMI-PC\\SQLEXPRESS;Database=BoardMgmtDb;User=sa;Password=Admin@123;Trusted_Connection=True;TrustServerCertificate=True;";


        services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlServer(cs, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));


        services.AddIdentityCore<AppUser>()
        .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>();


        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<DbContext, AppDbContext>();
        return services;
    }
}