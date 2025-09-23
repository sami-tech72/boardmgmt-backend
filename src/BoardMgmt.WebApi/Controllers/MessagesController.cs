using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Messages.Commands;
using BoardMgmt.Application.Messages.Queries;
using BoardMgmt.WebApi.Common.Http;
using BoardMgmt.WebApi.Controllers.Messages;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorage _files;
    private readonly DbContext _db;
    private readonly ICurrentUser _currentUser;

    public MessagesController(IMediator mediator, IFileStorage files, DbContext db, ICurrentUser currentUser)
    {
        _mediator = mediator;
        _files = files;
        _db = db;
        _currentUser = currentUser;
    }

    private Guid GetUserIdOrThrow()
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            throw new UnauthorizedAccessException();

        return Guid.Parse(_currentUser.UserId);
    }


    // GET /api/messages/{id}/thread
    [HttpGet("{id:guid}/thread")]
    public async Task<IActionResult> Thread(Guid id, CancellationToken ct)
    {
        // TODO: replace with your auth current user id
        var currentUserId = GetUserIdOrThrow();

        var vm = await _mediator.Send(new GetMessageThreadQuery(id, currentUserId), ct);
        return this.OkApi(vm, "Thread loaded");
    }


    // GET /api/messages
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? forUserId,
        [FromQuery] Guid? sentByUserId,
        [FromQuery] string? q,
        [FromQuery] string? priority,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var res = await _mediator.Send(
            new ListMessageItemsQuery(forUserId, sentByUserId, q, priority, status, page, pageSize),
            ct);

        return this.OkApi(new { items = res.Items, total = res.Total }, "Messages loaded");
    }

    // GET /api/messages/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var res = await _mediator.Send(new GetMessageViewQuery(id), ct);
        if (res is null) return this.NotFoundApi("not_found", "Message not found");
        return this.OkApi(res, "Message loaded");
    }

    // POST /api/messages
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMessageBody body, CancellationToken ct)
    {
        var senderId = GetUserIdOrThrow();

        var res = await _mediator.Send(new CreateMessageCommand(
            senderId,
            body.Subject,
            body.Body,
            body.Priority,
            body.ReadReceiptRequested,
            body.IsConfidential,
            body.RecipientIds,
            body.asDraft
        ), ct);

        return this.OkApi(res, "Message created");
    }


    // PUT /api/messages/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMessageBody body, CancellationToken ct)
    {
        var res = await _mediator.Send(new UpdateMessageCommand(
            id,
            body.Subject,
            body.Body,
            body.Priority,
            body.ReadReceiptRequested,
            body.IsConfidential,
            body.RecipientIds
        ), ct);

        return this.OkApi(res, "Message updated");
    }

    // DELETE /api/messages/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new DeleteMessageCommand(id), ct);
        return ok ? this.OkApi(new { id }, "Message deleted")
                  : this.NotFoundApi("not_found", "Message not found");
    }

    // POST /api/messages/{id}/send
    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct)
    {
        var res = await _mediator.Send(new SendMessageCommand(id), ct);
        return this.OkApi(res, "Message sent");
    }

    // POST /api/messages/{id}/read
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var userId = GetUserIdOrThrow();
        var ok = await _mediator.Send(new MarkMessageReadCommand(id, userId), ct);

        return ok ? this.OkApi(new { id }, "Marked as read")
                  : this.NotFoundApi("not_recipient", "Not a recipient or message not found");
    }


    // POST /api/messages/{id}/attachments
    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(Guid id, CancellationToken ct)
    {
        if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
            return this.BadRequestApi("no_files", "No files uploaded");

        var msg = await _db.Set<BoardMgmt.Domain.Messages.Message>()
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (msg is null) return this.NotFoundApi("not_found", "Message not found");

        var savedDtos = new List<object>();

        foreach (var file in Request.Form.Files)
        {
            await using var s = file.OpenReadStream();
            var path = await _files.SaveAsync("messages", file.FileName, s, ct);

            var att = new BoardMgmt.Domain.Messages.MessageAttachment
            {
                Id = Guid.NewGuid(),
                MessageId = msg.Id,
                FileName = file.FileName,
                ContentType = file.ContentType ?? "application/octet-stream",
                FileSize = file.Length,
                StoragePath = path
            };
            msg.Attachments.Add(att);

            savedDtos.Add(new
            {
                attachmentId = att.Id,
                fileName = att.FileName,
                contentType = att.ContentType,
                fileSize = att.FileSize
            });
        }

        await _db.SaveChangesAsync(ct);
        return this.OkApi(savedDtos, "Attachments uploaded");
    }

    // GET /api/messages/{id}/attachments/{attachmentId}
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var att = await _db.Set<BoardMgmt.Domain.Messages.MessageAttachment>()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.MessageId == id, ct);

        if (att is null) return this.NotFoundApi("not_found", "Attachment not found");

        var stream = await _files.OpenAsync(att.StoragePath, ct);
        return File(stream, att.ContentType, att.FileName);
    }
}
