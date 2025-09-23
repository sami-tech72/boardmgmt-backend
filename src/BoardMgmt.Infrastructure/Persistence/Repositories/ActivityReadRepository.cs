//using BoardMgmt.Application.Common.Interfaces.Repositories;
//using BoardMgmt.Application.Dashboard.DTOs;
//using Microsoft.EntityFrameworkCore;

//namespace BoardMgmt.Infrastructure.Persistence.Repositories;

//public class ActivityReadRepository : IActivityReadRepository
//{
//    private readonly DbContext _db;
//    public ActivityReadRepository(DbContext db) => _db = db;

//    public async Task<IReadOnlyList<DashboardActivityDto>> GetRecentAsync(int take, CancellationToken ct)
//    {
//        var now = DateTime.UtcNow;

//        //var data = await _db.Activities
//        var data = await _db.Activities
//            .OrderByDescending(a => a.CreatedAtUtc)
//            .Take(take)
//            .Select(a => new DashboardActivityDto(
//                a.Id,
//                a.Kind,
//                a.Title,
//                a.Text,
//                WhenAgo(now, a.CreatedAtUtc),
//                a.Color))
//            .ToListAsync(ct);

//        return data;
//    }

//    private static string WhenAgo(DateTime now, DateTime at)
//    {
//        var span = now - at;
//        if (span.TotalMinutes < 90) return $"{Math.Max(1, (int)span.TotalMinutes)} minutes ago";
//        if (span.TotalHours < 36) return $"{Math.Max(1, (int)span.TotalHours)} hours ago";
//        return $"{Math.Max(1, (int)span.TotalDays)} days ago";
//    }
//}




using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Application.Dashboard.DTOs;

namespace BoardMgmt.Infrastructure.Persistence.Repositories;

public class ActivityReadRepository : IActivityReadRepository
{
    public Task<IReadOnlyList<DashboardActivityDto>> GetRecentAsync(int take, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Static demo data
        var data = new List<DashboardActivityDto>
        {
            new DashboardActivityDto(
                Guid.NewGuid(),
                "meeting",
                "Board Meeting Scheduled",
                "Quarterly board meeting has been scheduled.",
                WhenAgo(now, now.AddMinutes(-20)),
                "info"),

            new DashboardActivityDto(
                Guid.NewGuid(),
                "vote",
                "Budget Approval Vote",
                "Budget approval voting is now open.",
                WhenAgo(now, now.AddHours(-2)),
                "warning"),

            new DashboardActivityDto(
                Guid.NewGuid(),
                "document",
                "New Policy Uploaded",
                "HR policy document uploaded.",
                WhenAgo(now, now.AddDays(-1)),
                "success"),

            new DashboardActivityDto(
                Guid.NewGuid(),
                "message",
                "Message Sent",
                "Confidential message sent to board members.",
                WhenAgo(now, now.AddDays(-3)),
                "primary"),
        };

        // Respect `take` parameter
        return Task.FromResult<IReadOnlyList<DashboardActivityDto>>(data.Take(take).ToList());
    }

    private static string WhenAgo(DateTime now, DateTime at)
    {
        var span = now - at;
        if (span.TotalMinutes < 90) return $"{Math.Max(1, (int)span.TotalMinutes)} minutes ago";
        if (span.TotalHours < 36) return $"{Math.Max(1, (int)span.TotalHours)} hours ago";
        return $"{Math.Max(1, (int)span.TotalDays)} days ago";
    }
}
