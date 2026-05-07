namespace Tourbooking.ViewModels;

public class AdminUsersViewModel
{
    public int TotalUsers { get; set; }
    public List<AdminUserRow> Users { get; set; } = new();
}

public record AdminUserRow(
    string Initials,
    string Name,
    string Email,
    string Role,
    string JoinedDate,
    string Status);
