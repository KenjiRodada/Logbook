namespace Logbook.Models;

public sealed class DashboardViewModel
{
    public long Households { get; set; }
    public long Members { get; set; }
    public long Villages { get; set; }
    public long Diseases { get; set; }
    public long WelfareMembers { get; set; }
    public bool IsSampleData { get; set; }
    public string? DbMessage { get; set; }
    public List<Announcement> Announcements { get; set; } = new();
}

public sealed class Announcement
{
    public string Title { get; set; } = "";
    public string Date { get; set; } = "";
    public string Color { get; set; } = "blue";
}
