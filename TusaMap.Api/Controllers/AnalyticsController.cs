using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;
using TusaMap.Api.Services;

namespace TusaMap.Api.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private static readonly string[] AllowedEvents = ["catalog_view", "event_open", "checkout_view", "payment_start", "purchase_success", "event_share", "organizer_lead"];
    private readonly TusaMapDbContext _db;
    private readonly ITelegramAuthService _auth;
    private readonly IUserStore _users;

    public AnalyticsController(TusaMapDbContext db, ITelegramAuthService auth, IUserStore users)
    {
        _db = db;
        _auth = auth;
        _users = users;
    }

    [HttpPost]
    public IActionResult Track([FromBody] TrackEventRequest request, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        if (!AllowedEvents.Contains(request.Name, StringComparer.Ordinal)) return BadRequest("Unknown analytics event");
        var user = _auth.ValidateInitData(initData);
        _db.FunnelEvents.Add(new Models.FunnelEvent { Name = request.Name, EventId = request.EventId, Ref = request.Ref, TelegramUserId = user?.Id });
        _db.SaveChanges();
        return NoContent();
    }

    [HttpGet("summary")]
    public IActionResult Summary([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _auth.ValidateInitData(initData);
        if (user is null || !_users.IsAdmin(user.Id)) return Forbid();
        var summary = _db.FunnelEvents.AsNoTracking().GroupBy(x => x.Name).Select(group => new { name = group.Key, count = group.Count() }).ToList();
        return Ok(summary);
    }
}

public class TrackEventRequest
{
    [Required, StringLength(40)] public string Name { get; set; } = "";
    [StringLength(80)] public string? EventId { get; set; }
    [StringLength(120)] public string? Ref { get; set; }
}