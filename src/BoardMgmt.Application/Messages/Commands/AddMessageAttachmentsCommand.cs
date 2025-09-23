using MediatR;

namespace BoardMgmt.Application.Messages.Commands;

public record AddMessageAttachmentsCommand(
    Guid MessageId,
    IReadOnlyList<AddMessageAttachmentsCommand.Att> Files
) : IRequest<int>
{
    public record Att(string FileName, string ContentType, long FileSize, string StoragePath);
}
