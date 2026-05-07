namespace Tourbooking.ViewModels;

public class AdminBookingsViewModel
{
    public int TotalBookings { get; set; }
    public string Revenue { get; set; } = string.Empty;
    public int ActiveTours { get; set; }
    public string AverageGroupSize { get; set; } = string.Empty;
    public List<AdminBookingRow> RecentBookings { get; set; } = new();
}

public record AdminBookingRow(
    string CustomerInitials,
    string CustomerName,
    string CustomerEmail,
    string TourName,
    string Date,
    int Quantity,
    string TotalPrice,
    string Status);
