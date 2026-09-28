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
        if (admin is null || !_users.IsAdmin(admin.Id)) return StatusCode(StatusCodes.Status403Forbidden);
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
        if (admin is null || !_users.IsAdmin(admin.Id)) return StatusCode(StatusCodes.Status403Forbidden);
        return Ok(_db.Tickets.AsNoTracking().Where(x => x.RefundStatus == "requested").ToList());
    }

    [HttpGet("tickets/by-event")]
    public IActionResult TicketsByEvent([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var admin = _auth.ValidateInitData(initData);
        if (admin is null || !_users.IsAdmin(admin.Id)) return StatusCode(StatusCodes.Status403Forbidden);
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

    [HttpGet("sales-summary")]
    public IActionResult SalesSummary([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var admin = _auth.ValidateInitData(initData);
        if (admin is null || !_users.IsAdmin(admin.Id)) return StatusCode(StatusCodes.Status403Forbidden);
        var tickets = _db.Tickets.AsNoTracking();
        return Ok(new
        {
            totalOrders = tickets.Count(),
            totalTickets = tickets.Sum(x => (int?)x.Quantity) ?? 0,
            paidOrders = tickets.Count(x => x.PaymentStatus == "paid"),
            pendingOrders = tickets.Count(x => x.PaymentStatus == "pending"),
            paidRevenue = tickets.Where(x => x.PaymentStatus == "paid").Sum(x => (decimal?)x.TotalAmount) ?? 0,
            requestedRefunds = tickets.Count(x => x.RefundStatus == "requested"),
            activeEvents = _db.Events.Count(x => x.Status == "approved"),
        });
    }

    [HttpPost("organizers/{telegramUserId:long}/subscription")]
    public IActionResult SetSubscription(long telegramUserId, [FromBody] SetSubscriptionRequest request, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var admin = _auth.ValidateInitData(initData);
        if (admin is null || !_users.IsAdmin(admin.Id)) return Forbid();
        if (request.Status is not ("active" or "inactive")) return BadRequest("Status must be active or inactive");
        var subscription = _db.OrganizerSubscriptions.Find(telegramUserId) ?? new Models.OrganizerSubscription { TelegramUserId = telegramUserId };
        subscription.Plan = request.Plan;
        subscription.Status = request.Status;
        subscription.ExpiresAt = request.ExpiresAt;
        subscription.UpdatedAt = DateTime.UtcNow;
        if (_db.Entry(subscription).State == EntityState.Detached) _db.OrganizerSubscriptions.Add(subscription);
        _db.SaveChanges();
        return Ok(subscription);
    }
}

public record SetRoleRequest(string Role);
public record SetSubscriptionRequest(string Plan, string Status, DateTime? ExpiresAt);
