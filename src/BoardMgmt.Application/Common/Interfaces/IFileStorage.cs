namespace BoardMgmt.Application.Common.Interfaces;

public interface IFileStorage
{
    Task<(string fileName, string url)> SaveAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken ct = default);

    string MapToPhysicalPath(string publicUrl);

    Task<Stream> OpenAsync(string pathOrUrl, CancellationToken ct = default);

    Task DeletePathAsync(string pathOrUrl, CancellationToken ct = default);

    Task DeleteAsync(string fileNameOrUrl, CancellationToken ct = default);
}
