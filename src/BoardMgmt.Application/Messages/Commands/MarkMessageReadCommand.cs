using MediatR;

namespace BoardMgmt.Application.Messages.Commands;

public record MarkMessageReadCommand(Guid MessageId, Guid UserId) : IRequest<bool>;
