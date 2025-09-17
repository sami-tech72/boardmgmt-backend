using BoardMgmt.Application.Common.Interfaces;            // <-- implement this interface
using BoardMgmt.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Persistence;

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

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Meeting>(e =>
        {
            e.Property(m => m.Title).HasMaxLength(200).IsRequired();
            e.Property(m => m.Location).HasMaxLength(200).IsRequired();

            e.HasMany(m => m.AgendaItems)
                .WithOne()
                .HasForeignKey(ai => ai.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(m => m.Documents)
                .WithOne()
                .HasForeignKey(d => d.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(m => m.Attendees)
                .WithOne(a => a.Meeting)
                .HasForeignKey(a => a.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(m => new { m.ScheduledAt, m.Status });
        });

        b.Entity<MeetingAttendee>(e =>
        {
            e.Property(a => a.Name).HasMaxLength(200).IsRequired();
            e.Property(a => a.Role).HasMaxLength(100);
            e.Property(a => a.IsRequired).HasDefaultValue(true);
            e.Property(a => a.IsConfirmed).HasDefaultValue(false);
            e.Property(a => a.Email).HasMaxLength(320);

            e.HasIndex(a => a.MeetingId);
            e.HasIndex(x => x.UserId);

            e.HasIndex(x => new { x.MeetingId, x.UserId });

            e.HasOne<AppUser>()
             .WithMany()                 // or .WithMany(u => u.MeetingAttendees)
             .HasForeignKey(a => a.UserId)
             .HasPrincipalKey(u => u.Id)
             .OnDelete(DeleteBehavior.NoAction);
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


        //b.Entity<Document>(e =>
        //{
        //    e.Property(x => x.OriginalName).HasMaxLength(260).IsRequired();
        //    e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        //    e.Property(x => x.FolderSlug).HasMaxLength(80).HasDefaultValue("root");
        //    e.Property(x => x.ContentType).HasMaxLength(200);
        //    e.HasIndex(x => x.FolderSlug);
        //    e.HasIndex(x => x.MeetingId);
        //    e.HasIndex(x => x.UploadedAt);
        //});


        b.Entity<Document>(e =>
        {
            e.Property(x => x.OriginalName).HasMaxLength(260).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.FolderSlug).HasMaxLength(80).HasDefaultValue("root");
            e.Property(x => x.ContentType).HasMaxLength(200);


            // A) Implicit enum->int
            e.Property(x => x.Access)
             .HasDefaultValue(DocumentAccess.Administrators | DocumentAccess.BoardMembers);

            // B) Explicit conversion (if you prefer)
            // e.Property(x => x.Access)
            //  .HasConversion<int>()
            //  .HasDefaultValue(DocumentAccess.Administrators | DocumentAccess.BoardMembers);

            e.HasIndex(x => x.FolderSlug);
            e.HasIndex(x => x.MeetingId);
            e.HasIndex(x => x.UploadedAt);
        });



        // --- VotePoll
        b.Entity<VotePoll>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.CreatedByUserId).HasMaxLength(450);

            // scope fks
            e.HasOne(x => x.Meeting)
             .WithMany(m => m.Votes)            // add List<VotePoll> Votes to Meeting if you want backref; or use WithMany()
             .HasForeignKey(x => x.MeetingId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.AgendaItem)
             .WithMany()                             // or .WithMany(ai => ai.Votes) if you add backref
             .HasForeignKey(x => x.AgendaItemId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasIndex(x => x.MeetingId);
            e.HasIndex(x => x.AgendaItemId);
            e.HasIndex(x => new { x.CreatedByUserId, x.CreatedAt });
        });

        // --- VoteOption
        b.Entity<VoteOption>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.VoteId, x.Order }).IsUnique();

            e.HasOne(x => x.Vote)
             .WithMany(v => v.Options)
             .HasForeignKey(x => x.VoteId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // --- VoteBallot
        b.Entity<VoteBallot>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(450).IsRequired();

            // one ballot per (Vote, User)
            e.HasIndex(x => new { x.VoteId, x.UserId }).IsUnique();

            e.HasOne(x => x.Vote)
             .WithMany(v => v.Ballots)
             .HasForeignKey(x => x.VoteId)
             .OnDelete(DeleteBehavior.Cascade);

            // optional option FK for MultipleChoice; keep Restrict to avoid cascading null issues
            e.HasOne(x => x.Option)
             .WithMany()
             .HasForeignKey(x => x.OptionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // --- VoteEligibleUser
        b.Entity<VoteEligibleUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(450).IsRequired();

            e.HasIndex(x => new { x.VoteId, x.UserId }).IsUnique();

            e.HasOne(x => x.Vote)
             .WithMany(v => v.EligibleUsers)
             .HasForeignKey(x => x.VoteId)
             .OnDelete(DeleteBehavior.Cascade);
        });


    }
}
