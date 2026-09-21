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

    public TicketsController(ITicketsStore ticketsStore, IEventsStore eventsStore, ITelegramAuthService telegramAuth, IUserStore users, ITicketPricingService pricing, TusaMapDbContext db)
    {
        _ticketsStore = ticketsStore;
        _eventsStore = eventsStore;
        _telegramAuth = telegramAuth;
        _users = users;
        _pricing = pricing;
        _db = db;
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

    [HttpPost]
    public ActionResult<Ticket> Purchase([FromBody] PurchaseTicketRequest request, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user == null)
            return Unauthorized("Invalid or missing Telegram initData");
        _users.Upsert(user);

        var ev = _eventsStore.GetById(request.EventId);
        if (ev == null)
            return NotFound("Event not found");

        if (request.PaymentMethod is not ("kaspi" or "telegram"))
            return BadRequest("PaymentMethod must be kaspi or telegram");

        var draft = _pricing.Quote(request.EventId, request.TicketCategoryId, request.Quantity, request.PromoCode, user.Id, out var error);
        if (draft is null) return BadRequest(error);

        using var transaction = _db.Database.BeginTransaction();
        var category = _db.TicketCategories.First(x => x.Id == draft.Category.Id);
        category.Sold += draft.Quantity;

        var ticket = new Ticket
        {
            EventId = ev.Id,
            EventTitle = ev.Title,
            EventDate = ev.Date,
            EventPlace = ev.Place,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = "pending",
            PaymentReference = Guid.NewGuid().ToString("N"),
            TicketCategoryId = draft.Category.Id,
            TicketCategoryName = draft.Category.Name,
            Quantity = draft.Quantity,
            BaseAmount = draft.BaseAmount,
            DiscountAmount = draft.DiscountAmount,
            CommissionAmount = draft.CommissionAmount,
            TotalAmount = draft.TotalAmount,
            PromoCode = draft.Promo?.Code
        };
        var created = _ticketsStore.Add(ticket, user.Id);
        if (draft.Promo is not null)
        {
            draft.Promo.UsedCount++;
            _db.PromoRedemptions.Add(new Models.PromoRedemption { PromoCodeId = draft.Promo.Id, TelegramUserId = user.Id, TicketId = created.Id });
            _db.SaveChanges();
        }
        transaction.Commit();
        return StatusCode(StatusCodes.Status202Accepted, created);
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
    public IActionResult Cancel(string id, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user is null) return Unauthorized();
        var ticket = _db.Tickets.FirstOrDefault(x => x.Id == id && x.TelegramUserId == user.Id);
        if (ticket is null) return NotFound();
        if (ticket.CancelledAt is not null) return Conflict("Ticket is already cancelled");
        ticket.CancelledAt = DateTime.UtcNow;
        ticket.RefundStatus = ticket.PaymentStatus == "paid" ? "requested" : "not_required";
        _db.SaveChanges();
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
