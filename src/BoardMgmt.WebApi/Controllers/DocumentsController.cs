using BoardMgmt.Domain.Entities;
using BoardMgmt.Infrastructure.Persistence;
using BoardMgmt.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public DocumentsController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db; _env = env;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentDto>>> List([FromQuery] ListDocumentsQuery q)
    {
        var query = _db.Documents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Folder))
            query = query.Where(d => d.FolderSlug == q.Folder);

        if (!string.IsNullOrWhiteSpace(q.Type))
        {
            var t = q.Type!.ToLowerInvariant();
            query = query.Where(d =>
                (t == "pdf" && d.ContentType.Contains("pdf")) ||
                (t == "word" && (d.ContentType.Contains("word") || d.OriginalName.EndsWith(".doc") || d.OriginalName.EndsWith(".docx"))) ||
                (t == "excel" && (d.ContentType.Contains("excel") || d.OriginalName.EndsWith(".xls") || d.OriginalName.EndsWith(".xlsx"))) ||
                (t == "powerpoint" && (d.ContentType.Contains("presentation") || d.OriginalName.EndsWith(".ppt") || d.OriginalName.EndsWith(".pptx")))
            );
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(d => d.OriginalName.Contains(q.Search!));

        var items = await query
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto(
                d.Id,
                d.OriginalName,
                MapType(d),
                d.SizeBytes,
                d.UploadedAt,
                d.FolderSlug,
                d.MeetingId,
                d.Url))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 52428800)] // 50MB per file
    [Authorize] // adjust
    public async Task<ActionResult<IEnumerable<DocumentDto>>> Upload(
        [FromForm] Guid? meetingId,
        [FromForm] string folderSlug,
        [FromForm] string? description,
        [FromForm] IFormFileCollection files)
    {
        if (files is null || files.Count == 0) return BadRequest("No files.");
        if (string.IsNullOrWhiteSpace(folderSlug)) folderSlug = "root";
        if (folderSlug != "root" && !await _db.Folders.AnyAsync(f => f.Slug == folderSlug))
            return BadRequest("Folder not found.");

        if (meetingId.HasValue && !await _db.Meetings.AnyAsync(m => m.Id == meetingId.Value))
            return BadRequest("Meeting not found.");

        var saved = new List<DocumentDto>();

        var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var basePath = Path.Combine(wwwroot, "uploads", folderSlug);
        Directory.CreateDirectory(basePath);

        foreach (var file in files)
        {
            var id = Guid.NewGuid();
            var safeName = Path.GetFileName(file.FileName);
            var ext = Path.GetExtension(safeName);
            var storedName = $"{id}{ext}";
            var physicalPath = Path.Combine(basePath, storedName);

            await using (var fs = System.IO.File.Create(physicalPath))
                await file.CopyToAsync(fs);

            var relativeUrl = $"/uploads/{folderSlug}/{storedName}";

            var doc = new Document
            {
                Id = id,
                MeetingId = meetingId,
                FolderSlug = folderSlug,
                FileName = storedName,
                OriginalName = safeName,
                Url = relativeUrl,
                ContentType = file.ContentType ?? "application/octet-stream",
                SizeBytes = file.Length,
                Description = description
            };

            _db.Documents.Add(doc);
            saved.Add(new DocumentDto(doc.Id, doc.OriginalName, MapType(doc), doc.SizeBytes, doc.UploadedAt, doc.FolderSlug, doc.MeetingId, doc.Url));
        }

        // update folder counters (optional)
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Slug == folderSlug);
        if (folder is not null)
            folder.DocumentCount = await _db.Documents.CountAsync(d => d.FolderSlug == folderSlug) + saved.Count;

        await _db.SaveChangesAsync();
        return Ok(saved);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();

        var physical = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            "uploads", doc.FolderSlug, doc.FileName);
        if (!System.IO.File.Exists(physical)) return NotFound();

        var stream = System.IO.File.OpenRead(physical);
        return File(stream, doc.ContentType, doc.OriginalName);
    }

    private static string MapType(Document d)
    {
        var n = d.OriginalName.ToLowerInvariant();
        if (n.EndsWith(".pdf")) return "pdf";
        if (n.EndsWith(".doc") || n.EndsWith(".docx")) return "word";
        if (n.EndsWith(".xls") || n.EndsWith(".xlsx")) return "excel";
        if (n.EndsWith(".ppt") || n.EndsWith(".pptx")) return "powerpoint";
        return "file";
    }
}
