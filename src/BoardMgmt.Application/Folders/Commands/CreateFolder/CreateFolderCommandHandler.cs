using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Common.Utilities;
using BoardMgmt.Application.Folders.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Folders.Commands.CreateFolder;

public sealed class CreateFolderCommandHandler : IRequestHandler<CreateFolderCommand, FolderDto>
{
    private readonly IAppDbContext _db;

    public CreateFolderCommandHandler(IAppDbContext db) => _db = db;

    public async Task<FolderDto> Handle(CreateFolderCommand request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 60)
            throw new ArgumentException("Invalid folder name.");

        var slug = SlugHelper.Slugify(name);

        var exists = await _db.Folders.AnyAsync(f => f.Slug == slug, ct);
        if (exists)
            throw new InvalidOperationException("Folder already exists.");

        var folder = new Folder
        {
            Name = name,
            Slug = slug,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Folders.Add(folder);
        await _db.SaveChangesAsync(ct);

        return new FolderDto(folder.Id, folder.Name, folder.Slug, 0);
    }
}
