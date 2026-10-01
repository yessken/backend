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
    EventItem? Reject(string id);
}

public class EventsStore : IEventsStore
{
    private readonly TusaMapDbContext _db;

    public EventsStore(TusaMapDbContext db)
    {
        _db = db;
        ReplaceRandomSeedImages();
    }

    private void ReplaceRandomSeedImages()
    {
        var stableImages = new Dictionary<string, string>
        {
            ["1"] = "https://images.unsplash.com/photo-1501386761578-eac5c94b800a?auto=format&fit=crop&w=1200&q=85",
            ["2"] = "https://images.unsplash.com/photo-1511192336575-5a79af67a629?auto=format&fit=crop&w=1200&q=85",
            ["3"] = "https://images.unsplash.com/photo-1470229722913-7c0e2dbbafd3?auto=format&fit=crop&w=1200&q=85",
            ["4"] = "https://images.unsplash.com/photo-1585699324551-f6c309eedeca?auto=format&fit=crop&w=1200&q=85",
        };
        var seedIds = stableImages.Keys.ToArray();
        var events = _db.Events.Where(eventItem => seedIds.Contains(eventItem.Id) && eventItem.ImageUrl.Contains("picsum.photos")).ToList();
        foreach (var eventItem in events) eventItem.ImageUrl = stableImages[eventItem.Id];
        if (events.Count > 0) _db.SaveChanges();
    }

    public IReadOnlyList<EventItem> GetAll() => _db.Events.AsNoTracking().Include(e => e.TicketCategories).Where(e => e.Status == "approved").ToList();
    public EventItem? GetById(string id) => _db.Events.AsNoTracking().Include(e => e.TicketCategories).FirstOrDefault(e => e.Id == id && e.Status == "approved");
    public IReadOnlyList<EventItem> GetPending() => _db.Events.AsNoTracking()
        .Where(e => e.Status == "pending")
        .OrderBy(e => e.CreatedAt)
        .ToList();
    public EventItem? Approve(string id)
    {
        var e = _db.Events.Find(id);
        if (e is null || e.Status != "pending") return null;
        e.Status = "approved";
        _db.SaveChanges();
        return e;
    }

    public EventItem? Reject(string id)
    {
        var e = _db.Events.Find(id);
        if (e is null || e.Status != "pending") return null;
        e.Status = "rejected";
        _db.SaveChanges();
        return e;
    }

    public EventItem Add(EventItem e)
    {
        e.Id = Guid.NewGuid().ToString("N");
        e.Status = "pending";
        _db.Events.Add(e);
        _db.SaveChanges();
        return e;
    }
}
