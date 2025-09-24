using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Messages.Commands;
using BoardMgmt.Application.Messages.DTOs;
using BoardMgmt.Application.Messages.Queries;
using BoardMgmt.Domain.Messages;
using BoardMgmt.WebApi.Hubs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IHubContext<MessagesHub> _hub;
    private readonly ICurrentUser _current;
    public MessagesController(ISender mediator, IHubContext<MessagesHub> hub, ICurrentUser current)
    { _mediator = mediator; _hub = hub; _current = current; }

    private Guid CurrentUserId => Guid.TryParse(_current.UserId, out var g)
        ? g : throw new UnauthorizedAccessException("No valid user id");

    public record CreateMessageBody(string Subject, string Body, string Priority,
        bool ReadReceiptRequested, bool IsConfidential, IReadOnlyList<string> RecipientIds, bool AsDraft);

    [HttpGet]
    public Task<PagedResult<MessageListItemVm>> List([FromQuery] Guid? forUserId, [FromQuery] Guid? sentByUserId,
        [FromQuery] string? q, [FromQuery] string? priority, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        _mediator.Send(new ListMessageItemsQuery(forUserId, sentByUserId, q, priority, status, page, pageSize));

    [HttpGet("{id:guid}")]
    public Task<MessageDetailVm?> Get(Guid id) => _mediator.Send(new GetMessageViewQuery(id));

    [HttpGet("{id:guid}/thread")]
    public Task<MessageThreadVm> Thread(Guid id) => _mediator.Send(new GetMessageThreadQuery(id, CurrentUserId));

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateMessageBody body)
    {
        var guids = body.RecipientIds.Where(x => Guid.TryParse(x, out _)).Select(Guid.Parse).Distinct().ToList();
        var id = await _mediator.Send(new CreateMessageCommand(CurrentUserId, body.Subject, body.Body,
            body.Priority, body.ReadReceiptRequested, body.IsConfidential, guids, body.AsDraft));

        if (!body.AsDraft)
            foreach (var rid in guids)
                await _hub.Clients.Group($"user:{rid}").SendAsync("NewMessage", new { id });

        return Ok(new { id });
    }

    [HttpPost("{id:guid}/send")]
    public async Task<ActionResult<object>> Send(Guid id)
    {
        var ok = await _mediator.Send(new SendMessageCommand(id));
        if (!ok) return NotFound();
        return Ok(new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, [FromBody] CreateMessageBody body)
    {
        var guids = body.RecipientIds.Where(x => Guid.TryParse(x, out _)).Select(Guid.Parse).Distinct().ToList();
        var ok = await _mediator.Send(new UpdateMessageCommand(id, body.Subject, body.Body, body.Priority,
            body.ReadReceiptRequested, body.IsConfidential, guids));
        return ok ? Ok(new { id }) : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<object>> Delete(Guid id)
        => (await _mediator.Send(new DeleteMessageCommand(id))) ? Ok(new { id }) : NotFound();

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<object>> MarkRead(Guid id)
    {
        var ok = await _mediator.Send(new MarkMessageReadCommand(id, CurrentUserId));
        if (!ok) return NotFound();
        await _hub.Clients.Group($"user:{CurrentUserId}").SendAsync("ReadReceipt", new { messageId = id, userId = CurrentUserId });
        return Ok(new { id });
    }

    [RequestSizeLimit(50_000_000)]
    [HttpPost("{id:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<object>>> Upload(Guid id)
    {
        if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
            return BadRequest(new { error = "No files uploaded" });

        var toSave = new List<(string FileName, string ContentType, long FileSize, string StoragePath)>();
        Directory.CreateDirectory(Path.Combine("storage", "messages", id.ToString()));
        foreach (var f in Request.Form.Files)
        {
            var path = Path.Combine("storage", "messages", id.ToString(), f.FileName);
            await using var fs = System.IO.File.Create(path);
            await f.CopyToAsync(fs);
            toSave.Add((f.FileName, f.ContentType ?? "application/octet-stream", f.Length, path));
        }

        await _mediator.Send(new AddMessageAttachmentsCommand(id, toSave));
        var dtos = toSave.Select(x => new { attachmentId = Guid.NewGuid(), fileName = x.FileName, contentType = x.ContentType, fileSize = x.FileSize });
        return Ok(dtos);
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid id, Guid attachmentId, [FromServices] DbContext db)
    {
        var att = await db.Set<MessageAttachment>().FirstOrDefaultAsync(a => a.Id == attachmentId && a.MessageId == id);
        if (att is null) return NotFound();
        var stream = System.IO.File.OpenRead(att.StoragePath);
        return File(stream, att.ContentType, att.FileName);
    }
}
