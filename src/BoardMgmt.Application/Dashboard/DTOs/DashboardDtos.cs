namespace BoardMgmt.Application.Dashboard.DTOs;

public record DashboardStatsDto(
    int UpcomingMeetings,
    int ActiveDocuments,
    int PendingVotes,
    int UnreadMessages);

public record DashboardMeetingDto(
    Guid Id,
    string Title,
    string? Subtitle,
    DateTime StartsAtUtc,
    string Status); // "Upcoming" | "Completed" | "Cancelled"

public record DashboardDocumentDto(
    Guid Id,
    string Title,
    string Kind,     // "pdf" | "word" | "excel" | "ppt" | "other"
    string UpdatedAgo);

public record DashboardActivityDto(
    Guid Id,
    string Kind,     // "upload" | "meeting_completed" | "vote_reminder" | "generic"
    string Title,
    string Text,
    string WhenAgo,
    string? Color);  // "primary" | "success" | "warning" | "info" | "danger"
