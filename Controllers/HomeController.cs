using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Tourbooking.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Tourbooking.ViewModels;

namespace Tourbooking.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var tours = await _context.Tours.AsNoTracking()
                .ToListAsync();

            var heroTours = tours
                .Where(t => !string.IsNullOrWhiteSpace(t.ImageUrl))
                .Take(3)
                .ToList();

            if (heroTours.Count < 3)
            {
                heroTours = tours.Take(3).ToList();
            }

            var dealsToday = tours
                .OrderBy(t => t.Price)
                .Take(3)
                .ToList();

            var monthlyDeals = tours
                .OrderByDescending(t => t.Price)
                .Take(3)
                .ToList();

            var topChoices = tours
                .OrderByDescending(t => t.TourId)
                .Take(4)
                .ToList();

            var promoCandidates = tours
                .Where(t => !string.IsNullOrWhiteSpace(t.ImageUrl))
                .Take(2)
                .ToList();

            var promoBanners = promoCandidates.Select((tour, index) => new HomePromoBanner
            {
                Title = index == 0 ? "Khuyến mãi hè rực rỡ" : "Gói gia đình tiết kiệm",
                Description = index == 0
                    ? "Giảm đến 30% cho tour biển đảo khi đặt sớm."
                    : "Miễn phí trẻ nhỏ dưới 5 tuổi cho các tour cuối tuần.",
                Cta = index == 0 ? "Đặt ngay" : "Liên hệ tư vấn",
                ImageUrl = NormalizeImageUrl(tour.ImageUrl) ?? string.Empty,
                TourId = tour.TourId
            }).ToList();

            var model = new HomePageViewModel
            {
                HeroTours = heroTours,
                DealsToday = dealsToday,
                MonthlyDeals = monthlyDeals,
                TopChoices = topChoices,
                PromoBanners = promoBanners
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Contact()
        {
            return View(new ContactViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // For now we just show a confirmation message. Integration with email or storage can be added later.
            TempData["ContactMessage"] = "Cảm ơn bạn đã liên hệ. Chúng tôi sẽ liên hệ lại sớm nhất có thể.";
            return RedirectToAction("Contact");
        }

        [Authorize]
        public async Task<IActionResult> Booking(int? tourId)
        {
            if (!tourId.HasValue)
            {
                TempData["BookingMessage"] = "Vui lòng chọn tour trước khi đặt chỗ.";
                return RedirectToAction("Index", "Tours");
            }

            var tour = await _context.Tours.AsNoTracking().FirstOrDefaultAsync(t => t.TourId == tourId.Value);
            if (tour == null)
            {
                TempData["BookingMessage"] = "Tour không tồn tại hoặc đã bị gỡ.";
                return RedirectToAction("Index", "Tours");
            }

            var user = await _userManager.GetUserAsync(User);
            var model = new BookingCreateViewModel
            {
                TourId = tour.TourId,
                TourName = tour.Name,
                TourLocation = tour.Location,
                TourImageUrl = NormalizeImageUrl(tour.ImageUrl),
                TourPrice = tour.Price,
                FullName = user?.FullName ?? string.Empty,
                Email = user?.Email ?? string.Empty,
                PhoneNumber = user?.PhoneNumber ?? string.Empty,
                GuestCount = 2,
                PaymentMethod = "Card"
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Booking(BookingCreateViewModel model)
        {
            var tour = await _context.Tours.AsNoTracking().FirstOrDefaultAsync(t => t.TourId == model.TourId);
            if (tour == null)
            {
                TempData["BookingMessage"] = "Tour không tồn tại hoặc đã bị gỡ.";
                return RedirectToAction("Index", "Tours");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var paymentMethod = string.Equals(model.PaymentMethod, "BankTransfer", StringComparison.OrdinalIgnoreCase)
                ? "BankTransfer"
                : "Card";

            if (!ModelState.IsValid)
            {
                model.TourName = tour.Name;
                model.TourLocation = tour.Location;
                model.TourImageUrl = NormalizeImageUrl(tour.ImageUrl);
                model.TourPrice = tour.Price;
                return View(model);
            }

            const decimal serviceFee = 45000m;
            const decimal localTax = 120000m;
            var total = (tour.Price * model.GuestCount) + serviceFee + localTax;
            var paymentStatus = paymentMethod == "BankTransfer" ? "PendingConfirmation" : "Pending";

            var booking = new Booking
            {
                TourId = tour.TourId,
                UserId = user.Id,
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber.Trim(),
                TravelDate = model.TravelDate ?? DateTime.Today,
                GuestCount = model.GuestCount,
                TotalPrice = total,
                Status = paymentStatus
            };

            var payment = new Payment
            {
                Booking = booking,
                UserId = user.Id,
                Amount = total,
                Method = paymentMethod,
                Status = paymentStatus,
                Provider = paymentMethod == "Card" ? "MoMo" : null,
                TransactionCode = model.TransactionCode?.Trim(),
                BankName = paymentMethod == "BankTransfer" ? model.BankName?.Trim() : null,
                BankAccountName = paymentMethod == "BankTransfer" ? booking.FullName : null,
                BankAccountNumber = paymentMethod == "BankTransfer" ? model.BankAccountNumber?.Trim() : null,
                BankReference = paymentMethod == "BankTransfer" ? model.BankReference?.Trim() : null
            };

            _context.Bookings.Add(booking);
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["BookingSuccess"] = "Đặt tour thành công. Cảm ơn bạn đã đặt chỗ!";
            return RedirectToAction("Index", "Home");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
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

        private static bool IsDalatTour(string? name, string? location)
        {
            return ContainsDalat(name) || ContainsDalat(location);
        }

        private static bool ContainsDalat(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Contains("Đà Lạt", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Da Lat", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Dalat", StringComparison.OrdinalIgnoreCase);
        }
    }
}
