namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Application.Common.Interfaces;

public sealed class CreateChannelHandler : IRequestHandler<CreateChannelCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CreateChannelHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateChannelCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ArgumentException("Channel name required");

        var conv = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Channel,
            Name = req.Name.Trim(),
            IsPrivate = req.IsPrivate,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var memberIds = req.MemberIds.Distinct().ToList();
        if (!memberIds.Contains(req.CreatorId))
            memberIds.Add(req.CreatorId);

        foreach (var uid in memberIds)
        {
            conv.Members.Add(new ConversationMember
            {
                Id = Guid.NewGuid(),
                ConversationId = conv.Id,
                UserId = uid,
                Role = uid == req.CreatorId ? ConversationMemberRole.Admin : ConversationMemberRole.Member,
                JoinedAtUtc = DateTime.UtcNow
            });
        }

        _db.Set<Conversation>().Add(conv);
        await _db.SaveChangesAsync(ct);
        return conv.Id;
    }
}
