using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;
using TusaMap.Api.Services;

namespace TusaMap.Api.Controllers;

[ApiController]
[Route("api/organizer")]
public class OrganizerController : ControllerBase
{
    private readonly TusaMapDbContext _db;
    private readonly ITelegramAuthService _auth;
    private readonly IUserStore _users;
    private readonly IConfiguration _configuration;

    public OrganizerController(TusaMapDbContext db, ITelegramAuthService auth, IUserStore users, IConfiguration configuration)
    {
        _db = db;
        _auth = auth;
        _users = users;
        _configuration = configuration;
    }

    [HttpGet("subscription/offer")]
    public IActionResult SubscriptionOffer()
    {
        var stars = _configuration.GetValue<int>("Payments:TelegramSubscriptionStars");
        return Ok(new
        {
            available = stars > 0,
            stars = Math.Max(0, stars),
            durationDays = 30,
            features = new[] { "Заказы по вашим событиям", "Статистика просмотров и продаж", "Инструменты организатора" }
        });
    }

    [HttpGet("subscription")]
    public IActionResult Subscription([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _auth.ValidateInitData(initData);
        if (user is null) return Unauthorized();
        _users.Upsert(user);
        var subscription = _db.OrganizerSubscriptions.AsNoTracking().FirstOrDefault(x => x.TelegramUserId == user.Id);
        var isActive = subscription?.Status == "active" && subscription.ExpiresAt > DateTime.UtcNow;
        return Ok(new
        {
            telegramUserId = user.Id,
            plan = isActive ? subscription!.Plan : "starter",
            status = isActive ? "active" : subscription?.Status == "refunded" ? "refunded" : "inactive",
            expiresAt = subscription?.ExpiresAt,
        });
    }
}
