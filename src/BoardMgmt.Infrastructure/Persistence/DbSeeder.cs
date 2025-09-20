using BoardMgmt.Domain.Auth;                  // Permission, AppModule
using BoardMgmt.Domain.Entities;             // AppUser, RolePermission (assumed here)
using BoardMgmt.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BoardMgmt.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<AppUser>>();
        var cfg = sp.GetRequiredService<IConfiguration>();

        // 1) Apply pending migrations (safe every start)
        await db.Database.MigrateAsync();

        // 2) Ensure Admin role
        if (!await roleMgr.RoleExistsAsync(AppUser.Admin))
        {
            var r = await roleMgr.CreateAsync(new IdentityRole(AppUser.Admin));
            if (!r.Succeeded)
            {
                var msg = string.Join("; ", r.Errors.Select(e => $"{e.Code}: {e.Description}"));
                logger.LogWarning("Creating role '{Role}' failed: {Errors}", AppUser.Admin, msg);
            }
        }

        // 3) Ensure admin user (email/pwd from config or defaults)
        var adminEmail = cfg["Seed:AdminEmail"] ?? "admin@flora.local";
        var adminPwd = cfg["Seed:AdminPassword"] ?? "Admin@123";

        async Task<AppUser> EnsureUserAsync(string email, string pwd)
        {
            var u = await userMgr.FindByEmailAsync(email);
            if (u is null)
            {
                u = new AppUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };
                var c = await userMgr.CreateAsync(u, pwd);
                if (!c.Succeeded)
                {
                    var msg = string.Join("; ", c.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    throw new InvalidOperationException("Failed to create admin user: " + msg);
                }
            }
            return u!;
        }

        async Task EnsureInRoleAsync(AppUser u, string role)
        {
            if (!await userMgr.IsInRoleAsync(u, role))
            {
                var a = await userMgr.AddToRoleAsync(u, role);
                if (!a.Succeeded)
                {
                    var msg = string.Join("; ", a.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    logger.LogWarning("Adding user to role '{Role}' failed: {Errors}", role, msg);
                }
            }
        }

        var adminUser = await EnsureUserAsync(adminEmail, adminPwd);
        await EnsureInRoleAsync(adminUser, AppUser.Admin);

        // 4) Grant FULL RolePermissions to Admin for every AppModule (upsert)
        var allPerms =
            Permission.View | Permission.Create | Permission.Update |
            Permission.Delete | Permission.Page | Permission.Clone;

        var adminRole = await roleMgr.FindByNameAsync(AppUser.Admin)
                        ?? throw new InvalidOperationException("Admin role not found after creation.");

        foreach (var module in Enum.GetValues<AppModule>())
        {
            var existing = await db.RolePermissions
                .FirstOrDefaultAsync(x => x.RoleId == adminRole.Id && x.Module == module);

            if (existing is null)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    Module = module,
                    Allowed = allPerms
                });
            }
            else
            {
                // keep it idempotent; always set to full
                existing.Allowed = allPerms;
            }
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Seeder done: ensured Admin role, user {Email}, and full RolePermissions across all modules.",
            adminEmail);
    }
}
