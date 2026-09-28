namespace TusaMap.Api.Models;

public class EventInterest
{
    public string EventId { get; set; } = "";
    public long TelegramUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
