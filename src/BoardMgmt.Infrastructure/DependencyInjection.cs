// File: src/BoardMgmt.Infrastructure/DependencyInjection.cs
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using BoardMgmt.Infrastructure.Auth;
using BoardMgmt.Infrastructure.Files;
using BoardMgmt.Infrastructure.Identity;
using BoardMgmt.Infrastructure.Persistence;
using BoardMgmt.Infrastructure.Persistence.Repositories;
using BoardMgmt.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

namespace BoardMgmt.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            // --- Connection string (fallback safe for local dev) ---
            var cs = config.GetConnectionString("DefaultConnection")
                     ?? "Server=localhost\\SQLEXPRESS;Database=BoardMgmtDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

            // --- DbContext with robust SQL Server + logging setup ---
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseSqlServer(cs, sql =>
                {
                    sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                    sql.CommandTimeout(60);
                });

                // Route EF logs through Microsoft.Extensions.Logging (which Program.cs sends to Serilog)
                var env = sp.GetRequiredService<IHostEnvironment>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                options.UseLoggerFactory(loggerFactory);

                options.EnableDetailedErrors();

                // Show parameter values only in Development (avoid secrets in prod logs)
                if (env.IsDevelopment())
                    options.EnableSensitiveDataLogging();

                // Optional: fine-grained EF event selection
                options.LogTo(
                    // Send EF messages to MEL -> Serilog
                    message =>
                    {
                        var efLogger = loggerFactory.CreateLogger("EFCore");
                        efLogger.LogInformation("{EFCoreMessage}", message);
                    },
                    new[]
                    {
                        RelationalEventId.CommandExecuting,
                        RelationalEventId.CommandExecuted,
                        RelationalEventId.CommandError,
                        RelationalEventId.ConnectionOpened,
                        RelationalEventId.ConnectionClosed,
                        CoreEventId.SaveChangesStarting,
                        CoreEventId.SaveChangesCompleted
                    },
                    LogLevel.Information
                );
            });

            // Expose the abstraction if you use it
            services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

            // --- ASP.NET Core Identity ---
            services
                .AddIdentityCore<AppUser>(o =>
                {
                    o.User.RequireUniqueEmail = true;

                    // Keep strong enough to satisfy your seeding password
                    o.Password.RequiredLength = 8;
                    o.Password.RequireDigit = true;
                    o.Password.RequireLowercase = true;
                    o.Password.RequireUppercase = true;
                    o.Password.RequireNonAlphanumeric = true;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            // --- Auth / Permissions ---
            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IIdentityUserReader, IdentityUserReader>();

            // Some handlers may still take DbContext directly:
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

            // Current user accessor
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();

            // ONE PermissionService per scope, exposed via both interfaces
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IRolePermissionStore>(sp =>
                (IRolePermissionStore)sp.GetRequiredService<IPermissionService>());

            // File storage
            //services.AddSingleton<IFileStorage, LocalFileStorage>();
            //services.AddScoped<IFileStorage, DiskFileStorage>();

            // SINGLE storage provider
            services.AddSingleton<IFileStorage, LocalFileStorage>();

            services.AddScoped<IMeetingReadRepository, MeetingReadRepository>();
            services.AddScoped<IDocumentReadRepository, DocumentReadRepository>();
            services.AddScoped<IVoteReadRepository, VoteReadRepository>();
            services.AddScoped<IActivityReadRepository, ActivityReadRepository>();
            services.AddScoped<IUserReadRepository, UserReadRepository>();

            return services;
        }
    }
}
