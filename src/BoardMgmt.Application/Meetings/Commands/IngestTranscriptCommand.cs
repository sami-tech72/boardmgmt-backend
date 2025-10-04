

using System;
using MediatR;

namespace BoardMgmt.Application.Meetings.Commands;

public sealed record IngestTranscriptCommand(Guid MeetingId) : IRequest<int>; // returns utterance count
