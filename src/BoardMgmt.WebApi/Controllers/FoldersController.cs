using BoardMgmt.Infrastructure.Persistence;
using BoardMgmt.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly AppDbContext _db;
    public FoldersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FolderDto>>> Get()
    {
        var counts = await _db.Documents
            .GroupBy(d => d.FolderSlug)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();

        var list = await _db.Folders
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FolderDto(
                f.Id, f.Name, f.Slug,
                counts.FirstOrDefault(c => c.Key == f.Slug)?.Count ?? 0))
            .ToListAsync();

        // include synthetic "root"
        list.Add(new FolderDto(Guid.Empty, "Root", "root",
            counts.FirstOrDefault(c => c.Key == "root")?.Count ?? 0));

        return Ok(list);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")] // adjust for your roles
    public async Task<ActionResult<FolderDto>> Create([FromBody] CreateFolderRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Length > 60)
            return BadRequest("Invalid folder name.");

        var slug = Slugify(req.Name);
        if (await _db.Folders.AnyAsync(f => f.Slug == slug))
            return Conflict("Folder already exists.");

        var folder = new Domain.Entities.Folder { Name = req.Name.Trim(), Slug = slug };
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync();

        return Ok(new FolderDto(folder.Id, folder.Name, folder.Slug, 0));
    }

    private static string Slugify(string s)
    {
        s = s.ToLowerInvariant().Trim();
        s = Regex.Replace(s, @"[^\w\s-]", "");
        s = Regex.Replace(s, @"\s+", "-");
        s = Regex.Replace(s, @"-+", "-");
        return s;
    }
}
