using BoardMgmt.Application.Meetings.DTOs;
using MediatR;

namespace BoardMgmt.Application.Meetings.Queries;

public sealed record GetTranscriptByMeetingIdQuery(Guid MeetingId) : IRequest<TranscriptDto?>;
