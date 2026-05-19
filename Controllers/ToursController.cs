using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Tourbooking.Models;

namespace Tourbooking.Controllers
{
    public class ToursController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ToursController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Tours
        public async Task<IActionResult> Index(string? query, decimal? minPrice, decimal? maxPrice, int page = 1)
        {
            if (User?.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
            {
                return RedirectToAction("Tours", "Admin");
            }

            const int pageSize = 6;

            var toursQuery = _context.Tours.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                toursQuery = toursQuery.Where(t => t.Name.Contains(query) || t.Location.Contains(query));
            }

            if (minPrice.HasValue)
            {
                toursQuery = toursQuery.Where(t => t.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                toursQuery = toursQuery.Where(t => t.Price <= maxPrice.Value);
            }

            var totalTours = await toursQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalTours / (double)pageSize);

            if (page < 1)
            {
                page = 1;
            }
            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var tours = await toursQuery
                .OrderBy(t => t.TourId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["TotalTours"] = totalTours;
            ViewData["Query"] = query;
            ViewData["MinPrice"] = minPrice;
            ViewData["MaxPrice"] = maxPrice;

            return View(tours);
        }

        // GET: Tours/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tour = await _context.Tours
                .FirstOrDefaultAsync(m => m.TourId == id);
            if (tour == null)
            {
                return NotFound();
            }

            return View(tour);
        }

        // GET: Tours/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Tours/Create
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TourId,Name,Location,Price,Description,CategoryId")] Tour tour, IFormFile imageFile)
        {
            TryNormalizePriceFromRequest(tour);

            if (ModelState.IsValid)
            {
                try
                {
                    // Xử lý upload file
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                        
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }

                        tour.ImageUrl = "/images/" + uniqueFileName;
                    }

                    _context.Add(tour);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("imageFile", "Lỗi: " + ex.Message);
                }
            }
            return View(tour);
        }

        // GET: Tours/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tour = await _context.Tours.FindAsync(id);
            if (tour == null)
            {
                return NotFound();
            }
            return View(tour);
        }

        // POST: Tours/Edit/5
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TourId,Name,Location,Price,Description,ImageUrl,CategoryId")] Tour tour, IFormFile imageFile)
        {
            TryNormalizePriceFromRequest(tour);

            if (id != tour.TourId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingTour = await _context.Tours.AsNoTracking().FirstOrDefaultAsync(t => t.TourId == id);
                    if (existingTour == null)
                    {
                        return NotFound();
                    }

                    if (string.IsNullOrWhiteSpace(tour.ImageUrl))
                    {
                        tour.ImageUrl = existingTour.ImageUrl;
                    }

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                        
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        if (!string.IsNullOrEmpty(existingTour.ImageUrl))
                        {
                            string oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, existingTour.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }

                        tour.ImageUrl = "/images/" + uniqueFileName;
                    }

                    _context.Update(tour);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TourExists(tour.TourId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("imageFile", "Lỗi: " + ex.Message);
                }
            }
            return View(tour);
        }

        // GET: Tours/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tour = await _context.Tours
                .FirstOrDefaultAsync(m => m.TourId == id);
            if (tour == null)
            {
                return NotFound();
            }

            return View(tour);
        }

        // POST: Tours/Delete/5
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tour = await _context.Tours.FindAsync(id);
            if (tour != null)
            {
                _context.Tours.Remove(tour);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TourExists(int id)
        {
            return _context.Tours.Any(e => e.TourId == id);
        }

        private void TryNormalizePriceFromRequest(Tour tour)
        {
            var rawPrice = Request.Form["Price"].ToString();

            if (string.IsNullOrWhiteSpace(rawPrice))
            {
                return;
            }

            var styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;

            if (decimal.TryParse(rawPrice, styles, CultureInfo.CurrentCulture, out var currentCulturePrice)
                || decimal.TryParse(rawPrice, styles, new CultureInfo("vi-VN"), out currentCulturePrice)
                || decimal.TryParse(rawPrice, styles, CultureInfo.InvariantCulture, out currentCulturePrice))
            {
                tour.Price = currentCulturePrice;
                ModelState.Remove(nameof(Tour.Price));
            }
        }
    }
}
