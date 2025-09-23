using System;
using MediatR;
using BoardMgmt.Application.Messages.DTOs;

namespace BoardMgmt.Application.Messages.Queries;

public record GetMessageThreadQuery(Guid AnchorMessageId, Guid CurrentUserId)
  : IRequest<MessageThreadVm>;
