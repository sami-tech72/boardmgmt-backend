using BoardMgmt.Application.Documents.Commands.DeleteDocument;
using BoardMgmt.Application.Documents.Commands.ReplaceDocumentFile;
using BoardMgmt.Application.Documents.Commands.UpdateDocument;
using BoardMgmt.Application.Documents.Commands.UploadDocuments;
using BoardMgmt.Application.Documents.Queries.DownloadDocument;
using BoardMgmt.Application.Documents.Queries.GetDocumentById;
using BoardMgmt.Application.Documents.Queries.ListDocuments;
using BoardMgmt.WebApi.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    public DocumentsController(IMediator mediator) => _mediator = mediator;

    // READ
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Documents.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetDocumentByIdQuery(id), ct);
        if (dto is null) return this.NotFoundApi("documents.not_found", $"Document '{id}' was not found.");
        return this.OkApi(dto);
    }

    [HttpGet]
    [Authorize(Policy = "Documents.View")]
    public async Task<IActionResult> List([FromQuery] string? folderSlug, [FromQuery] string? type,
                                          [FromQuery] string? search, [FromQuery] string? datePreset,
                                          CancellationToken ct)
    {
        var result = await _mediator.Send(new ListDocumentsQuery(folderSlug, type, search, datePreset), ct);
        return this.OkApi(result);
    }

    // CREATE (multipart)
    [HttpPost]
    [Authorize(Policy = "Documents.Create")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(250 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] Guid? meetingId,
                                            [FromForm] string? folderSlug,
                                            [FromForm] string? description,
                                            [FromForm] List<string>? roleIds,
                                            CancellationToken ct)
    {
        var files = Request.Form?.Files;
        if (files is null || files.Count == 0)
            return this.BadRequestApi("documents.no_files", "No files were provided.");

        var items = new List<UploadDocumentsCommand.UploadItem>(files.Count);
        foreach (var f in files)
        {
            if (f is null || f.Length == 0) continue;
            items.Add(new UploadDocumentsCommand.UploadItem(
                f.OpenReadStream(),
                f.FileName,
                string.IsNullOrWhiteSpace(f.ContentType) ? "application/octet-stream" : f.ContentType,
                f.Length));
        }

        if (items.Count == 0)
            return this.BadRequestApi("documents.empty_files", "All uploaded files were empty.");

        var result = await _mediator.Send(new UploadDocumentsCommand(
            meetingId,
            string.IsNullOrWhiteSpace(folderSlug) ? "root" : folderSlug!,
            description,
            (roleIds != null && roleIds.Count > 0) ? roleIds : null,
            items), ct);

        var first = result.FirstOrDefault();
        if (first is not null)
            return this.CreatedApi(nameof(GetById), new { id = first.Id }, result);

        return this.CreatedApi(nameof(List), null, result, "No documents created.");
    }

    // UPDATE (JSON metadata only)
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Documents.Update")]
    public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] UpdateDocumentCommand body, CancellationToken ct)
    {
        if (id != body.Id)
            return this.BadRequestApi("documents.mismatched_id", "The route id does not match the body id.");

        var dto = await _mediator.Send(body, ct);
        return this.OkApi(dto, "Document updated.");
    }

    // UPDATE (multipart metadata + optional file)
    [HttpPut("{id:guid}/form")]
    [Authorize(Policy = "Documents.Update")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UpdateViaForm(Guid id,
        [FromForm] string? originalName,
        [FromForm] string? description,
        [FromForm] string? folderSlug,
        [FromForm] List<string>? roleIds,
        CancellationToken ct)
    {
        var dto = await _mediator.Send(new UpdateDocumentCommand(
            id,
            string.IsNullOrWhiteSpace(originalName) ? null : originalName,
            description,
            string.IsNullOrWhiteSpace(folderSlug) ? null : folderSlug,
            (roleIds != null && roleIds.Count > 0) ? roleIds : null
        ), ct);

        var file = Request.Form.Files.FirstOrDefault();
        if (file is not null && file.Length > 0)
        {
            dto = await _mediator.Send(new ReplaceDocumentFileCommand(
                Id: id,
                OriginalName: string.IsNullOrWhiteSpace(originalName) ? file.FileName : originalName!,
                ContentType: string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                SizeBytes: file.Length,
                Content: file.OpenReadStream()
            ), ct);
        }

        return this.OkApi(dto, file is null ? "Document updated." : "Document and file updated.");
    }

    // REPLACE file only
    [HttpPut("{id:guid}/file")]
    [Authorize(Policy = "Documents.Update")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> ReplaceFile(Guid id, [FromForm] IFormFile? file, [FromForm] string? displayName, CancellationToken ct)
    {
        if (file is null) return this.BadRequestApi("documents.no_file", "No file provided.");
        if (file.Length == 0) return this.BadRequestApi("documents.empty_file", "The uploaded file is empty.");

        var dto = await _mediator.Send(new ReplaceDocumentFileCommand(
            Id: id,
            OriginalName: string.IsNullOrWhiteSpace(displayName) ? file.FileName : displayName!,
            ContentType: string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeBytes: file.Length,
            Content: file.OpenReadStream()
        ), ct);

        return this.OkApi(dto, "File replaced.");
    }

    // DELETE
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Documents.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteDocumentCommand(id), ct);
        return this.OkApi(new { id }, "Document deleted.");
    }

    // DOWNLOAD stream (works even when public URL can’t be embedded)
    [HttpGet("{id:guid}/download")]
    [Authorize(Policy = "Documents.View")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DownloadDocumentQuery(id), ct);
        return File(result.Stream, result.ContentType, result.DownloadFileName, enableRangeProcessing: true);
    }
}
