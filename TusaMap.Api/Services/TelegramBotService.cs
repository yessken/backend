using System.Net.Http.Json;
using TusaMap.Api.Models;

namespace TusaMap.Api.Services;

public interface ITelegramBotService
{
    Task SendTicketAsync(Ticket ticket, CancellationToken cancellationToken = default);
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
}
