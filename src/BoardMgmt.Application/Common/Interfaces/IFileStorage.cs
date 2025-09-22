namespace BoardMgmt.Application.Common.Interfaces;


public interface IFileStorage
{
 
    Task<(string fileName, string url)> SaveAsync(
    Stream content,
    string originalFileName,
    string contentType,
    CancellationToken ct = default);


    
    string MapToPhysicalPath(string publicUrl);

    Task DeleteAsync(string fileName, CancellationToken ct = default);
}