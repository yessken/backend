using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using TusaMap.Api.Data;
using TusaMap.Api.Models;
using TusaMap.Api.Services;

namespace TusaMap.Api.Controllers;

[ApiController]
[Route("api/events/{eventId}")]
public class TicketCatalogController : ControllerBase
{
    private readonly TusaMapDbContext _db;
    private readonly ITelegramAuthService _auth;
    private readonly IUserStore _users;

    public TicketCatalogController(TusaMapDbContext db, ITelegramAuthService auth, IUserStore users)
    {
        _db = db;
        _auth = auth;
        _users = users;
    }

    [HttpPost("ticket-categories")]
    public ActionResult<TicketCategory> AddCategory(string eventId, [FromBody] TicketCategoryRequest request, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _auth.ValidateInitData(initData);
        var ev = _db.Events.Find(eventId);
        if (user is null || ev is null) return Unauthorized();
        if (ev.OrganizerTelegramId != user.Id && !_users.IsAdmin(user.Id)) return Forbid();
        var category = new TicketCategory { EventId = eventId, Name = request.Name.Trim(), Description = request.Description ?? "", Price = request.Price, Capacity = request.Capacity };
        _db.TicketCategories.Add(category);
        _db.SaveChanges();
        return Ok(category);
    }

    [HttpPost("promo-codes")]
    public ActionResult<PromoCode> AddPromoCode(string eventId, [FromBody] PromoCodeRequest request, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _auth.ValidateInitData(initData);
        var ev = _db.Events.Find(eventId);
        if (user is null || ev is null) return Unauthorized();
        if (ev.OrganizerTelegramId != user.Id && !_users.IsAdmin(user.Id)) return Forbid();
        if (request.DiscountType is not ("percent" or "fixed")) return BadRequest("DiscountType must be percent or fixed");
        if (request.DiscountType == "percent" && request.Value > 100) return BadRequest("Percent discount cannot exceed 100");
        var promo = new PromoCode { EventId = eventId, Code = request.Code.Trim().ToUpperInvariant(), DiscountType = request.DiscountType, Value = request.Value, MaxUses = request.MaxUses, PerUserLimit = request.PerUserLimit, StartsAt = request.StartsAt, ExpiresAt = request.ExpiresAt };
        _db.PromoCodes.Add(promo);
        _db.SaveChanges();
        return Ok(promo);
    }
}

public class TicketCategoryRequest
{
    [Required, StringLength(80, MinimumLength = 2)] public string Name { get; set; } = "";
    [Range(0, 100000000)] public decimal Price { get; set; }
    [Range(1, 1000000)] public int Capacity { get; set; }
    [StringLength(300)] public string? Description { get; set; }
}

public class PromoCodeRequest
{
    [Required, StringLength(40, MinimumLength = 3)] public string Code { get; set; } = "";
    [Required] public string DiscountType { get; set; } = "percent";
    [Range(0, 100000000)] public decimal Value { get; set; }
    [Range(1, 1000000)] public int? MaxUses { get; set; }
    [Range(1, 100)] public int PerUserLimit { get; set; } = 1;
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
