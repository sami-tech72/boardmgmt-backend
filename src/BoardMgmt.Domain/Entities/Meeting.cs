using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BoardMgmt.Domain.Common;

namespace BoardMgmt.Domain.Entities;

public enum MeetingStatus { Draft = 0, Scheduled = 1, Completed = 2, Cancelled = 3 }
public enum MeetingType { Board = 0, Committee = 1, Emergency = 2 }

public class Meeting : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public MeetingType? Type { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public string Location { get; set; } = "TBD";
    public MeetingStatus Status { get; set; } = MeetingStatus.Scheduled;

    public List<AgendaItem> AgendaItems { get; set; } = new();
    public List<Document> Documents { get; set; } = new();
    public ICollection<MeetingAttendee> Attendees { get; set; } = new List<MeetingAttendee>();

    public List<VotePoll> Votes { get; set; } = new();

    public ICollection<Transcript> Transcripts { get; set; } = new List<Transcript>();

    [MaxLength(256)]
    public string? ExternalCalendar { get; set; }

    [MaxLength(320)]
    public string? ExternalCalendarMailbox { get; set; }

    [MaxLength(200)]
    public string? ExternalEventId { get; set; }

    [MaxLength(1000)]
    public string? OnlineJoinUrl { get; set; }

    public string? HostIdentity { get; set; }
}
