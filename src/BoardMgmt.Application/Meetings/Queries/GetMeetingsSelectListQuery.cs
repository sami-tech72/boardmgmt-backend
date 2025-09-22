using BoardMgmt.Application.Meetings.DTOs;
using MediatR;

namespace BoardMgmt.Application.Meetings.Queries;

public sealed record GetMeetingsSelectListQuery : IRequest<IReadOnlyList<MeetingSelectListItemDto>>;
