namespace TusaMap.Api.Models;

public class PromoCode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Code { get; set; } = "";
    public string? EventId { get; set; }
    public string? TicketCategoryId { get; set; }
    public string DiscountType { get; set; } = "percent";
    public decimal Value { get; set; }
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; }
    public int PerUserLimit { get; set; } = 1;
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}
