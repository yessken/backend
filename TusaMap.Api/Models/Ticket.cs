namespace TusaMap.Api.Models;

public class Ticket
{
    public string Id { get; set; } = "";
    public string EventId { get; set; } = "";
    public string EventTitle { get; set; } = "";
    public string EventDate { get; set; } = "";
    public string EventPlace { get; set; } = "";
    public string? QrCode { get; set; }
    public string PurchasedAt { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public string PaymentStatus { get; set; } = "pending";
    public long TelegramUserId { get; set; }
    public string? PaymentReference { get; set; }
    public int? TelegramStarsAmount { get; set; }
    public string? TelegramPaymentChargeId { get; set; }
    public string TicketCategoryId { get; set; } = "";
    public string TicketCategoryName { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public decimal BaseAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? PromoCode { get; set; }
    public string RefundStatus { get; set; } = "none";
    public DateTime? CancelledAt { get; set; }
    public TicketCheckIn? CheckIn { get; set; }
}
