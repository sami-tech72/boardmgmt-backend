using System;
using System.Collections.Generic;
using BoardMgmt.Domain.Common;

namespace BoardMgmt.Domain.Entities;

public class AgendaItem : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }

    public List<VotePoll> VotePolls { get; set; } = new();
}
