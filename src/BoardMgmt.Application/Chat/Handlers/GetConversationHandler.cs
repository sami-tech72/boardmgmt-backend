namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Domain.Entities;

public sealed class GetConversationHandler : IRequestHandler<GetConversationQuery, ConversationDetailDto>
{
    private readonly DbContext _db;
    public GetConversationHandler(DbContext db) => _db = db;

    public async Task<ConversationDetailDto> Handle(GetConversationQuery req, CancellationToken ct)
    {
        var isMember = await _db.Set<ConversationMember>()
            .AnyAsync(m => m.ConversationId == req.ConversationId && m.UserId == req.ForUserId, ct);
        if (!isMember) throw new UnauthorizedAccessException();

        var conv = await _db.Set<Conversation>().FirstAsync(c => c.Id == req.ConversationId, ct);

        var memberIds = await _db.Set<ConversationMember>()
            .Where(m => m.ConversationId == req.ConversationId)
            .Select(m => m.UserId).ToListAsync(ct);

        var users = await _db.Set<AppUser>()
            .Where(u => memberIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(ct);

        static string? NameOf(string? first, string? last)
            => string.IsNullOrWhiteSpace(last) ? first : $"{first} {last}";

        var members = users
            .Select(u => new MinimalUserDto(u.Id, NameOf(u.FirstName, u.LastName), u.Email))
            .ToList();

        return new ConversationDetailDto(conv.Id, conv.Name ?? "", conv.Type.ToString(), conv.IsPrivate, members);
    }
}
