using System;
using System.ComponentModel.DataAnnotations;
using BoardMgmt.Domain.Common;

namespace BoardMgmt.Domain.Entities;

public class MeetingAttendee : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public string? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }

    public string? Email { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool IsConfirmed { get; set; } = false;
}
