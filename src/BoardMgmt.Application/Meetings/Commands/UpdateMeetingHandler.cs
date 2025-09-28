// BoardMgmt.Application/Meetings/Commands/UpdateMeetingHandler.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Application.Calendars;

namespace BoardMgmt.Application.Meetings.Commands
{
    public sealed class UpdateMeetingHandler : IRequestHandler<UpdateMeetingCommand, bool>
    {
        private readonly DbContext _db;
        private readonly IIdentityUserReader _users;
        private readonly ICalendarService _calendar;

        public UpdateMeetingHandler(DbContext db, IIdentityUserReader users, ICalendarService calendar)
        {
            _db = db;
            _users = users;
            _calendar = calendar;
        }

        public async Task<bool> Handle(UpdateMeetingCommand request, CancellationToken ct)
        {
            var meeting = await _db.Set<Meeting>()
                .Include(m => m.Attendees)
                .FirstOrDefaultAsync(m => m.Id == request.Id, ct);

            if (meeting is null)
                return false;

            // Update scalar fields
            meeting.Title = request.Title.Trim();
            meeting.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim();
            meeting.Type = request.Type;
            meeting.ScheduledAt = request.ScheduledAt;
            meeting.EndAt = request.EndAt;
            meeting.Location = string.IsNullOrWhiteSpace(request.Location) ? "TBD" : request.Location.Trim();

            await SyncAttendeesAsync(meeting, request, ct);
            if (string.IsNullOrWhiteSpace(meeting.ExternalEventId))
            {
                var created = await _calendar.CreateEventAsync(meeting, ct);
                meeting.ExternalCalendar = "Microsoft365";
                meeting.ExternalEventId = created.eventId;
                meeting.OnlineJoinUrl = created.joinUrl;
            }
            else
            {
                var updated = await _calendar.UpdateEventAsync(meeting, ct);
                if (updated.ok)
                    meeting.OnlineJoinUrl = updated.joinUrl ?? meeting.OnlineJoinUrl;
            }
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // Prefer client token; if missing/malformed, fall back to DB value so EF doesn't raise a false conflict
        private void StampOriginal(MeetingAttendee entity, string? rowVersionBase64)
        {
            var prop = _db.Entry(entity).Property(x => x.RowVersion);

            if (!string.IsNullOrWhiteSpace(rowVersionBase64))
            {
                try
                {
                    prop.OriginalValue = Convert.FromBase64String(rowVersionBase64);
                    return;
                }
                catch
                {
                    // ignore parse errors and fall through to DB value
                }
            }

            prop.OriginalValue = entity.RowVersion;
        }

        private async Task SyncAttendeesAsync(Meeting meeting, UpdateMeetingCommand request, CancellationToken ct)
        {
            // Map of incoming rich attendees by Attendee.Id (Guid)
            var incomingRichById = (request.AttendeesRich ?? new List<UpdateAttendeeDto>())
                .ToDictionary(a => a.Id, a => a);

            // ---------- 1) Identity-backed mode (attendeeUserIds) ----------
            var hasUserIds = request.AttendeeUserIds is { Count: > 0 } &&
                             request.AttendeeUserIds!.Any(id => !string.IsNullOrWhiteSpace(id));

            if (hasUserIds)
            {
                var cleanIds = request.AttendeeUserIds!
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                var users = await _users.GetByIdsAsync(cleanIds, ct);
                var foundIds = users.Select(u => u.Id).ToHashSet(StringComparer.Ordinal);

                var unknown = cleanIds.Where(id => !foundIds.Contains(id)).ToList();
                if (unknown.Count > 0)
                    throw new InvalidOperationException($"Unknown user ids: {string.Join(", ", unknown)}");

                var desired = users.Select(u => new
                {
                    u.Id,
                    Name = string.IsNullOrWhiteSpace(u.DisplayName) ? (u.Email ?? "Unknown") : u.DisplayName!,
                    u.Email
                }).ToList();

                var existingByUserId = meeting.Attendees
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .ToDictionary(a => a.UserId!, a => a, StringComparer.Ordinal);

                // Upsert desired identity attendees
                foreach (var d in desired)
                {
                    if (existingByUserId.TryGetValue(d.Id, out var existing))
                    {
                        // Stamp original rowversion (client token if provided; fallback to DB)
                        if (incomingRichById.TryGetValue(existing.Id, out var inc))
                            StampOriginal(existing, inc.RowVersionBase64);
                        else
                            StampOriginal(existing, null);

                        existing.Name = d.Name;
                        existing.Email = d.Email;
                        existing.IsRequired = true;
                    }
                    else
                    {
                        meeting.Attendees.Add(new MeetingAttendee
                        {
                            UserId = d.Id,
                            Name = d.Name,
                            Email = d.Email,
                            IsRequired = true,
                            IsConfirmed = false
                        });
                    }
                }

                // Remove identity attendees no longer desired
                var desiredIds = new HashSet<string>(desired.Select(x => x.Id), StringComparer.Ordinal);
                var toRemove = meeting.Attendees
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId) && !desiredIds.Contains(a.UserId!))
                    .ToList();

                foreach (var r in toRemove)
                {
                    if (incomingRichById.TryGetValue(r.Id, out var inc))
                        StampOriginal(r, inc.RowVersionBase64);
                    else
                        StampOriginal(r, null);

                    _db.Remove(r);
                }
            }

            // ---------- 2) Rich list (external + explicit edits to existing) ----------
            var existingById = meeting.Attendees.ToDictionary(a => a.Id, a => a);

            // Add / Update from incoming rich list
            foreach (var inc in request.AttendeesRich ?? Enumerable.Empty<UpdateAttendeeDto>())
            {
                if (inc.Id == Guid.Empty)
                {
                    // NEW attendee
                    meeting.Attendees.Add(new MeetingAttendee
                    {
                        UserId = string.IsNullOrWhiteSpace(inc.UserId) ? null : inc.UserId,
                        Name = inc.Name?.Trim() ?? string.Empty,
                        Role = string.IsNullOrWhiteSpace(inc.Role) ? null : inc.Role!.Trim(),
                        Email = string.IsNullOrWhiteSpace(inc.Email) ? null : inc.Email!.Trim(),
                        IsRequired = true,
                        IsConfirmed = false
                    });
                }
                else if (existingById.TryGetValue(inc.Id, out var ex))
                {
                    // Stamp original before updating
                    StampOriginal(ex, inc.RowVersionBase64);

                    ex.UserId = string.IsNullOrWhiteSpace(inc.UserId) ? null : inc.UserId;
                    ex.Name = inc.Name?.Trim() ?? ex.Name;
                    ex.Role = string.IsNullOrWhiteSpace(inc.Role) ? null : inc.Role!.Trim();
                    ex.Email = string.IsNullOrWhiteSpace(inc.Email) ? null : inc.Email!.Trim();
                    ex.IsRequired = true;
                }
            }

            // Delete *external* attendees that are missing from incoming rich list
            var incomingIds = (request.AttendeesRich ?? new())
                .Where(a => a.Id != Guid.Empty)
                .Select(a => a.Id)
                .ToHashSet();

            var nonUserToDelete = meeting.Attendees
                .Where(a => string.IsNullOrWhiteSpace(a.UserId) && a.Id != Guid.Empty && !incomingIds.Contains(a.Id))
                .ToList();

            foreach (var del in nonUserToDelete)
            {
                if (incomingRichById.TryGetValue(del.Id, out var inc))
                    StampOriginal(del, inc.RowVersionBase64);
                else
                    StampOriginal(del, null);

                _db.Remove(del);
            }
        }
    }
}
