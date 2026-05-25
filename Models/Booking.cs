using System.ComponentModel.DataAnnotations;

namespace Tourbooking.Models;

public class Booking
{
    [Key]
    public int BookingId { get; set; }

    [Required]
    public int TourId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    public DateTime TravelDate { get; set; }

    [Range(1, 20)]
    public int GuestCount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TotalPrice { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tour? Tour { get; set; }
    public ApplicationUser? User { get; set; }
}
