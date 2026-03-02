using TusaMap.Api.Models;

namespace TusaMap.Api.Services;

public interface ITicketsStore
{
    IReadOnlyList<Ticket> GetByUserId(long telegramUserId);
    Ticket Add(Ticket t, long telegramUserId);
}

public class TicketsStore : ITicketsStore
{
    private readonly List<(Ticket Ticket, long UserId)> _data = new();
    private int _idCounter = 1;

    public TicketsStore()
    {
        _data.Add((new Ticket { Id = "t1", EventId = "1", EventTitle = "Ночной концерт в столице", EventDate = "2025-03-15", EventPlace = "Клуб «Астана»", QrCode = "TUSA-T1-XXXX", PurchasedAt = "2025-03-01T12:00:00Z" }, 0));
        _idCounter = 2;
    }

    public IReadOnlyList<Ticket> GetByUserId(long telegramUserId)
        => _data.Where(x => x.UserId == telegramUserId).Select(x => x.Ticket).ToList();

    public Ticket Add(Ticket t, long telegramUserId)
    {
        t.Id = "t" + (_idCounter++);
        t.PurchasedAt = DateTime.UtcNow.ToString("O");
        _data.Add((t, telegramUserId));
        return t;
    }
}
