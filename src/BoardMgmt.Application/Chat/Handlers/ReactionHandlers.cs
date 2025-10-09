namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using BoardMgmt.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;

public sealed class AddReactionHandler : IRequestHandler<AddReactionCommand, bool>
{
    private readonly IAppDbContext _db;
    public AddReactionHandler(IAppDbContext db) => _db = db;

    public async Task<bool> Handle(AddReactionCommand req, CancellationToken ct)
    {
        // all string now
        var exists = await _db.Set<ChatReaction>()
            .AnyAsync(r => r.MessageId == req.MessageId && r.UserId == req.UserId && r.Emoji == req.Emoji, ct);
        if (exists) return true;

        _db.Set<ChatReaction>().Add(new ChatReaction
        {
            Id = Guid.NewGuid(),
            MessageId = req.MessageId,
            UserId = req.UserId,
            Emoji = req.Emoji,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class RemoveReactionHandler : IRequestHandler<RemoveReactionCommand, bool>
{
    private readonly IAppDbContext _db;
    public RemoveReactionHandler(IAppDbContext db) => _db = db;

    public async Task<bool> Handle(RemoveReactionCommand req, CancellationToken ct)
    {
        var r = await _db.Set<ChatReaction>()
            .FirstOrDefaultAsync(x => x.MessageId == req.MessageId && x.UserId == req.UserId && x.Emoji == req.Emoji, ct);
        if (r is null) return true;

        _db.Remove(r);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
