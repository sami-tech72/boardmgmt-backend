namespace BoardMgmt.Application.Meetings.DTOs;

public sealed record TranscriptUtteranceDto(
    string Start,    // "hh:mm:ss.fff"
    string End,
    string Text,
    string? SpeakerName,
    string? SpeakerEmail,
    string? UserId
);

public sealed record TranscriptDto(
    Guid TranscriptId,
    string Provider,
    DateTimeOffset CreatedUtc,
    IReadOnlyList<TranscriptUtteranceDto> Utterances
);
