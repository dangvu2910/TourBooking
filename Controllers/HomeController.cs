using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Tourbooking.Models;
using Microsoft.EntityFrameworkCore;

namespace Tourbooking.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
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

            return View(tour);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
