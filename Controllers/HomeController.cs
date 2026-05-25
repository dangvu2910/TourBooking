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
            var tours = await _context.Tours.ToListAsync();
            return View(tours);
        }

        public IActionResult Privacy()
        {
            return View();
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
                TourImageUrl = tour.ImageUrl,
                TourPrice = tour.Price,
                FullName = user?.FullName ?? string.Empty,
                Email = user?.Email ?? string.Empty,
                PhoneNumber = user?.PhoneNumber ?? string.Empty,
                GuestCount = 2
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

            if (!ModelState.IsValid)
            {
                model.TourName = tour.Name;
                model.TourLocation = tour.Location;
                model.TourImageUrl = tour.ImageUrl;
                model.TourPrice = tour.Price;
                return View(model);
            }

            const decimal serviceFee = 45000m;
            const decimal localTax = 120000m;
            var total = (tour.Price * model.GuestCount) + serviceFee + localTax;

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
                Status = "Pending"
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            TempData["BookingSuccess"] = "Đặt tour thành công. Cảm ơn bạn đã đặt chỗ!";
            return RedirectToAction("Index", "Home");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
