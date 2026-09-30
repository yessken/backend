using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using TusaMap.Api.Models;
using TusaMap.Api.Services;
using TusaMap.Api.Data;

namespace TusaMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventsStore _store;
    private readonly ITelegramAuthService _telegramAuth;
    private readonly IUserStore _users;
    private readonly TusaMapDbContext _db;
    private readonly ITelegramBotService _telegramBot;

    public EventsController(IEventsStore store, ITelegramAuthService telegramAuth, IUserStore users, TusaMapDbContext db, ITelegramBotService telegramBot)
    {
        _store = store;
        _telegramAuth = telegramAuth;
        _users = users;
        _db = db;
        _telegramBot = telegramBot;
    }

    [HttpGet]
    public ActionResult<IEnumerable<EventItem>> Get([FromQuery] string? category = null, [FromQuery] string? q = null)
    {
        var list = _store.GetAll();
        if (!string.IsNullOrEmpty(category))
            list = list.Where(e => string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var query = q.Trim();
            list = list.Where(e => new[] { e.Title, e.Place, e.Address, e.Description, e.Category }
                .Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
        }
        return Ok(list);
    }

    [HttpGet("{id}")]
    public ActionResult<EventItem> GetById(string id)
    {
        var e = _store.GetById(id);
        if (e == null)
            return NotFound();
        return Ok(e);
    }

    [HttpPost]
    public async Task<ActionResult<EventItem>> Create([FromBody] CreateEventRequest req, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData, CancellationToken cancellationToken)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user == null)
            return Unauthorized("Invalid or missing Telegram initData");
        _users.Upsert(user);
        return await AddEventAsync(req, user.Id, cancellationToken);
    }

    [HttpPost("public")]
    [EnableRateLimiting("public-event-submissions")]
    public async Task<ActionResult<EventItem>> CreatePublic([FromBody] CreateEventRequest req, CancellationToken cancellationToken)
    {
        return await AddEventAsync(req, 0, cancellationToken);
    }

    private async Task<ActionResult<EventItem>> AddEventAsync(CreateEventRequest req, long organizerTelegramId, CancellationToken cancellationToken)
    {
        var e = new EventItem
        {
            Title = req.Title,
            Description = req.Description ?? "",
            Date = req.Date,
            Time = req.Time ?? "",
            Place = req.Place,
            Address = req.Address ?? "",
            Lat = req.Lat,
            Lng = req.Lng,
            Category = req.Category ?? "концерт",
            Price = req.Price,
            ImageUrl = req.ImageUrl ?? "",
            OrganizerName = req.OrganizerName ?? ""
            ,OrganizerEmail = req.OrganizerEmail ?? ""
            ,OrganizerPhone = req.OrganizerPhone ?? ""
            ,OrganizerTelegramId = organizerTelegramId
        };
        var created = _store.Add(e);
        if (req.TicketCategories.Count > 0)
        {
            _db.TicketCategories.AddRange(req.TicketCategories.Select(category => new TicketCategory
            {
                EventId = created.Id,
                Name = category.Name,
                Description = category.Description ?? "",
                Price = category.Price,
                Capacity = category.Capacity,
            }));
            await _db.SaveChangesAsync(cancellationToken);
        }
        await _telegramBot.NotifyAdminsOfEventSubmissionAsync(created, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("pending")]
    public ActionResult<IEnumerable<EventItem>> Pending([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user is null || !_users.IsAdmin(user.Id)) return Forbid();
        return Ok(_store.GetPending());
    }

    [HttpPost("{id}/approve")]
    public ActionResult<EventItem> Approve(string id, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user is null || !_users.IsAdmin(user.Id)) return Forbid();
        var approved = _store.Approve(id);
        return approved is null ? NotFound() : Ok(approved);
    }

    [HttpPost("{id}/reject")]
    public ActionResult<EventItem> Reject(string id, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user is null || !_users.IsAdmin(user.Id)) return Forbid();
        var rejected = _store.Reject(id);
        return rejected is null ? NotFound() : Ok(rejected);
    }
}

public class CreateEventRequest
{
    [Required, StringLength(120, MinimumLength = 3)]
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    [Required, StringLength(10)]
    public string Date { get; set; } = "";
    public string? Time { get; set; }
    [Required, StringLength(160, MinimumLength = 2)]
    public string Place { get; set; } = "";
    public string? Address { get; set; }
    [Range(-90, 90)]
    public double Lat { get; set; }
    [Range(-180, 180)]
    public double Lng { get; set; }
    public string? Category { get; set; }
    [Range(0, 100000000)]
    public decimal? Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? OrganizerName { get; set; }
    [EmailAddress, StringLength(254)]
    public string? OrganizerEmail { get; set; }
    [Phone, StringLength(30)]
    public string? OrganizerPhone { get; set; }
    public List<CreateTicketCategoryRequest> TicketCategories { get; set; } = [];
}

public class CreateTicketCategoryRequest
{
    [Required, StringLength(80, MinimumLength = 2)] public string Name { get; set; } = "";
    [Range(0, 100000000)] public decimal Price { get; set; }
    [Range(1, 1000000)] public int Capacity { get; set; }
    [StringLength(300)] public string? Description { get; set; }
}
