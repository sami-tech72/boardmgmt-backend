namespace BoardMgmt.Domain.Entities;

public class MeetingAttendee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = default!;
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }

    // NEW — link to Identity user if this attendee is an internal member
    public string? UserId { get; set; }      // AspNetUsers.Id (nvarchar(450))
    public string? Email { get; set; }       // optional fallback
    public bool IsRequired { get; set; } = true;
    public bool IsConfirmed { get; set; } = false;
}
