namespace BoardMgmt.Application.Meetings.DTOs;

public sealed class MeetingSelectListItemDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = default!;
    public DateTimeOffset ScheduledAt { get; init; }
}
