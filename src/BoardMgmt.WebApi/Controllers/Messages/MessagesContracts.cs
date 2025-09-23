namespace BoardMgmt.WebApi.Controllers.Messages;

public record CreateMessageBody(
    string Subject,
    string Body,
    string Priority,                 // "Low|Normal|High|Urgent"
    bool ReadReceiptRequested,
    bool IsConfidential,
    IReadOnlyList<Guid> RecipientIds,
    bool asDraft
);

public record UpdateMessageBody(
    string Subject,
    string Body,
    string Priority,
    bool ReadReceiptRequested,
    bool IsConfidential,
    IReadOnlyList<Guid> RecipientIds
);
