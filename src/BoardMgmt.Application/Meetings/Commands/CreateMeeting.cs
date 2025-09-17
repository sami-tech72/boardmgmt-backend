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
    List<string>? attendeeUserIds,      // Identity user IDs coming from UI
    List<string>? Attendees = null      // OPTIONAL: "Full Name (Role)" strings
) : IRequest<Guid>;
