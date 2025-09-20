using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Infrastructure.Auth;
using BoardMgmt.Infrastructure.Identity;
using BoardMgmt.Infrastructure.Persistence;
using BoardMgmt.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BoardMgmt.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            var cs = config.GetConnectionString("DefaultConnection")
                     ?? "Server=localhost\\SQLEXPRESS;Database=BoardMgmtDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

            services.AddDbContext<AppDbContext>(opt =>
                opt.UseSqlServer(cs, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

            services.AddScoped<IAppDbContext, AppDbContext>();

            services
                .AddIdentityCore<AppUser>(o =>
                {
                    o.User.RequireUniqueEmail = true;

                    // Keep this strong enough to pass your seed password
                    o.Password.RequiredLength = 8;
                    o.Password.RequireDigit = true;
                    o.Password.RequireLowercase = true;
                    o.Password.RequireUppercase = true;
                    o.Password.RequireNonAlphanumeric = true;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IJwtTokenService, JwtTokenService>();

            services.AddScoped<IIdentityUserReader, IdentityUserReader>();

            // If some handlers still take DbContext directly:
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

            // Current user accessor
            services.AddScoped<ICurrentUser, CurrentUser>();

            // 🔑 ONE PermissionService instance per scope, exposed via both interfaces
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IRolePermissionStore>(sp =>
                (IRolePermissionStore)sp.GetRequiredService<IPermissionService>());

            services.AddSingleton<IFileStorage, LocalFileStorage>();
            return services;
        }
    }
}
