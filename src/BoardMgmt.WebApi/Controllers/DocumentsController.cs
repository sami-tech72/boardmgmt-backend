using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.Commands.UploadDocuments;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Application.Documents.Queries.GetDocumentById;
using BoardMgmt.Application.Documents.Queries.ListDocuments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorage _storage;

    public DocumentsController(IMediator mediator, IFileStorage storage)
    {
        _mediator = mediator;
        _storage = storage;
    }


    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id)
    {
        var dto = await _mediator.Send(new GetDocumentByIdQuery(id));
        if (dto is null) return NotFound();
        return Ok(dto);
    }
    /// <summary>
    /// List documents with optional filtering.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List(
        [FromQuery] string? folderSlug,
        [FromQuery] string? type,
        [FromQuery] string? search,
        [FromQuery] string? datePreset)
    {
        var result = await _mediator.Send(new ListDocumentsQuery(folderSlug, type, search, datePreset));
        return Ok(result);
    }

    /// <summary>
    /// Upload one or more files to a folder/meeting with optional role restrictions.
    /// Send as multipart/form-data. For roles, pass multiple `roleIds` fields.
    /// </summary>
    [HttpPost("upload")]
    [Authorize] // dynamic permission is enforced inside the handler
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52428800 * 5)] // ~250 MB total request limit (adjust to your needs)
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> Upload(
        [FromForm] Guid? meetingId,
        [FromForm] string? folderSlug,
        [FromForm] string? description,
        [FromForm] List<string>? roleIds // dynamic AspNetRoles IDs
    )
    {
        // Touch the field so IDE0052 (unused field) won't fire if warnings-as-errors are enabled.
        _ = _storage;

        var files = Request.Form.Files;
        if (files.Count == 0)
            return BadRequest("No files sent.");

        var items = new List<UploadDocumentsCommand.UploadItem>();
        foreach (var f in files)
        {
            items.Add(new UploadDocumentsCommand.UploadItem(
                f.OpenReadStream(),
                f.FileName,
                f.ContentType,
                f.Length));
        }

        var result = await _mediator.Send(new UploadDocumentsCommand(
            meetingId,
            string.IsNullOrWhiteSpace(folderSlug) ? "root" : folderSlug!,
            description,
            roleIds,
            items));

        return Ok(result);
    }
}
