using BoardMgmt.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;


namespace BoardMgmt.Infrastructure.Storage;


public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath; // physical base, e.g., wwwroot/uploads
    private readonly string _publicBase; // public base, e.g., /uploads


    public LocalFileStorage(IConfiguration config, IWebHostEnvironment env)
    {
        _publicBase = config["Uploads:PublicBase"] ?? "/uploads";
        var physical = config["Uploads:PhysicalRoot"];
        if (string.IsNullOrWhiteSpace(physical))
            physical = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), _publicBase.TrimStart('/'));
        _rootPath = physical;
        Directory.CreateDirectory(_rootPath);
    }


    public async Task<(string fileName, string url)> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default)
    {
        var datePath = Path.Combine(DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        var folder = Path.Combine(_rootPath, datePath);
        Directory.CreateDirectory(folder);


        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        var ext = Path.GetExtension(originalFileName);
        var safeBase = string.Join("-", baseName.Split(Path.GetInvalidFileNameChars())).Trim('-');
        var fileName = $"{safeBase}-{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);


        using (var fs = File.Create(fullPath))
        {
            await content.CopyToAsync(fs, ct);
        }


        var url = $"{_publicBase}/{datePath.Replace(Path.DirectorySeparatorChar, '/')}/{fileName}";
        return (fileName, url);
    }


    public string MapToPhysicalPath(string publicUrl)
    {
        // Assumes publicUrl starts with public base
        var relative = publicUrl.Replace(_publicBase, string.Empty).TrimStart('/', '\\');
        return Path.Combine(_rootPath, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}