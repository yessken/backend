using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
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
    private readonly IUserStore _users;

    public TicketsController(ITicketsStore ticketsStore, IEventsStore eventsStore, ITelegramAuthService telegramAuth, IUserStore users)
    {
        _ticketsStore = ticketsStore;
        _eventsStore = eventsStore;
        _telegramAuth = telegramAuth;
        _users = users;
    }

    [HttpGet("me")]
    public ActionResult<IReadOnlyList<Ticket>> GetMyTickets([FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user == null)
            return Unauthorized("Invalid or missing Telegram initData");
        _users.Upsert(user);

        var list = _ticketsStore.GetByUserId(user.Id);
        return Ok(list);
    }

    [HttpPost]
    public ActionResult<Ticket> Purchase([FromBody] PurchaseTicketRequest request, [FromHeader(Name = "X-Telegram-Init-Data")] string? initData)
    {
        var user = _telegramAuth.ValidateInitData(initData);
        if (user == null)
            return Unauthorized("Invalid or missing Telegram initData");
        _users.Upsert(user);

        var ev = _eventsStore.GetById(request.EventId);
        if (ev == null)
            return NotFound("Event not found");

        if (request.PaymentMethod is not ("kaspi" or "telegram"))
            return BadRequest("PaymentMethod must be kaspi or telegram");

        var ticket = new Ticket
        {
            EventId = ev.Id,
            EventTitle = ev.Title,
            EventDate = ev.Date,
            EventPlace = ev.Place,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = "pending",
            PaymentReference = Guid.NewGuid().ToString("N")
        };
        var created = _ticketsStore.Add(ticket, user.Id);
        return StatusCode(StatusCodes.Status202Accepted, created);
    }
}

public class PurchaseTicketRequest
{
    [Required]
    public string EventId { get; set; } = "";
    [Required]
    public string PaymentMethod { get; set; } = "";
}
