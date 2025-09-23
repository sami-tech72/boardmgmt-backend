using MediatR;
using BoardMgmt.Application.Messages.DTOs;

namespace BoardMgmt.Application.Messages.Queries;

public record GetMessageQuery(Guid MessageId) : IRequest<MessageDto>;
