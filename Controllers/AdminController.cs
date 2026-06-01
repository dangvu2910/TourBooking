using System;
using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var notifications = await BuildNotificationsAsync();
        ViewData["Notifications"] = notifications;
        ViewData["NotificationCount"] = notifications.Count;
        await next();
    }

    public async Task<IActionResult> Dashboard()
    {
        var totalTours = await _context.Tours.AsNoTracking().CountAsync();
        var totalBookings = await _context.Bookings.AsNoTracking().CountAsync();
        var totalUsers = await _userManager.Users.AsNoTracking().CountAsync();
        var confirmedBookings = await _context.Bookings.AsNoTracking()
            .CountAsync(b => b.Status == "Confirmed");
        var pendingBookings = await _context.Bookings.AsNoTracking()
            .CountAsync(b => b.Status == "Pending" || b.Status == "PendingConfirmation" || b.Status == "Processing");
        var newUsersLast30Days = await _userManager.Users.AsNoTracking()
            .CountAsync(u => u.CreatedAt >= DateTime.UtcNow.AddDays(-30));
        var totalRevenue = await _context.Bookings.AsNoTracking()
            .Where(b => b.Status == "Confirmed")
            .SumAsync(b => (decimal?)b.TotalPrice) ?? 0m;

        var topTours = await _context.Tours.AsNoTracking()
            .GroupJoin(_context.Bookings.AsNoTracking(),
                tour => tour.TourId,
                booking => booking.TourId,
                (tour, bookings) => new
                {
                    Tour = tour,
                    BookingCount = bookings.Count(),
                    Revenue = bookings.Where(b => b.Status == "Confirmed")
                        .Sum(b => (decimal?)b.TotalPrice) ?? 0m
                })
            .OrderByDescending(x => x.BookingCount)
            .ThenByDescending(x => x.Revenue)
            .Take(3)
            .Select(x => new AdminTourCard
            {
                TourId = x.Tour.TourId,
                Name = x.Tour.Name,
                ImageUrl = NormalizeImageUrl(x.Tour.ImageUrl) ?? string.Empty,
                Location = x.Tour.Location,
                Metric = $"{x.BookingCount} đặt chỗ"
            })
            .ToListAsync();

        var recentBookings = await _context.Bookings.AsNoTracking()
            .Include(b => b.Tour)
            .OrderByDescending(b => b.CreatedAt)
            .Take(3)
            .ToListAsync();

        var recentUsers = await _userManager.Users.AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Take(2)
            .ToListAsync();

        var activityItems = new List<(DateTime Stamp, AdminActivity Activity)>();
        foreach (var booking in recentBookings)
        {
            var tourName = booking.Tour?.Name ?? "Tour";
            activityItems.Add((booking.CreatedAt, new AdminActivity(
                "Đặt chỗ mới",
                $"{booking.FullName} - {tourName}")));
        }

        foreach (var user in recentUsers)
        {
            var displayName = string.IsNullOrWhiteSpace(user.FullName)
                ? (user.UserName ?? user.Email ?? "Người dùng mới")
                : user.FullName;
            activityItems.Add((user.CreatedAt, new AdminActivity(
                "Người dùng mới",
                $"{displayName} vừa đăng ký")));
        }

        var recentActivities = activityItems
            .OrderByDescending(x => x.Stamp)
            .Select(x => x.Activity)
            .Take(5)
            .ToList();

        var now = DateTime.UtcNow;
        var startMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
        var bookingTrendsRaw = await _context.Bookings.AsNoTracking()
            .Where(b => b.CreatedAt >= startMonth)
            .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        var bookingTrends = new List<BookingTrendItem>();
        for (var i = 0; i < 6; i++)
        {
            var point = startMonth.AddMonths(i);
            var matched = bookingTrendsRaw.FirstOrDefault(b => b.Year == point.Year && b.Month == point.Month);
            var label = $"Thg {point.Month}";
            bookingTrends.Add(new BookingTrendItem(label, matched?.Count ?? 0));
        }

        var model = new AdminDashboardViewModel
        {
            TotalTours = totalTours,
            TotalBookings = totalBookings,
            TotalUsers = totalUsers,
            ConfirmedBookings = confirmedBookings,
            PendingBookings = pendingBookings,
            NewUsersLast30Days = newUsersLast30Days,
            TotalRevenue = totalRevenue.ToString("C0", VnCulture),
            TopTours = topTours,
            RecentActivities = recentActivities,
            BookingTrends = bookingTrends
        };

        return View(model);
    }

    private async Task<List<AdminNotificationItem>> BuildNotificationsAsync()
    {
        var items = new List<(DateTime Stamp, AdminNotificationItem Item)>();

        var recentBookings = await _context.Bookings.AsNoTracking()
            .Include(b => b.Tour)
            .OrderByDescending(b => b.CreatedAt)
            .Take(4)
            .ToListAsync();

        foreach (var booking in recentBookings)
        {
            var tourName = booking.Tour?.Name ?? "Tour";
            items.Add((booking.CreatedAt, new AdminNotificationItem(
                "Đặt chỗ mới",
                $"{booking.FullName} - {tourName}",
                booking.CreatedAt)));
        }

        var recentPayments = await _context.Payments.AsNoTracking()
            .Include(p => p.Booking)
            .OrderByDescending(p => p.CreatedAt)
            .Take(3)
            .ToListAsync();

        foreach (var payment in recentPayments)
        {
            var customer = payment.Booking?.FullName ?? "Khách";
            var amount = payment.Amount.ToString("C0", VnCulture);
            items.Add((payment.CreatedAt, new AdminNotificationItem(
                "Thanh toán",
                $"{customer} - {amount} ({payment.Status})",
                payment.CreatedAt)));
        }

        var recentReviews = await _context.TourReviews.AsNoTracking()
            .Include(r => r.Tour)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .Take(3)
            .ToListAsync();

        foreach (var review in recentReviews)
        {
            var tourName = review.Tour?.Name ?? "Tour";
            var reviewer = !string.IsNullOrWhiteSpace(review.User?.FullName)
                ? review.User!.FullName!
                : (review.User?.UserName ?? "Khách");
            items.Add((review.CreatedAt, new AdminNotificationItem(
                "Đánh giá mới",
                $"{reviewer} - {tourName}",
                review.CreatedAt)));
        }

        var recentUsers = await _userManager.Users.AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Take(3)
            .ToListAsync();

        foreach (var user in recentUsers)
        {
            var displayName = !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName!
                : (user.UserName ?? user.Email ?? "Người dùng mới");
            items.Add((user.CreatedAt, new AdminNotificationItem(
                "Người dùng mới",
                $"{displayName} vừa đăng ký",
                user.CreatedAt)));
        }

        return items
            .OrderByDescending(x => x.Stamp)
            .Select(x => x.Item)
            .Take(8)
            .ToList();
    }

    public async Task<IActionResult> Notifications()
    {
        var notifications = await BuildNotificationsAsync();
        return View(notifications);
    }

    [HttpGet]
    public async Task<IActionResult> TourDetail(int id)
    {
        var tour = await _context.Tours.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TourId == id);
        if (tour == null)
        {
            return NotFound();
        }

        var bookingCount = await _context.Bookings.AsNoTracking()
            .CountAsync(b => b.TourId == id);

        var fields = new List<object>
        {
            new { label = "Mã tour", value = $"TR-{tour.TourId}" },
            new { label = "Tên tour", value = tour.Name },
            new { label = "Địa điểm", value = tour.Location },
            new { label = "Giá", value = tour.Price.ToString("C0", VnCulture) },
            new { label = "Số đặt chỗ", value = bookingCount.ToString(CultureInfo.InvariantCulture) },
            new { label = "Danh mục", value = tour.CategoryId.ToString(CultureInfo.InvariantCulture) },
            new { label = "Mô tả", value = string.IsNullOrWhiteSpace(tour.Description) ? "-" : tour.Description },
            new { label = "Ảnh", value = NormalizeImageUrl(tour.ImageUrl) ?? "-" }
        };

        return Json(new { title = $"Tour TR-{tour.TourId}", fields });
    }

    [HttpGet]
    public async Task<IActionResult> BookingDetail(int id)
    {
        var booking = await _context.Bookings.AsNoTracking()
            .Include(b => b.Tour)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.BookingId == id);
        if (booking == null)
        {
            return NotFound();
        }

        var payments = await _context.Payments.AsNoTracking()
            .Where(p => p.BookingId == id)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var paymentLines = payments.Count == 0
            ? new List<string> { "Chưa có thanh toán" }
            : payments.Select(p =>
                $"{p.Amount.ToString("C0", VnCulture)} • {p.Method} • {p.Status} • {(p.PaidAt.HasValue ? p.PaidAt.Value.ToLocalTime().ToString("g") : "Chưa thanh toán")}")
                .ToList();

        var fields = new List<object>
        {
            new { label = "Mã đặt chỗ", value = $"BK-{booking.BookingId}" },
            new { label = "Khách hàng", value = booking.FullName },
            new { label = "Email", value = booking.Email },
            new { label = "Số điện thoại", value = booking.PhoneNumber },
            new { label = "Tour", value = booking.Tour?.Name ?? "Tour" },
            new { label = "Ngày đi", value = booking.TravelDate.ToLocalTime().ToString("dd/MM/yyyy") },
            new { label = "Số khách", value = booking.GuestCount.ToString(CultureInfo.InvariantCulture) },
            new { label = "Tổng giá", value = booking.TotalPrice.ToString("C0", VnCulture) },
            new { label = "Trạng thái", value = booking.Status },
            new { label = "Ngày tạo", value = booking.CreatedAt.ToLocalTime().ToString("g") },
            new { label = "Thanh toán", value = string.Join("\n", paymentLines) }
        };

        return Json(new { title = $"Đặt chỗ BK-{booking.BookingId}", fields });
    }

    [HttpGet]
    public async Task<IActionResult> ReviewDetail(int id)
    {
        var review = await _context.TourReviews.AsNoTracking()
            .Include(r => r.Tour)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.ReviewId == id);
        if (review == null)
        {
            return NotFound();
        }

        var reviewer = !string.IsNullOrWhiteSpace(review.User?.FullName)
            ? review.User!.FullName!
            : (review.User?.UserName ?? review.User?.Email ?? "Khách");

        var fields = new List<object>
        {
            new { label = "Mã đánh giá", value = $"RV-{review.ReviewId}" },
            new { label = "Người đánh giá", value = reviewer },
            new { label = "Email", value = review.User?.Email ?? string.Empty },
            new { label = "Tour", value = review.Tour?.Name ?? "Tour" },
            new { label = "Số sao", value = $"{review.Rating}/5" },
            new { label = "Tiêu đề", value = review.Title },
            new { label = "Nội dung", value = review.Content },
            new { label = "Ngày tạo", value = review.CreatedAt.ToLocalTime().ToString("g") },
            new { label = "Cập nhật", value = review.UpdatedAt.ToLocalTime().ToString("g") }
        };

        return Json(new { title = $"Đánh giá RV-{review.ReviewId}", fields });
    }

    [HttpGet]
    public async Task<IActionResult> UserDetail(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await _userManager.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var status = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow
            ? "Tạm dừng"
            : "Đang hoạt động";

        var displayName = !string.IsNullOrWhiteSpace(user.FullName)
            ? user.FullName!
            : (user.UserName ?? user.Email ?? "Người dùng");

        var fields = new List<object>
        {
            new { label = "Mã người dùng", value = user.Id },
            new { label = "Tên", value = displayName },
            new { label = "Email", value = user.Email ?? string.Empty },
            new { label = "Số điện thoại", value = user.PhoneNumber ?? string.Empty },
            new { label = "Địa chỉ", value = user.Address ?? string.Empty },
            new { label = "Vai trò", value = roles.Count == 0 ? "User" : string.Join(", ", roles) },
            new { label = "Trạng thái", value = status },
            new { label = "Ngày tham gia", value = user.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy") }
        };

        return Json(new { title = $"Người dùng {displayName}", fields });
    }

    public async Task<IActionResult> Reviews(string? query, int page = 1)
    {
        const int pageSize = 10;
        var reviewsQuery = _context.TourReviews
            .AsNoTracking()
            .Include(r => r.Tour)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .AsQueryable();

        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();

        if (!string.IsNullOrWhiteSpace(query))
        {
            reviewsQuery = reviewsQuery.Where(r =>
                r.Title.Contains(query)
                || r.Content.Contains(query)
                || (r.Tour != null && r.Tour.Name.Contains(query))
                || (r.User != null && r.User.FullName != null && r.User.FullName.Contains(query))
                || (r.User != null && r.User.Email != null && r.User.Email.Contains(query))
                || (r.User != null && r.User.UserName != null && r.User.UserName.Contains(query)));
        }

        var totalReviews = await reviewsQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalReviews / (double)pageSize);

        if (totalPages == 0)
        {
            totalPages = 1;
        }

        if (page < 1)
        {
            page = 1;
        }

        if (page > totalPages)
        {
            page = totalPages;
        }

        var reviews = await reviewsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var rows = reviews.Select(r => new AdminReviewRow(
            r.ReviewId,
            r.User?.FullName ?? r.User?.UserName ?? r.User?.Email ?? "Khách",
            r.User?.Email ?? string.Empty,
            r.Tour?.Name ?? "Tour",
            r.Rating,
            r.Title,
            r.Content,
            r.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"))).ToList();

        ViewData["SearchQuery"] = query;
        ViewData["SearchPlaceholder"] = "Tìm kiếm người đánh giá, tour, nội dung...";

        var model = new AdminReviewsViewModel
        {
            TotalReviews = totalReviews,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = pageSize,
            Reviews = rows
        };

        return View(model);
    }

    public async Task<IActionResult> Tours(string? region, string? query, int page = 1)
    {
        const int pageSize = 10;
        var toursQuery = _context.Tours.AsNoTracking();
        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();

        if (!string.IsNullOrWhiteSpace(region))
        {
            var keywords = GetRegionKeywords(new[] { region });
            if (keywords.Count > 0)
            {
                toursQuery = toursQuery.Where(t => keywords.Any(k => t.Location.Contains(k)));
            }
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            toursQuery = toursQuery.Where(t => t.Name.Contains(query) || t.Location.Contains(query));
        }

        var totalTours = await toursQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalTours / (double)pageSize);

        if (totalPages == 0)
        {
            totalPages = 1;
        }

        if (page < 1)
        {
            page = 1;
        }

        if (page > totalPages)
        {
            page = totalPages;
        }

        var tours = await toursQuery
            .OrderBy(t => t.TourId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var destinations = tours
            .Select(t => t.Location)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        ViewData["SearchQuery"] = query;
        ViewData["SearchPlaceholder"] = "Tìm kiếm tour, địa điểm...";

        var model = new AdminToursViewModel
        {
            TotalTours = totalTours,
            Destinations = destinations,
            AverageBookingRate = 88,
            Regions = new List<string> { "Miền Bắc", "Miền Trung", "Miền Nam" },
            SelectedRegion = region,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = pageSize,
            Tours = tours.Select(t => new AdminTourRow
            {
                TourId = t.TourId,
                Name = t.Name,
                Location = t.Location,
                ImageUrl = NormalizeImageUrl(t.ImageUrl) ?? string.Empty,
                Price = t.Price.ToString("C0", VnCulture),
                Status = "Published"
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportToursExcel(string? region)
    {
        var toursQuery = _context.Tours.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(region))
        {
            var keywords = GetRegionKeywords(new[] { region });
            if (keywords.Count > 0)
            {
                toursQuery = toursQuery.Where(t => keywords.Any(k => t.Location.Contains(k)));
            }
        }

        var tours = await toursQuery.OrderBy(t => t.TourId).ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Tours");
        sheet.Cell(1, 1).Value = "TourId";
        sheet.Cell(1, 2).Value = "Ten tour";
        sheet.Cell(1, 3).Value = "Dia diem";
        sheet.Cell(1, 4).Value = "Gia";
        sheet.Cell(1, 5).Value = "Trang thai";
        sheet.Cell(1, 6).Value = "Anh";

        var row = 2;
        foreach (var tour in tours)
        {
            var status = "Da dang";
            sheet.Cell(row, 1).Value = tour.TourId;
            sheet.Cell(row, 2).Value = tour.Name;
            sheet.Cell(row, 3).Value = tour.Location;
            sheet.Cell(row, 4).Value = tour.Price;
            sheet.Cell(row, 5).Value = status;
            sheet.Cell(row, 6).Value = tour.ImageUrl ?? string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"tours-report-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public async Task<IActionResult> Bookings(string? status, string? query, int page = 1)
    {
        const int pageSize = 10;
        var bookingsQuery = _context.Bookings
            .AsNoTracking()
            .Include(b => b.Tour)
            .OrderByDescending(b => b.CreatedAt)
            .AsQueryable();
        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "PendingConfirmation", StringComparison.OrdinalIgnoreCase))
            {
                bookingsQuery = bookingsQuery.Where(b =>
                    b.Status == "PendingConfirmation" || b.Status == "Pending" || b.Status == "Processing");
            }
            else
            {
                bookingsQuery = bookingsQuery.Where(b => b.Status == status);
            }
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            bookingsQuery = bookingsQuery.Where(b =>
                b.FullName.Contains(query)
                || b.Email.Contains(query)
                || (b.Tour != null && b.Tour.Name.Contains(query)));
        }

        var totalBookings = await bookingsQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalBookings / (double)pageSize);

        if (totalPages == 0)
        {
            totalPages = 1;
        }

        if (page < 1)
        {
            page = 1;
        }

        if (page > totalPages)
        {
            page = totalPages;
        }

        var bookings = await bookingsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalRevenue = bookings.Sum(b => b.TotalPrice);
        var averageGroup = totalBookings == 0
            ? 0
            : bookings.Average(b => b.GuestCount);
        var locations = await _context.Tours
            .AsNoTracking()
            .Select(t => t.Location)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToListAsync();

        var provinceCount = locations
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        ViewData["SearchQuery"] = query;
        ViewData["SearchPlaceholder"] = "Tìm kiếm khách, tour, email...";

        var model = new AdminBookingsViewModel
        {
            TotalBookings = totalBookings,
            Revenue = totalRevenue.ToString("C0", VnCulture),
            ActiveTours = provinceCount,
            AverageGroupSize = totalBookings == 0
                ? "0"
                : averageGroup.ToString("0.0", CultureInfo.InvariantCulture),
            Statuses = new List<string> { "PendingConfirmation", "Confirmed", "Cancelled" },
            SelectedStatus = status,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = pageSize,
            RecentBookings = bookings.Select(b => new AdminBookingRow(
                b.BookingId,
                GetInitials(b.FullName),
                b.FullName,
                b.Email,
                b.Tour?.Name ?? "-",
                b.TravelDate.ToString("dd/MM/yyyy"),
                b.GuestCount,
                b.TotalPrice.ToString("C0", VnCulture),
                b.Status)).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportBookingsExcel()
    {
        var bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Tour)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Bookings");
        sheet.Cell(1, 1).Value = "BookingId";
        sheet.Cell(1, 2).Value = "Khach hang";
        sheet.Cell(1, 3).Value = "Email";
        sheet.Cell(1, 4).Value = "Ten tour";
        sheet.Cell(1, 5).Value = "Ngay di";
        sheet.Cell(1, 6).Value = "So luong";
        sheet.Cell(1, 7).Value = "Tong gia";
        sheet.Cell(1, 8).Value = "Trang thai";

        var row = 2;
        foreach (var booking in bookings)
        {
            sheet.Cell(row, 1).Value = booking.BookingId;
            sheet.Cell(row, 2).Value = booking.FullName;
            sheet.Cell(row, 3).Value = booking.Email;
            sheet.Cell(row, 4).Value = booking.Tour?.Name ?? "-";
            sheet.Cell(row, 5).Value = booking.TravelDate.ToString("dd/MM/yyyy");
            sheet.Cell(row, 6).Value = booking.GuestCount;
            sheet.Cell(row, 7).Value = booking.TotalPrice;
            sheet.Cell(row, 8).Value = booking.Status;
            row++;
        }

        sheet.Columns().AdjustToContents();

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"bookings-report-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBookingStatus(int id, string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return RedirectToAction(nameof(Bookings));
        }

        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == id);
        if (booking == null)
        {
            return RedirectToAction(nameof(Bookings));
        }

        var normalized = status.Trim();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Confirmed",
            "Cancelled",
            "PendingConfirmation",
            "Pending"
        };

        if (!allowed.Contains(normalized))
        {
            return RedirectToAction(nameof(Bookings));
        }

        booking.Status = normalized.Equals("Pending", StringComparison.OrdinalIgnoreCase)
            ? "PendingConfirmation"
            : normalized;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Bookings));
    }

    public async Task<IActionResult> Users(string? query, int page = 1)
    {
        const int pageSize = 10;
        var usersQuery = _userManager.Users.AsNoTracking().AsQueryable();
        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();

        if (!string.IsNullOrWhiteSpace(query))
        {
            usersQuery = usersQuery.Where(u =>
                (u.Email != null && u.Email.Contains(query))
                || (u.UserName != null && u.UserName.Contains(query))
                || (u.FullName != null && u.FullName.Contains(query)));
        }

        usersQuery = usersQuery.OrderBy(u => u.Email);
        var totalUsers = await usersQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);

        if (totalPages == 0)
        {
            totalPages = 1;
        }

        if (page < 1)
        {
            page = 1;
        }

        if (page > totalPages)
        {
            page = totalPages;
        }

        var users = await usersQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var rows = new List<AdminUserRow>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "User";
            var status = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow
                ? "Paused"
                : "Active";
            var displayName = string.IsNullOrWhiteSpace(user.UserName) ? user.Email ?? "" : user.UserName;
            var joinedDate = user.CreatedAt == default
                ? "-"
                : user.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy");
            rows.Add(new AdminUserRow(
                user.Id,
                GetInitials(displayName),
                displayName,
                user.Email ?? string.Empty,
                role,
                joinedDate,
                status));
        }

        ViewData["SearchQuery"] = query;
        ViewData["SearchPlaceholder"] = "Tìm kiếm người dùng, email...";

        var model = new AdminUsersViewModel
        {
            TotalUsers = totalUsers,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = pageSize,
            Users = rows
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportUsers()
    {
        var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Users");
        sheet.Cell(1, 1).Value = "Ho va ten";
        sheet.Cell(1, 2).Value = "Email";
        sheet.Cell(1, 3).Value = "Vai tro";
        sheet.Cell(1, 4).Value = "Ngay gia nhap";
        sheet.Cell(1, 5).Value = "Trang thai";
        sheet.Cell(1, 6).Value = "So dien thoai";
        sheet.Cell(1, 7).Value = "Dia chi";

        var row = 2;
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "User";
            var roleLabel = role == "Admin" ? "Quan tri" : "Nguoi dung";
            var status = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow
                ? "Tam dung"
                : "Dang hoat dong";
            var joinedDate = user.CreatedAt == default
                ? string.Empty
                : user.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy");
            var displayName = string.IsNullOrWhiteSpace(user.UserName) ? user.Email ?? "" : user.UserName;

            sheet.Cell(row, 1).Value = displayName;
            sheet.Cell(row, 2).Value = user.Email ?? string.Empty;
            sheet.Cell(row, 3).Value = roleLabel;
            sheet.Cell(row, 4).Value = joinedDate;
            sheet.Cell(row, 5).Value = status;
            sheet.Cell(row, 6).Value = user.PhoneNumber ?? string.Empty;
            sheet.Cell(row, 7).Value = user.Address ?? string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"users-report-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public async Task<IActionResult> EditUser(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var model = new EditUserViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            Role = roles.FirstOrDefault() ?? "User"
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(EditUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
        {
            return NotFound();
        }

        if (!string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmailResult = await _userManager.SetEmailAsync(user, model.Email);
            if (!setEmailResult.Succeeded)
            {
                foreach (var error in setEmailResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            var setUserNameResult = await _userManager.SetUserNameAsync(user, model.Email);
            if (!setUserNameResult.Succeeded)
            {
                foreach (var error in setUserNameResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }
        }

        user.FullName = model.FullName;
        user.PhoneNumber = model.PhoneNumber;
        user.Address = model.Address;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
            if (!resetResult.Succeeded)
            {
                foreach (var error in resetResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var targetRole = string.IsNullOrWhiteSpace(model.Role) ? "User" : model.Role;
        if (!currentRoles.Contains(targetRole))
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                foreach (var error in removeResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            var addResult = await _userManager.AddToRoleAsync(user, targetRole);
            if (!addResult.Succeeded)
            {
                foreach (var error in addResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        await _userManager.DeleteAsync(user);
        return RedirectToAction(nameof(Users));
    }

    public IActionResult CreateUser()
    {
        var model = new CreateUserViewModel
        {
            Role = "User"
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            Address = model.Address,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        var role = string.IsNullOrWhiteSpace(model.Role) ? "User" : model.Role;
        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        return RedirectToAction(nameof(Users));
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

    private static List<string> GetRegionKeywords(IEnumerable<string> regions)
    {
        var regionKeywords = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Miền Bắc"] = new[]
            {
                "Hà Nội", "Ha Noi", "Hải Phòng", "Hai Phong", "Quảng Ninh", "Quang Ninh",
                "Lào Cai", "Lao Cai", "Sa Pa", "Sapa", "Hà Giang", "Ha Giang",
                "Sơn La", "Son La", "Mộc Châu", "Moc Chau", "Ninh Bình", "Ninh Binh"
            },
            ["Miền Trung"] = new[]
            {
                "Đà Nẵng", "Da Nang", "Quảng Nam", "Quang Nam", "Hội An", "Hoi An",
                "Huế", "Hue", "Thừa Thiên", "Bình Định", "Binh Dinh", "Quy Nhơn", "Quy Nhon",
                "Phú Yên", "Phu Yen", "Khánh Hòa", "Khanh Hoa", "Nha Trang",
                "Đắk Lắk", "Dak Lak", "Lâm Đồng", "Lam Dong", "Đà Lạt", "Da Lat",
                "Quảng Ngãi", "Quang Ngai"
            },
            ["Miền Nam"] = new[]
            {
                "TP. Hồ Chí Minh", "TP Ho Chi Minh", "Hồ Chí Minh", "Ho Chi Minh", "Sài Gòn", "Sai Gon",
                "Vũng Tàu", "Vung Tau", "Đồng Nai", "Dong Nai", "Cần Thơ", "Can Tho",
                "Phú Quốc", "Phu Quoc", "Bình Dương", "Binh Duong", "Tây Ninh", "Tay Ninh",
                "Long An", "Bến Tre", "Ben Tre", "Tiền Giang", "Tien Giang", "Kiên Giang", "Kien Giang"
            }
        };

        var keywords = new List<string>();
        foreach (var region in regions)
        {
            if (string.IsNullOrWhiteSpace(region))
            {
                continue;
            }

            if (regionKeywords.TryGetValue(region, out var regionItems))
            {
                keywords.AddRange(regionItems);
            }
            else
            {
                keywords.Add(region);
            }
        }

        return keywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || imageUrl.StartsWith("~", StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }

        if (imageUrl.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase))
        {
            return "/" + imageUrl.Substring("wwwroot/".Length).TrimStart('/');
        }

        if (imageUrl.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl.Replace("\\", "/");
        }

        if (imageUrl.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
        {
            return "/" + imageUrl.TrimStart('/');
        }

        return "/images/" + imageUrl.TrimStart('/');
    }
}
