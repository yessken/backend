using System.Net.Http.Json;
using TusaMap.Api.Models;

namespace TusaMap.Api.Services;

public interface ITelegramBotService
{
    Task SendTicketAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task NotifyAdminsOfTicketSaleAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task NotifyAdminsOfSubscriptionAsync(long telegramUserId, int stars, CancellationToken cancellationToken = default);
    Task<bool> SendPrivateVenueAddressAsync(long telegramUserId, string eventTitle, string address, string date, string time, CancellationToken cancellationToken = default);
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

    public Task NotifyAdminsOfTicketSaleAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        var message = $"Новая оплата TUSA\nСобытие: {ticket.EventTitle}\nБилеты: {ticket.Quantity}\nСумма: {ticket.TotalAmount:N0} ₸ / {ticket.TelegramStarsAmount ?? 0} ⭐\nTicket ID: {ticket.Id}";
        return NotifyAdminsAsync(message, cancellationToken);
    }

    public Task NotifyAdminsOfSubscriptionAsync(long telegramUserId, int stars, CancellationToken cancellationToken = default)
    {
        var message = $"Новая оплата Organizer Pro\nTelegram ID: {telegramUserId}\nСумма: {stars} ⭐\nСрок: 30 дней";
        return NotifyAdminsAsync(message, cancellationToken);
    }

    private async Task NotifyAdminsAsync(string text, CancellationToken cancellationToken)
    {
        var token = _configuration["Telegram:BotToken"];
        var adminIds = _configuration.GetSection("Telegram:AdminUserIds").Get<long[]>() ?? [];
        if (string.IsNullOrWhiteSpace(token) || adminIds.Length == 0) return;

        foreach (var adminId in adminIds.Distinct())
        {
            try
            {
                var response = await _clients.CreateClient().PostAsJsonAsync(
                    $"https://api.telegram.org/bot{token}/sendMessage",
                    new { chat_id = adminId, text }, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("Admin payment notification failed for admin {AdminId}: {StatusCode}", adminId, response.StatusCode);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Admin payment notification transport failed for admin {AdminId}", adminId);
            }
        }
    }

    public async Task<bool> SendPrivateVenueAddressAsync(long telegramUserId, string eventTitle, string address, string date, string time, CancellationToken cancellationToken = default)
    {
        var token = _configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token) || telegramUserId == 0) return false;
        var text = $"Адрес события TUSA 2026\n\n{eventTitle}\n{date} · {time}\n\nМесто проведения: {address}\n\nЭто сообщение отправлено только пользователям с оплаченным билетом. Покажите билет/QR на входе.";
        var response = await _clients.CreateClient().PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new { chat_id = telegramUserId, text }, cancellationToken);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Private venue delivery failed for Telegram user {TelegramUserId}: {StatusCode}", telegramUserId, response.StatusCode);
        return response.IsSuccessStatusCode;
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
