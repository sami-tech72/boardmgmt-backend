using BoardMgmt.Domain.Entities;

namespace BoardMgmt.Application.Votes.DTOs;

public sealed record VoteOptionDto(Guid Id, string Text, int Order, int Count);

public sealed record VoteResultsDto(
    int TotalBallots,
    int Yes,
    int No,
    int Abstain,
    IReadOnlyList<VoteOptionDto> Options // for MultipleChoice, counts per option
);

public sealed record VoteSummaryDto(
    Guid Id,
    string Title,
    string? Description,
    VoteType Type,
    DateTimeOffset Deadline,
    bool IsOpen,
    VoteEligibility Eligibility,
    VoteResultsDto Results
);

// New: per-user vote row (populate only when not Anonymous and caller authorized)
public sealed record IndividualVoteDto(
    string UserId,
    string DisplayName,
    string? ChoiceLabel,   // "Yes" | "No" | "Abstain" for Y/N/Abstain polls
    string? OptionText,    // filled for MultipleChoice
    DateTimeOffset VotedAt


);

public sealed record VoteDetailDto(
    Guid Id,
    Guid? MeetingId,
    Guid? AgendaItemId,
    string Title,
    string? Description,
    VoteType Type,
    bool AllowAbstain,
    bool Anonymous,
    DateTimeOffset CreatedAt,
    DateTimeOffset Deadline,
    VoteEligibility Eligibility,
    IReadOnlyList<VoteOptionDto> Options,
    VoteResultsDto Results,
    bool CanVote,            // current user can vote?
    bool AlreadyVoted,       // current user already voted?
    IReadOnlyList<IndividualVoteDto>? IndividualVotes // null or empty when Anonymous
);
