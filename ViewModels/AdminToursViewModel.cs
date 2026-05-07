namespace Tourbooking.ViewModels;

public class AdminToursViewModel
{
    public int TotalTours { get; set; }
    public int Destinations { get; set; }
    public int AverageBookingRate { get; set; }
    public List<AdminTourRow> Tours { get; set; } = new();
}

public class AdminTourRow
{
    public int TourId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
