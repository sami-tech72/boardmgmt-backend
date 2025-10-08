using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BoardMgmt.Domain.Common;

namespace BoardMgmt.Domain.Entities;

public enum VoteType
{
    YesNo = 0,
    ApproveReject = 1,
    MultipleChoice = 2
}

public enum VoteEligibility
{
    Public = 0,
    MeetingAttendees = 1,
    SpecificUsers = 2
}

public class VotePoll : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public Guid? AgendaItemId { get; set; }
    public AgendaItem? AgendaItem { get; set; }

    [MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public VoteType Type { get; set; } = VoteType.YesNo;
    public bool AllowAbstain { get; set; } = true;
    public bool Anonymous { get; set; } = false;

    public DateTimeOffset Deadline { get; set; } = DateTimeOffset.UtcNow.AddDays(3);

    public VoteEligibility Eligibility { get; set; } = VoteEligibility.MeetingAttendees;

    public List<VoteOption> Options { get; set; } = new();
    public List<VoteBallot> Ballots { get; set; } = new();
    public List<VoteEligibleUser> EligibleUsers { get; set; } = new();

    public bool IsOpen(DateTimeOffset now) => now <= Deadline;
}

public class VoteOption : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VoteId { get; set; }
    public VotePoll? Vote { get; set; }

    [MaxLength(200)]
    public string Text { get; set; } = string.Empty;

    public int Order { get; set; } = 0;
}

public class VoteBallot : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VoteId { get; set; }
    public VotePoll? Vote { get; set; }

    public string UserId { get; set; } = string.Empty;

    public VoteChoice? Choice { get; set; }

    public Guid? OptionId { get; set; }
    public VoteOption? Option { get; set; }

    public DateTimeOffset VotedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class VoteEligibleUser : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VoteId { get; set; }
    public VotePoll? Vote { get; set; }

    public string UserId { get; set; } = string.Empty;
}
