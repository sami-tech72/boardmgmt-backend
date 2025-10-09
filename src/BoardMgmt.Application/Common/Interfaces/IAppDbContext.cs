using System.Threading.Tasks;
using System.Threading;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Common.Interfaces
{
    public interface IAppDbContext
    {
        DbSet<Meeting> Meetings { get; }
        DbSet<AgendaItem> AgendaItems { get; }
        DbSet<Document> Documents { get; }
        DbSet<MeetingAttendee> MeetingAttendees { get; }
        DbSet<Folder> Folders { get; }

        DbSet<VotePoll> VotePolls { get; }
        DbSet<VoteOption> VoteOptions { get; }
        DbSet<VoteBallot> VoteBallots { get; }
        DbSet<VoteEligibleUser> VoteEligibleUsers { get; }

        DbSet<RolePermission> RolePermissions { get; }
        DbSet<DocumentRoleAccess> DocumentRoleAccess { get; }

        DbSet<Department> Departments { get; }
        DbSet<Transcript> Transcripts { get; }
        DbSet<TranscriptUtterance> TranscriptUtterances { get; }
        DbSet<GeneratedReport> GeneratedReports { get; }

        DbSet<Conversation> Conversations { get; }
        DbSet<ConversationMember> ConversationMembers { get; }
        DbSet<ChatMessage> ChatMessages { get; }
        DbSet<ChatAttachment> ChatAttachments { get; }
        DbSet<ChatReaction> ChatReactions { get; }

        DbSet<AppUser> Users { get; }

        DbSet<TEntity> Set<TEntity>() where TEntity : class;

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
