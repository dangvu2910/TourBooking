namespace Tourbooking.ViewModels;

public class AccountBookingsViewModel
{
    public int TotalBookings { get; set; }
    public int UpcomingBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public List<AccountBookingRow> Bookings { get; set; } = new();
}

public record AccountBookingRow(
    int BookingId,
    string TourName,
    string TourLocation,
    string? ImageUrl,
    DateTime TravelDate,
    int GuestCount,
    decimal TotalPrice,
    string Status);
