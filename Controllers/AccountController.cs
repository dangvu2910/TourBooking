using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tourbooking.Models;
using Tourbooking.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Tourbooking.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Account/Login
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        // POST: Account/Login
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return RedirectToAction("Dashboard", "Admin");
                    }

                    var adminEmail = _configuration["AdminUser:Email"];
                    if (!string.IsNullOrWhiteSpace(adminEmail)
                        && string.Equals(adminEmail, user.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        await _userManager.AddToRoleAsync(user, "Admin");
                        return RedirectToAction("Dashboard", "Admin");
                    }
                }

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Tours");
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        // GET: Account/Register
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        // POST: Account/Register
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // GET: Account/Logout
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
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

            if (!string.Equals(user.PhoneNumber, model.PhoneNumber, StringComparison.OrdinalIgnoreCase))
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, model.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    foreach (var error in setPhoneResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View(model);
                }
            }

            user.FullName = model.FullName;
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

            await _signInManager.RefreshSignInAsync(user);

            TempData["ProfileMessage"] = "Đã cập nhật thông tin cá nhân.";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["ChangePasswordMessage"] = "Đã đổi mật khẩu thành công.";
            return RedirectToAction(nameof(ChangePassword));
        }

        [Authorize]
        public async Task<IActionResult> Bookings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var bookings = await _context.Bookings
                .Include(b => b.Tour)
                .Where(b => b.UserId == user.Id)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var model = new AccountBookingsViewModel
            {
                TotalBookings = bookings.Count,
                UpcomingBookings = bookings.Count(b => b.TravelDate.Date >= DateTime.Today && !string.Equals(b.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)),
                CompletedBookings = bookings.Count(b => b.TravelDate.Date < DateTime.Today && !string.Equals(b.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)),
                CancelledBookings = bookings.Count(b => string.Equals(b.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)),
                Bookings = bookings.Select(b => new AccountBookingRow(
                    b.BookingId,
                    b.Tour?.Name ?? "Tour",
                    b.Tour?.Location ?? string.Empty,
                    ResolveTourImageUrl(b.Tour?.ImageUrl, b.Tour?.Location, b.Tour?.Name),
                    b.TravelDate,
                    b.GuestCount,
                    b.TotalPrice,
                    b.Status)).ToList()
            };

            return View(model);
        }

        [Authorize]
        public async Task<IActionResult> Reviews()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var completedBookings = await _context.Bookings
                .Include(b => b.Tour)
                .Where(b => b.UserId == user.Id
                    && b.TravelDate.Date < DateTime.Today
                    && b.Status != "Cancelled")
                .OrderByDescending(b => b.TravelDate)
                .ToListAsync();

            var bookingIds = completedBookings.Select(b => b.BookingId).ToList();
            var reviews = await _context.TourReviews
                .Where(r => r.UserId == user.Id && bookingIds.Contains(r.BookingId))
                .ToListAsync();

            var model = new AccountReviewsViewModel
            {
                Reviews = completedBookings.Select(booking =>
                {
                    var review = reviews.FirstOrDefault(r => r.BookingId == booking.BookingId);
                    return new AccountReviewRow(
                        booking.BookingId,
                        booking.TourId,
                        booking.Tour?.Name ?? "Tour",
                        booking.Tour?.Location ?? string.Empty,
                        ResolveTourImageUrl(booking.Tour?.ImageUrl, booking.Tour?.Location, booking.Tour?.Name),
                        booking.TravelDate,
                        booking.Status,
                        review == null,
                        review != null,
                        review?.Rating,
                        review?.Title,
                        review?.Content,
                        review?.CreatedAt);
                }).ToList()
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reviews(int bookingId, int rating, string title, string content)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var booking = await _context.Bookings
                .Include(b => b.Tour)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == user.Id);

            if (booking == null)
            {
                TempData["ReviewMessage"] = "Không tìm thấy booking cần đánh giá.";
                return RedirectToAction(nameof(Reviews));
            }

            var isCancelled = string.Equals(booking.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
            var isCompleted = booking.TravelDate.Date < DateTime.Today && !isCancelled;
            if (!isCompleted)
            {
                TempData["ReviewMessage"] = "Bạn chỉ có thể đánh giá các tour đã hoàn thành.";
                return RedirectToAction(nameof(Reviews));
            }

            var existingReview = await _context.TourReviews
                .FirstOrDefaultAsync(r => r.BookingId == booking.BookingId && r.UserId == user.Id);

            if (existingReview != null)
            {
                TempData["ReviewMessage"] = "Tour này đã được đánh giá trước đó.";
                return RedirectToAction(nameof(Reviews));
            }

            if (rating < 1 || rating > 5 || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                TempData["ReviewMessage"] = "Vui lòng nhập đủ thông tin đánh giá.";
                return RedirectToAction(nameof(Reviews));
            }

            var review = new TourReview
            {
                BookingId = booking.BookingId,
                TourId = booking.TourId,
                UserId = user.Id,
                Rating = rating,
                Title = title.Trim(),
                Content = content.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.TourReviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["ReviewMessage"] = "Đã lưu đánh giá của bạn.";
            return RedirectToAction(nameof(Reviews));
        }

        private string? ResolveTourImageUrl(string? imageUrl, string? location = null, string? name = null)
        {
            if (IsHoiAnTour(location, name))
            {
                return "/images/839e713c-76f9-40d3-9764-758b56220ae0_hoian.webp";
            }

            return NormalizeImageUrl(imageUrl);
        }

        private bool IsHoiAnTour(string? location, string? name)
        {
            return ContainsHoiAn(location) || ContainsHoiAn(name);
        }

        private static bool ContainsHoiAn(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Contains("Hội An", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Hoi An", StringComparison.OrdinalIgnoreCase)
                || value.Contains("HoiAn", StringComparison.OrdinalIgnoreCase);
        }

        private string? NormalizeImageUrl(string? imageUrl)
        {
            const string fallbackImage = "/images/8c18b822-a807-48e3-b471-ef035840c58c_mientay.jpeg";

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return fallbackImage;
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
                var normalizedPath = imageUrl.Replace("\\", "/");
                var localPath = normalizedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, localPath);
                return System.IO.File.Exists(physicalPath) ? normalizedPath : fallbackImage;
            }

            if (imageUrl.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedPath = "/" + imageUrl.TrimStart('/');
                var localPath = normalizedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, localPath);
                return System.IO.File.Exists(physicalPath) ? normalizedPath : fallbackImage;
            }

            var resolvedPath = "/images/" + imageUrl.TrimStart('/');
            var resolvedLocalPath = resolvedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var resolvedPhysicalPath = Path.Combine(_webHostEnvironment.WebRootPath, resolvedLocalPath);
            return System.IO.File.Exists(resolvedPhysicalPath) ? resolvedPath : fallbackImage;
        }
    }
}
