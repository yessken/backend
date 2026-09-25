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

    public OrganizerController(TusaMapDbContext db, ITelegramAuthService auth, IUserStore users)
    {
        _db = db;
        _auth = auth;
        _users = users;
    }

    [HttpGet("subscription")]
    public IActionResult Subscription([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _auth.ValidateInitData(initData);
        if (user is null) return Unauthorized();
        _users.Upsert(user);
        var subscription = _db.OrganizerSubscriptions.AsNoTracking().FirstOrDefault(x => x.TelegramUserId == user.Id);
        return Ok(new
        {
            telegramUserId = user.Id,
            plan = subscription?.Plan ?? "starter",
            status = subscription?.Status ?? "inactive",
            expiresAt = subscription?.ExpiresAt,
        });
    }
}
