using BoardMgmt.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<AgendaItem> AgendaItems => Set<AgendaItem>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<MeetingAttendee> MeetingAttendees => Set<MeetingAttendee>();

    // NEW
    public DbSet<Folder> Folders => Set<Folder>();
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
            e.HasIndex(a => a.MeetingId);
            e.Property(a => a.Email).HasMaxLength(320);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.MeetingId, x.UserId });

            // FK relationship to Identity user (optional, allows NULL)
            e.HasOne<AppUser>()
             .WithMany()                 // or .WithMany(u => u.MeetingAttendees) if you add nav property
             .HasForeignKey(a => a.UserId)
             .HasPrincipalKey(u => u.Id) // AppUser.Id is string
             .OnDelete(DeleteBehavior.NoAction); // don’t cascade delete meetings if user deleted

        });

        b.Entity<AgendaItem>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.MeetingId, x.Order });
        });

        // NEW: Folders
        b.Entity<Folder>(e =>
        {
            e.Property(f => f.Name).HasMaxLength(100).IsRequired();
            e.Property(f => f.Slug).HasMaxLength(100).IsRequired();
            e.HasIndex(f => f.Slug).IsUnique();
        });

        // NEW: Documents
        b.Entity<Document>(e =>
        {
            e.Property(d => d.FolderSlug).HasMaxLength(100).HasDefaultValue("root");
            e.Property(d => d.FileName).HasMaxLength(260).IsRequired();
            e.Property(d => d.OriginalName).HasMaxLength(260).IsRequired();
            e.Property(d => d.ContentType).HasMaxLength(150);
            e.Property(d => d.Description).HasMaxLength(1000);
            e.HasIndex(d => new { d.FolderSlug, d.UploadedAt });
        });
    }
}
