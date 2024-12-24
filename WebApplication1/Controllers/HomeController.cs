using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Data;
using WebApplication1.Service;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ScraperService _scraperService;
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ScraperService scraperService, ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _scraperService = scraperService;
            _logger = logger;
            _context = context;
        }

        [HttpGet]
        public IActionResult Index(string authorName, string title, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            const int itemsPerPage = 10;

            if (string.IsNullOrEmpty(authorName) && string.IsNullOrEmpty(title))
            {
                ViewData["TotalPages"] = 1;
                ViewData["CurrentPage"] = 1;
                return View(new List<ResearchWork>());
            }

            try
            {
                var researchWorks = _scraperService.ScrapeResearchDataByAuthor(authorName);

                if (!string.IsNullOrEmpty(title))
                {
                    researchWorks = researchWorks.Where(w => w.Title.Contains(title, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (startDate.HasValue)
                {
                    researchWorks = researchWorks.Where(w => w.PublicationDate >= startDate).ToList();
                }
                if (endDate.HasValue)
                {
                    researchWorks = researchWorks.Where(w => w.PublicationDate <= endDate).ToList();
                }

                int totalItems = researchWorks.Count;
                int totalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage);

                var paginatedResearchWorks = researchWorks
                    .Skip((page - 1) * itemsPerPage)
                    .Take(itemsPerPage)
                    .ToList();

                ViewData["TotalPages"] = totalPages;
                ViewData["CurrentPage"] = page;

                return View(paginatedResearchWorks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving research data.");
                return View("Error", ex);
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveToDatabase(List<ResearchWork> researchWorks)
        {
            if (researchWorks == null && !researchWorks.Any())
            {
                _logger.LogWarning("No research works provided to save.");
                return BadRequest("No research works provided to save.");
            }

            try
            {
                foreach (var researchWork in researchWorks)
                {
                    var existingWork = await _context.ResearchWorks
                        .FirstOrDefaultAsync(r => r.Title == researchWork.Title &&
                                                  r.AuthorName == researchWork.AuthorName &&
                                                  r.SourceUrl == researchWork.SourceUrl);

                    if (existingWork != null)
                    {
                        _logger.LogInformation("Duplicate research work found and skipped: {Title}, {AuthorName}", researchWork.Title, researchWork.AuthorName);
                        continue;
                    }

                    await _context.ResearchWorks.AddAsync(researchWork);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully saved {Count} research works to the database.", researchWorks.Count);

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while saving research works to the database.");
                return StatusCode(500, "An error occurred while saving data to the database.");
            }
        }

        public IActionResult ExportCsv(string authorName, string title, DateTime? startDate, DateTime? endDate)
        {
            if (string.IsNullOrEmpty(authorName) && string.IsNullOrEmpty(title))
            {
                _logger.LogWarning("Either author name or title is required to export CSV.");
                return BadRequest("Please provide either author name or title to export CSV.");
            }

            try
            {
                _logger.LogInformation("Exporting CSV for author: {AuthorName}, title: {Title}, startDate: {StartDate}, endDate: {EndDate}",
                                       authorName, title, startDate, endDate);

                List<ResearchWork> researchWorks = new List<ResearchWork>();

                if (!string.IsNullOrEmpty(title))
                {
                    _logger.LogInformation("Fetching research works by title: {Title}", title);
                    researchWorks.AddRange(_scraperService.ScrapeResearchDataByTitle(title));
                }

                if (!string.IsNullOrEmpty(authorName))
                {
                    _logger.LogInformation("Fetching research works by author: {AuthorName}", authorName);
                    researchWorks.AddRange(_scraperService.ScrapeResearchDataByAuthor(authorName));
                }

                researchWorks = researchWorks
                    .GroupBy(r => new { r.Title, r.AuthorName, r.SourceUrl })
                    .Select(g => g.First())
                    .ToList();

                if (startDate.HasValue)
                {
                    researchWorks = researchWorks.Where(w => w.PublicationDate >= startDate).ToList();
                }

                if (endDate.HasValue)
                {
                    researchWorks = researchWorks.Where(w => w.PublicationDate <= endDate).ToList();
                }

                var csvData = new StringBuilder();
                csvData.AppendLine("Title,Author,Abstract,PublicationDate,SourceUrl");

                foreach (var work in researchWorks)
                {
                    csvData.AppendLine($"\"{work.Title}\",\"{work.AuthorName}\",\"{work.Abstract}\",\"{work.PublicationDate?.ToString("yyyy-MM-dd")}\",\"{work.SourceUrl}\"");
                }

                return File(
                    Encoding.UTF8.GetBytes(csvData.ToString()),
                    "text/csv",
                    $"{(title ?? authorName)}_ResearchWorks.csv"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while exporting CSV for author: {AuthorName}, title: {Title}", authorName, title);
                return StatusCode(500, "An error occurred while exporting CSV.");
            }
        }
    }
}
