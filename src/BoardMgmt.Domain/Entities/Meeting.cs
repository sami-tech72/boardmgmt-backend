namespace BoardMgmt.Domain.Entities;

public enum MeetingStatus { Draft = 0, Scheduled = 1, Completed = 2, Cancelled = 3 }
public enum MeetingType { Board = 0, Committee = 1, Emergency = 2 }

public class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public MeetingType? Type { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public string Location { get; set; } = "TBD";
    public MeetingStatus Status { get; set; } = MeetingStatus.Scheduled;

    // nav
    public List<AgendaItem> AgendaItems { get; set; } = new();
    public List<Document> Documents { get; set; } = new();
    public ICollection<MeetingAttendee> Attendees { get; set; } = new List<MeetingAttendee>();

    public List<VotePoll> Votes { get; set; } = new();


}
