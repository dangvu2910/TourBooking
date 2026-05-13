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

        public IActionResult Booking()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
