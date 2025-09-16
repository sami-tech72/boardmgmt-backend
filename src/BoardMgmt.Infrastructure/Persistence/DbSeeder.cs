using BoardMgmt.Domain.Entities;
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

        foreach (var r in new[] { "Admin", "BoardMember", "Secretary" })
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole(r));

        var admin = await userMgr.FindByEmailAsync("admin@board.local");
        if (admin is null)
        {
            admin = new AppUser { UserName = "admin@board.local", Email = "admin@board.local", EmailConfirmed = true };
            await userMgr.CreateAsync(admin, "P@ssw0rd!");
            await userMgr.AddToRoleAsync(admin, "Admin");
        }

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

        if (await db.Meetings.AnyAsync()) return;

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
                new MeetingAttendee { Name = "John Doe", Role = "Chairman", IsRequired = true, IsConfirmed = true },
                new MeetingAttendee { Name = "Sarah Johnson", Role = "CFO", IsRequired = true, IsConfirmed = true },
                new MeetingAttendee { Name = "Mike Brown", Role = "CTO" }
            },
            AgendaItems =
            {
                new AgendaItem { Title = "Q4 Financials", Description = "Review P&L and Balance Sheet", Order = 1 },
                new AgendaItem { Title = "2025 Roadmap", Description = "Strategic initiatives discussion", Order = 2 }
            },
            Documents =
            {
                new Document
                {
                    OriginalName = "Q4-Financials.pdf",
                    FileName = "Q4-Financials.pdf",
                    Url = "https://example.com/q4.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 2_500_000,
                    FolderSlug = "financial"
                },
                new Document
                {
                    OriginalName = "Strategy-Deck.pptx",
                    FileName = "Strategy-Deck.pptx",
                    Url = "https://example.com/strategy.pptx",
                    ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                    SizeBytes = 5_700_000,
                    FolderSlug = "board-meetings"
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
            AgendaItems =
            {
                new AgendaItem { Title = "Operating Budget", Order = 1 },
                new AgendaItem { Title = "CapEx Plan", Order = 2 }
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

        // optional counter refresh
        var counts = await db.Documents.GroupBy(d => d.FolderSlug)
            .Select(g => new { Slug = g.Key, Count = g.Count() }).ToListAsync();
        foreach (var f in db.Folders)
            f.DocumentCount = counts.FirstOrDefault(c => c.Slug == f.Slug)?.Count ?? 0;
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded meetings and updated folder counters.");
    }
}
