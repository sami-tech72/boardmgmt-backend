// Application/Calendars/Commands/MoveCalendarEventHandler.cs
using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Calendars;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Calendars.Commands
{
    public sealed class MoveCalendarEventHandler : IRequestHandler<MoveCalendarEventCommand, bool>
    {
        private readonly IAppDbContext _db;
        private readonly ICalendarServiceSelector _calSelector;

        public MoveCalendarEventHandler(IAppDbContext db, ICalendarServiceSelector calSelector)
        {
            _db = db;
            _calSelector = calSelector;
        }

        public async Task<bool> Handle(MoveCalendarEventCommand request, CancellationToken ct)
        {
            var meeting = await _db.Set<Meeting>()
                .FirstOrDefaultAsync(m => m.Id == request.Id, ct);

            if (meeting is null) return false;

            // Basic guard
            if (request.NewEndUtc.HasValue && request.NewEndUtc.Value <= request.NewStartUtc)
                return false;

            meeting.ScheduledAt = request.NewStartUtc;
            meeting.EndAt = request.NewEndUtc;

            // Push change to external provider if applicable
            var provider = meeting.ExternalCalendar ?? CalendarProviders.Microsoft365;
            var svc = _calSelector.For(provider);
            await svc.UpdateEventAsync(meeting, ct); // returns (eventId, joinUrl); joinUrl usually unchanged

            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}
