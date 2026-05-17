using System.ComponentModel.DataAnnotations;

public class Tour
{
    [Key]
    public int TourId { get; set; }

    [Display(Name = "Tên tour")]
    public string Name { get; set; }

    [Display(Name = "Địa điểm")]
    public string Location { get; set; }

    [Display(Name = "Giá")]
    public decimal Price { get; set; }

    [Display(Name = "Mô tả")]
    public string Description { get; set; }

    [Display(Name = "Hình ảnh")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Danh mục")]
    public int CategoryId { get; set; }
}