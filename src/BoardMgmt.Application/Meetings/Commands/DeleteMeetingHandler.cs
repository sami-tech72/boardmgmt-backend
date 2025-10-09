// Application/Meetings/Commands/DeleteMeetingHandler.cs
using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Meetings.Commands
{
    public sealed class DeleteMeetingHandler : IRequestHandler<DeleteMeetingCommand, bool>
    {
                private readonly IAppDbContext _db;
        private readonly ICalendarServiceSelector _calSelector;

        public DeleteMeetingHandler(IAppDbContext db, ICalendarServiceSelector calSelector)
        {
            _db = db;
            _calSelector = calSelector;
        }

        public async Task<bool> Handle(DeleteMeetingCommand request, CancellationToken ct)
        {
            var entity = await _db.Set<Meeting>().FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null) return false;

            if (!string.IsNullOrWhiteSpace(entity.ExternalCalendar) &&
                !string.IsNullOrWhiteSpace(entity.ExternalEventId))
            {
                var svc = _calSelector.For(entity.ExternalCalendar);
                await svc.CancelEventAsync(entity, ct);
            }

            _db.Meetings.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}
