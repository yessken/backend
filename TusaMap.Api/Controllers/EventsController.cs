using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TusaMap.Api.Models;
using TusaMap.Api.Services;

namespace TusaMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventsStore _store;

    public EventsController(IEventsStore store)
    {
        _store = store;
    }

    [HttpGet]
    public ActionResult<IEnumerable<EventItem>> Get([FromQuery] string? category = null)
    {
        var list = _store.GetAll();
        if (!string.IsNullOrEmpty(category))
            list = list.Where(e => string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
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
    public ActionResult<EventItem> Create([FromBody] CreateEventRequest req, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = HttpContext.RequestServices.GetRequiredService<ITelegramAuthService>().ValidateInitData(initData);
        if (user == null)
            return Unauthorized("Invalid or missing Telegram initData");

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
        };
        var created = _store.Add(e);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
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
}
