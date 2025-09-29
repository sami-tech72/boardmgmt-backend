// Application/Meetings/Commands/UpdateMeetingHandler.cs
using BoardMgmt.Application.Calendars;
using BoardMgmt.Domain.Calendars;
using MediatR;
using Microsoft.EntityFrameworkCore;


namespace BoardMgmt.Application.Meetings.Commands;


public class UpdateMeetingHandler : IRequestHandler<UpdateMeetingCommand, bool>
{
    private readonly DbContext _db;
    private readonly ICalendarServiceSelector _calSelector;


    public UpdateMeetingHandler(DbContext db, ICalendarServiceSelector calSelector)
    {
        _db = db;
        _calSelector = calSelector;
    }


    public async Task<bool> Handle(UpdateMeetingCommand request, CancellationToken ct)
    {
        var entity = await _db.Set<Domain.Entities.Meeting>()
        .Include(m => m.Attendees)
        .FirstOrDefaultAsync(m => m.Id == request.Id, ct);
        if (entity is null) return false;


        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim();
        entity.Type = request.Type;
        entity.ScheduledAt = request.ScheduledAt;
        entity.EndAt = request.EndAt;
        entity.Location = string.IsNullOrWhiteSpace(request.Location) ? "TBD" : request.Location.Trim();


        // (Update attendees omitted for brevity)


        var svc = _calSelector.For(entity.ExternalCalendar ?? CalendarProviders.Microsoft365);
        var (_, joinUrl) = await svc.UpdateEventAsync(entity, ct);
        entity.OnlineJoinUrl = joinUrl;


        await _db.SaveChangesAsync(ct);
        return true;
    }
}