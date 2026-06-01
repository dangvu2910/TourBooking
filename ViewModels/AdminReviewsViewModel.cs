namespace Tourbooking.ViewModels;

public class AdminReviewsViewModel
{
    public int TotalReviews { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public List<AdminReviewRow> Reviews { get; set; } = new();
}

public record AdminReviewRow(
    int ReviewId,
    string ReviewerName,
    string ReviewerEmail,
    string TourName,
    int Rating,
    string Title,
    string Content,
    string CreatedDate);
