namespace TusaMap.Api.Models;

public sealed class EventParticipation
{
    public string EventId { get; set; } = "";
    public long TelegramUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}