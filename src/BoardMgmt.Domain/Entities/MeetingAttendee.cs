namespace BoardMgmt.Domain.Entities;

public class MeetingAttendee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool IsConfirmed { get; set; } = false;
}
