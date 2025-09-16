using BoardMgmt.Application.Meetings.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;


namespace BoardMgmt.Application.Meetings.Queries;


public class GetMeetingsQueryHandler : IRequestHandler<GetMeetingsQuery, IReadOnlyList<MeetingDto>>
{
    private readonly DbContext _db;
    public GetMeetingsQueryHandler(DbContext db) => _db = db;


    public async Task<IReadOnlyList<MeetingDto>> Handle(GetMeetingsQuery request, CancellationToken ct)
    {
        return await _db.Set<Meeting>()
        .Include(m => m.AgendaItems)
        .OrderBy(m => m.ScheduledAt)
        .Select(m => new MeetingDto(
        m.Id,
        m.Title,
        m.ScheduledAt,
        m.Location,
        m.Status,
        m.AgendaItems
        .OrderBy(ai => ai.Order)
        .Select(ai => new AgendaItemDto(ai.Id, ai.Title, ai.Description, ai.Order))
        .ToList()
        ))
        .ToListAsync(ct);
    }
}