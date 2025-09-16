namespace BoardMgmt.Domain.Entities;

public enum VoteChoice { Yes = 1, No = 2, Abstain = 3 }

public class AgendaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }

    public List<Vote> Votes { get; set; } = new();
}
