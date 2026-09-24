using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TusaMap.Api.Services;

namespace TusaMap.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _clients;
    private readonly ITicketsStore _tickets;
    private readonly ITelegramBotService _telegramBot;

    public TelegramWebhookController(IConfiguration configuration, IHttpClientFactory clients, ITicketsStore tickets, ITelegramBotService telegramBot)
    {
        _configuration = configuration;
        _clients = clients;
        _tickets = tickets;
        _telegramBot = telegramBot;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Receive([FromBody] TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (!IsValidSecret(Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString())) return Unauthorized();
        var token = _configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token)) return StatusCode(503);

        if (update.PreCheckoutQuery is not null)
        {
            await _clients.CreateClient().PostAsJsonAsync(
                $"https://api.telegram.org/bot{token}/answerPreCheckoutQuery",
                new { pre_checkout_query_id = update.PreCheckoutQuery.Id, ok = true }, cancellationToken);
        }

        var payment = update.Message?.SuccessfulPayment;
        if (payment is not null)
        {
            var existing = _tickets.GetByPaymentReference(payment.InvoicePayload);
            if (existing is not null && existing.PaymentStatus != "paid")
            {
                var ticket = _tickets.MarkPaid(payment.InvoicePayload);
                if (ticket is not null) await _telegramBot.SendTicketAsync(ticket, cancellationToken);
            }
        }

        return Ok();
    }

    private bool IsValidSecret(string actual)
    {
        var expected = _configuration["Telegram:WebhookSecret"];
        return !string.IsNullOrWhiteSpace(expected) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected));
    }
}

public sealed class TelegramUpdate
{
    [JsonPropertyName("message")] public TelegramMessage? Message { get; set; }
    [JsonPropertyName("pre_checkout_query")] public TelegramPreCheckoutQuery? PreCheckoutQuery { get; set; }
}

public sealed class TelegramMessage
{
    [JsonPropertyName("successful_payment")] public TelegramSuccessfulPayment? SuccessfulPayment { get; set; }
}

public sealed class TelegramSuccessfulPayment
{
    [JsonPropertyName("invoice_payload")] public string InvoicePayload { get; set; } = "";
}

public sealed class TelegramPreCheckoutQuery
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
}