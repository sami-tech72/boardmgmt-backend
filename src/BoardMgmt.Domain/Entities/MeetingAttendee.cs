using System.ComponentModel.DataAnnotations;

namespace BoardMgmt.Domain.Entities;

public class MeetingAttendee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public Meeting? Meeting { get; set; }           // ← navigation back to Meeting

    public string? UserId { get; set; }             // Identity user id (string)
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }

    // NEW — link to Identity user if this attendee is an internal member
    
    public string? Email { get; set; }       // optional fallback
    public bool IsRequired { get; set; } = true;
    public bool IsConfirmed { get; set; } = false;


    [Timestamp] // optimistic concurrency token
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

}
