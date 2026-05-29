namespace Tourbooking.ViewModels;

public record PublicTourReviewRow(
    string ReviewerName,
    int Rating,
    string Title,
    string Content,
    DateTime CreatedAt);