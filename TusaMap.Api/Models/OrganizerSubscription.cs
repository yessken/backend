namespace TusaMap.Api.Models;

public class OrganizerSubscription
{
    public long TelegramUserId { get; set; }
    public string Plan { get; set; } = "starter";
    public string Status { get; set; } = "inactive";
    public DateTime? ExpiresAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
