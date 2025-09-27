namespace BoardMgmt.Application.Dashboard.DTOs;

public record DashboardStatsDto(
    int UpcomingMeetings,
    int ActiveDocuments,
    int PendingVotes,
    int ActiveUsers);

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




public record MeetingItemDto(
    Guid Id,
    string Title,
    string? Subtitle,
    DateTime StartsAtUtc,
    string Status);

public record DocumentItemDto(
    Guid Id,
    string Title,
    string Kind,
    string UpdatedAgo);

public record VoteItemDto(
    Guid Id,
    string Title,
    DateTime DeadlineUtc);

public record UnreadMessageItemDto(
    Guid Id,
    string Subject,
    string FromName,
    DateTime SentAtUtc);

public record PagedResultDto<T>(
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<T> Items);


public record ActiveUserItemDto(
    string Id,
    string DisplayName,
    string? Email,
    DateTimeOffset? LastSeenUtc
);
