using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;
using TusaMap.Api.Models;

namespace TusaMap.Api.Services;

public record TicketOrderDraft(
    TicketCategory Category,
    int Quantity,
    decimal BaseAmount,
    decimal DiscountAmount,
    decimal CommissionAmount,
    decimal TotalAmount,
    PromoCode? Promo);

public interface ITicketPricingService
{
    TicketOrderDraft? Quote(string eventId, string categoryId, int quantity, string? promoCode, long userId, out string? error);
}

public class TicketPricingService : ITicketPricingService
{
    private const decimal CommissionRate = 0.10m;
    private readonly TusaMapDbContext _db;

    public TicketPricingService(TusaMapDbContext db) => _db = db;

    public TicketOrderDraft? Quote(string eventId, string categoryId, int quantity, string? promoCode, long userId, out string? error)
    {
        error = null;
        if (quantity is < 1 or > 10) { error = "Quantity must be between 1 and 10"; return null; }
        var category = _db.TicketCategories.FirstOrDefault(x => x.Id == categoryId && x.EventId == eventId && x.IsActive);
        if (category is null) { error = "Ticket category not found"; return null; }
        if (category.Capacity - category.Sold < quantity) { error = "Not enough tickets available"; return null; }

        var baseAmount = category.Price * quantity;
        PromoCode? promo = null;
        var discount = 0m;
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            promo = _db.PromoCodes.FirstOrDefault(x => x.Code == promoCode.Trim().ToUpperInvariant() && x.IsActive);
            if (promo is null || (promo.EventId is not null && promo.EventId != eventId) || (promo.TicketCategoryId is not null && promo.TicketCategoryId != categoryId))
            { error = "Promo code is not valid for this ticket"; return null; }
            var now = DateTime.UtcNow;
            if (promo.StartsAt > now || promo.ExpiresAt < now || promo.MaxUses is not null && promo.UsedCount >= promo.MaxUses)
            { error = "Promo code has expired or reached its limit"; return null; }
            if (userId != 0 && _db.PromoRedemptions.Count(x => x.PromoCodeId == promo.Id && x.TelegramUserId == userId) >= promo.PerUserLimit)
            { error = "Promo code has already been used"; return null; }
            discount = promo.DiscountType == "fixed" ? promo.Value : baseAmount * promo.Value / 100m;
            discount = Math.Min(discount, baseAmount);
        }

        var net = baseAmount - discount;
        var commission = Math.Round(net * CommissionRate, 2, MidpointRounding.AwayFromZero);
        return new TicketOrderDraft(category, quantity, baseAmount, discount, commission, net + commission, promo);
    }
}
