using TusaMap.Api.Models;
using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;

namespace TusaMap.Api.Services;

public interface ITicketsStore
{
    IReadOnlyList<Ticket> GetByUserId(long telegramUserId);
    Ticket Add(Ticket t, long telegramUserId);
    Ticket? GetByPaymentReference(string reference);
    Ticket? MarkPaid(string reference);
}

public class TicketsStore : ITicketsStore
{
    private readonly TusaMapDbContext _db;

    public TicketsStore(TusaMapDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Ticket> GetByUserId(long telegramUserId)
        => _db.Tickets.AsNoTracking().Where(x => x.TelegramUserId == telegramUserId).ToList();

    public Ticket Add(Ticket t, long telegramUserId)
    {
        t.Id = Guid.NewGuid().ToString("N");
        t.TelegramUserId = telegramUserId;
        t.PurchasedAt = DateTime.UtcNow.ToString("O");
        _db.Tickets.Add(t);
        _db.SaveChanges();
        return t;
    }

    public Ticket? GetByPaymentReference(string reference) => _db.Tickets.FirstOrDefault(x => x.PaymentReference == reference);

    public Ticket? MarkPaid(string reference)
    {
        var ticket = GetByPaymentReference(reference);
        if (ticket is null) return null;
        ticket.PaymentStatus = "paid";
        ticket.QrCode ??= "TUSA-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        _db.SaveChanges();
        return ticket;
    }
}
