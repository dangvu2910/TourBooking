namespace Tourbooking.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalTours { get; set; }
    public int TotalBookings { get; set; }
    public int TotalUsers { get; set; }
    public int ConfirmedBookings { get; set; }
    public int PendingBookings { get; set; }
    public int NewUsersLast30Days { get; set; }
    public string TotalRevenue { get; set; } = string.Empty;
    public List<AdminTourCard> TopTours { get; set; } = new();
    public List<AdminActivity> RecentActivities { get; set; } = new();
    public List<BookingTrendItem> BookingTrends { get; set; } = new();
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

public record BookingTrendItem(string Label, int Count);

public record AdminNotificationItem(string Title, string Description, DateTime CreatedAt);
