using BoardMgmt.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace BoardMgmt.Infrastructure.Files;

public class DiskFileStorage : IFileStorage
{
    private readonly string _webRoot;
    private readonly string _uploadsRoot;         // .../wwwroot/uploads
    private const string UploadsUrlPrefix = "/uploads";

    public DiskFileStorage(IWebHostEnvironment env)
    {
        // Prefer WebRoot (wwwroot). Fall back to ContentRoot/wwwroot if needed.
        _webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;

        _uploadsRoot = Path.Combine(_webRoot, "uploads");
        Directory.CreateDirectory(_uploadsRoot);
    }

    // === High-level save: returns (fileName, url) ===
    public async Task<(string fileName, string url)> SaveAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        // Put in root "uploads" (no custom folder)
        var safeName = MakeSafeFileName(originalFileName);
        var (physPath, relPath) = GetPaths(folder: null, safeName);
        await WriteFileAsync(physPath, content, ct);
        var url = ToPublicUrl(relPath);
        return (safeName, url);
    }

    // === Foldered save: returns physical path (as per your controller usage) ===
    public async Task<string> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct)
    {
        var safeName = MakeSafeFileName(fileName);
        var (physPath, _) = GetPaths(folder, safeName);
        await WriteFileAsync(physPath, content, ct);
        return physPath;
    }

    public Task<Stream> OpenAsync(string pathOrUrl, CancellationToken ct)
    {
        var physicalPath = IsProbablyUrl(pathOrUrl)
            ? MapToPhysicalPath(pathOrUrl)
            : pathOrUrl;

        Stream s = File.OpenRead(physicalPath);
        return Task.FromResult(s);
    }

    public Task DeletePathAsync(string pathOrUrl, CancellationToken ct)
    {
        var physicalPath = IsProbablyUrl(pathOrUrl)
            ? MapToPhysicalPath(pathOrUrl)
            : pathOrUrl;

        if (File.Exists(physicalPath))
            File.Delete(physicalPath);

        return Task.CompletedTask;
    }

    // Convenience: delete by (public) file name or url
    public Task DeleteAsync(string fileNameOrUrl, CancellationToken ct = default)
        => DeletePathAsync(fileNameOrUrl, ct);

    // Map a public URL (/uploads/...) → physical path
    public string MapToPhysicalPath(string publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl))
            throw new ArgumentException("URL is empty.", nameof(publicUrl));

        // Normalize and strip querystring/fragment if any
        var clean = publicUrl.Split('?', '#')[0];

        if (!clean.StartsWith(UploadsUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // If the caller passed a relative path (e.g., "uploads/.."), normalize it
            clean = clean.TrimStart('~');
            if (!clean.StartsWith("/")) clean = "/" + clean;
            if (!clean.StartsWith(UploadsUrlPrefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"URL must start with '{UploadsUrlPrefix}'. Value: {publicUrl}");
        }

        var relative = clean.Substring(UploadsUrlPrefix.Length).TrimStart('/'); // folder/file
        var physical = Path.Combine(_uploadsRoot, relative);
        return physical;
    }

    // ===== helpers =====

    private static string MakeSafeFileName(string originalName)
    {
        var nameOnly = Path.GetFileName(originalName);
        return $"{Guid.NewGuid():N}_{nameOnly}";
    }

    private (string physicalPath, string relativePath) GetPaths(string? folder, string safeFileName)
    {
        string folderRel = string.IsNullOrWhiteSpace(folder) ? "" : folder.Trim().Replace('\\', '/').Trim('/');
        string targetDir = string.IsNullOrEmpty(folderRel) ? _uploadsRoot : Path.Combine(_uploadsRoot, folderRel);
        Directory.CreateDirectory(targetDir);

        var physical = Path.Combine(targetDir, safeFileName);
        var relative = string.IsNullOrEmpty(folderRel) ? safeFileName : $"{folderRel}/{safeFileName}";
        return (physical, relative);
    }

    private static async Task WriteFileAsync(string physicalPath, Stream content, CancellationToken ct)
    {
        // Ensure parent dir exists (should already from GetPaths)
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        using var fs = File.Create(physicalPath);
        await content.CopyToAsync(fs, ct);
    }

    private static bool IsProbablyUrl(string value)
        => value.StartsWith("/", StringComparison.Ordinal) || value.StartsWith("~/", StringComparison.Ordinal);

    private static string ToPublicUrl(string relativePath)
        => $"{UploadsUrlPrefix}/{relativePath.Replace('\\', '/').TrimStart('/')}";
}
