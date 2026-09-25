using System.Net.Http.Json;
using TusaMap.Api.Models;

namespace TusaMap.Api.Services;

public interface ITelegramBotService
{
    Task SendTicketAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task ConfigureWebAppAsync(CancellationToken cancellationToken = default);
    Task ConfigureWebhookAsync(CancellationToken cancellationToken = default);
}

public class TelegramBotService : ITelegramBotService
{
    private readonly IHttpClientFactory _clients;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(IHttpClientFactory clients, IConfiguration configuration, ILogger<TelegramBotService> logger)
    {
        _clients = clients;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendTicketAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        var token = _configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token) || ticket.TelegramUserId == 0) return;

        var text = $"Билет TUSA подтверждён\n\n{ticket.EventTitle}\n{ticket.EventDate} · {ticket.EventPlace}\nКатегория: {ticket.TicketCategoryName}\nКоличество: {ticket.Quantity}\nКод билета: {ticket.QrCode}";
        var response = await _clients.CreateClient().PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new { chat_id = ticket.TelegramUserId, text },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Telegram ticket delivery failed for ticket {TicketId}: {StatusCode}", ticket.Id, response.StatusCode);
    }

    public async Task ConfigureWebAppAsync(CancellationToken cancellationToken = default)
    {
        var token = _configuration["Telegram:BotToken"];
        var webAppUrl = _configuration["Telegram:WebAppUrl"];
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(webAppUrl)) return;

        var response = await _clients.CreateClient().PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/setChatMenuButton",
            new
            {
                menu_button = new
                {
                    type = "web_app",
                    text = "Открыть TUSA",
                    web_app = new { url = webAppUrl },
                },
            }, cancellationToken);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Telegram Web App menu configuration failed: {StatusCode}", response.StatusCode);
    }

    public async Task ConfigureWebhookAsync(CancellationToken cancellationToken = default)
    {
        var token = _configuration["Telegram:BotToken"];
        var webhookUrl = _configuration["Telegram:WebhookUrl"];
        var secret = _configuration["Telegram:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(webhookUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning("Telegram webhook is not configured: BotToken, WebhookUrl and WebhookSecret are required");
            return;
        }

        var response = await _clients.CreateClient().PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/setWebhook",
            new { url = webhookUrl, secret_token = secret, allowed_updates = new[] { "message", "pre_checkout_query" } },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Telegram webhook configuration failed: {StatusCode}", response.StatusCode);
    }
}
