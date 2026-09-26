using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;
using TusaMap.Api.Models;
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
    private readonly IEventsStore _events;
    private readonly ITicketPricingService _pricing;
    private readonly TusaMapDbContext _db;
    private readonly IUserStore _users;

    public TelegramWebhookController(IConfiguration configuration, IHttpClientFactory clients, ITicketsStore tickets, ITelegramBotService telegramBot, IEventsStore events, ITicketPricingService pricing, TusaMapDbContext db, IUserStore users)
    {
        _configuration = configuration;
        _clients = clients;
        _tickets = tickets;
        _telegramBot = telegramBot;
        _events = events;
        _pricing = pricing;
        _db = db;
        _users = users;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Receive([FromBody] TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (!IsValidSecret(Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString())) return Unauthorized();
        var token = _configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token)) return StatusCode(503);

        var message = update.Message;
        var text = message?.Text?.Trim() ?? "";
        if (message?.From is not null && text.StartsWith("/start event_", StringComparison.OrdinalIgnoreCase))
            await StartEventPaymentAsync(token, message, text[13..].Trim(), cancellationToken);
        else if (message?.From is not null && text.Equals("/start subscribe_pro", StringComparison.OrdinalIgnoreCase))
            await StartSubscriptionPaymentAsync(token, message, cancellationToken);

        if (update.PreCheckoutQuery is not null)
        {
            var query = update.PreCheckoutQuery;
            var valid = query.From is not null && IsValidInvoice(query.InvoicePayload, query.Currency, query.TotalAmount, query.From.Id);
            var answer = valid
                ? new { pre_checkout_query_id = query.Id, ok = true, error_message = (string?)null }
                : new { pre_checkout_query_id = query.Id, ok = false, error_message = (string?)"Заказ не найден, истёк или сумма изменилась. Создайте заказ заново." };
            await _clients.CreateClient().PostAsJsonAsync(
                $"https://api.telegram.org/bot{token}/answerPreCheckoutQuery",
                answer, cancellationToken);
        }

        var payment = update.Message?.SuccessfulPayment;
        if (payment is not null)
        {
            var payerId = update.Message!.From?.Id ?? 0;
            if (payment.InvoicePayload.StartsWith("subscription:", StringComparison.OrdinalIgnoreCase))
            {
                if (IsValidInvoice(payment.InvoicePayload, payment.Currency, payment.TotalAmount, payerId))
                    await ActivateSubscriptionAsync(token, update.Message.Chat.Id, payment.InvoicePayload, payment.TelegramPaymentChargeId, cancellationToken);
            }
            else
            {
                var existing = _tickets.GetByPaymentReference(payment.InvoicePayload);
                if (existing?.PaymentStatus == "paid" && existing.TelegramPaymentChargeId == payment.TelegramPaymentChargeId)
                    return Ok();
                if (existing is not null && IsValidInvoice(payment.InvoicePayload, payment.Currency, payment.TotalAmount, payerId))
                {
                    var ticket = _tickets.MarkPaid(payment.InvoicePayload, payment.TelegramPaymentChargeId);
                    if (ticket is not null) await _telegramBot.SendTicketAsync(ticket, cancellationToken);
                }
            }
        }

        return Ok();
    }

    private async Task StartEventPaymentAsync(string token, TelegramMessage message, string eventId, CancellationToken cancellationToken)
    {
        var userId = message.From!.Id;
        _users.Upsert(message.From);
        var ev = _events.GetById(eventId);
        var category = ev?.TicketCategories.FirstOrDefault(x => x.IsActive && x.Capacity > x.Sold);
        var starsPerKzt = _configuration.GetValue<decimal>("Payments:TelegramStarsPerKzt");
        var testMode = _configuration.GetValue<bool>("Payments:TelegramTestMode");
        if (ev is null || category is null)
        {
            await SendMessageAsync(token, message.Chat.Id, "Событие не найдено или билеты закончились.", cancellationToken);
            return;
        }
        if (starsPerKzt <= 0)
        {
            await SendMessageAsync(token, message.Chat.Id, "Оплата Telegram Stars ещё не настроена.", cancellationToken);
            return;
        }

        var draft = _pricing.Quote(ev.Id, category.Id, 1, null, userId, out var error);
        if (draft is null)
        {
            await SendMessageAsync(token, message.Chat.Id, error ?? "Не удалось рассчитать стоимость.", cancellationToken);
            return;
        }

        var stars = testMode ? 1 : Math.Max(1, (int)Math.Round(draft.TotalAmount * starsPerKzt, MidpointRounding.AwayFromZero));
        using var transaction = _db.Database.BeginTransaction();
        var reserved = _db.Database.ExecuteSqlInterpolated($"UPDATE TicketCategories SET Sold = Sold + {draft.Quantity} WHERE Id = {draft.Category.Id} AND IsActive = 1 AND Capacity - Sold >= {draft.Quantity}");
        if (reserved != 1)
        {
            await SendMessageAsync(token, message.Chat.Id, "Этот билет только что закончился.", cancellationToken);
            return;
        }
        var ticket = _tickets.Add(new Ticket
        {
            EventId = ev.Id,
            EventTitle = ev.Title,
            EventDate = ev.Date,
            EventPlace = ev.Place,
            PaymentMethod = "telegram",
            PaymentStatus = "pending",
            PaymentReference = Guid.NewGuid().ToString("N"),
            TelegramStarsAmount = stars,
            TicketCategoryId = draft.Category.Id,
            TicketCategoryName = draft.Category.Name,
            Quantity = draft.Quantity,
            BaseAmount = draft.BaseAmount,
            DiscountAmount = draft.DiscountAmount,
            CommissionAmount = draft.CommissionAmount,
            TotalAmount = draft.TotalAmount,
        }, userId);
        var response = await _clients.CreateClient().PostAsJsonAsync($"https://api.telegram.org/bot{token}/sendInvoice", new
        {
            chat_id = message.Chat.Id,
            title = ev.Title,
            description = $"{ev.Date} · {ev.Place} · {draft.Category.Name}",
            payload = ticket.PaymentReference,
            provider_token = "",
            currency = "XTR",
            prices = new[] { new { label = draft.Category.Name, amount = stars } },
        }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            transaction.Rollback();
            await SendMessageAsync(token, message.Chat.Id, "Не удалось открыть оплату Telegram. Попробуйте позже.", cancellationToken);
            return;
        }
        transaction.Commit();
    }

    private async Task StartSubscriptionPaymentAsync(string token, TelegramMessage message, CancellationToken cancellationToken)
    {
        var stars = _configuration.GetValue<int>("Payments:TelegramSubscriptionStars");
        if (stars <= 0)
        {
            await SendMessageAsync(token, message.Chat.Id, "Подписка организатора пока не настроена.", cancellationToken);
            return;
        }

        var payload = $"subscription:{message.From!.Id}:pro:{stars}";
        var response = await _clients.CreateClient().PostAsJsonAsync($"https://api.telegram.org/bot{token}/sendInvoice", new
        {
            chat_id = message.Chat.Id,
            title = "TUSA Organizer Pro",
            description = "Доступ к заказам, аналитике и инструментам организатора на 30 дней.",
            payload,
            provider_token = "",
            currency = "XTR",
            prices = new[] { new { label = "Organizer Pro · 30 дней", amount = stars } },
        }, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await SendMessageAsync(token, message.Chat.Id, "Не удалось открыть оплату подписки. Попробуйте позже.", cancellationToken);
    }

    private async Task ActivateSubscriptionAsync(string token, long chatId, string payload, string chargeId, CancellationToken cancellationToken)
    {
        var parts = payload.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !long.TryParse(parts[1], out var userId) || parts[2] != "pro") return;
        var subscription = _db.OrganizerSubscriptions.Find(userId) ?? new OrganizerSubscription { TelegramUserId = userId };
        if (subscription.LastTelegramChargeId == chargeId) return;
        subscription.Plan = "pro";
        subscription.Status = "active";
        var now = DateTime.UtcNow;
        subscription.ExpiresAt = (subscription.ExpiresAt is { } expiry && expiry > now ? expiry : now).AddDays(30);
        subscription.UpdatedAt = DateTime.UtcNow;
        subscription.LastTelegramChargeId = chargeId;
        if (_db.Entry(subscription).State == EntityState.Detached) _db.OrganizerSubscriptions.Add(subscription);
        _db.SaveChanges();
        await SendMessageAsync(token, chatId, "Подписка Organizer Pro активирована на 30 дней.", cancellationToken);
    }

    private bool IsValidInvoice(string payload, string currency, int totalAmount, long payerId)
    {
        if (!string.Equals(currency, "XTR", StringComparison.Ordinal)) return false;
        if (payload.StartsWith("subscription:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = payload.Split(':', StringSplitOptions.TrimEntries);
            return parts.Length == 4 && long.TryParse(parts[1], out var subscriptionUserId) && subscriptionUserId == payerId &&
                   parts[2] == "pro" && int.TryParse(parts[3], out var invoiceStars) && invoiceStars > 0 && totalAmount == invoiceStars;
        }

        var ticket = _tickets.GetByPaymentReference(payload);
        return ticket is not null && ticket.PaymentStatus == "pending" && ticket.CancelledAt is null &&
               ticket.PaymentMethod == "telegram" && ticket.TelegramUserId == payerId &&
               ticket.TelegramStarsAmount == totalAmount;
    }

    private async Task SendMessageAsync(string token, long chatId, string text, CancellationToken cancellationToken)
    {
        await _clients.CreateClient().PostAsJsonAsync($"https://api.telegram.org/bot{token}/sendMessage", new { chat_id = chatId, text }, cancellationToken);
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
    [JsonPropertyName("from")] public TelegramUser? From { get; set; }
    [JsonPropertyName("chat")] public TelegramChat Chat { get; set; } = new();
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("successful_payment")] public TelegramSuccessfulPayment? SuccessfulPayment { get; set; }
}

public sealed class TelegramChat
{
    [JsonPropertyName("id")] public long Id { get; set; }
}

public sealed class TelegramSuccessfulPayment
{
    [JsonPropertyName("invoice_payload")] public string InvoicePayload { get; set; } = "";
    [JsonPropertyName("currency")] public string Currency { get; set; } = "";
    [JsonPropertyName("total_amount")] public int TotalAmount { get; set; }
    [JsonPropertyName("telegram_payment_charge_id")] public string TelegramPaymentChargeId { get; set; } = "";
}

public sealed class TelegramPreCheckoutQuery
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("invoice_payload")] public string InvoicePayload { get; set; } = "";
    [JsonPropertyName("currency")] public string Currency { get; set; } = "";
    [JsonPropertyName("total_amount")] public int TotalAmount { get; set; }
    [JsonPropertyName("from")] public TelegramUser? From { get; set; }
}