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

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Meeting>().Property(m => m.Title).HasMaxLength(200).IsRequired();
        b.Entity<Meeting>().Property(m => m.Location).HasMaxLength(200).IsRequired();

        b.Entity<Meeting>().HasMany(m => m.AgendaItems).WithOne()
            .HasForeignKey(ai => ai.MeetingId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Meeting>().HasMany(m => m.Documents).WithOne()
            .HasForeignKey(d => d.MeetingId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Meeting>().HasMany(m => m.Attendees).WithOne()
            .HasForeignKey(a => a.MeetingId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<AgendaItem>().Property(ai => ai.Title).HasMaxLength(200).IsRequired();
        b.Entity<AgendaItem>().HasIndex(x => new { x.MeetingId, x.Order }).IsUnique();

        b.Entity<Document>().Property(d => d.FileName).HasMaxLength(200).IsRequired();
        b.Entity<Document>().Property(d => d.Url).HasMaxLength(1000).IsRequired();

        b.Entity<MeetingAttendee>().Property(a => a.Name).HasMaxLength(200).IsRequired();
        b.Entity<MeetingAttendee>().Property(a => a.Role).HasMaxLength(100);
    }
}
