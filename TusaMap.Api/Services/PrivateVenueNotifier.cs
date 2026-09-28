using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;

namespace TusaMap.Api.Services;

public sealed class PrivateVenueNotifier : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PrivateVenueNotifier> _logger;

    public PrivateVenueNotifier(IServiceScopeFactory scopeFactory, ILogger<PrivateVenueNotifier> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await NotifyDueEventsAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Private venue address notification cycle failed");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }

    private async Task NotifyDueEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TusaMapDbContext>();
        var telegram = scope.ServiceProvider.GetRequiredService<ITelegramBotService>();
        var now = DateTime.UtcNow;
        var events = await db.Events
            .Where(e => e.Status == "approved" && !e.IsDemo && e.AddressIsPrivate &&
                        e.AddressRevealAt != null && e.AddressRevealAt <= now && e.PrivateAddress != "")
            .ToListAsync(cancellationToken);

        foreach (var ev in events)
        {
            var paidTickets = await db.Tickets
                .Where(t => t.EventId == ev.Id && t.PaymentStatus == "paid" && t.CancelledAt == null &&
                            t.RefundStatus != "requested" && t.RefundStatus != "refunded" &&
                            t.TelegramUserId != 0 && t.PrivateAddressSentAt == null)
                .ToListAsync(cancellationToken);
            foreach (var ticket in paidTickets)
            {
                var delivered = await telegram.SendPrivateVenueAddressAsync(ticket.TelegramUserId, ev.Title, ev.PrivateAddress, ev.Date, ev.Time, cancellationToken);
                if (!delivered) continue;
                ticket.PrivateAddressSentAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
