using System.ComponentModel.DataAnnotations;

public class Tour
{
    [Key]
    public int TourId { get; set; }

    public string Name { get; set; }
    public string Location { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public string? ImageUrl { get; set; }

    public int CategoryId { get; set; }
}