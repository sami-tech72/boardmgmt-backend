using BoardMgmt.Domain.Entities;

namespace BoardMgmt.Application.Meetings.DTOs;

public record AgendaItemDto(Guid Id, string Title, string? Description, int Order);

public record MeetingDto(
    Guid Id,
    string Title,
    string? Description,
    MeetingType? Type,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? EndAt,
    string Location,
    MeetingStatus Status,
    int AttendeesCount,
    
    IReadOnlyList<AgendaItemDto> AgendaItems,
     IReadOnlyList<AttendeeDto> Attendees,
    string? JoinUrl, // ← NEW
     string? Provider
);
