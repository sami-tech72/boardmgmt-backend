using BoardMgmt.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace BoardMgmt.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;   // .../wwwroot/uploads
    private readonly string _publicBase; // /uploads

    public LocalFileStorage(IConfiguration config, IWebHostEnvironment env)
    {
        _publicBase = config["Uploads:PublicBase"] ?? "/uploads";

        var physical = config["Uploads:PhysicalRoot"];
        if (string.IsNullOrWhiteSpace(physical))
        {
            var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath;

            physical = Path.Combine(webRoot, _publicBase.TrimStart('/', '\\'));
        }

        _rootPath = physical;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<(string fileName, string url)> SaveAsync(
        Stream content, string originalFileName, string contentType, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var y = now.ToString("yyyy");
        var m = now.ToString("MM");
        var targetDir = Path.Combine(_rootPath, y, m);
        Directory.CreateDirectory(targetDir);

        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        var ext = Path.GetExtension(originalFileName);
        var safeBase = string.Join("-", baseName.Split(Path.GetInvalidFileNameChars())).Trim('-');
        if (string.IsNullOrEmpty(ext)) ext = "";

        var fileName = $"{safeBase}-{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(targetDir, fileName);

        using (var fs = File.Create(fullPath))
            await content.CopyToAsync(fs, ct);

        var url = $"{_publicBase}/{y}/{m}/{fileName}".Replace('\\', '/');
        return (fileName, url);
    }

    public string MapToPhysicalPath(string publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl))
            throw new ArgumentException("URL is empty.", nameof(publicUrl));

        var clean = publicUrl.Split('?', '#')[0];
        if (clean.StartsWith('~')) clean = clean[1..];
        if (!clean.StartsWith('/')) clean = "/" + clean;

        if (!clean.StartsWith(_publicBase, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"URL must start with '{_publicBase}'. Value: {publicUrl}");

        var relative = clean.Substring(_publicBase.Length).TrimStart('/', '\\');
        return Path.Combine(_rootPath, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    public Task<Stream> OpenAsync(string pathOrUrl, CancellationToken ct = default)
    {
        var p = IsUrlLike(pathOrUrl) ? MapToPhysicalPath(pathOrUrl) : pathOrUrl;
        Stream s = File.OpenRead(p);
        return Task.FromResult(s);
    }

    public Task DeletePathAsync(string pathOrUrl, CancellationToken ct = default)
    {
        var p = IsUrlLike(pathOrUrl) ? MapToPhysicalPath(pathOrUrl) : pathOrUrl;
        if (File.Exists(p)) File.Delete(p);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string fileNameOrUrl, CancellationToken ct = default)
    {
        if (IsUrlLike(fileNameOrUrl)) return DeletePathAsync(fileNameOrUrl, ct);
        var fullPath = Path.Combine(_rootPath, fileNameOrUrl.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private static bool IsUrlLike(string value)
        => !string.IsNullOrEmpty(value) && (value.StartsWith('/') || value.StartsWith("~/"));
}
