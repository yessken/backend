namespace TusaMap.Api.Models;

public class EventItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Date { get; set; } = "";
    public string Time { get; set; } = "";
    public string Place { get; set; } = "";
    public string Address { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string Category { get; set; } = "";
    public decimal? Price { get; set; }
    public string ImageUrl { get; set; } = "";
    public string OrganizerName { get; set; } = "";
}
