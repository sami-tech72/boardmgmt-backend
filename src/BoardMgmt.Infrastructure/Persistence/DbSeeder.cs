using System;
using System.Linq;
using System.Threading.Tasks;
using BoardMgmt.Domain.Auth;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BoardMgmt.Infrastructure.Persistence;

public static class DbSeeder
{
    private static readonly Permission All =
        Permission.View | Permission.Create | Permission.Update |
        Permission.Delete | Permission.Page | Permission.Clone;

    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<AppUser>>();

        // 1) migrate
        await db.Database.MigrateAsync();

        // 2) roles
        foreach (var role in AppRoles.All)
            if (!await roleMgr.RoleExistsAsync(role))
                _ = await roleMgr.CreateAsync(new IdentityRole(role));

        // 3) users
        async Task<AppUser> EnsureUserAsync(string email, string? pwd = "P@ssw0rd!")
        {
            var u = await userMgr.FindByEmailAsync(email);
            if (u is null)
            {
                u = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
                await userMgr.CreateAsync(u, pwd!);
            }
            return u!;
        }
        async Task EnsureInRoleAsync(AppUser u, string role)
        {
            if (!await userMgr.IsInRoleAsync(u, role))
                await userMgr.AddToRoleAsync(u, role);
        }

        var admin = await EnsureUserAsync("admin@board.local");
        var secretary = await EnsureUserAsync("secretary@board.local");
        var boardUser = await EnsureUserAsync("board@board.local");
        var committee = await EnsureUserAsync("committee@board.local");
        var observer = await EnsureUserAsync("observer@board.local");

        await EnsureInRoleAsync(admin, AppRoles.Admin);
        await EnsureInRoleAsync(secretary, AppRoles.Secretary);
        await EnsureInRoleAsync(boardUser, AppRoles.BoardMember);
        await EnsureInRoleAsync(committee, AppRoles.CommitteeMember);
        await EnsureInRoleAsync(observer, AppRoles.Observer);

        // 4) folders
        if (!await db.Folders.AnyAsync())
        {
            db.Folders.AddRange(
                new Folder { Name = "Board Meetings", Slug = "board-meetings" },
                new Folder { Name = "Financial Reports", Slug = "financial" },
                new Folder { Name = "Legal Documents", Slug = "legal" },
                new Folder { Name = "Policies", Slug = "policies" }
            );
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded default folders.");
        }

        // 5) meetings + docs
        if (!await db.Meetings.AnyAsync())
        {
            DateTimeOffset L(int d, int h) => new(DateTime.Today.AddDays(d).AddHours(h));

            var mBoard = new Meeting
            {
                Title = "Board of Directors Meeting",
                Description = "Quarterly Review & Strategic Planning",
                Type = MeetingType.Board,
                ScheduledAt = L(3, 14),
                EndAt = L(3, 16),
                Location = "Conference Room A",
                Status = MeetingStatus.Scheduled,
                Attendees =
                {
                    new MeetingAttendee { UserId = admin.Id,     Name = "Admin",        Role = "Chairman",  IsRequired = true, IsConfirmed = true },
                    new MeetingAttendee { UserId = boardUser.Id, Name = "Board Member", Role = "Director",  IsRequired = true, IsConfirmed = true },
                    new MeetingAttendee { UserId = secretary.Id, Name = "Secretary",    Role = "Secretary", IsRequired = true, IsConfirmed = true }
                },
                AgendaItems =
                {
                    new AgendaItem { Title = "Q4 Financials", Description = "Review P&L and Balance Sheet", Order = 1 },
                    new AgendaItem { Title = "2025 Roadmap",  Description = "Strategic initiatives discussion", Order = 2 }
                },
                Documents =
                {
                    new Document
                    {
                        OriginalName = "Q4-Financials.pdf",
                        FileName     = "Q4-Financials.pdf",
                        Url          = "https://example.com/q4.pdf",
                        ContentType  = "application/pdf",
                        SizeBytes    = 2_500_000,
                        FolderSlug   = "financial"
                    },
                    new Document
                    {
                        OriginalName = "Strategy-Deck.pptx",
                        FileName     = "Strategy-Deck.pptx",
                        Url          = "https://example.com/strategy.pptx",
                        ContentType  = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                        SizeBytes    = 5_700_000,
                        FolderSlug   = "board-meetings"
                    }
                }
            };

            var mFinance = new Meeting
            {
                Title = "Finance Committee",
                Description = "Budget Review & Approval",
                Type = MeetingType.Committee,
                ScheduledAt = L(6, 10),
                EndAt = L(6, 12),
                Location = "https://zoom.us/j/1234567890",
                Status = MeetingStatus.Scheduled,
                Attendees =
                {
                    new MeetingAttendee { UserId = committee.Id, Name = "Committee Member", Role = "Member",   IsRequired = true,  IsConfirmed = false },
                    new MeetingAttendee { UserId = admin.Id,     Name = "Admin",            Role = "Observer", IsRequired = false, IsConfirmed = true  }
                },
                AgendaItems =
                {
                    new AgendaItem { Title = "Operating Budget", Order = 1 },
                    new AgendaItem { Title = "CapEx Plan",       Order = 2 }
                }
            };

            var mPast = new Meeting
            {
                Title = "Strategic Planning Session",
                Description = "2025 Roadmap Discussion",
                Type = MeetingType.Board,
                ScheduledAt = L(-2, 15),
                EndAt = L(-2, 17),
                Location = "Conference Room B",
                Status = MeetingStatus.Completed
            };

            db.Meetings.AddRange(mBoard, mFinance, mPast);
            await db.SaveChangesAsync();

            // update folder counters
            var counts = await db.Documents.GroupBy(d => d.FolderSlug)
                .Select(g => new { Slug = g.Key, Count = g.Count() }).ToListAsync();
            foreach (var f in db.Folders)
                f.DocumentCount = counts.FirstOrDefault(c => c.Slug == f.Slug)?.Count ?? 0;

            await db.SaveChangesAsync();
            logger.LogInformation("Seeded meetings and updated folder counters.");
        }

