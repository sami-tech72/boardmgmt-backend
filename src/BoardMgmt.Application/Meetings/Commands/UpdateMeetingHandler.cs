using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Calendars;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Meetings.Commands;

public class UpdateMeetingHandler : IRequestHandler<UpdateMeetingCommand, bool>
{
    private readonly DbContext _db;
    private readonly IIdentityUserReader _users;
    private readonly ICalendarServiceSelector _calSelector;

    public UpdateMeetingHandler(DbContext db, IIdentityUserReader users, ICalendarServiceSelector calSelector)
    {
        _db = db;
        _users = users;
        _calSelector = calSelector;
    }

    public async Task<bool> Handle(UpdateMeetingCommand request, CancellationToken ct)
    {
        var entity = await _db.Set<Meeting>()
            .Include(m => m.Attendees)
            .FirstOrDefaultAsync(m => m.Id == request.Id, ct);
        if (entity is null) return false;

        // Basic meeting fields
        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Type = request.Type;
        entity.ScheduledAt = request.ScheduledAt;
        entity.EndAt = request.EndAt;
        entity.Location = string.IsNullOrWhiteSpace(request.Location) ? "TBD" : request.Location.Trim();

        // Attendees
        if (request.AttendeesRich is not null)
        {
            var existingById = entity.Attendees.ToDictionary(a => a.Id, a => a);
            var originalIds = existingById.Keys.ToHashSet();

            var attendeeUserIds = request.AttendeesRich
                .Select(a => a.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!.Trim())
                .Where(id => id.Length > 0)
                .Distinct()
                .ToList();

            var usersById = attendeeUserIds.Count == 0
                ? new Dictionary<string, AppUser>(StringComparer.OrdinalIgnoreCase)
                : (await _users.GetByIdsAsync(attendeeUserIds, ct)).ToDictionary(u => u.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var dto in request.AttendeesRich)
            {
                var userKey = dto.UserId?.Trim();
                var hasUser = userKey is not null && userKey.Length > 0 && usersById.TryGetValue(userKey, out var user);

                if (dto.Id.HasValue && existingById.TryGetValue(dto.Id.Value, out var att))
                {
                    // update existing
                    att.UserId = dto.UserId;
                    if (hasUser && user is not null)
                    {
                        att.Name = string.IsNullOrWhiteSpace(dto.Name)
                            ? (!string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName! : user.Email ?? dto.Name)
                            : dto.Name;
                        att.Email = user.Email;
                    }
                    else
                    {
                        att.Name = dto.Name;
                        att.Email = dto.Email;
                    }
                    att.Role = dto.Role;
                    att.IsRequired = dto.IsRequired;
                    att.IsConfirmed = dto.IsConfirmed;
                }
                else
                {
                    // add new
                    var attendee = new MeetingAttendee
                    {
                        Id = dto.Id ?? Guid.NewGuid(),
                        MeetingId = entity.Id,
                        UserId = dto.UserId,
                        Role = dto.Role,
                        IsRequired = dto.IsRequired,
                        IsConfirmed = dto.IsConfirmed
                    };

                    if (hasUser && user is not null)
                    {
                        attendee.Name = string.IsNullOrWhiteSpace(dto.Name)
                            ? (!string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName! : user.Email ?? user.Id)
                            : dto.Name;
                        attendee.Email = user.Email;
                    }
                    else
                    {
                        attendee.Name = dto.Name;
                        attendee.Email = dto.Email;
                    }

                    entity.Attendees.Add(attendee);
                }
            }

            // remove those not present in incoming list (compare by existing ids only)
            var keepIds = request.AttendeesRich.Where(a => a.Id.HasValue).Select(a => a.Id!.Value).ToHashSet();
            var toRemove = entity.Attendees
                .Where(a => originalIds.Contains(a.Id) && !keepIds.Contains(a.Id))
                .ToList();
            foreach (var r in toRemove)
            {
                entity.Attendees.Remove(r);
                _db.Set<MeetingAttendee>().Remove(r);
            }
        }

        // Calendar provider update (optional)
        var svc = _calSelector.For(entity.ExternalCalendar ?? CalendarProviders.Microsoft365);
        var (_, joinUrl) = await svc.UpdateEventAsync(entity, ct);
        entity.OnlineJoinUrl = joinUrl;

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
