using MediatR;
using BoardMgmt.Application.Messages.DTOs;

namespace BoardMgmt.Application.Messages.Commands;

public record SendMessageCommand(Guid MessageId) : IRequest<MessageDto>;
