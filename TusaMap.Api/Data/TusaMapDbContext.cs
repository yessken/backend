using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Models;

namespace TusaMap.Api.Data;

public class TusaMapDbContext : DbContext
{
    public TusaMapDbContext(DbContextOptions<TusaMapDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<EventItem> Events => Set<EventItem>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasKey(x => x.TelegramUserId);
        modelBuilder.Entity<EventItem>().Property(x => x.Price).HasPrecision(12, 2);
        modelBuilder.Entity<Ticket>().HasIndex(x => x.PaymentReference).IsUnique();
        modelBuilder.Entity<Ticket>().HasIndex(x => new { x.TelegramUserId, x.EventId });
    }
}