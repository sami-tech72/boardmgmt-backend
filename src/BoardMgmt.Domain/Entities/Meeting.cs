using System.ComponentModel.DataAnnotations;

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
    // Calendar integration metadata
    [MaxLength(256)]
    public string? ExternalCalendar { get; set; } // e.g. "Microsoft365"

    [MaxLength(320)]
    public string? ExternalCalendarMailbox { get; set; } // e.g. board@yourco.com

    [MaxLength(200)]
    public string? ExternalEventId { get; set; } // Graph event id

    [MaxLength(1000)]
    public string? OnlineJoinUrl { get; set; } // Teams join link

}
