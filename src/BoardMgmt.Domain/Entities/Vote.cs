namespace BoardMgmt.Domain.Entities;

public class Vote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public Guid AgendaItemId { get; set; }

    public string Motion { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public VoteChoice Choice { get; set; }

    public int Yes { get; set; }
    public int No { get; set; }
    public int Abstain { get; set; }

    public DateTimeOffset VotedAt { get; set; } = DateTimeOffset.UtcNow;
}
