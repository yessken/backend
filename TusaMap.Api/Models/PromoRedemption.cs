namespace TusaMap.Api.Models;

public class PromoRedemption
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PromoCodeId { get; set; } = "";
    public long TelegramUserId { get; set; }
    public string TicketId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
