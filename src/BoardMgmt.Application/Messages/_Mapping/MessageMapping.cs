using BoardMgmt.Application.Messages.DTOs;
using BoardMgmt.Domain.Messages;

namespace BoardMgmt.Application.Messages._Mapping;

internal static class MessageMapping
{
    public static MessageDto ToDto(Message m) =>
    new(
        m.Id, m.SenderId, m.Subject, m.Body,
        m.Priority.ToString(), m.ReadReceiptRequested, m.IsConfidential,
        m.Status.ToString(), m.SentAtUtc, m.CreatedAtUtc, m.UpdatedAtUtc,
        m.Recipients.Select(r => new MessageRecipientDto(r.UserId, r.IsRead, r.ReadAtUtc)).ToList(),
        m.Attachments.Select(a => new MessageAttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSize)).ToList(),
        m.Attachments.Any() // <-- supply HasAttachments
    );
}
