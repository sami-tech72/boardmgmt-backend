using MediatR;
using BoardMgmt.Application.Messages.DTOs;

namespace BoardMgmt.Application.Messages.Queries;

public record GetMessageViewQuery(Guid Id) : IRequest<MessageDetailVm?>;
