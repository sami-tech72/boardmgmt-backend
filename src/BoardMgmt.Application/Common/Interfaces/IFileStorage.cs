namespace BoardMgmt.Application.Common.Interfaces;


public interface IFileStorage
{
 
    Task<(string fileName, string url)> SaveAsync(
    Stream content,
    string originalFileName,
    string contentType,
    CancellationToken ct = default);


    
    string MapToPhysicalPath(string publicUrl);
    Task<string> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct);
    Task<Stream> OpenAsync(string path, CancellationToken ct);
    Task DeletePathAsync(string path, CancellationToken ct);
    Task DeleteAsync(string fileName, CancellationToken ct = default);



}