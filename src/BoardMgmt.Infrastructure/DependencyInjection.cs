using BoardMgmt.Application.Common.Identity;
using BoardMgmt.Application.Common.Interfaces;            // <-- add
using BoardMgmt.Infrastructure.Auth;
using BoardMgmt.Infrastructure.Persistence;
using BoardMgmt.Infrastructure.Storage;
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
                 // Pick one style (Windows auth OR SQL auth) and keep it consistent:
                 ?? "Server=SAMI-PC\\SQLEXPRESS;Database=BoardMgmtDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

        // Concrete context
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(cs, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        // Optional: also resolve EF's DbContext as AppDbContext
        services.AddDbContext<DbContext, AppDbContext>(opt =>
            opt.UseSqlServer(cs, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        // 🔑 Map the interface used by Application handlers
        services.AddScoped<IAppDbContext, AppDbContext>();

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
        services.AddScoped<IIdentityUserReader, IdentityUserReader>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        // JWT service
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
