using TusaMap.Api.Models;

namespace TusaMap.Api.Services;

public interface IEventsStore
{
    IReadOnlyList<EventItem> GetAll();
    EventItem? GetById(string id);
    EventItem Add(EventItem e);
}

public class EventsStore : IEventsStore
{
    private readonly List<EventItem> _events = new();
    private int _idCounter = 1;

    public EventsStore()
    {
        Seed();
    }

    private void Seed()
    {
        foreach (var e in new[]
        {
            new EventItem { Id = "1", Title = "Ночной концерт в столице", Description = "Живая музыка, бар, танцы до утра. Возраст 18+.", Date = "2025-03-15", Time = "22:00", Place = "Клуб «Астана»", Address = "ул. Кенесары, 40", Lat = 51.1605, Lng = 71.4704, Category = "концерт", Price = 3500, ImageUrl = "https://picsum.photos/400/200?random=1", OrganizerName = "Астана Events" },
            new EventItem { Id = "2", Title = "Джаз под звёздами", Description = "Открытая площадка, джаз-бэнд, коктейли.", Date = "2025-03-20", Time = "20:00", Place = "Парк Первого Президента", Address = "пр. Республики", Lat = 51.1252, Lng = 71.4305, Category = "концерт", Price = null, ImageUrl = "https://picsum.photos/400/200?random=2", OrganizerName = "Jazz Astana" },
            new EventItem { Id = "3", Title = "Техно-вечеринка", Description = "DJ-сет, два этажа, лаунж и танцпол.", Date = "2025-03-22", Time = "23:00", Place = "Лофт «Тусовка»", Address = "ул. Сыганак, 12", Lat = 51.1694, Lng = 71.4494, Category = "вечеринка", Price = 5000, ImageUrl = "https://picsum.photos/400/200?random=3", OrganizerName = "Loft Club" },
            new EventItem { Id = "4", Title = "Stand-up вечер", Description = "Стендап комики из Астаны и Алматы.", Date = "2025-03-18", Time = "19:00", Place = "Театр «Жастар»", Address = "ул. Есенберлина, 10", Lat = 51.1489, Lng = 71.4369, Category = "развлечения", Price = 2500, ImageUrl = "https://picsum.photos/400/200?random=4", OrganizerName = "Comedy Astana" },
        })
            _events.Add(e);
        _idCounter = 5;
    }

    public IReadOnlyList<EventItem> GetAll() => _events.AsReadOnly();
    public EventItem? GetById(string id) => _events.Find(e => e.Id == id);

    public EventItem Add(EventItem e)
    {
        e.Id = (++_idCounter).ToString();
        _events.Add(e);
        return e;
    }
}
