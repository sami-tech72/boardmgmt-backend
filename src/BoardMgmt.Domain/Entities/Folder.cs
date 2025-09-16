namespace BoardMgmt.Domain.Entities;

public class Folder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    // kebab-case unique id (e.g., "board-meetings")
    public string Slug { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // convenience counters (optional to persist)
    public int DocumentCount { get; set; }


}
