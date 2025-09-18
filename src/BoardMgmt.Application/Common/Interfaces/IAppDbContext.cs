using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace BoardMgmt.Application.Common.Interfaces
{
    public interface IAppDbContext
    {
        DbSet<Meeting> Meetings { get; }
        DbSet<AgendaItem> AgendaItems { get; }
        DbSet<Document> Documents { get; }
        DbSet<Vote> Votes { get; }
        DbSet<MeetingAttendee> MeetingAttendees { get; }
        DbSet<Folder> Folders { get; }

        DbSet<VotePoll> VotePolls { get; }
        DbSet<VoteOption> VoteOptions { get; }
        DbSet<VoteBallot> VoteBallots { get; }
        DbSet<VoteEligibleUser> VoteEligibleUsers { get; }

        DbSet<RolePermission> RolePermissions { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
