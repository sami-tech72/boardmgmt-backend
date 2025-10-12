using System.Linq;
using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Calendars;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Meetings.Commands;

public class UpdateMeetingHandler : IRequestHandler<UpdateMeetingCommand, bool>
{
    private readonly IAppDbContext _db;
    private readonly IIdentityUserReader _users;
    private readonly ICalendarServiceSelector _calSelector;

    public UpdateMeetingHandler(IAppDbContext db, IIdentityUserReader users, ICalendarServiceSelector calSelector)
    {
        _db = db;
        _users = users;
        _calSelector = calSelector;
    }

    public async Task<bool> Handle(UpdateMeetingCommand request, CancellationToken ct)
    {
        var entity = await _db.Set<Meeting>()
            .Include(m => m.Attendees) // tracked for updates
            .FirstOrDefaultAsync(m => m.Id == request.Id, ct);

        if (entity is null) return false;

        // -------------------
        // Basic meeting fields
        // -------------------
        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Type = request.Type;
        entity.ScheduledAt = request.ScheduledAt;
        entity.EndAt = request.EndAt;
        entity.Location = string.IsNullOrWhiteSpace(request.Location) ? "TBD" : request.Location.Trim();

        // -------------------
        // Attendees upsert
        // -------------------
        if (request.AttendeesRich is not null)
        {
            // 1) Prepare identity map for authoritative name/email resolution
            var incomingUserIds = request.AttendeesRich
                .Select(a => a.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var usersById = incomingUserIds.Count == 0
                ? new Dictionary<string, AppUser>(StringComparer.OrdinalIgnoreCase)
                : (await _users.GetByIdsAsync(incomingUserIds, ct))
                    .ToDictionary(u => u.Id, StringComparer.OrdinalIgnoreCase);

            // 2) Index existing attendees (tracked)
            var existingById = entity.Attendees.ToDictionary(a => a.Id, a => a);
            var existingIds = existingById.Keys.ToHashSet();

            // 3) Apply updates / adds
            var seenExistingIds = new HashSet<Guid>();
            var toAdd = new List<MeetingAttendee>();

            foreach (var dto in request.AttendeesRich)
            {
                MeetingAttendee? tracked = null;
                var hasExisting = dto.Id.HasValue && existingById.TryGetValue(dto.Id.Value, out tracked);

                AppUser? user = null;
                var hasUser = !string.IsNullOrWhiteSpace(dto.UserId)
                              && usersById.TryGetValue(dto.UserId!.Trim(), out user);

                if (hasExisting && tracked is not null)
                {
                    // Update existing
                    tracked.UserId = dto.UserId;
                    tracked.Role = dto.Role;
                    tracked.IsRequired = dto.IsRequired;
                    tracked.IsConfirmed = dto.IsConfirmed;

                    if (hasUser && user is not null)
                    {
                        tracked.Name = string.IsNullOrWhiteSpace(dto.Name)
                            ? (!string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName! : user.Email ?? user.Id)
                            : dto.Name;
                        tracked.Email = user.Email;
                    }
                    else
                    {
                        tracked.Name = dto.Name;
                        tracked.Email = dto.Email;
                    }

                    seenExistingIds.Add(tracked.Id);
                }
                else
                {
                    // Add new
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

                    toAdd.Add(attendee);
                }
            }


            // 4) Remove those missing from incoming list (compare only original existing IDs)
            var keepIds = request.AttendeesRich.Where(a => a.Id.HasValue).Select(a => a.Id!.Value).ToHashSet();
            var toRemoveIds = existingIds.Where(id => !keepIds.Contains(id)).ToList();

            if (toRemoveIds.Count > 0)
            {
                // Prefer set-based delete to avoid collection state issues
                await _db.Set<MeetingAttendee>()
                    .Where(a => a.MeetingId == entity.Id && toRemoveIds.Contains(a.Id))
                    .ExecuteDeleteAsync(ct);

                // Also evict any now-stale tracked entries from local collection to keep change tracker clean
                entity.Attendees.RemoveAll(a => toRemoveIds.Contains(a.Id));
            }

            if (toAdd.Count > 0)
                await _db.Set<MeetingAttendee>().AddRangeAsync(toAdd, ct);
        }

        // -------------------
        // Calendar provider update (optional)
        // (Do this AFTER local model is correct, BEFORE final SaveChanges)
        // -------------------
        var svc = _calSelector.For(entity.ExternalCalendar ?? CalendarProviders.Microsoft365);
        var (_, joinUrl) = await svc.UpdateEventAsync(entity, ct);
        entity.OnlineJoinUrl = joinUrl;

        // -------------------
        // Save with concurrency handling for removed attendees
        // -------------------
        await SaveChangesHandlingRemovedAttendeesAsync(ct);
        return true;
    }

    private async Task SaveChangesHandlingRemovedAttendeesAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // If a concurrent process already deleted some attendees we tried to delete,
            // detach those entries and retry once.
            var attendeeDeletes = ex.Entries
                .Where(e => e.Entity is MeetingAttendee && e.State == EntityState.Deleted)
                .ToList();

            if (attendeeDeletes.Count == 0)
                throw;

            foreach (var entry in attendeeDeletes)
                entry.State = EntityState.Detached;

            await _db.SaveChangesAsync(ct);
        }
    }
}

// Small helper for removing items from a List<T> based on predicate without multiple enumerations
file static class ListExtensions
{
    public static void RemoveAll<T>(this ICollection<T> source, Func<T, bool> predicate)
    {
        var toRemove = source.Where(predicate).ToList();
        foreach (var item in toRemove) source.Remove(item);
    }
}
