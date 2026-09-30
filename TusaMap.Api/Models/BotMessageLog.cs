namespace TusaMap.Api.Models;

public sealed class BotMessageLog
{
    public long Id { get; set; }
    public long UpdateId { get; set; }
    public long MessageId { get; set; }
    public long ChatId { get; set; }
    public long TelegramUserId { get; set; }
    public string SenderName { get; set; } = "";
    public string? Username { get; set; }
    public string MessageType { get; set; } = "text";
    public string Content { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
}