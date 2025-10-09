using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Chat.Handlers;

public sealed class CreateOrGetDirectConversationHandler
    : IRequestHandler<CreateOrGetDirectConversationCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CreateOrGetDirectConversationHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateOrGetDirectConversationCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            throw new ArgumentException("UserId is required.", nameof(request.UserId));
        if (string.IsNullOrWhiteSpace(request.OtherUserId))
            throw new ArgumentException("OtherUserId is required.", nameof(request.OtherUserId));
        if (request.UserId == request.OtherUserId)
            throw new InvalidOperationException("Cannot start a direct chat with yourself.");

        var a = request.UserId;
        var b = request.OtherUserId;

        // Look for an existing 1:1 DM with exactly these two members
        var existing = await _db.Set<Conversation>()
            .Where(c => c.Type == ConversationType.Direct && !c.IsPrivate)
            .Where(c => _db.Set<ConversationMember>().Count(m => m.ConversationId == c.Id) == 2)
            .Where(c => _db.Set<ConversationMember>().Any(m => m.ConversationId == c.Id && m.UserId == a))
            .Where(c => _db.Set<ConversationMember>().Any(m => m.ConversationId == c.Id && m.UserId == b))
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != Guid.Empty)
            return existing;

        // Create new DM
        var conv = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            Name = null,
            IsPrivate = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        conv.Members.Add(new ConversationMember
        {
            Id = Guid.NewGuid(),
            ConversationId = conv.Id,
            UserId = a,
            Role = ConversationMemberRole.Admin,
            JoinedAtUtc = DateTime.UtcNow
        });
        conv.Members.Add(new ConversationMember
        {
            Id = Guid.NewGuid(),
            ConversationId = conv.Id,
            UserId = b,
            Role = ConversationMemberRole.Member,
            JoinedAtUtc = DateTime.UtcNow
        });

        _db.Set<Conversation>().Add(conv);
        await _db.SaveChangesAsync(ct);

        return conv.Id;
    }
}
