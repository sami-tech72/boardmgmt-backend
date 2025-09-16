using BoardMgmt.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;


namespace BoardMgmt.Infrastructure.Persistence;


public class AppUser : IdentityUser { }


public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }


    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<AgendaItem> AgendaItems => Set<AgendaItem>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Vote> Votes => Set<Vote>();


    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Meeting>().HasMany(m => m.AgendaItems).WithOne().HasForeignKey(ai => ai.MeetingId);
        b.Entity<Meeting>().HasMany(m => m.Documents).WithOne().HasForeignKey(d => d.MeetingId);
        b.Entity<AgendaItem>().HasMany(ai => ai.Votes).WithOne().HasForeignKey(v => v.AgendaItemId);
        b.Entity<AgendaItem>().HasIndex(x => new { x.MeetingId, x.Order }).IsUnique();
    }
}