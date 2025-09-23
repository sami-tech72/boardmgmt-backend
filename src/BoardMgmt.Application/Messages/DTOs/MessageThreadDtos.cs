using System;
using System.Collections.Generic;

namespace BoardMgmt.Application.Messages.DTOs;

public record MessageBubbleVm(
    Guid Id,
    MinimalUserDto FromUser,
    string Body,
    DateTime CreatedAtUtc,
    IReadOnlyList<MessageAttachmentDto> Attachments
);

public record MessageThreadVm(
    Guid AnchorMessageId,
    string Subject,
    IReadOnlyList<MinimalUserDto> Participants,
    IReadOnlyList<MessageBubbleVm> Items
);
