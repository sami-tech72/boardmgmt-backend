using BoardMgmt.Application.Meetings.DTOs;
using MediatR;
using System.Collections.Generic;

namespace BoardMgmt.Application.Meetings.Queries;

public record GetMeetingsQuery() : IRequest<IReadOnlyList<MeetingDto>>;
