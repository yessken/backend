using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;
using TusaMap.Api.Services;

namespace TusaMap.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly TusaMapDbContext _db;
    private readonly ITelegramAuthService _auth;
    private readonly IUserStore _users;

    public AdminController(TusaMapDbContext db, ITelegramAuthService auth, IUserStore users)
    {
        _db = db;
        _auth = auth;
        _users = users;
    }

    [HttpPost("users/{telegramUserId:long}/role")]
    public IActionResult SetRole(long telegramUserId, [FromBody] SetRoleRequest request, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var admin = _auth.ValidateInitData(initData);
        if (admin is null || !_users.IsAdmin(admin.Id)) return Forbid();
        if (request.Role is not ("user" or "organizer" or "checker" or "admin")) return BadRequest("Unknown role");
        var target = _db.Users.FirstOrDefault(x => x.TelegramUserId == telegramUserId);
        if (target is null) return NotFound();
        target.Role = request.Role;
        _db.SaveChanges();
        return Ok(new { target.TelegramUserId, target.Role });
    }

    [HttpGet("tickets/refunds")]
    public IActionResult Refunds([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var admin = _auth.ValidateInitData(initData);
        if (admin is null || !_users.IsAdmin(admin.Id)) return Forbid();
        return Ok(_db.Tickets.AsNoTracking().Where(x => x.RefundStatus == "requested").ToList());
    }
}

public record SetRoleRequest(string Role);
