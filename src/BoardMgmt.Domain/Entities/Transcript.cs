using System.ComponentModel.DataAnnotations;

namespace BoardMgmt.Domain.Entities;

public class Transcript
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = default!;

    [MaxLength(64)]
    public string Provider { get; set; } = "";      // "Microsoft365" | "Zoom"

    [MaxLength(256)]
    public string ProviderTranscriptId { get; set; } = ""; // Teams transcriptId or Zoom file id

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<TranscriptUtterance> Utterances { get; set; } = new List<TranscriptUtterance>();
}

public class TranscriptUtterance
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TranscriptId { get; set; }
    public Transcript Transcript { get; set; } = default!;

    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }

    [MaxLength(4000)]
    public string Text { get; set; } = "";

    [MaxLength(256)]
    public string? SpeakerName { get; set; }

    [MaxLength(320)]
    public string? SpeakerEmail { get; set; }

    // link to Identity user if you can resolve
    public string? UserId { get; set; }
}
