namespace TusaMap.Api.Models;

public class TicketCheckIn
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TicketId { get; set; } = "";
    public long CheckedByTelegramUserId { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
