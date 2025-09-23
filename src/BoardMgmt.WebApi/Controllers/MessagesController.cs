using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Messages.Commands;
using BoardMgmt.Application.Messages.Queries;
using BoardMgmt.WebApi.Common.Http; // ✅ use the extensions
using BoardMgmt.WebApi.Controllers.Messages;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorage _files;

    public MessagesController(IMediator mediator, IFileStorage files)
    {
        _mediator = mediator;
        _files = files;
    }

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

        // Angular expects { items, total }
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
        var senderId = Guid.NewGuid(); // TODO: replace with authenticated user

        var res = await _mediator.Send(new CreateMessageCommand(
            senderId, body.Subject, body.Body, body.Priority,
            body.ReadReceiptRequested, body.IsConfidential, body.RecipientIds, body.asDraft), ct);

        return this.OkApi(res, "Message created");
        // or if you want `201 Created`:
        // return this.CreatedApi(nameof(Get), new { id = res.Id }, res, "Message created");
    }

    // PUT /api/messages/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMessageBody body, CancellationToken ct)
    {
        var res = await _mediator.Send(new UpdateMessageCommand(
            id, body.Subject, body.Body, body.Priority,
            body.ReadReceiptRequested, body.IsConfidential, body.RecipientIds), ct);

        return this.OkApi(res, "Message updated");
    }

    // DELETE /api/messages/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new DeleteMessageCommand(id), ct);
        return ok
            ? this.OkApi(new { id }, "Message deleted")
            : this.NotFoundApi("not_found", "Message not found");
    }

    // POST /api/messages/{id}/send
    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct)
    {
        var res = await _mediator.Send(new SendMessageCommand(id), ct);
        return this.OkApi(res, "Message sent");
    }

    // POST /api/messages/{id}/attachments
    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(Guid id, CancellationToken ct)
    {
        if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
            return this.BadRequestApi("no_files", "No files uploaded");

        var saved = new List<object>();
        foreach (var file in Request.Form.Files)
        {
            await using var s = file.OpenReadStream();
            var path = await _files.SaveAsync("messages", file.FileName, s, ct);
            saved.Add(new { file.FileName, file.ContentType, file.Length, path });
            // TODO: persist MessageAttachment via DbContext
        }

        return this.OkApi(saved, "Attachments uploaded");
    }
}
