using BoardMgmt.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace BoardMgmt.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;   // physical base, e.g. .../wwwroot/uploads
    private readonly string _publicBase; // public base, e.g. /uploads

    public LocalFileStorage(IConfiguration config, IWebHostEnvironment env)
    {
        _publicBase = config["Uploads:PublicBase"] ?? "/uploads";

        var physical = config["Uploads:PhysicalRoot"];
        if (string.IsNullOrWhiteSpace(physical))
        {
            var webRoot = env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(env.ContentRootPath, "wwwroot");

            physical = Path.Combine(webRoot, _publicBase.TrimStart('/', '\\'));
        }

        _rootPath = physical;
        Directory.CreateDirectory(_rootPath);
    }

    // ========== IFileStorage members ==========

    // 1) Save returning (fileName, url)
    public async Task<(string fileName, string url)> SaveAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        // yyyy/MM
        var y = DateTime.UtcNow.ToString("yyyy");
        var m = DateTime.UtcNow.ToString("MM");
        var folder = Path.Combine(_rootPath, y, m);
        Directory.CreateDirectory(folder);

        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        var ext = Path.GetExtension(originalFileName);
        var safeBase = string.Join("-", baseName.Split(Path.GetInvalidFileNameChars()))
                            .Trim('-');
        var fileName = $"{safeBase}-{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        using (var fs = File.Create(fullPath))
            await content.CopyToAsync(fs, ct);

        var url = $"{_publicBase}/{y}/{m}/{fileName}";
        return (fileName, url);
    }

    // 2) Map public URL -> physical path
    public string MapToPhysicalPath(string publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl))
            throw new ArgumentException("URL is empty.", nameof(publicUrl));

        var clean = publicUrl.Split('?', '#')[0];
        // normalize prefix (accept "~/uploads", "uploads", "/uploads")
        if (clean.StartsWith('~')) clean = clean[1..];
        if (!clean.StartsWith('/')) clean = "/" + clean;

        if (!clean.StartsWith(_publicBase, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"URL must start with '{_publicBase}'. Value: {publicUrl}");

        var relative = clean.Substring(_publicBase.Length).TrimStart('/', '\\');
        return Path.Combine(_rootPath, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    // 3) Save with folder, returns physical path
    public async Task<string> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct)
    {
        var safeFolder = (folder ?? "").Replace('\\', '/').Trim('/');
        var targetDir = string.IsNullOrEmpty(safeFolder)
            ? _rootPath
            : Path.Combine(_rootPath, safeFolder.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(targetDir);

        var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(targetDir, safeName);

        using (var fs = File.Create(fullPath))
            await content.CopyToAsync(fs, ct);

        return fullPath; // physical path as per interface contract/your usage
    }

    // 4) Open by path or URL
    public Task<Stream> OpenAsync(string path, CancellationToken ct)
    {
        var p = IsUrlLike(path) ? MapToPhysicalPath(path) : path;
        Stream s = File.OpenRead(p);
        return Task.FromResult(s);
    }

    // 5) Delete by path or URL
    public Task DeletePathAsync(string path, CancellationToken ct)
    {
        var p = IsUrlLike(path) ? MapToPhysicalPath(path) : path;
        if (File.Exists(p)) File.Delete(p);
        return Task.CompletedTask;
    }

    // 6) Delete by fileName (treat as URL or relative path under root)
    public Task DeleteAsync(string fileName, CancellationToken ct = default)
    {
        // if looks like URL, defer to DeletePathAsync
        if (IsUrlLike(fileName))
            return DeletePathAsync(fileName, ct);

        var fullPath = Path.Combine(_rootPath, fileName.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    // ========== helpers ==========
    private static bool IsUrlLike(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.StartsWith('/') || value.StartsWith("~/");
    }
}
