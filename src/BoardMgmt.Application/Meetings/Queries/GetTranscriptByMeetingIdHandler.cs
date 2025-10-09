using System.Linq;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Meetings.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Meetings.Queries;

public sealed class GetTranscriptByMeetingIdHandler : IRequestHandler<GetTranscriptByMeetingIdQuery, TranscriptDto?>
{
    private readonly IAppDbContext _db;
    public GetTranscriptByMeetingIdHandler(IAppDbContext db) => _db = db;

    public async Task<TranscriptDto?> Handle(GetTranscriptByMeetingIdQuery request, CancellationToken ct)
    {
        var tr = await _db.Set<Transcript>()
            .Include(t => t.Utterances)
            .Where(t => t.MeetingId == request.MeetingId)
            .OrderByDescending(t => t.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (tr is null) return null;

        var utterances = tr.Utterances
            .OrderBy(u => u.Start)
            .Select(u => new TranscriptUtteranceDto(
                Start: u.Start.ToString(@"hh\:mm\:ss\.fff"),
                End: u.End.ToString(@"hh\:mm\:ss\.fff"),
                Text: u.Text,
                SpeakerName: u.SpeakerName,
                SpeakerEmail: u.SpeakerEmail,
                UserId: u.UserId))
            .ToList();

        return new TranscriptDto(tr.Id, tr.Provider, tr.CreatedUtc, utterances);
    }
}
