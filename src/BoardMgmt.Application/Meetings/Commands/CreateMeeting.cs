using BoardMgmt.Domain.Entities;
using MediatR;

namespace BoardMgmt.Application.Meetings.Commands;

public record CreateMeetingCommand(
    string Title,
    string? Description,
   MeetingType? Type,                  // "board" | "committee" | "emergency"
    DateTimeOffset ScheduledAt,
    DateTimeOffset? EndAt,
    string Location,
    IReadOnlyList<string>? Attendees  // ["John Doe (Chairman)", ...]
) : IRequest<Guid>;
