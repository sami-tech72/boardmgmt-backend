using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.Commands.UploadDocuments;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Application.Documents.Queries.ListDocuments;
using BoardMgmt.Domain.Entities;
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
    { _mediator = mediator; _storage = storage; }

    // GET api/documents?folderSlug=&type=&search=&datePreset=
    [HttpGet]
    [Authorize] // or AllowAnonymous if observers are public
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List(
        [FromQuery] string? folderSlug, [FromQuery] string? type,
        [FromQuery] string? search, [FromQuery] string? datePreset)
        => Ok(await _mediator.Send(new ListDocumentsQuery(folderSlug, type, search, datePreset)));


    // POST api/documents/upload  (multipart/form-data)
    [HttpPost("upload")]
    [Authorize(Roles = "Admin,BoardMember,CommitteeMember,Secretary")]
    [RequestSizeLimit(52428800 * 5)]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> Upload(
        [FromForm] Guid? meetingId,
        [FromForm] string? folderSlug,
        [FromForm] string? description,

        // Option A: a single integer bitmask
        [FromForm] int? access,

        // Option B: four booleans (matches your UI checkboxes)
        [FromForm] bool? boardMembers,
        [FromForm] bool? committeeMembers,
        [FromForm] bool? observers,
        [FromForm] bool? administrators)
    {
        var files = Request.Form.Files;
        if (files.Count == 0) return BadRequest("No files sent.");

        var items = new List<UploadDocumentsCommand.UploadItem>();
        foreach (var f in files)
            items.Add(new UploadDocumentsCommand.UploadItem(f.OpenReadStream(), f.FileName, f.ContentType, f.Length));

        DocumentAccess flags;
        if (access.HasValue)
        {
            flags = (DocumentAccess)access.Value;
        }
        else
        {
            flags = DocumentAccess.None;
            if (boardMembers == true) flags |= DocumentAccess.BoardMembers;
            if (committeeMembers == true) flags |= DocumentAccess.CommitteeMembers;
            if (observers == true) flags |= DocumentAccess.Observers;
            if (administrators == true) flags |= DocumentAccess.Administrators;
            if (flags == DocumentAccess.None)
                flags = DocumentAccess.Administrators | DocumentAccess.BoardMembers;
        }

        var result = await _mediator.Send(new UploadDocumentsCommand(
            meetingId,
            string.IsNullOrWhiteSpace(folderSlug) ? "root" : folderSlug!,
            description,
            flags,
            items));

        return Ok(result);
    }

    // (download & delete can stay as you had)
}
