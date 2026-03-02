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
}
