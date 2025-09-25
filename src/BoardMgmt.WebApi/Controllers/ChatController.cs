using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Application.Chat;
using BoardMgmt.Application.Chat.Handlers; // not strictly required if using MediatR only
using BoardMgmt.Domain.Chat;
using BoardMgmt.WebApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using BoardMgmt.Application.Common.Interfaces; // Your ICurrentUser abstraction

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ICurrentUser _current;

    public ChatController(ISender mediator, IHubContext<ChatHub> hub, ICurrentUser current)
    { _mediator = mediator; _hub = hub; _current = current; }

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
        var memberIds = (b.MemberIds ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
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
        await _hub.Clients.Group($"conv:{id}").SendAsync("MessageCreated", new { id = msgId, conversationId = id });
        return Ok(new { id = msgId });
    }

    public record EditMessageBody(string BodyHtml);

    [HttpPut("messages/{id:guid}")]
    public async Task<ActionResult> Edit(Guid id, [FromBody] EditMessageBody body)
    {
        var ok = await _mediator.Send(new EditChatMessageCommand(id, CurrentUserId, body.BodyHtml));
        if (ok) await _hub.Clients.All.SendAsync("MessageEdited", new { id });
        return ok ? Ok() : NotFound();
    }

    [HttpDelete("messages/{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var ok = await _mediator.Send(new DeleteChatMessageCommand(id, CurrentUserId));
        if (ok) await _hub.Clients.All.SendAsync("MessageDeleted", new { id });
        return ok ? Ok() : NotFound();
    }

    // ===== Reactions =====

    [HttpPost("messages/{id:guid}/reactions")]
    public async Task<ActionResult> AddReaction(Guid id, [FromBody] string emoji)
    {
        var ok = await _mediator.Send(new AddReactionCommand(id, CurrentUserId, emoji));
        if (ok) await _hub.Clients.All.SendAsync("ReactionUpdated", new { messageId = id });
        return ok ? Ok() : NotFound();
    }

    [HttpDelete("messages/{id:guid}/reactions")]
    public async Task<ActionResult> RemoveReaction(Guid id, [FromBody] string emoji)
    {
        var ok = await _mediator.Send(new RemoveReactionCommand(id, CurrentUserId, emoji));
        if (ok) await _hub.Clients.All.SendAsync("ReactionUpdated", new { messageId = id });
        return ok ? Ok() : NotFound();
    }

    // ===== Attachments =====

    [RequestSizeLimit(50_000_000)]
    [HttpPost("messages/{id:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<object>>> Upload(Guid id, [FromServices] DbContext db)
    {
        if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
            return BadRequest(new { error = "No files uploaded" });

        Directory.CreateDirectory(Path.Combine("storage", "chat", id.ToString()));
        var toSave = new List<(string FileName, string ContentType, long FileSize, string StoragePath)>();

        foreach (var f in Request.Form.Files)
        {
            var path = Path.Combine("storage", "chat", id.ToString(), f.FileName);
            await using var fs = System.IO.File.Create(path);
            await f.CopyToAsync(fs);
            toSave.Add((f.FileName, f.ContentType ?? "application/octet-stream", f.Length, path));
        }

        var added = await _mediator.Send(new AddChatAttachmentsCommand(id, toSave));
        var atts = await db.Set<ChatAttachment>()
            .Where(a => a.MessageId == id)
            .Select(a => new { a.Id, a.FileName, a.ContentType, a.FileSize })
            .ToListAsync();

        return Ok(atts.Select(a => new {
            attachmentId = a.Id,
            fileName = a.FileName,
            contentType = a.ContentType,
            fileSize = a.FileSize
        }));
    }

    [HttpGet("messages/{messageId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid messageId, Guid attachmentId, [FromServices] DbContext db)
    {
        var att = await db.Set<ChatAttachment>().FirstOrDefaultAsync(a => a.Id == attachmentId && a.MessageId == messageId);
        if (att is null) return NotFound();
        var stream = System.IO.File.OpenRead(att.StoragePath);
        return File(stream, att.ContentType, att.FileName);
    }

    // ===== Typing & search =====

    [HttpPost("conversations/{id:guid}/typing")]
    public async Task<ActionResult> Typing(Guid id, [FromBody] bool isTyping)
    {
        await _hub.Clients.Group($"conv:{id}").SendAsync("Typing", new { conversationId = id, userId = CurrentUserId, isTyping });
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
        var ids = (b.MemberIds ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        // handler ensures creator is included and min size == 2
        var id = await _mediator.Send(new CreateDirectConversationCommand(CurrentUserId, ids));
        return Ok(new { id });
    }

    [HttpPost("direct/{otherUserId}")]
    public async Task<ActionResult<object>> StartDirect(string otherUserId)
    {
        // MediatR command you should already have; or implement similarly:
        // Creates or returns existing 1:1 Direct conversation between CurrentUserId and otherUserId
        var id = await _mediator.Send(new CreateOrGetDirectConversationCommand(CurrentUserId, otherUserId));
        return Ok(new { id });
    }


}
