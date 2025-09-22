namespace BoardMgmt.Domain.Entities;


public class AgendaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }

    public List<Vote> Votes { get; set; } = new();



}
