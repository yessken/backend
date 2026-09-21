using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Models;

namespace TusaMap.Api.Data;

public class TusaMapDbContext : DbContext
{
    public TusaMapDbContext(DbContextOptions<TusaMapDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<EventItem> Events => Set<EventItem>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<PromoRedemption> PromoRedemptions => Set<PromoRedemption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasKey(x => x.TelegramUserId);
        modelBuilder.Entity<EventItem>().Property(x => x.Price).HasPrecision(12, 2);
        modelBuilder.Entity<Ticket>().HasIndex(x => x.PaymentReference).IsUnique();
        modelBuilder.Entity<Ticket>().HasIndex(x => new { x.TelegramUserId, x.EventId });
        modelBuilder.Entity<TicketCategory>().HasKey(x => x.Id);
        modelBuilder.Entity<EventItem>()
            .HasMany(x => x.TicketCategories)
            .WithOne()
            .HasForeignKey(x => x.EventId)
            .HasPrincipalKey(x => x.Id);
        modelBuilder.Entity<TicketCategory>().Property(x => x.Price).HasPrecision(12, 2);
        modelBuilder.Entity<PromoCode>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<PromoCode>().Property(x => x.Value).HasPrecision(12, 2);
        modelBuilder.Entity<PromoRedemption>().HasIndex(x => new { x.PromoCodeId, x.TelegramUserId });
    }
}