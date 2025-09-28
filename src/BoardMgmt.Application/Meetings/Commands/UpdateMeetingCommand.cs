// BoardMgmt.Application/Meetings/Commands/UpdateMeetingCommand.cs
using BoardMgmt.Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;


namespace BoardMgmt.Application.Meetings.Commands
{
    public sealed record UpdateMeetingCommand(
        Guid Id,
        string Title,
        string? Description,
        MeetingType? Type,
        DateTimeOffset ScheduledAt,
        DateTimeOffset? EndAt,
        string Location,
        List<string>? AttendeeUserIds,              // identity-backed mode (optional)
        List<UpdateAttendeeDto>? AttendeesRich      // full rows with RowVersion
    ) : IRequest<bool>;

    public sealed record UpdateAttendeeDto(
        Guid Id,                 // Guid.Empty for new attendees
        string? UserId,          // null for external/non-user
        string Name,
        string? Role,
        string? Email,
        string RowVersionBase64  // empty for new
    );
}
