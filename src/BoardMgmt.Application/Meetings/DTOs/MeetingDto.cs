using BoardMgmt.Domain.Entities;


namespace BoardMgmt.Application.Meetings.DTOs;


public record AgendaItemDto(Guid Id, string Title, string? Description, int Order);


public record MeetingDto(
Guid Id,
string Title,
DateTimeOffset ScheduledAt,
string Location,
MeetingStatus Status,
IReadOnlyList<AgendaItemDto> AgendaItems
);