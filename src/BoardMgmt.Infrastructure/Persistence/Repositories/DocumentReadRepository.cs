using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Application.Dashboard.DTOs;
using BoardMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Infrastructure.Persistence;

namespace BoardMgmt.Infrastructure.Persistence.Repositories
{
    public class DocumentReadRepository : IDocumentReadRepository
    {
        private readonly AppDbContext _db;
        public DocumentReadRepository(AppDbContext db) => _db = db;

        // You don't have IsActive on Document, so we just count all documents
        public Task<int> CountActiveAsync(CancellationToken ct) =>
            _db.Set<Document>().CountAsync(ct);

        public async Task<IReadOnlyList<DashboardDocumentDto>> GetRecentAsync(int take, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;

            var raw = await _db.Set<Document>()
                .OrderByDescending(d => d.UploadedAt)
                .Take(take)
                .Select(d => new
                {
                    d.Id,
                    Title = d.OriginalName,
                    d.FileName,
                    d.ContentType,
                    d.UploadedAt
                })
                .ToListAsync(ct);

            var data = raw
                .Select(d => new DashboardDocumentDto(
                    d.Id,
                    d.Title,
                    MapKind(d.ContentType, d.FileName),
                    UpdatedAgo(now, d.UploadedAt)))
                .ToList();

            return data;
        }

        public async Task<(int total, IReadOnlyList<DocumentItemDto> items)> GetActivePagedAsync(int page, int pageSize, CancellationToken ct)
        {
            // You don't have IsActive/UpdatedAt/Kind on Document, so:
            //   - no IsActive filter
            //   - order by UploadedAt
            //   - derive Kind from ContentType/extension
            //   - compute "UpdatedAgo" from UploadedAt
            var baseQuery = _db.Set<Document>();

            var total = await baseQuery.CountAsync(ct);
            var now = DateTimeOffset.UtcNow;

            var pageRows = await baseQuery
                .OrderByDescending(d => d.UploadedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.Id,
                    Title = d.OriginalName,
                    d.FileName,
                    d.ContentType,
                    d.UploadedAt
                })
                .ToListAsync(ct);

            var items = pageRows
                .Select(d => new DocumentItemDto(
                    d.Id,
                    d.Title,
                    MapKind(d.ContentType, d.FileName),
                    UpdatedAgo(now, d.UploadedAt)))
                .ToList();

            return (total, items);
        }

        private static string UpdatedAgo(DateTimeOffset now, DateTimeOffset updated)
        {
            var span = now - updated;
            if (span.TotalMinutes < 90) return $"{Math.Max(1, (int)span.TotalMinutes)} minutes ago";
            if (span.TotalHours < 36) return $"{Math.Max(1, (int)span.TotalHours)} hours ago";
            return $"{Math.Max(1, (int)span.TotalDays)} days ago";
        }

        private static string MapKind(string? contentType, string fileName)
        {
            var ct = (contentType ?? "").ToLowerInvariant();
            if (ct.Contains("pdf")) return "pdf";
            if (ct.Contains("word") || ct.Contains("msword") || ct.Contains("officedocument.wordprocessingml")) return "word";
            if (ct.Contains("excel") || ct.Contains("spreadsheet") || ct.Contains("officedocument.spreadsheetml")) return "excel";
            if (ct.Contains("powerpoint") || ct.Contains("presentation") || ct.Contains("officedocument.presentationml")) return "ppt";

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "pdf",
                ".doc" or ".docx" => "word",
                ".xls" or ".xlsx" => "excel",
                ".ppt" or ".pptx" => "ppt",
                _ => "file"
            };
        }
    }
}
