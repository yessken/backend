using Microsoft.AspNetCore.Mvc;
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

    public TicketsController(ITicketsStore ticketsStore, IEventsStore eventsStore, ITelegramAuthService telegramAuth)
    {
        _ticketsStore = ticketsStore;
        _eventsStore = eventsStore;
        _telegramAuth = telegramAuth;
    }

    [HttpGet("me")]
    public ActionResult<IReadOnlyList<Ticket>> GetMyTickets([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user == null)
            return Unauthorized("Invalid or missing Telegram initData");

        var list = _ticketsStore.GetByUserId(user.Id);
        return Ok(list);
    }

    [HttpPost]
    public ActionResult<Ticket> Purchase([FromBody] PurchaseTicketRequest request, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user == null)
            return Unauthorized("Invalid or missing Telegram initData");

        var ev = _eventsStore.GetById(request.EventId);
        if (ev == null)
            return NotFound("Event not found");

        var ticket = new Ticket
        {
            EventId = ev.Id,
            EventTitle = ev.Title,
            EventDate = ev.Date,
            EventPlace = ev.Place,
            QrCode = "TUSA-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()
        };
        var created = _ticketsStore.Add(ticket, user.Id);
        return Ok(created);
    }
}

public class PurchaseTicketRequest
{
    public string EventId { get; set; } = "";
}
