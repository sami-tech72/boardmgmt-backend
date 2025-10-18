// File: src/BoardMgmt.Infrastructure/DependencyInjection.cs
using Azure.Identity;
using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Email;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Application.Common.Options;
using BoardMgmt.Domain.Calendars;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using BoardMgmt.Infrastructure.Auth;
using BoardMgmt.Infrastructure.Calendars;
using BoardMgmt.Infrastructure.Email;
using BoardMgmt.Infrastructure.Files;
using BoardMgmt.Infrastructure.Graph;
using BoardMgmt.Infrastructure.Identity;
using BoardMgmt.Infrastructure.Persistence;
using BoardMgmt.Infrastructure.Persistence.Repositories;
using BoardMgmt.Infrastructure.Persistence.Seed;
using BoardMgmt.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;
using System.IO;
using GraphCalendarOptions = BoardMgmt.Infrastructure.Calendars.GraphOptions;
using GraphIntegrationOptions = BoardMgmt.Infrastructure.Graph.GraphOptions;

namespace BoardMgmt.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            // Bind shared options from configuration so application handlers can request them.
            services.Configure<AppOptions>(config.GetSection("App"));
            services.Configure<SmtpOptions>(config.GetSection("Smtp"));

            // --- Connection string (fallback safe for local dev) ---
            var (connectionString, useSqlite) = ConnectionStringHelper.Resolve(
                config.GetConnectionString("DefaultConnection"),
                AppContext.BaseDirectory);

            // --- DbContext with robust SQL Server + logging setup ---
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                if (useSqlite)
                {
                    options.UseSqlite(connectionString, sqlite =>
                    {
                        sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    });
                }
                else
                {
                    options.UseSqlServer(connectionString, sql =>
                    {
                        sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                        sql.CommandTimeout(60);
                    });
                }

                // Route EF logs through Microsoft.Extensions.Logging (which Program.cs sends to Serilog)
                var env = sp.GetRequiredService<IHostEnvironment>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                options.UseLoggerFactory(loggerFactory);

                options.EnableDetailedErrors();

                // Show parameter values only in Development (avoid secrets in prod logs)
                if (env.IsDevelopment())
                    options.EnableSensitiveDataLogging();

                // Use the built-in EF Core logging category so Serilog configuration
                // (MinimumLevel.Override["Microsoft.EntityFrameworkCore"] = Warning)
                // can suppress high-volume messages such as CommandExecuted from being
                // written to the Logs table.  Create the logger once instead of per
                // message to avoid unnecessary allocations.
                var efLogger = loggerFactory.CreateLogger("Microsoft.EntityFrameworkCore");

                options.LogTo(
                    message => efLogger.LogInformation("{EFCoreMessage}", message),
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
                    env.IsDevelopment() ? LogLevel.Information : LogLevel.Warning
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


            // Current user accessor
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();
            services.AddScoped<Func<ICurrentUser?>>(
                sp =>
                {
                    var serviceProvider = sp;
                    return () => serviceProvider.GetService<ICurrentUser>();
                });

            // ONE PermissionService per scope, exposed via both interfaces
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IRolePermissionStore>(sp =>
                (IRolePermissionStore)sp.GetRequiredService<IPermissionService>());

            // File storage
            services.AddSingleton<IFileStorage, LocalFileStorage>();

            // Outbound email
            services.AddScoped<IEmailSender, SmtpEmailSender>();

            services.AddScoped<IMeetingReadRepository, MeetingReadRepository>();
            services.AddScoped<IDocumentReadRepository, DocumentReadRepository>();
            services.AddScoped<IVoteReadRepository, VoteReadRepository>();
            services.AddScoped<IActivityReadRepository, ActivityReadRepository>();
            services.AddScoped<IUserReadRepository, UserReadRepository>();

            services.AddCalendarIntegrations(config);

            return services;
        }


        private static IServiceCollection AddCalendarIntegrations(this IServiceCollection services, IConfiguration config)
        {
            // Options
            services.Configure<GraphCalendarOptions>(config.GetSection("Graph"));
            services.Configure<GraphIntegrationOptions>(config.GetSection("Graph"));
            services.Configure<ZoomOptions>(config.GetSection("Zoom"));


            // Graph client (app-only)
            var graphOpts = config.GetSection("Graph").Get<GraphCalendarOptions>()!;
            var credential = new ClientSecretCredential(graphOpts.TenantId, graphOpts.ClientId, graphOpts.ClientSecret);
            var graphClient = new GraphServiceClient(credential);
            services.AddSingleton(graphClient);

            services.AddSingleton<IGraphSubscriptionManager, GraphSubscriptionManager>();


            // Zoom HttpClient
            services.AddHttpClient("Zoom", client =>
            {
                client.BaseAddress = new Uri("https://api.zoom.us/v2/");
                client.Timeout = TimeSpan.FromMinutes(5);
                // BaseAddress optional; we send absolute URLs.
            });




           

            // Register token provider (scoped or singleton; scoped is fine)
            services.AddScoped<IZoomTokenProvider, ZoomTokenProvider>();


            // Concrete services
            services.AddSingleton<Microsoft365CalendarService>();
            services.AddSingleton<ZoomCalendarService>();


            // Selector registration (map provider keys to services)
            services.AddSingleton<ICalendarServiceSelector>(sp => new CalendarServiceSelector(new[]
            {
                new KeyValuePair<string, ICalendarService>(CalendarProviders.Microsoft365, sp.GetRequiredService<Microsoft365CalendarService>()),
                new KeyValuePair<string, ICalendarService>(CalendarProviders.Zoom, sp.GetRequiredService<ZoomCalendarService>())
            }));


            return services;
        }


        public static async Task InitializeInfrastructureAsync(this IServiceProvider services, ILogger logger)
        {
            using (var scope = services.CreateScope())
            {
                var scopedProvider = scope.ServiceProvider;
                var db = scopedProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                await DepartmentSeeder.SeedAsync(db);
            }

            await DbSeeder.SeedAsync(services, logger);
        }
    }
}