        // 6) Role permissions — de-dup using one SQL statement (avoids GroupBy translation issues)
        await db.Database.ExecuteSqlRawAsync(@"
                ;WITH d AS (
                    SELECT
                        Id, RoleId, Module, Allowed,
                        ROW_NUMBER() OVER (PARTITION BY RoleId, Module ORDER BY Allowed DESC) AS rn
                    FROM [dbo].[RolePermissions]
                )
                DELETE FROM d WHERE rn > 1;
                ");
        // end cleanup

        // helpers
        async Task<string> RoleIdAsync(string roleName)
            => (await roleMgr.FindByNameAsync(roleName))!.Id;

        async Task SetAsync(string roleName, AppModule module, Permission allowed)
        {
            var rid = await RoleIdAsync(roleName);
            var existing = await db.RolePermissions.FirstOrDefaultAsync(x => x.RoleId == rid && x.Module == module);
            if (existing is null)
                db.RolePermissions.Add(new RolePermission { RoleId = rid, Module = module, Allowed = allowed });
            else
                existing.Allowed = allowed;
        }

        // Admin: full everywhere
        foreach (var m in Enum.GetValues<AppModule>())
            await SetAsync(AppRoles.Admin, m, All);

        // Secretary
        var secView = Permission.View | Permission.Page;
        var secEdit = secView | Permission.Create | Permission.Update | Permission.Delete;

        await SetAsync(AppRoles.Secretary, AppModule.Dashboard, secView);
        await SetAsync(AppRoles.Secretary, AppModule.Meetings, secEdit);
        await SetAsync(AppRoles.Secretary, AppModule.Documents, secEdit);
        await SetAsync(AppRoles.Secretary, AppModule.Votes, secEdit);
        await SetAsync(AppRoles.Secretary, AppModule.Reports, secView);
        await SetAsync(AppRoles.Secretary, AppModule.Messages, secView);
        await SetAsync(AppRoles.Secretary, AppModule.Users, secView);
        await SetAsync(AppRoles.Secretary, AppModule.Settings, secView);

        // BoardMember
        var viewOnly = Permission.View | Permission.Page;
        await SetAsync(AppRoles.BoardMember, AppModule.Dashboard, viewOnly);
        await SetAsync(AppRoles.BoardMember, AppModule.Meetings, viewOnly);
        await SetAsync(AppRoles.BoardMember, AppModule.Documents, viewOnly);
        await SetAsync(AppRoles.BoardMember, AppModule.Votes, viewOnly | Permission.Create);
        await SetAsync(AppRoles.BoardMember, AppModule.Reports, viewOnly);
        await SetAsync(AppRoles.BoardMember, AppModule.Messages, viewOnly);
        await SetAsync(AppRoles.BoardMember, AppModule.Users, Permission.None);
        await SetAsync(AppRoles.BoardMember, AppModule.Settings, Permission.None);

        // CommitteeMember
        foreach (var m in new[] { AppModule.Dashboard, AppModule.Meetings, AppModule.Documents, AppModule.Votes, AppModule.Reports, AppModule.Messages })
            await SetAsync(AppRoles.CommitteeMember, m, viewOnly);
        await SetAsync(AppRoles.CommitteeMember, AppModule.Users, Permission.None);
        await SetAsync(AppRoles.CommitteeMember, AppModule.Settings, Permission.None);

        // Observer
        foreach (var m in new[] { AppModule.Dashboard, AppModule.Meetings, AppModule.Reports })
            await SetAsync(AppRoles.Observer, m, viewOnly);
        await SetAsync(AppRoles.Observer, AppModule.Documents, Permission.None);
        await SetAsync(AppRoles.Observer, AppModule.Votes, Permission.None);
        await SetAsync(AppRoles.Observer, AppModule.Messages, Permission.None);
        await SetAsync(AppRoles.Observer, AppModule.Users, Permission.None);
        await SetAsync(AppRoles.Observer, AppModule.Settings, Permission.None);

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded role permissions.");
    }
}
