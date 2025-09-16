namespace BoardMgmt.Domain.Entities;


public class Vote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgendaItemId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public VoteChoice Choice { get; set; }
    public DateTimeOffset VotedAt { get; set; } = DateTimeOffset.UtcNow;
}