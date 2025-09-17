//using BoardMgmt.Application.Common.Interfaces;
//using BoardMgmt.Application.Documents.DTOs;
//using BoardMgmt.Domain.Entities;
//using MediatR;


//namespace BoardMgmt.Application.Documents.Commands.UploadDocuments;


//public class UploadDocumentsCommandHandler : IRequestHandler<UploadDocumentsCommand, IReadOnlyList<DocumentDto>>
//{
//    private readonly IAppDbContext _db;
//    private readonly IFileStorage _storage;


//    public UploadDocumentsCommandHandler(IAppDbContext db, IFileStorage storage)
//    {
//        _db = db; _storage = storage;
//    }


//    public async Task<IReadOnlyList<DocumentDto>> Handle(UploadDocumentsCommand request, CancellationToken ct)
//    {
//        if (string.IsNullOrWhiteSpace(request.FolderSlug)) request = request with { FolderSlug = "root" };


//        var results = new List<DocumentDto>();
//        foreach (var f in request.Files)
//        {
//            var (savedName, url) = await _storage.SaveAsync(f.Content, f.OriginalName, f.ContentType, ct);


//            var doc = new Document
//            {
//                MeetingId = request.MeetingId,
//                FolderSlug = request.FolderSlug,
//                FileName = savedName,
//                OriginalName = f.OriginalName,
//                Url = url,
//                ContentType = f.ContentType,
//                SizeBytes = f.SizeBytes,
//                Description = request.Description,
//                UploadedAt = DateTimeOffset.UtcNow
//            };


//            _db.Documents.Add(doc);
//            results.Add(new DocumentDto(doc.Id, doc.OriginalName, doc.Url, doc.ContentType, doc.SizeBytes,
//            doc.Version, doc.FolderSlug, doc.MeetingId, doc.Description, doc.UploadedAt));
//        }


//        await _db.SaveChangesAsync(ct);
//        return results;
//    }
//}





using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;

namespace BoardMgmt.Application.Documents.Commands.UploadDocuments;

public class UploadDocumentsCommandHandler : IRequestHandler<UploadDocumentsCommand, IReadOnlyList<DocumentDto>>
{
    private readonly IAppDbContext _db;
    private readonly IFileStorage _storage;

    public UploadDocumentsCommandHandler(IAppDbContext db, IFileStorage storage)
    { _db = db; _storage = storage; }

    public async Task<IReadOnlyList<DocumentDto>> Handle(UploadDocumentsCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FolderSlug))
            request = request with { FolderSlug = "root" };

        var results = new List<DocumentDto>();

        foreach (var f in request.Files)
        {
            var (savedName, url) = await _storage.SaveAsync(f.Content, f.OriginalName, f.ContentType, ct);

            var doc = new Document
            {
                MeetingId = request.MeetingId,
                FolderSlug = request.FolderSlug,
                FileName = savedName,
                OriginalName = f.OriginalName,
                Url = url,
                ContentType = f.ContentType,
                SizeBytes = f.SizeBytes,
                Description = request.Description,
                UploadedAt = DateTimeOffset.UtcNow,
                Access = request.Access
            };

            _db.Documents.Add(doc);

            results.Add(new DocumentDto(
                doc.Id, doc.OriginalName, doc.Url, doc.ContentType, doc.SizeBytes, doc.Version,
                doc.FolderSlug, doc.MeetingId, doc.Description, doc.UploadedAt, doc.Access));
        }

        await _db.SaveChangesAsync(ct);
        return results;
    }
}
