using MediatR;

namespace BoardMgmt.Application.Messages.Commands;

public record DeleteMessageCommand(Guid MessageId) : IRequest<bool>;
