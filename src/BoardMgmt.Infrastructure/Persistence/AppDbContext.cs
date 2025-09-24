using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Messages;
using BoardMgmt.Domain.Identity; // ✅ use Domain.AppUser
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<AppUser>, IAppDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Meeting> Meetings => Set<Meeting>();
        public DbSet<AgendaItem> AgendaItems => Set<AgendaItem>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<Vote> Votes => Set<Vote>();
        public DbSet<MeetingAttendee> MeetingAttendees => Set<MeetingAttendee>();
        public DbSet<Folder> Folders => Set<Folder>();

        public DbSet<VotePoll> VotePolls => Set<VotePoll>();
        public DbSet<VoteOption> VoteOptions => Set<VoteOption>();
        public DbSet<VoteBallot> VoteBallots => Set<VoteBallot>();
        public DbSet<VoteEligibleUser> VoteEligibleUsers => Set<VoteEligibleUser>();

        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<DocumentRoleAccess> DocumentRoleAccess => Set<DocumentRoleAccess>();
        public DbSet<Department> Departments => Set<Department>();

        public DbSet<Message> Messages => Set<Message>();
        public DbSet<AppUser> AppUser => Set<AppUser>();
        public DbSet<MessageRecipient> MessageRecipients => Set<MessageRecipient>();
        public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();


        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => base.SaveChangesAsync(cancellationToken);



        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);


            // ------- Meetings (unchanged) -------
            b.Entity<Meeting>(e =>
            {
                e.Property(m => m.Title).HasMaxLength(200).IsRequired();
                e.Property(m => m.Location).HasMaxLength(200).IsRequired();
               
                e.HasMany(m => m.AgendaItems).WithOne().HasForeignKey(ai => ai.MeetingId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(m => m.Documents).WithOne().HasForeignKey(d => d.MeetingId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(m => m.Attendees).WithOne(a => a.Meeting).HasForeignKey(a => a.MeetingId).OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(m => new { m.ScheduledAt, m.Status });
            });

            //b.Entity<MeetingAttendee>(e =>
            //{
            //    e.Property(a => a.Name).HasMaxLength(200).IsRequired();
            //    e.Property(a => a.Role).HasMaxLength(100);
            //    e.Property(a => a.IsRequired).HasDefaultValue(true);
            //    e.Property(a => a.IsConfirmed).HasDefaultValue(false);
            //    e.Property(a => a.Email).HasMaxLength(320);

            //    e.HasIndex(a => a.MeetingId);
            //    e.Property(a => a.UserId).HasMaxLength(450);   // nvarchar(450)
            //                                                   // lookups
            //    e.HasIndex(a => a.MeetingId);
            //    e.HasIndex(a => a.UserId);

            //    // 👇 prevent duplicate (MeetingId, UserId) attendees, but allow multiple external attendees (UserId = NULL)
            //    e.HasIndex(x => new { x.MeetingId, x.UserId })
            //     .IsUnique()
            //     .HasFilter("[UserId] IS NOT NULL"); // SQL Server filter; for PostgreSQL use "WHERE ""UserId"" IS NOT NULL"

            //    e.HasOne<AppUser>()
            //     .WithMany()
            //     .HasForeignKey(a => a.UserId)
            //     .HasPrincipalKey(u => u.Id)
            //     .OnDelete(DeleteBehavior.NoAction);
            //});

            // BoardMgmt.Infrastructure/Persistence/AppDbContext.cs (excerpt)
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

                e.Property(x => x.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();



                e.HasOne(x => x.Meeting)
                    .WithMany(m => m.Attendees)
                    .HasForeignKey(x => x.MeetingId)
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

            // ------- Voting (unchanged) -------
            b.Entity<VotePoll>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).HasMaxLength(160).IsRequired();
                e.Property(x => x.Description).HasMaxLength(2000);
                e.Property(x => x.CreatedByUserId).HasMaxLength(450);

                e.HasOne(x => x.Meeting).WithMany(m => m.Votes).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.AgendaItem).WithMany().HasForeignKey(x => x.AgendaItemId).OnDelete(DeleteBehavior.NoAction);

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

            // ------- RolePermission (SINGLE mapping) -------
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
                e.HasIndex(x => x.RoleId); // ✅ helpful for visibility queries

                e.HasOne(x => x.Document)
                    .WithMany(d => d.RoleAccesses)
                    .HasForeignKey(x => x.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);

                // RoleId references AspNetRoles.Id (string), but we keep it as plain string FK
                // If you want FK constraint to AspNetRoles, you can add it, but it's optional:
                // e.HasOne<IdentityRole>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            });


            // Department config
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


            // ------- Messaging (new) -------
            b.Entity<Message>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.Subject)
                    .HasMaxLength(300)
                    .IsRequired();

                e.Property(x => x.Body)
                    .IsRequired();

                e.Property(x => x.Priority)
                    .HasConversion<string>()
                    .HasMaxLength(16);

                e.Property(x => x.Status)
                    .HasConversion<string>()
                    .HasMaxLength(16);

                e.Property(x => x.ReadReceiptRequested);
                e.Property(x => x.IsConfidential);

                e.Property(x => x.CreatedAtUtc)
                    .IsRequired();

                e.Property(x => x.UpdatedAtUtc)
                    .IsRequired();

                e.HasMany(x => x.Recipients)
                    .WithOne()
                    .HasForeignKey(r => r.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasMany(x => x.Attachments)
                    .WithOne()
                    .HasForeignKey(a => a.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<MessageRecipient>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.UserId).HasMaxLength(450);
                e.Property(x => x.IsRead).HasDefaultValue(false);
                e.HasIndex(x => new { x.MessageId, x.UserId }).IsUnique();
            });

            b.Entity<MessageAttachment>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.FileName).HasMaxLength(255).IsRequired();
                e.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
                e.Property(x => x.StoragePath).HasMaxLength(1024).IsRequired();
            });

        }
    }
}