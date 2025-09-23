namespace BoardMgmt.WebApi.Controllers.Messages;

public record CreateMessageBody(
    string Subject,
    string Body,
    string Priority,                   // "Low|Normal|High"
    bool ReadReceiptRequested,
    bool IsConfidential,
    List<Guid> RecipientIds,
    bool asDraft = true
);

public record UpdateMessageBody(
    string Subject,
    string Body,
    string Priority,
    bool ReadReceiptRequested,
    bool IsConfidential,
    List<Guid> RecipientIds
);
