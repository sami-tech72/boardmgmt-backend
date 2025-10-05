using System.Text;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BoardMgmt.Application.Reports.Commands;

public sealed class GenerateReportHandler : IRequestHandler<GenerateReportCommand, Guid>
{
    private readonly DbContext _db;
    private readonly IHostEnvironment _env;
    private readonly IHttpContextAccessor _http;

    public GenerateReportHandler(DbContext db, IHostEnvironment env, IHttpContextAccessor http)
    {
        _db = db; _env = env; _http = http;
    }

    public async Task<Guid> Handle(GenerateReportCommand cmd, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Resolve period if custom not supplied
        var (start, end, label) = ResolvePeriod(cmd.Period, cmd.Start, cmd.End, now);

        // TODO: pull data for the type / sections and render file accordingly
        // For the sample, emit a tiny HTML
        var id = Guid.NewGuid();
        var fileName = $"{id}.html";
        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot"); // or use _env.ContentRootPath
        var reportsDir = Path.Combine(webRoot, "uploads", "reports");
        Directory.CreateDirectory(reportsDir);
        var filePath = Path.Combine(reportsDir, fileName);

        var html = new StringBuilder()
            .AppendLine("<!doctype html><html><head><meta charset='utf-8'><title>Report</title></head><body>")
            .AppendLine($"<h1>{cmd.Type.ToUpperInvariant()} Report</h1>")
            .AppendLine($"<p>Period: {label}</p>")
            .AppendLine($"<p>Generated at: {now:yyyy-MM-dd HH:mm:ss} UTC</p>")
            .AppendLine("<hr/>")
            .AppendLine("<p>(This is a sample HTML output. Replace with QuestPDF/OpenXML later.)</p>")
            .AppendLine("</body></html>")
            .ToString();

        await File.WriteAllTextAsync(filePath, html, ct);

        var baseUrl = $"{_http.HttpContext!.Request.Scheme}://{_http.HttpContext!.Request.Host}";
        var fileUrl = $"{baseUrl}/uploads/reports/{fileName}";

        var userId = _http.HttpContext!.User?.Identity?.IsAuthenticated == true
            ? _http.HttpContext.User.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier"))?.Value
            : null;

        var entity = new GeneratedReport
        {
            Id = id,
            Name = BuildReportName(cmd.Type, label),
            Type = cmd.Type,
            GeneratedAt = now,
            GeneratedByUserId = userId,
            FileUrl = fileUrl,
            Format = cmd.Format,
            PeriodLabel = label,
            StartDate = start,
            EndDate = end
        };

        _db.Add(entity);
        await _db.SaveChangesAsync(ct);

        return id;
    }

    private static (DateTimeOffset start, DateTimeOffset end, string label) ResolvePeriod(string period,
        DateTimeOffset? customStart, DateTimeOffset? customEnd, DateTimeOffset now)
    {
        var end = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1).AddTicks(-1);
        DateTimeOffset start;
        string label;

        switch (period)
        {
            case "last-month":
                var lm = now.AddMonths(-1);
                start = new DateTimeOffset(lm.Year, lm.Month, 1, 0, 0, 0, TimeSpan.Zero);
                end = new DateTimeOffset(lm.Year, lm.Month, DateTime.DaysInMonth(lm.Year, lm.Month), 23, 59, 59, TimeSpan.Zero);
                label = lm.ToString("MMM yyyy");
                break;

            case "last-quarter":
                int q = (now.Month - 1) / 3; // 0..3
                var qEndMonth = q * 3;
                var qStart = new DateTimeOffset(now.Year, qEndMonth - 2, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-3); // previous quarter start
                start = qStart;
                end = qStart.AddMonths(3).AddTicks(-1);
                label = "Last Quarter";
                break;

            case "last-year":
                start = new DateTimeOffset(now.Year - 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
                end = new DateTimeOffset(now.Year - 1, 12, 31, 23, 59, 59, TimeSpan.Zero);
                label = (now.Year - 1).ToString();
                break;

            case "custom":
            default:
                start = customStart ?? now.AddMonths(-1);
                end = customEnd ?? now;
                label = $"{start:yyyy-MM-dd} to {end:yyyy-MM-dd}";
                break;
        }
        return (start, end, label);
    }

    private static string BuildReportName(string type, string label)
        => $"{ToTitle(type)} Report — {label}";

    private static string ToTitle(string s)
        => string.IsNullOrWhiteSpace(s) ? "Custom" :
           char.ToUpperInvariant(s[0]) + s[1..];
}
