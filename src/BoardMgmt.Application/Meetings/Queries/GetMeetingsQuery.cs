using BoardMgmt.Application.Meetings.DTOs;
using MediatR;


namespace BoardMgmt.Application.Meetings.Queries;


public record GetMeetingsQuery() : IRequest<IReadOnlyList<MeetingDto>>;