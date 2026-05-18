using System;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tourbooking.Models;
using Tourbooking.ViewModels;

namespace Tourbooking.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private static readonly CultureInfo VnCulture = CultureInfo.GetCultureInfo("vi-VN");
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
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
                    Metric = t.Price.ToString("C0", VnCulture)
                })
                .ToList(),
            RecentActivities = new List<AdminActivity>
            {
                new("Đặt chỗ mới được xác nhận", "Đang chờ mô đun đặt chỗ"),
                new("Sự cố thanh toán", "Chưa kết nối cổng thanh toán"),
                new("Người dùng mới đăng ký", "Đã kích hoạt đăng ký" )
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
                Price = t.Price.ToString("C0", VnCulture),
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

    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync();

        var rows = new List<AdminUserRow>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "User";
            var status = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow
                ? "Paused"
                : "Active";
            var displayName = string.IsNullOrWhiteSpace(user.UserName) ? user.Email ?? "" : user.UserName;
            rows.Add(new AdminUserRow(
                GetInitials(displayName),
                displayName,
                user.Email ?? string.Empty,
                role,
                "-",
                status));
        }

        var model = new AdminUsersViewModel
        {
            TotalUsers = users.Count,
            Users = rows
        };

        return View(model);
    }

    private static string GetInitials(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "?";
        }

        var parts = value
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 1)
        {
            return parts[0].Length >= 2
                ? string.Concat(parts[0][0], parts[0][1]).ToUpperInvariant()
                : parts[0][0].ToString().ToUpperInvariant();
        }

        var first = parts[0][0];
        var last = parts[^1][0];
        return string.Concat(first, last).ToUpperInvariant();
    }
}
