// Application/Calendars/Commands/MoveCalendarEventCommand.cs
using MediatR;
using System;

// Application/Calendars/Commands/MoveCalendarEventCommand.cs
namespace BoardMgmt.Application.Calendars.Commands
{
    public sealed record MoveCalendarEventCommand(
        Guid Id,
        DateTimeOffset NewStartUtc,
        DateTimeOffset? NewEndUtc
    ) : IRequest<bool>;
}

