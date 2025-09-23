using BoardMgmt.Application.Messages.DTOs;
using BoardMgmt.Application.Messages.Queries;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using BoardMgmt.Domain.Messages;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BoardMgmt.Application.Messages.Handlers;

public sealed class GetMessageThreadHandler : IRequestHandler<GetMessageThreadQuery, MessageThreadVm>
{
    private readonly DbContext _db;
    public GetMessageThreadHandler(DbContext db) => _db = db;

    public async Task<MessageThreadVm> Handle(GetMessageThreadQuery req, CancellationToken ct)
    {
        var anchor = await _db.Set<Message>()
            .Include(m => m.Recipients)
            .Include(m => m.Attachments)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == req.AnchorMessageId, ct)
            ?? throw new KeyNotFoundException("Message not found");

        // Normalize subject (strip Re:/Fwd: prefixes)
        var subjectKey = NormalizeSubject(anchor.Subject);

        // Work out participants
        var current = req.CurrentUserId;
        var otherIds = new HashSet<Guid>(
            anchor.Recipients.Select(r => r.UserId));
        if (anchor.SenderId != current) otherIds.Add(anchor.SenderId);
        otherIds.Remove(current); // only others

        // Candidate messages: between current and any of the "otherIds", both directions
        var candidates = await _db.Set<Message>()
            .Include(m => m.Recipients)
            .Include(m => m.Attachments)
            .Where(m =>
                // me -> them
                (m.SenderId == current && m.Recipients.Any(r => otherIds.Contains(r.UserId)))
                ||
                // them -> me
                (otherIds.Contains(m.SenderId) && m.Recipients.Any(r => r.UserId == current))
            )
            // Limit to reasonable window (optional, comment out if you want all time)
            //.Where(m => m.CreatedAtUtc >= anchor.CreatedAtUtc.AddMonths(-6))
            .AsNoTracking()
            .ToListAsync(ct);

        // Subject filter in memory (cheap on the reduced set)
        var threadMessages = candidates
            .Where(m => NormalizeSubject(m.Subject) == subjectKey)
            .OrderBy(m => m.CreatedAtUtc)
            .ToList();

        // Build user map
        var userIds = threadMessages
            .Select(m => m.SenderId.ToString())
            .Concat(threadMessages.SelectMany(m => m.Recipients.Select(r => r.UserId.ToString())))
            .Append(current.ToString())
            .Distinct()
            .ToList();

        var rawUsers = await _db.Set<AppUser>()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .AsNoTracking()
            .ToListAsync(ct);

        MinimalUserDto MapUser(Guid id)
        {
            var s = id.ToString();
            var u = rawUsers.FirstOrDefault(x => x.Id == s);
            var name = (u is null)
                ? null
                : (string.IsNullOrWhiteSpace(u.LastName) ? u.FirstName : $"{u.FirstName} {u.LastName}");
            return new MinimalUserDto(id, name, u?.Email);
        }

        var bubbles = threadMessages.Select(m => new MessageBubbleVm(
            m.Id,
            MapUser(m.SenderId),
            m.Body,
            m.CreatedAtUtc,
            m.Attachments.Select(a => new MessageAttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSize)).ToList()
        )).ToList();

        // Participants are current + others from anchor
        var participants = new List<MinimalUserDto> { MapUser(current) };
        participants.AddRange(otherIds.Select(MapUser));

        return new MessageThreadVm(
            AnchorMessageId: anchor.Id,
            Subject: anchor.Subject,
            Participants: participants,
            Items: bubbles
        );
    }

    private static string NormalizeSubject(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var x = s.Trim();
        // repeatedly strip "re:" or "fwd:" prefixes (any spacing, any case)
        var re = new Regex(@"^(re|fwd)\s*:\s*", RegexOptions.IgnoreCase);
        while (re.IsMatch(x)) x = re.Replace(x, "").Trim();
        return x.ToLowerInvariant();
    }
}
