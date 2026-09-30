using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;
using TusaMap.Api.Services;

namespace TusaMap.Api.Services;

public interface IEventInterestNotifier
{
    Task NotifyIfTicketsAvailableAsync(string eventId, CancellationToken cancellationToken = default);
}

public sealed class EventInterestNotifier : IEventInterestNotifier
{
    private readonly TusaMapDbContext _db;
    private readonly ITelegramBotService _telegram;
    private readonly IConfiguration _configuration;

    public EventInterestNotifier(TusaMapDbContext db, ITelegramBotService telegram, IConfiguration configuration)
    {
        _db = db;
        _telegram = telegram;
        _configuration = configuration;
    }

    public async Task NotifyIfTicketsAvailableAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var eventItem = await _db.Events.AsNoTracking()
            .Include(item => item.TicketCategories)
            .FirstOrDefaultAsync(item => item.Id == eventId && item.Status == "approved" && !item.IsDemo, cancellationToken);
        if (eventItem is null || !eventItem.TicketCategories.Any(category => category.IsActive && category.Capacity > category.Sold)) return;

        var appUrl = _configuration["Telegram:WebAppUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(appUrl)) return;
        var eventUrl = $"{appUrl}/events/{Uri.EscapeDataString(eventId)}";
        var waitingUsers = await _db.EventInterests
            .Where(interest => interest.EventId == eventId && interest.TicketAvailabilityNotifiedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var interest in waitingUsers)
        {
            if (!await _telegram.NotifyTicketAvailabilityAsync(interest.TelegramUserId, eventItem.Title, eventUrl, cancellationToken)) continue;
            interest.TicketAvailabilityNotifiedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
