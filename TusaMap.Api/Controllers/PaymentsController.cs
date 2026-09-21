using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using TusaMap.Api.Services;

namespace TusaMap.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly ITicketsStore _tickets;
    private readonly IConfiguration _configuration;

    public PaymentsController(ITicketsStore tickets, IConfiguration configuration)
    {
        _tickets = tickets;
        _configuration = configuration;
    }

    [HttpPost("webhook")]
    public ActionResult<WebhookResponse> Webhook(
        [FromBody] PaymentWebhookRequest request,
        [FromHeader(Name = "X-Payment-Webhook-Secret")] string? secret)
    {
        var expected = _configuration["Payments:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(expected) ||
            !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(secret ?? ""),
                System.Text.Encoding.UTF8.GetBytes(expected)))
            return Unauthorized();

        var ticket = _tickets.MarkPaid(request.PaymentReference);
        return ticket is null
            ? NotFound()
            : Ok(new WebhookResponse(ticket.Id, ticket.PaymentStatus, ticket.QrCode));
    }
}

public class PaymentWebhookRequest
{
    [Required]
    public string PaymentReference { get; set; } = "";
}

public record WebhookResponse(string TicketId, string Status, string? QrCode);