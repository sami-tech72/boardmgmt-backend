using System.ComponentModel.DataAnnotations;

namespace BoardMgmt.Domain.Entities;

public enum VoteType { YesNo = 0, ApproveReject = 1, MultipleChoice = 2 }
public enum VoteEligibility { Public = 0, MeetingAttendees = 1, SpecificUsers = 2 }
//public enum VoteChoice { Yes = 1, No = 2, Abstain = 3 }   // ✅ add back

public class VotePoll
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Scope
    public Guid? MeetingId { get; set; }
    public Guid? AgendaItemId { get; set; }

    // ✅ navs required for .Include(...)
    public Meeting? Meeting { get; set; }

    public AgendaItem? AgendaItem { get; set; }

    // Definition
    [MaxLength(160)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string? Description { get; set; }
    public VoteType Type { get; set; } = VoteType.YesNo;
    public bool AllowAbstain { get; set; } = true;
    public bool Anonymous { get; set; } = false;

    // Window
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset Deadline { get; set; } = DateTimeOffset.UtcNow.AddDays(3);

    // Eligibility
    public VoteEligibility Eligibility { get; set; } = VoteEligibility.MeetingAttendees;

    // Creator
    public string CreatedByUserId { get; set; } = string.Empty;

    // Navigation
    public List<VoteOption> Options { get; set; } = new();            // for MultipleChoice
    public List<VoteBallot> Ballots { get; set; } = new();            // cast ballots
    public List<VoteEligibleUser> EligibleUsers { get; set; } = new(); // if SpecificUsers

    // Convenience
    public bool IsOpen(DateTimeOffset now) => now <= Deadline;
}

public class VoteOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VoteId { get; set; }              // FK -> VotePoll
    public VotePoll? Vote { get; set; }           // optional nav
    [MaxLength(200)]
    public string Text { get; set; } = string.Empty;
    public int Order { get; set; } = 0;
}

public class VoteBallot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VoteId { get; set; }              // FK -> VotePoll
    public VotePoll? Vote { get; set; }

    // prevent multi-vote; identity is never exposed when Anonymous=true
    public string UserId { get; set; } = string.Empty;

    // Choice (Yes/No/Abstain) OR OptionId (MultipleChoice)
    public VoteChoice? Choice { get; set; }
    public Guid? OptionId { get; set; }           // FK -> VoteOption (nullable)
    public VoteOption? Option { get; set; }

    public DateTimeOffset VotedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class VoteEligibleUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VoteId { get; set; }              // FK -> VotePoll
    public VotePoll? Vote { get; set; }
    public string UserId { get; set; } = string.Empty;
}
