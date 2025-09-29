using BoardMgmt.Domain.Entities;
using MediatR;

namespace BoardMgmt.Application.Meetings.Commands;

public sealed record CreateMeetingCommand(
   string Title,
    string? Description,
    MeetingType? Type,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? EndAt,
    string Location,
    List<string>? AttendeeUserIds,
    List<string>? Attendees,
    string Provider, // "Microsoft365" | "Zoom"
    string? HostIdentity // mailbox (M365) OR host email (Zoom). Optional; falls back to options
) : IRequest<Guid>;
