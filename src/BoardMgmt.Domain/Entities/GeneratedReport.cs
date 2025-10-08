using System;
using System.ComponentModel.DataAnnotations;
using BoardMgmt.Domain.Common;

namespace BoardMgmt.Domain.Entities;

public class GeneratedReport : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public string Name { get; set; } = default!;

    [MaxLength(100)]
    public string Type { get; set; } = default!;

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? GeneratedByUserId { get; set; }
    public AppUser? GeneratedByUser { get; set; }

    [MaxLength(1024)]
    public string? FileUrl { get; set; }

    [MaxLength(100)]
    public string? Format { get; set; }

    [MaxLength(120)]
    public string? PeriodLabel { get; set; }

    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
}
