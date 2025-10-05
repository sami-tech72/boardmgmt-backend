using System.ComponentModel.DataAnnotations;

namespace BoardMgmt.Domain.Entities;

public class GeneratedReport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public string Name { get; set; } = default!;

    [MaxLength(100)]
    public string Type { get; set; } = default!; // attendance | voting | documents | performance | custom

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    // NOTE: nullable to allow OnDelete(SetNull)
    public string? GeneratedByUserId { get; set; }
    public AppUser? GeneratedByUser { get; set; }

    [MaxLength(1024)]
    public string? FileUrl { get; set; }

    // Useful metadata for listing/filters
    [MaxLength(100)]
    public string? Format { get; set; } // pdf | excel | powerpoint | html

    [MaxLength(120)]
    public string? PeriodLabel { get; set; } // e.g., "Last Quarter", "Nov 2024", "2025-01 to 2025-06"

    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
}
