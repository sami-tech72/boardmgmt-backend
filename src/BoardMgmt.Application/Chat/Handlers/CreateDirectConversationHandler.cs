namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Domain.Entities;

public sealed class CreateDirectConversationHandler : IRequestHandler<CreateDirectConversationCommand, Guid>
{
    private readonly DbContext _db;
    public CreateDirectConversationHandler(DbContext db) => _db = db;

    public async Task<Guid> Handle(CreateDirectConversationCommand req, CancellationToken ct)
    {
        // members must contain creator, and at least 2 distinct users
        var members = (req.MemberIds ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (!members.Contains(req.CreatorId)) members.Add(req.CreatorId);
        if (members.Count < 2) throw new ArgumentException("Direct conversation requires at least 2 users.");

        // normalize a canonical key for "same participants" detection (order-independent)
        var key = string.Join("|", members.OrderBy(x => x, StringComparer.Ordinal));

        // Find an existing Direct with the exact same member set
        var candidateIds = await _db.Set<ConversationMember>()
            .Where(m => members.Contains(m.UserId))
            .GroupBy(m => m.ConversationId)
            .Where(g => g.Count() == members.Count)       // quick filter: same size
            .Select(g => g.Key)
            .ToListAsync(ct);

        foreach (var convId in candidateIds)
        {
            var ids = await _db.Set<ConversationMember>()
                .Where(m => m.ConversationId == convId)
                .Select(m => m.UserId)
                .OrderBy(x => x)
                .ToListAsync(ct);

            if (string.Equals(key, string.Join("|", ids), StringComparison.Ordinal))
            {
                var existing = await _db.Set<Conversation>().FirstAsync(c => c.Id == convId, ct);
                if (existing.Type == ConversationType.Direct) return existing.Id;
            }
        }

        // Create a new Direct conversation
        var conv = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            IsPrivate = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        // Optional: name – for DMs we can show the "other" user's name to the creator;
        // leave blank; client can render from members. Still we store a simple name.
        var users = await _db.Set<AppUser>()
            .Where(u => members.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(ct);

        static string Full(string? f, string? l, string? e)
            => string.IsNullOrWhiteSpace(l) ? (f ?? e ?? "Unknown") : $"{f} {l}";

        conv.Name = string.Join(", ",
            users.Where(u => u.Id != req.CreatorId).Select(u => Full(u.FirstName, u.LastName, u.Email)));

        foreach (var uid in members)
        {
            conv.Members.Add(new ConversationMember
            {
                Id = Guid.NewGuid(),
                ConversationId = conv.Id,
                UserId = uid,
                Role = ConversationMemberRole.Member,
                JoinedAtUtc = DateTime.UtcNow
            });
        }

        _db.Set<Conversation>().Add(conv);
        await _db.SaveChangesAsync(ct);
        return conv.Id;
    }
}
