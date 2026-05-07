using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tourbooking.ViewModels;

namespace Tourbooking.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Dashboard()
    {
        var tours = await _context.Tours.AsNoTracking().ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TotalTours = tours.Count,
            TotalBookings = 0,
            TotalUsers = await _userManager.Users.CountAsync(),
            TopTours = tours
                .OrderByDescending(t => t.Price)
                .Take(3)
                .Select(t => new AdminTourCard
                {
                    TourId = t.TourId,
                    Name = t.Name,
                    ImageUrl = t.ImageUrl ?? string.Empty,
                    Location = t.Location,
                    Metric = "$" + t.Price.ToString("0")
                })
                .ToList(),
            RecentActivities = new List<AdminActivity>
            {
                new("New booking confirmed", "Awaiting booking module"),
                new("Payment issue", "No payment gateway connected"),
                new("New user registration", "Identity registration enabled")
            }
        };

        return View(model);
    }

    public async Task<IActionResult> Tours()
    {
        var tours = await _context.Tours.AsNoTracking().ToListAsync();
        var destinations = tours
            .Select(t => t.Location)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var model = new AdminToursViewModel
        {
            TotalTours = tours.Count,
            Destinations = destinations,
            AverageBookingRate = 88,
            Tours = tours.Select(t => new AdminTourRow
            {
                TourId = t.TourId,
                Name = t.Name,
                Location = t.Location,
                ImageUrl = t.ImageUrl ?? string.Empty,
                Price = "$" + t.Price.ToString("0"),
                Status = "Published"
            }).ToList()
        };

        return View(model);
    }

    public IActionResult Bookings()
    {
        var model = new AdminBookingsViewModel
        {
            TotalBookings = 1284,
            Revenue = "$42.8k",
            ActiveTours = 56,
            AverageGroupSize = "3.2",
            RecentBookings = new List<AdminBookingRow>
            {
                new("SJ", "Sarah Jenkins", "sarah@voyager.com", "Bali Zen Sanctuary Retreat", "Oct 24, 2024", 2, "$2,450.00", "Confirmed"),
                new("MT", "Marcus Thompson", "marcus@voyager.com", "Kyoto Traditional Trails", "Nov 12, 2024", 4, "$5,120.00", "Pending"),
                new("ER", "Elena Rodriguez", "elena@voyager.com", "Amalfi Coastal Dream", "Dec 05, 2024", 1, "$1,890.00", "Processing"),
                new("DW", "David Wilson", "david@voyager.com", "Imperial Heritage Tour", "Oct 28, 2024", 2, "$3,100.00", "Cancelled")
            }
        };

        return View(model);
    }

    public IActionResult Users()
    {
        var model = new AdminUsersViewModel
        {
            TotalUsers = 128,
            Users = new List<AdminUserRow>
            {
                new("LN", "Nguyen Linh Chi", "linhchi.nguyen@voyager.vn", "Admin", "12/03/2024", "Active"),
                new("HL", "Tran Hoang Long", "hoanglong@gmail.com", "User", "05/02/2024", "Active"),
                new("TH", "Le Thu Ha", "ha.le88@voyager.vn", "User", "15/01/2024", "Paused")
            }
        };

        return View(model);
    }
}
