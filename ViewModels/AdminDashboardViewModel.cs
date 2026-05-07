namespace Tourbooking.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalTours { get; set; }
    public int TotalBookings { get; set; }
    public int TotalUsers { get; set; }
    public List<AdminTourCard> TopTours { get; set; } = new();
    public List<AdminActivity> RecentActivities { get; set; } = new();
}

public class AdminTourCard
{
    public int TourId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
}

public record AdminActivity(string Title, string Description);
