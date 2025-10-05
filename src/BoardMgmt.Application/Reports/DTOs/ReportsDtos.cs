namespace BoardMgmt.Application.Reports.DTOs;

public record AttendancePoint(string Month, int Meetings, int ConfirmedAttendees);
public record VotingTrendPoint(string Month, int Polls, int Ballots, double ParticipationRatePct);
public record DocumentUsagePoint(string Month, int Documents, long SizeBytes);

public record PerformanceMetricsDto(
    int MeetingsScheduled,
    int MeetingsCompleted,
    double AvgAgendaItemsPerMeeting,
    double AvgDocsPerMeeting,
    double AvgAttendeesPerMeeting,
    double PollsPerMeeting
);

public record RecentReportDto(
    Guid Id,
    string Name,
    string Type,
    string GeneratedBy,
    DateTimeOffset GeneratedAt,
    string? FileUrl,
    string? Format,
    string? PeriodLabel
);

public class ReportsDashboardDto
{
    public List<AttendancePoint> Attendance { get; set; } = new();
    public List<VotingTrendPoint> Voting { get; set; } = new();
    public List<DocumentUsagePoint> Documents { get; set; } = new();
    public PerformanceMetricsDto Performance { get; set; } = new(
        0, 0, 0, 0, 0, 0
    );
    public List<RecentReportDto> Recent { get; set; } = new();
}
