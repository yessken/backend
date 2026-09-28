using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;

namespace TusaMap.Api.Services;

public sealed class PendingTelegramTicketCleanupService : BackgroundService
{
    private static readonly TimeSpan ReservationLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CheckoutLifetime = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingTelegramTicketCleanupService> _logger;

    public PendingTelegramTicketCleanupService(IServiceScopeFactory scopeFactory, ILogger<PendingTelegramTicketCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExpireReservationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to expire abandoned Telegram ticket reservations");
            }
        }
    }

    private async Task ExpireReservationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TusaMapDbContext>();
        var now = DateTime.UtcNow;
        var pendingCutoff = now.Subtract(ReservationLifetime);
        var checkoutCutoff = now.Subtract(CheckoutLifetime);
        var candidates = await db.Tickets.AsNoTracking()
            .Where(ticket => ticket.PaymentMethod == "telegram_provider" &&
                (ticket.PaymentStatus == "pending" || ticket.PaymentStatus == "checkout"))
            .Select(ticket => new { ticket.Id, ticket.TicketCategoryId, ticket.Quantity, ticket.PaymentStatus })
            .ToListAsync(cancellationToken);
        var purchaseTimes = await db.Tickets.AsNoTracking()
            .Where(ticket => ticket.PaymentMethod == "telegram_provider" && ticket.PaymentStatus == "pending")
            .Select(ticket => new { ticket.Id, ticket.PurchasedAt })
            .ToDictionaryAsync(ticket => ticket.Id, ticket => ticket.PurchasedAt, cancellationToken);
        var checkoutTimes = await db.Tickets.AsNoTracking()
            .Where(ticket => ticket.PaymentMethod == "telegram_provider" && ticket.PaymentStatus == "checkout")
            .Select(ticket => new { ticket.Id, ticket.PaymentCheckoutAt })
            .ToDictionaryAsync(ticket => ticket.Id, ticket => ticket.PaymentCheckoutAt, cancellationToken);
        candidates = candidates.Where(candidate => candidate.PaymentStatus == "pending"
                ? purchaseTimes.TryGetValue(candidate.Id, out var purchasedAt) &&
                  DateTimeOffset.TryParse(purchasedAt, out var parsedPurchase) && parsedPurchase.UtcDateTime < pendingCutoff
                : checkoutTimes.TryGetValue(candidate.Id, out var checkoutAt) && checkoutAt is not null && checkoutAt < checkoutCutoff)
            .ToList();

        foreach (var candidate in candidates)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var expired = candidate.PaymentStatus == "checkout"
                ? await db.Tickets.Where(ticket => ticket.Id == candidate.Id && ticket.PaymentMethod == "telegram_provider" &&
                        ticket.PaymentStatus == "checkout" && ticket.PaymentCheckoutAt != null && ticket.PaymentCheckoutAt < checkoutCutoff)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(ticket => ticket.PaymentStatus, "expired")
                        .SetProperty(ticket => ticket.RefundStatus, "not_required")
                        .SetProperty(ticket => ticket.CancelledAt, now), cancellationToken)
                : await db.Tickets.Where(ticket => ticket.Id == candidate.Id && ticket.PaymentMethod == "telegram_provider" &&
                    ticket.PaymentStatus == "pending")
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(ticket => ticket.PaymentStatus, "expired")
                        .SetProperty(ticket => ticket.RefundStatus, "not_required")
                        .SetProperty(ticket => ticket.CancelledAt, now), cancellationToken);

            if (expired == 1)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE TicketCategories SET Sold = CASE WHEN Sold >= {candidate.Quantity} THEN Sold - {candidate.Quantity} ELSE 0 END WHERE Id = {candidate.TicketCategoryId}",
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }
    }
}
