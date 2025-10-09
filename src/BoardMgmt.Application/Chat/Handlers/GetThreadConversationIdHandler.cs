namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Application.Common.Interfaces;

public sealed class GetThreadConversationIdHandler : IRequestHandler<GetThreadConversationIdQuery, Guid>
{
    private readonly IAppDbContext _db;
    public GetThreadConversationIdHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(GetThreadConversationIdQuery req, CancellationToken ct)
    {
        var m = await _db.Set<ChatMessage>().FirstOrDefaultAsync(x => x.Id == req.ThreadRootMessageId, ct)
                ?? throw new KeyNotFoundException("Thread root not found");
        return m.ConversationId;
    }
}
