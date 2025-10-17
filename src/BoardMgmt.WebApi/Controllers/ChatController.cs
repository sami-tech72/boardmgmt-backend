// Controllers/ChatController.cs
using System.IO;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Application.Chat;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Domain.Entities;
using BoardMgmt.WebApi.Auth;
using BoardMgmt.WebApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using BoardMgmt.Application.Common.Interfaces;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = PolicyNames.Messages.View)]
public class ChatController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ICurrentUser _current;
    private readonly IAppDbContext _db;

    public ChatController(ISender mediator, IHubContext<ChatHub> hub, ICurrentUser current, IAppDbContext db)
    {
        _mediator = mediator;
        _hub = hub;
        _current = current;
        _db = db;
    }

    private string CurrentUserId => _current.UserId ?? throw new UnauthorizedAccessException("No user");

    // ===== Conversations =====

    [HttpGet("conversations")]
    public Task<IReadOnlyList<ConversationListItemDto>> ListConversations()
        => _mediator.Send(new ListConversationsQuery(CurrentUserId));

    [HttpGet("conversations/{id:guid}")]
    public Task<ConversationDetailDto> GetConversation(Guid id)
        => _mediator.Send(new GetConversationQuery(id, CurrentUserId));

    [HttpPost("conversations/{id:guid}/join")]
    public Task<bool> Join(Guid id)
        => _mediator.Send(new JoinChannelCommand(id, CurrentUserId));

    [HttpPost("conversations/{id:guid}/leave")]
    public Task<bool> Leave(Guid id)
        => _mediator.Send(new LeaveConversationCommand(id, CurrentUserId));

    [HttpPost("conversations/{id:guid}/read")]
    public Task<bool> MarkRead(Guid id, [FromBody] DateTime? readAtUtc)
        => _mediator.Send(new MarkConversationReadCommand(id, CurrentUserId, readAtUtc ?? DateTime.UtcNow));

    // ===== Channels =====

    public record CreateChannelBody(string Name, bool IsPrivate, IReadOnlyList<string> MemberIds);

    [HttpPost("channels")]
    public async Task<ActionResult<object>> CreateChannel([FromBody] CreateChannelBody b)
    {
        var memberIds = (b.MemberIds ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

        if (!memberIds.Contains(CurrentUserId)) memberIds.Add(CurrentUserId);

        var id = await _mediator.Send(new CreateChannelCommand(CurrentUserId, b.Name, b.IsPrivate, memberIds));
        return Ok(new { id });
    }

    // ===== Messages & history =====

    [HttpGet("conversations/{id:guid}/history")]
    public Task<PagedResult<ChatMessageDto>> History(Guid id, [FromQuery] DateTime? beforeUtc, [FromQuery] int take = 50)
        => _mediator.Send(new GetHistoryQuery(id, beforeUtc, take, CurrentUserId));

    [HttpGet("threads/{rootId:guid}")]
    public async Task<PagedResult<ChatMessageDto>> Thread(Guid rootId, [FromQuery] DateTime? beforeUtc, [FromQuery] int take = 50)
    {
        var convId = await _mediator.Send(new GetThreadConversationIdQuery(rootId));
        return await _mediator.Send(new GetHistoryQuery(convId, beforeUtc, take, CurrentUserId, rootId));
    }

    public record CreateMessageBody(string BodyHtml, Guid? ThreadRootId);

    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<ActionResult<object>> SendMessage(Guid id, [FromBody] CreateMessageBody body)
    {
        var msgId = await _mediator.Send(new SendChatMessageCommand(id, CurrentUserId, body.BodyHtml, body.ThreadRootId));

        // include threadRootId for thread panes to live-update
        var payload = await BuildMessageChangePayload(msgId);
        if (payload is not null)
        {
            await ChatHubEvents.MessageCreated(_hub, payload);
        }

        return Ok(new { id = msgId });
    }

    public record EditMessageBody(string BodyHtml);

    [HttpPut("messages/{id:guid}")]
    public async Task<ActionResult> Edit(Guid id, [FromBody] EditMessageBody body)
    {
        var ok = await _mediator.Send(new EditChatMessageCommand(id, CurrentUserId, body.BodyHtml));
        if (!ok) return NotFound();

        var payload = await BuildMessageChangePayload(id);
        if (payload is not null)
        {
            await ChatHubEvents.MessageEdited(_hub, payload);
        }

        return Ok();
    }

    [HttpDelete("messages/{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var ok = await _mediator.Send(new DeleteChatMessageCommand(id, CurrentUserId));
        if (!ok) return NotFound();

        var payload = await BuildMessageChangePayload(id);
        if (payload is not null)
        {
            await ChatHubEvents.MessageDeleted(_hub, payload);
        }

        return Ok();
    }

    // ===== Reactions =====

    [HttpPost("messages/{id:guid}/reactions")]
    public async Task<ActionResult> AddReaction(Guid id, [FromBody] string emoji)
    {
        var ok = await _mediator.Send(new AddReactionCommand(id, CurrentUserId, emoji));
        if (!ok) return NotFound();

        var msg = await _db.Set<ChatMessage>()
            .AsNoTracking()
            .Select(m => new { m.Id, m.ConversationId, m.ThreadRootId })
            .FirstOrDefaultAsync(m => m.Id == id);

        if (msg is not null)
        {
            var reactions = await BuildReactionPayload(msg.Id);

            await ChatHubEvents.ReactionUpdated(_hub, msg.ConversationId, new
            {
                messageId = msg.Id,
                conversationId = msg.ConversationId,
                threadRootId = msg.ThreadRootId,
                reactions
            });
        }

        return Ok();
    }

    [HttpDelete("messages/{id:guid}/reactions")]
    public async Task<ActionResult> RemoveReaction(Guid id, [FromBody] string emoji)
    {
        var ok = await _mediator.Send(new RemoveReactionCommand(id, CurrentUserId, emoji));
        if (!ok) return NotFound();

        var msg = await _db.Set<ChatMessage>()
            .AsNoTracking()
            .Select(m => new { m.Id, m.ConversationId, m.ThreadRootId })
            .FirstOrDefaultAsync(m => m.Id == id);

        if (msg is not null)
        {
            var reactions = await BuildReactionPayload(msg.Id);

            await ChatHubEvents.ReactionUpdated(_hub, msg.ConversationId, new
            {
                messageId = msg.Id,
                conversationId = msg.ConversationId,
                threadRootId = msg.ThreadRootId,
                reactions
            });
        }

        return Ok();
    }

    //private async Task<IReadOnlyList<ReactionDto>> BuildReactionPayload(Guid messageId)
    //{
    //    var reactions = await _db.Set<ChatReaction>()
    //    .Where(r => r.MessageId == messageId)
    //    .GroupBy(r => r.Emoji)
    //    .Select(g => new ReactionDto(
    //        g.Key,
    //        g.Count(),
    //        g.Any(r => r.UserId == CurrentUserId)))
    //    .ToListAsync();

    //    return reactions;
    //}

    private async Task<IReadOnlyList<ReactionDto>> BuildReactionPayload(Guid messageId)
    {
        var rows = await _db.Set<ChatReaction>()
            .Where(r => r.MessageId == messageId)
            .GroupBy(r => r.Emoji)
            .Select(g => new
            {
                Emoji = g.Key,
                Count = g.Count(),
                ReactedByMe = g.Any(r => r.UserId == CurrentUserId)
            })
            .ToListAsync();

        return rows
            .Select(x => new ReactionDto(Emoji: x.Emoji, Count: x.Count, ReactedByMe: x.ReactedByMe))
            .ToList();
    }


    // ===== Attachments =====

    [RequestSizeLimit(50_000_000)]
    [HttpPost("messages/{id:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<object>>> Upload(Guid id)
    {
        if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
            return BadRequest(new { error = "No files uploaded" });

        var storageRoot = Path.Combine("storage", "chat", id.ToString());
        Directory.CreateDirectory(storageRoot);
        var toSave = new List<(string FileName, string ContentType, long FileSize, string StoragePath)>();

        foreach (var f in Request.Form.Files)
        {
            var originalName = SanitizeFileName(f.FileName);
            var extension = Path.GetExtension(originalName);
            var storageFileName = $"{Guid.NewGuid():N}{extension}";
            var storagePath = Path.Combine(storageRoot, storageFileName);

            await using var fs = System.IO.File.Create(storagePath);
            await f.CopyToAsync(fs);

            toSave.Add((originalName, f.ContentType ?? "application/octet-stream", f.Length, storagePath));
        }

        var _ = await _mediator.Send(new AddChatAttachmentsCommand(id, toSave));

        var atts = await _db.Set<ChatAttachment>()
            .Where(a => a.MessageId == id)
            .Select(a => new { a.Id, a.FileName, a.ContentType, a.FileSize })
            .ToListAsync();

        return Ok(atts.Select(a => new
        {
            attachmentId = a.Id,
            fileName = a.FileName,
            contentType = a.ContentType,
            fileSize = a.FileSize
        }));
    }

    [HttpGet("messages/{messageId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid messageId, Guid attachmentId)
    {
        var att = await _db.Set<ChatAttachment>()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.MessageId == messageId);

        if (att is null || !System.IO.File.Exists(att.StoragePath)) return NotFound();

        var stream = System.IO.File.OpenRead(att.StoragePath);
        return File(stream, att.ContentType, att.FileName);
    }

    // ===== Typing & search =====

    [HttpPost("conversations/{id:guid}/typing")]
    public async Task<ActionResult> Typing(Guid id, [FromBody] bool isTyping)
    {
        await ChatHubEvents.Typing(_hub, id, CurrentUserId, isTyping);
        return Ok();
    }

    [HttpGet("search")]
    public Task<IReadOnlyList<ChatMessageDto>> Search([FromQuery] string term, [FromQuery] int take = 50)
        => _mediator.Send(new SearchMessagesQuery(CurrentUserId, term, take));

    // ===== Direct Messages =====

    public record CreateDirectBody(IReadOnlyList<string> MemberIds);

    [HttpPost("directs")]
    public async Task<ActionResult<object>> CreateDirect([FromBody] CreateDirectBody b)
    {
        var ids = (b.MemberIds ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

        var id = await _mediator.Send(new CreateDirectConversationCommand(CurrentUserId, ids));
        return Ok(new { id });
    }

    [HttpPost("direct/{otherUserId}")]
    public async Task<ActionResult<object>> StartDirect(string otherUserId)
    {
        var id = await _mediator.Send(new CreateOrGetDirectConversationCommand(CurrentUserId, otherUserId));
        return Ok(new { id });
    }

    private static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "attachment";
        }

        var justName = Path.GetFileName(fileName);

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            justName = justName.Replace(invalid, '_');
        }

        if (string.IsNullOrWhiteSpace(justName))
        {
            return "attachment";
        }

        return justName.Length > 255 ? justName[..255] : justName;
}

    private static string? NameOf(string? first, string? last)
        => string.IsNullOrWhiteSpace(last) ? first : $"{first} {last}";

    private async Task<ChatMessageDto?> LoadMessageDto(Guid messageId)
    {
        var message = await _db.Set<ChatMessage>()
            .Where(m => m.Id == messageId)
            .Select(m => new
            {
                m.Id,
                m.ConversationId,
                m.ThreadRootId,
                m.SenderId,
                m.BodyHtml,
                m.CreatedAtUtc,
                m.EditedAtUtc,
                m.DeletedAtUtc,
                Attachments = m.Attachments
                    .Select(a => new ChatAttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSize))
                    .ToList(),
                Reactions = m.Reactions
                    .GroupBy(r => r.Emoji)
                    .Select(g => new { Emoji = g.Key, Count = g.Count() })
                    .ToList(),
                ThreadReplyCount = _db.Set<ChatMessage>()
                    .Count(x => x.ThreadRootId == m.Id && x.DeletedAtUtc == null)
            })
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (message is null) return null;

        var sender = await _db.Set<AppUser>()
            .Where(u => u.Id == message.SenderId)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync();

        var from = new MinimalUserDto(
            message.SenderId,
            NameOf(sender?.FirstName, sender?.LastName),
            sender?.Email);

        var reactions = message.Reactions
            .Select(r => new ReactionDto(r.Emoji, r.Count, false))
            .ToList();

        return new ChatMessageDto(
            message.Id,
            message.ConversationId,
            message.ThreadRootId,
            from,
            message.BodyHtml,
            message.CreatedAtUtc,
            message.EditedAtUtc,
            message.DeletedAtUtc != null,
            message.Attachments,
            reactions,
            message.ThreadReplyCount);
    }

    private async Task<ChatHubEvents.MessageChangePayload?> BuildMessageChangePayload(Guid messageId)
    {
        var message = await LoadMessageDto(messageId);
        if (message is null) return null;

        ChatMessageDto? threadRoot = null;
        if (message.ThreadRootId is Guid rootId)
        {
            threadRoot = await LoadMessageDto(rootId);
        }

        return new ChatHubEvents.MessageChangePayload(
            message.ConversationId,
            message.ThreadRootId,
            message,
            threadRoot);
    }
}