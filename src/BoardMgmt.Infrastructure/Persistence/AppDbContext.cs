using System;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Domain.Common;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;          // AppUser
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<AppUser>, IAppDbContext
    {
        private readonly Func<ICurrentUser?>? _currentUserFactory;
        private ICurrentUser? _currentUser;

        public AppDbContext(DbContextOptions<AppDbContext> options, Func<ICurrentUser?>? currentUserFactory = null)
            : base(options)
        {
            _currentUserFactory = currentUserFactory;
        }

        // -------- Core / Meetings --------
        public DbSet<Meeting> Meetings => Set<Meeting>();
        public DbSet<AgendaItem> AgendaItems => Set<AgendaItem>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<Folder> Folders => Set<Folder>();
        public DbSet<MeetingAttendee> MeetingAttendees => Set<MeetingAttendee>();

        // -------- Voting --------
        //public DbSet<Vote> Votes => Set<Vote>();
        public DbSet<VotePoll> VotePolls => Set<VotePoll>();
        public DbSet<VoteOption> VoteOptions => Set<VoteOption>();
        public DbSet<VoteBallot> VoteBallots => Set<VoteBallot>();
        public DbSet<VoteEligibleUser> VoteEligibleUsers => Set<VoteEligibleUser>();

        // -------- Permissions / Departments --------
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<DocumentRoleAccess> DocumentRoleAccess => Set<DocumentRoleAccess>();
        public DbSet<Department> Departments => Set<Department>();

       

        // -------- Chat (new) --------
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();
        public DbSet<ChatReaction> ChatReactions => Set<ChatReaction>();


        public DbSet<Transcript> Transcripts => Set<Transcript>();
        public DbSet<TranscriptUtterance> TranscriptUtterances => Set<TranscriptUtterance>();
        public DbSet<GeneratedReport> GeneratedReports => Set<GeneratedReport>();
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditMetadata();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyAuditMetadata()
        {
            var now = DateTimeOffset.UtcNow;
            var userId = GetCurrentUser()?.UserId;
            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    if (string.IsNullOrEmpty(entry.Entity.CreatedByUserId) && !string.IsNullOrEmpty(userId))
                    {
                        entry.Entity.CreatedByUserId = userId;
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Property(x => x.CreatedByUserId).IsModified = false;

                    entry.Entity.UpdatedAt = now;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        entry.Entity.UpdatedByUserId = userId;
                    }
                }
            }
        }

        private ICurrentUser? GetCurrentUser()
        {
            if (_currentUser is not null)
            {
                return _currentUser;
            }

            if (_currentUserFactory is null)
            {
                return null;
            }

            _currentUser = _currentUserFactory();
            return _currentUser;
        }

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // ---------- Meetings ----------
            b.Entity<Meeting>(e =>
            {
                e.HasKey(m => m.Id);
                e.Property(m => m.Title).HasMaxLength(200).IsRequired();
                e.Property(m => m.Location).HasMaxLength(200).IsRequired();

                e.HasMany(m => m.AgendaItems).WithOne().HasForeignKey(ai => ai.MeetingId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(m => m.Documents).WithOne().HasForeignKey(d => d.MeetingId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(m => m.Attendees).WithOne(a => a.Meeting).HasForeignKey(a => a.MeetingId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(m => m.Transcripts).WithOne(t => t.Meeting).HasForeignKey(t => t.MeetingId).OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(m => new { m.ScheduledAt, m.Status });
            });

            b.Entity<MeetingAttendee>(e =>
            {
                e.HasKey(a => a.Id);

                e.Property(a => a.Name).HasMaxLength(200).IsRequired();
                e.Property(a => a.Role).HasMaxLength(100);
                e.Property(a => a.IsRequired).HasDefaultValue(true);
                e.Property(a => a.IsConfirmed).HasDefaultValue(false);
                e.Property(a => a.Email).HasMaxLength(320);
                e.Property(a => a.UserId).HasMaxLength(450);

                e.HasIndex(a => a.MeetingId);
                e.HasIndex(a => a.UserId);

                e.HasOne(a => a.Meeting)
                    .WithMany(m => m.Attendees)
                    .HasForeignKey(a => a.MeetingId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne<AppUser>()
                    .WithMany()
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
            });


            b.Entity<AgendaItem>(e =>
            {
                e.Property(x => x.Title).HasMaxLength(200).IsRequired();
                e.HasIndex(x => new { x.MeetingId, x.Order });
            });

            b.Entity<Folder>(e =>
            {
                e.HasIndex(x => x.Slug).IsUnique();
                e.Property(x => x.Name).HasMaxLength(60).IsRequired();
                e.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            });

            b.Entity<Document>(e =>
            {
                e.Property(x => x.OriginalName).HasMaxLength(260).IsRequired();
                e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
                e.Property(x => x.FolderSlug).HasMaxLength(80).HasDefaultValue("root");
                e.Property(x => x.ContentType).HasMaxLength(200);

                e.HasIndex(x => x.FolderSlug);
                e.HasIndex(x => x.MeetingId);
                e.HasIndex(x => x.UploadedAt);
            });

            // ---------- Voting ----------
            b.Entity<VotePoll>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).HasMaxLength(160).IsRequired();
                e.Property(x => x.Description).HasMaxLength(2000);
                e.Property(x => x.CreatedByUserId).HasMaxLength(450);

                // FK to Meeting
                e.HasOne(x => x.Meeting)
                 .WithMany(m => m.Votes)
                 .HasForeignKey(x => x.MeetingId)
                 .OnDelete(DeleteBehavior.NoAction);

                // FK to AgendaItem (optional) — corrected
                e.HasOne(x => x.AgendaItem)
                 .WithMany(a => a.VotePolls) // navigation collection added
                 .HasForeignKey(x => x.AgendaItemId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.NoAction);

                // Indexes
                e.HasIndex(x => x.MeetingId);
                e.HasIndex(x => x.AgendaItemId);
                e.HasIndex(x => new { x.CreatedByUserId, x.CreatedAt });
            });


            b.Entity<VoteOption>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Text).HasMaxLength(200).IsRequired();
                e.HasIndex(x => new { x.VoteId, x.Order }).IsUnique();

                e.HasOne(x => x.Vote).WithMany(v => v.Options).HasForeignKey(x => x.VoteId).OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<VoteBallot>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.UserId).HasMaxLength(450).IsRequired();

                e.HasIndex(x => new { x.VoteId, x.UserId }).IsUnique();

                e.HasOne(x => x.Vote).WithMany(v => v.Ballots).HasForeignKey(x => x.VoteId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Option).WithMany().HasForeignKey(x => x.OptionId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<VoteEligibleUser>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.UserId).HasMaxLength(450).IsRequired();
                e.HasIndex(x => new { x.VoteId, x.UserId }).IsUnique();

                e.HasOne(x => x.Vote).WithMany(v => v.EligibleUsers).HasForeignKey(x => x.VoteId).OnDelete(DeleteBehavior.Cascade);
            });

            // ---------- RolePermission / DocumentRoleAccess ----------
            b.Entity<RolePermission>(e =>
            {
                e.ToTable("RolePermissions");
                e.HasKey(x => x.Id);

                e.Property(x => x.RoleId).HasMaxLength(450).IsRequired();
                e.Property(x => x.Module).HasConversion<int>();
                e.Property(x => x.Allowed).HasConversion<int>();

                e.HasIndex(x => new { x.RoleId, x.Module }).IsUnique();

                e.HasOne<IdentityRole>()
                    .WithMany()
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<DocumentRoleAccess>(e =>
            {
                e.HasKey(x => new { x.DocumentId, x.RoleId });
                e.HasIndex(x => x.RoleId);

                e.HasOne(x => x.Document)
                    .WithMany(d => d.RoleAccesses)
                    .HasForeignKey(x => x.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
                // Optional FK to AspNetRoles:
                // e.HasOne<IdentityRole>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            });

            // ---------- Departments / AppUser ----------
            b.Entity<Department>(cfg =>
            {
                cfg.ToTable("Departments");
                cfg.HasKey(x => x.Id);
                cfg.Property(x => x.Name).HasMaxLength(160).IsRequired();
                cfg.Property(x => x.Description).HasMaxLength(400);
                cfg.Property(x => x.IsActive).HasDefaultValue(true);
                cfg.HasIndex(x => x.Name).IsUnique();
            });

            b.Entity<AppUser>(cfg =>
            {
                cfg.HasOne(u => u.Department)
                   .WithMany(d => d.Users)
                   .HasForeignKey(u => u.DepartmentId)
                   .OnDelete(DeleteBehavior.SetNull);
            });

          
            // ---------- Chat (new) ----------
            b.Entity<Conversation>(e =>
            {
                e.ToTable("Chat_Conversations");
                e.HasKey(x => x.Id);

                e.Property(x => x.Name).HasMaxLength(120);

                e.HasMany(x => x.Members)
                    .WithOne(x => x.Conversation)
                    .HasForeignKey(x => x.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasMany(x => x.Messages)
                    .WithOne() // or .WithOne(m => m.Conversation) if you have the nav
                    .HasForeignKey(x => x.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<ConversationMember>(e =>
            {
                e.ToTable("Chat_ConversationMembers");
                e.HasKey(x => x.Id);

                e.HasIndex(x => new { x.ConversationId, x.UserId }).IsUnique();
                e.Property(x => x.UserId).HasMaxLength(450).IsRequired();

                e.HasOne<AppUser>()
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            

            

            b.Entity<ChatMessage>(e =>
            {
                e.ToTable("Chat_Messages");
                e.HasKey(x => x.Id);

                e.HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });
                e.HasIndex(x => x.ThreadRootId);

                e.Property(x => x.ConversationId).IsRequired();
                e.Property(x => x.CreatedAtUtc).IsRequired();
                e.Property(x => x.SenderId).HasMaxLength(450).IsRequired();

                // Threading (self-FK), optional
                e.HasOne<ChatMessage>()
                    .WithMany()
                    .HasForeignKey(x => x.ThreadRootId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Sender
                e.HasOne<AppUser>()
                    .WithMany()
                    .HasForeignKey(x => x.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

           

            b.Entity<ChatAttachment>(e =>
            {
                e.ToTable("Chat_Attachments");
                e.HasKey(x => x.Id);

                e.HasIndex(x => x.MessageId);

                e.Property(x => x.MessageId).IsRequired();
                e.Property(x => x.FileName).HasMaxLength(255).IsRequired();
                e.Property(x => x.ContentType).HasMaxLength(128);
                e.Property(x => x.StoragePath).HasMaxLength(1024);

                e.HasOne<ChatMessage>()
                    .WithMany() // or .WithMany(m => m.Attachments)
                    .HasForeignKey(x => x.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<ChatReaction>(e =>
            {
                e.ToTable("Chat_Reactions");
                e.HasKey(x => x.Id);

                e.HasIndex(x => new { x.MessageId, x.UserId, x.Emoji }).IsUnique();

                e.Property(x => x.MessageId).IsRequired();
                e.Property(x => x.UserId).HasMaxLength(450).IsRequired();
                e.Property(x => x.Emoji).HasMaxLength(64).IsRequired();

                e.HasOne<ChatMessage>()
                    .WithMany() // or .WithMany(m => m.Reactions)
                    .HasForeignKey(x => x.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);

                // FK to AppUser (string) — now compatible
                e.HasOne<AppUser>()
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });


            b.Entity<Transcript>(e =>
            {
                e.ToTable("Transcripts");
                e.HasKey(x => x.Id);
                e.Property(x => x.Provider).HasMaxLength(64).IsRequired();
                e.Property(x => x.ProviderTranscriptId).HasMaxLength(256).IsRequired();
                e.HasIndex(x => new { x.MeetingId, x.Provider }).IsUnique();

                e.HasOne(x => x.Meeting)
                 .WithMany(m => m.Transcripts)
                 .HasForeignKey(x => x.MeetingId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<TranscriptUtterance>(e =>
            {
                e.ToTable("TranscriptUtterances");
                e.HasKey(x => x.Id);
                e.Property(x => x.Text).HasMaxLength(4000).IsRequired();
                e.Property(x => x.SpeakerName).HasMaxLength(256);
                e.Property(x => x.SpeakerEmail).HasMaxLength(320);
                e.HasIndex(x => new { x.TranscriptId, x.Start });

                e.HasOne(x => x.Transcript)
                 .WithMany(t => t.Utterances)
                 .HasForeignKey(x => x.TranscriptId)
                 .OnDelete(DeleteBehavior.Cascade);
            });


            // inside OnModelCreating(ModelBuilder b)
            b.Entity<GeneratedReport>(e =>
            {
                e.HasIndex(x => x.GeneratedAt);
                e.HasOne(x => x.GeneratedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.GeneratedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });



        }
    }
}
