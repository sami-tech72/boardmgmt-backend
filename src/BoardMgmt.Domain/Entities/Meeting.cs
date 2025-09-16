using System.Reflection.Metadata;

namespace BoardMgmt.Domain.Entities;


public enum MeetingStatus { Draft = 0, Scheduled = 1, Completed = 2, Cancelled = 3 }

//public enum MeetingStatus
//{
//    Scheduled,
//    Completed,
//    Cancelled
//}


public class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset ScheduledAt { get; set; }
    public string Location { get; set; } = string.Empty;
    public MeetingStatus Status { get; set; } = MeetingStatus.Draft;


    public List<AgendaItem> AgendaItems { get; set; } = new();
    public List<Document> Documents { get; set; } = new();
}