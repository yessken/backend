namespace TusaMap.Api.Models;

public class FunnelEvent
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? EventId { get; set; }
    public string? Ref { get; set; }
    public long? TelegramUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
