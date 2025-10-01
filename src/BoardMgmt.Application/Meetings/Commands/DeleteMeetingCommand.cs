// Application/Meetings/Commands/DeleteMeetingCommand.cs
using MediatR;
using System;

namespace BoardMgmt.Application.Meetings.Commands
{
    public sealed record DeleteMeetingCommand(Guid Id) : IRequest<bool>;
}
