using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BoardMgmt.Domain.Common;

namespace BoardMgmt.Domain.Chat;

public class Conversation : AuditableEntity
{
    public Guid Id { get; set; }
    public ConversationType Type { get; set; } = ConversationType.Channel;
    [MaxLength(120)] public string? Name { get; set; }
    public bool IsPrivate { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ConversationMember : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string UserId { get; set; } = default!;
    public ConversationMemberRole Role { get; set; } = ConversationMemberRole.Member;
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastReadAtUtc { get; set; }

    public Conversation Conversation { get; set; } = null!;
}

public class ChatMessage : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string SenderId { get; set; } = default!;

    public Guid? ThreadRootId { get; set; }

    public string BodyHtml { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();
    public ICollection<ChatReaction> Reactions { get; set; } = new List<ChatReaction>();
}

public class ChatAttachment : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;
}

public class ChatReaction : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string UserId { get; set; } = default!;

    [MaxLength(32)]
    public string Emoji { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
