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

    [HttpGet("tickets/by-event")]
    public IActionResult TicketsByEvent([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var admin = _auth.ValidateInitData(initData);
        if (admin is null || !_users.IsAdmin(admin.Id)) return Forbid();
        var report = (from ticket in _db.Tickets.AsNoTracking()
                      join ev in _db.Events.AsNoTracking() on ticket.EventId equals ev.Id
                      group ticket by new { ev.Id, ev.Title } into groupByEvent
                      orderby groupByEvent.Key.Title
                      select new
                      {
                          eventId = groupByEvent.Key.Id,
                          eventTitle = groupByEvent.Key.Title,
                          orders = groupByEvent.Count(),
                          tickets = groupByEvent.Sum(x => x.Quantity),
                          paid = groupByEvent.Where(x => x.PaymentStatus == "paid").Sum(x => x.Quantity),
                          pending = groupByEvent.Where(x => x.PaymentStatus == "pending").Sum(x => x.Quantity),
                          revenue = groupByEvent.Where(x => x.PaymentStatus == "paid").Sum(x => x.TotalAmount),
                      }).ToList();
        return Ok(report);
    }
}

public record SetRoleRequest(string Role);
