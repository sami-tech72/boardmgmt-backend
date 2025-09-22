using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Meetings.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Meetings.Queries;

public sealed class GetMeetingsSelectListHandler
    : IRequestHandler<GetMeetingsSelectListQuery, IReadOnlyList<MeetingSelectListItemDto>>
{
    private readonly IAppDbContext _db;

    public GetMeetingsSelectListHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<MeetingSelectListItemDto>> Handle(
        GetMeetingsSelectListQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _db.Meetings
            .OrderByDescending(m => m.ScheduledAt)
            .Select(m => new MeetingSelectListItemDto
            {
                Id = m.Id,
                Title = m.Title,
                ScheduledAt = m.ScheduledAt
            })
            .Take(200)
            .ToListAsync(cancellationToken);

        return items;
    }
}
