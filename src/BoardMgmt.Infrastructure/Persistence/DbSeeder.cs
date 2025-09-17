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
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<AppUser>>();

        // --- Roles ---
        foreach (var role in AppRoles.All)
            if (!await roleMgr.RoleExistsAsync(role))
                _ = await roleMgr.CreateAsync(new IdentityRole(role));

        // --- Admin user ---
        var admin = await userMgr.FindByEmailAsync("admin@board.local");
        if (admin is null)
        {
            admin = new AppUser { UserName = "admin@board.local", Email = "admin@board.local", EmailConfirmed = true };
            await userMgr.CreateAsync(admin, "P@ssw0rd!");
        }
        if (!await userMgr.IsInRoleAsync(admin, AppRoles.Admin))
            await userMgr.AddToRoleAsync(admin, AppRoles.Admin);

        // Demo users
        async Task Ensure(string email, string role)
        {
            var u = await userMgr.FindByEmailAsync(email);
            if (u is null)
            {
                u = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
                await userMgr.CreateAsync(u, "P@ssw0rd!");
            }
            if (!await userMgr.IsInRoleAsync(u, role))
                await userMgr.AddToRoleAsync(u, role);
        }
        await Ensure("secretary@board.local", AppRoles.Secretary);
        await Ensure("board@board.local", AppRoles.BoardMember);
        await Ensure("committee@board.local", AppRoles.CommitteeMember);
        await Ensure("observer@board.local", AppRoles.Observer);

        // Fetch users
        var uAdmin = await userMgr.FindByEmailAsync("admin@board.local");
        var uSecretary = await userMgr.FindByEmailAsync("secretary@board.local");
        var uBoard = await userMgr.FindByEmailAsync("board@board.local");
        var uCommittee = await userMgr.FindByEmailAsync("committee@board.local");
        var uObserver = await userMgr.FindByEmailAsync("observer@board.local");

        // --- Folders ---
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

        // --- Meetings (+ sample docs) ---
        if (!await db.Meetings.AnyAsync())
        {
            DateTimeOffset L(int d, int h) => new DateTimeOffset(DateTime.Today.AddDays(d).AddHours(h));

            var board = new Meeting
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
                    new MeetingAttendee { UserId = uAdmin!.Id,     Name = "Admin",        Role = "Chairman",  IsRequired = true, IsConfirmed = true },
                    new MeetingAttendee { UserId = uBoard!.Id,     Name = "Board Member", Role = "Director",  IsRequired = true, IsConfirmed = true },
                    new MeetingAttendee { UserId = uSecretary!.Id, Name = "Secretary",    Role = "Secretary", IsRequired = true, IsConfirmed = true }
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

            var finance = new Meeting
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
                    new MeetingAttendee { UserId = uCommittee!.Id, Name = "Committee Member", Role = "Member",   IsRequired = true,  IsConfirmed = false },
                    new MeetingAttendee { UserId = uAdmin!.Id,     Name = "Admin",            Role = "Observer", IsRequired = false, IsConfirmed = true  }
                },
                AgendaItems =
                {
                    new AgendaItem { Title = "Operating Budget", Order = 1 },
                    new AgendaItem { Title = "CapEx Plan",       Order = 2 }
                }
            };

            var past = new Meeting
            {
                Title = "Strategic Planning Session",
                Description = "2025 Roadmap Discussion",
                Type = MeetingType.Board,
                ScheduledAt = L(-2, 15),
                EndAt = L(-2, 17),
                Location = "Conference Room B",
                Status = MeetingStatus.Completed
            };

            db.Meetings.AddRange(board, finance, past);
            await db.SaveChangesAsync();

            // update folder counters
            var counts = await db.Documents.GroupBy(d => d.FolderSlug)
                .Select(g => new { Slug = g.Key, Count = g.Count() }).ToListAsync();
            foreach (var f in db.Folders)
                f.DocumentCount = counts.FirstOrDefault(c => c.Slug == f.Slug)?.Count ?? 0;
            await db.SaveChangesAsync();

            logger.LogInformation("Seeded meetings and updated folder counters.");
        }

        // --- Role permissions (NEW) ---
        // helpers
        Permission All => Permission.View | Permission.Create | Permission.Update | Permission.Delete | Permission.Page | Permission.Clone;

        async Task<string> RoleId(string roleName)
            => (await roleMgr.FindByNameAsync(roleName))!.Id;

        async Task Set(string roleName, AppModule m, Permission p)
        {
            var rid = await RoleId(roleName);
            var existing = await db.RolePermissions.FirstOrDefaultAsync(x => x.RoleId == rid && x.Module == m);
            if (existing is null)
                db.RolePermissions.Add(new RolePermission { RoleId = rid, Module = m, Allowed = p });
            else
                existing.Allowed = p;
        }

        // Admin: full everywhere
        foreach (var m in Enum.GetValues<AppModule>())
            await Set(AppRoles.Admin, m, All);

        // Secretary: create/update meetings & documents; manage votes; view users
        var secView = Permission.View | Permission.Page;
        var secEdit = secView | Permission.Create | Permission.Update | Permission.Delete;

        await Set(AppRoles.Secretary, AppModule.Dashboard, secView);
        await Set(AppRoles.Secretary, AppModule.Meetings, secEdit);
        await Set(AppRoles.Secretary, AppModule.Documents, secEdit);
        await Set(AppRoles.Secretary, AppModule.Voting, secEdit);
        await Set(AppRoles.Secretary, AppModule.Reports, secView);
        await Set(AppRoles.Secretary, AppModule.Messages, secView);
        await Set(AppRoles.Secretary, AppModule.Users, secView);
        await Set(AppRoles.Secretary, AppModule.Settings, secView);

        // BoardMember: view everything, can vote
        var member = Permission.View | Permission.Page;
        await Set(AppRoles.BoardMember, AppModule.Dashboard, member);
        await Set(AppRoles.BoardMember, AppModule.Meetings, member);
        await Set(AppRoles.BoardMember, AppModule.Documents, member);
        await Set(AppRoles.BoardMember, AppModule.Voting, member | Permission.Create); // allow creating personal votes if you want
        await Set(AppRoles.BoardMember, AppModule.Reports, member);
        await Set(AppRoles.BoardMember, AppModule.Messages, member);
        await Set(AppRoles.BoardMember, AppModule.Users, Permission.None);
        await Set(AppRoles.BoardMember, AppModule.Settings, Permission.None);

        // CommitteeMember: similar to BoardMember
        foreach (var m in new[] { AppModule.Dashboard, AppModule.Meetings, AppModule.Documents, AppModule.Voting, AppModule.Reports, AppModule.Messages })
            await Set(AppRoles.CommitteeMember, m, member);
        await Set(AppRoles.CommitteeMember, AppModule.Users, Permission.None);
        await Set(AppRoles.CommitteeMember, AppModule.Settings, Permission.None);

        // Observer: read-only minimal
        foreach (var m in new[] { AppModule.Dashboard, AppModule.Meetings, AppModule.Reports })
            await Set(AppRoles.Observer, m, member);
        await Set(AppRoles.Observer, AppModule.Documents, Permission.None);
        await Set(AppRoles.Observer, AppModule.Voting, Permission.None);
        await Set(AppRoles.Observer, AppModule.Messages, Permission.None);
        await Set(AppRoles.Observer, AppModule.Users, Permission.None);
        await Set(AppRoles.Observer, AppModule.Settings, Permission.None);

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded role permissions.");
    }
}
