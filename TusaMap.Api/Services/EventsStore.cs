using TusaMap.Api.Models;
using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;

namespace TusaMap.Api.Services;

public interface IEventsStore
{
    IReadOnlyList<EventItem> GetAll();
    EventItem? GetById(string id);
    EventItem Add(EventItem e);
    IReadOnlyList<EventItem> GetPending();
    EventItem? Approve(string id);
}

public class EventsStore : IEventsStore
{
    private readonly TusaMapDbContext _db;

    public EventsStore(TusaMapDbContext db)
    {
        _db = db;
        if (!_db.Events.Any()) Seed();
    }

    private void Seed()
    {
        foreach (var e in new[]
        {
            new EventItem { Id = "1", Title = "Ночной концерт в столице", Description = "Живая музыка, бар, танцы до утра. Возраст 18+.", Date = "2026-10-15", Time = "22:00", Place = "Клуб «Астана»", Address = "ул. Кенесары, 40", Lat = 51.1605, Lng = 71.4704, Category = "концерт", Price = 3500, ImageUrl = "https://picsum.photos/400/200?random=1", OrganizerName = "Астана Events", Status = "approved" },
            new EventItem { Id = "2", Title = "Джаз под звёздами", Description = "Открытая площадка, джаз-бэнд, коктейли.", Date = "2026-10-20", Time = "20:00", Place = "Парк Первого Президента", Address = "пр. Республики", Lat = 51.1252, Lng = 71.4305, Category = "концерт", Price = null, ImageUrl = "https://picsum.photos/400/200?random=2", OrganizerName = "Jazz Astana", Status = "approved" },
            new EventItem { Id = "3", Title = "Техно-вечеринка", Description = "DJ-сет, два этажа, лаунж и танцпол.", Date = "2026-10-22", Time = "23:00", Place = "Лофт «Тусовка»", Address = "ул. Сыганак, 12", Lat = 51.1694, Lng = 71.4494, Category = "вечеринка", Price = 5000, ImageUrl = "https://picsum.photos/400/200?random=3", OrganizerName = "Loft Club", Status = "approved" },
            new EventItem { Id = "4", Title = "Stand-up вечер", Description = "Стендап комики из Астаны и Алматы.", Date = "2026-10-18", Time = "19:00", Place = "Театр «Жастар»", Address = "ул. Есенберлина, 10", Lat = 51.1489, Lng = 71.4369, Category = "развлечения", Price = 2500, ImageUrl = "https://picsum.photos/400/200?random=4", OrganizerName = "Comedy Astana", Status = "approved" },
        })
            _db.Events.Add(e);
        _db.SaveChanges();
    }

    public IReadOnlyList<EventItem> GetAll() => _db.Events.AsNoTracking().Where(e => e.Status == "approved").ToList();
    public EventItem? GetById(string id) => _db.Events.AsNoTracking().FirstOrDefault(e => e.Id == id && e.Status == "approved");
    public IReadOnlyList<EventItem> GetPending() => _db.Events.AsNoTracking().Where(e => e.Status == "pending").ToList();
    public EventItem? Approve(string id) { var e = _db.Events.Find(id); if (e is null) return null; e.Status = "approved"; _db.SaveChanges(); return e; }

    public EventItem Add(EventItem e)
    {
        e.Id = Guid.NewGuid().ToString("N");
        e.Status = "pending";
        _db.Events.Add(e);
        _db.SaveChanges();
        return e;
    }
}
