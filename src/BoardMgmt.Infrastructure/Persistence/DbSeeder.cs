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

        // roles
        foreach (var r in new[] { "Admin", "BoardMember", "Secretary" })
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole(r));

        // admin
        var admin = await userMgr.FindByEmailAsync("admin@board.local");
        if (admin is null)
        {
            admin = new AppUser { UserName = "admin@board.local", Email = "admin@board.local", EmailConfirmed = true };
            await userMgr.CreateAsync(admin, "P@ssw0rd!");
            await userMgr.AddToRoleAsync(admin, "Admin");
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
                new Document { FileName = "Q4-Financials.pdf", Url = "https://example.com/q4.pdf" },
                new Document { FileName = "Strategy-Deck.pptx", Url = "https://example.com/strategy.pptx" }
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
                new MeetingAttendee { Name = "Sarah Johnson", Role = "CFO", IsRequired = true, IsConfirmed = true },
                new MeetingAttendee { Name = "Mike Brown", Role = "CTO" }
            },
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
        logger.LogInformation("Seeded {Count} meetings.", 3);
    }
}
