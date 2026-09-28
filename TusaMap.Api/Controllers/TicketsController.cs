using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;
using TusaMap.Api.Models;
using TusaMap.Api.Services;

namespace TusaMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketsStore _ticketsStore;
    private readonly IEventsStore _eventsStore;
    private readonly ITelegramAuthService _telegramAuth;
    private readonly IUserStore _users;
    private readonly ITicketPricingService _pricing;
    private readonly TusaMapDbContext _db;
    private readonly ITelegramBotService _telegramBot;

    public TicketsController(ITicketsStore ticketsStore, IEventsStore eventsStore, ITelegramAuthService telegramAuth, IUserStore users, ITicketPricingService pricing, TusaMapDbContext db, ITelegramBotService telegramBot)
    {
        _ticketsStore = ticketsStore;
        _eventsStore = eventsStore;
        _telegramAuth = telegramAuth;
        _users = users;
        _pricing = pricing;
        _db = db;
        _telegramBot = telegramBot;
    }

    [HttpGet("me")]
    public ActionResult<IReadOnlyList<Ticket>> GetMyTickets([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user == null)
            return Unauthorized("Invalid or missing Telegram initData");
        _users.Upsert(user);

        var list = _ticketsStore.GetByUserId(user.Id);
        return Ok(list);
    }

    [HttpGet("organizer")]
    public ActionResult<IReadOnlyList<OrganizerOrderRow>> OrganizerOrders([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user is null) return Unauthorized();
        _users.Upsert(user);
        var isAdmin = _users.IsAdmin(user.Id);
        var subscription = _db.OrganizerSubscriptions.AsNoTracking().FirstOrDefault(x => x.TelegramUserId == user.Id);
        if (!isAdmin && (subscription?.Status != "active" || subscription.ExpiresAt <= DateTime.UtcNow)) return Forbid();
        var rows = (from ticket in _db.Tickets.AsNoTracking()
                    join ev in _db.Events.AsNoTracking() on ticket.EventId equals ev.Id
                    where ev.OrganizerTelegramId == user.Id
                    orderby ticket.PurchasedAt descending
                    select new OrganizerOrderRow(ticket.Id, ev.Id, ev.Title, ticket.PaymentStatus, ticket.PaymentMethod, ticket.Quantity, ticket.TotalAmount, ticket.PurchasedAt))
            .ToList();
        return Ok(rows);
    }

    [HttpPost]
    public ActionResult<Ticket> Purchase([FromBody] PurchaseTicketRequest request, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user == null)
            return Unauthorized("Invalid or missing Telegram initData");
        _users.Upsert(user);
        return CreateOrder(request, user.Id);
    }

    [HttpPost("public")]
    public ActionResult<Ticket> PurchasePublic([FromBody] PurchaseTicketRequest request)
    {
        return CreateOrder(request, 0);
    }

    private ActionResult<Ticket> CreateOrder(PurchaseTicketRequest request, long userId)
    {
        if (_eventsStore.GetById(request.EventId) is null)
            return NotFound("Event not found");
        if (request.PaymentMethod != "telegram_provider")
            return BadRequest("Выберите официальный Telegram-счёт; Telegram Stars и прямые Kaspi-заказы для офлайн-входа отключены.");
        return StatusCode(StatusCodes.Status503ServiceUnavailable,
            "Создание неоплаченного заказа через API отключено. Оформите билет через официальный Telegram-счёт события.");
    }

    [HttpPost("quote")]
    public ActionResult<TicketQuoteResponse> Quote([FromBody] QuoteTicketRequest request, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user is null) return Unauthorized("Invalid or missing Telegram initData");
        _users.Upsert(user);
        var draft = _pricing.Quote(request.EventId, request.TicketCategoryId, request.Quantity, request.PromoCode, user.Id, out var error);
        if (draft is null) return BadRequest(error);
        return Ok(new TicketQuoteResponse(draft.Category.Id, draft.Category.Name, draft.Quantity, draft.BaseAmount, draft.DiscountAmount, draft.CommissionAmount, draft.TotalAmount));
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData, CancellationToken cancellationToken)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user is null) return Unauthorized();
        var ticket = _db.Tickets.FirstOrDefault(x => x.Id == id && x.TelegramUserId == user.Id);
        if (ticket is null) return NotFound();
        if (ticket.CancelledAt is not null) return Conflict("Ticket is already cancelled");
        ticket.CancelledAt = DateTime.UtcNow;
        ticket.RefundStatus = ticket.PaymentStatus == "paid" ? "requested" : "not_required";
        if (ticket.PaymentStatus == "paid" && ticket.PaymentMethod == "telegram" && !string.IsNullOrWhiteSpace(ticket.TelegramPaymentChargeId))
        {
            var refunded = await _telegramBot.RefundStarPaymentAsync(user.Id, ticket.TelegramPaymentChargeId, cancellationToken);
            ticket.PaymentStatus = refunded ? "refunded" : "refund_requested";
            ticket.RefundStatus = refunded ? "refunded" : "requested";
        }
        if (ticket.PaymentStatus == "paid" && ticket.PaymentMethod == "telegram_provider")
            await _telegramBot.NotifyAdminsOfPaymentSupportAsync(user.Id,
                $"Запрошена отмена KZT-билета. Заказ: {ticket.Id}; provider charge: {ticket.ProviderPaymentChargeId ?? "не указан"}. Возврат нужно выполнить у провайдера.",
                cancellationToken);
        if (ticket.PaymentStatus is "pending" or "checkout")
        {
            ticket.PaymentStatus = "expired";
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE TicketCategories SET Sold = CASE WHEN Sold >= {ticket.Quantity} THEN Sold - {ticket.Quantity} ELSE 0 END WHERE Id = {ticket.TicketCategoryId}",
                cancellationToken);
        }
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { ticket.Id, ticket.RefundStatus, ticket.CancelledAt });
    }
}

public class PurchaseTicketRequest
{
    [Required]
    public string EventId { get; set; } = "";
    [Required]
    public string TicketCategoryId { get; set; } = "";
    [Range(1, 10)]
    public int Quantity { get; set; } = 1;
    public string? PromoCode { get; set; }
    [Required]
    public string PaymentMethod { get; set; } = "";
}

public class QuoteTicketRequest
{
    [Required] public string EventId { get; set; } = "";
    [Required] public string TicketCategoryId { get; set; } = "";
    [Range(1, 10)] public int Quantity { get; set; } = 1;
    public string? PromoCode { get; set; }
}

public record TicketQuoteResponse(string TicketCategoryId, string TicketCategoryName, int Quantity, decimal BaseAmount, decimal DiscountAmount, decimal CommissionAmount, decimal TotalAmount);
public record OrganizerOrderRow(string TicketId, string EventId, string EventTitle, string PaymentStatus, string PaymentMethod, int Quantity, decimal TotalAmount, string PurchasedAt);
