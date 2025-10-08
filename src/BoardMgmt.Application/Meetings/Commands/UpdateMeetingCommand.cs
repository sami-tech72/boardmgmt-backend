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
        List<string>? AttendeeUserIds,
        List<UpdateAttendeeDto>? AttendeesRich
    ) : IRequest<bool>;

    public sealed record UpdateAttendeeDto(
        Guid? Id,
        string Name,
        string? Email,
        string? Role,
        bool IsRequired,
        bool IsConfirmed
    );
}
