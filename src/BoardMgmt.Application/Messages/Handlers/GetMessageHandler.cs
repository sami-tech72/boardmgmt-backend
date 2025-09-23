using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Application.Messages.Queries;
using BoardMgmt.Application.Messages.DTOs;
using BoardMgmt.Application.Messages._Mapping;
using BoardMgmt.Domain.Messages;

public class GetMessageHandler : IRequestHandler<GetMessageQuery, MessageDto>
{
    private readonly DbContext _db;
    public GetMessageHandler(DbContext db) => _db = db;

    public async Task<MessageDto> Handle(GetMessageQuery req, CancellationToken ct)
    {
        var msg = await _db.Set<Message>()
            .Include(m => m.Recipients)
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == req.MessageId, ct)
            ?? throw new KeyNotFoundException("Message not found");

        return MessageMapping.ToDto(msg);
    }
}
