namespace BoardMgmt.Domain.Entities;

public class Folder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; // Human name
    public string Slug { get; set; } = string.Empty; // Unique kebab-case id
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;


    // Optional convenience counter (maintained by queries or background job)
    public int DocumentCount { get; set; }







}
