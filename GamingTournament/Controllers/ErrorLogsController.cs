using GamingTournament.Data;
using GamingTournament.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamingTournament.Controllers;

// the error journal is for the administrator only
[Authorize(Roles = "Admin")]
public class ErrorLogsController : Controller
{
    private const int PageSize = 10;

    private readonly ApplicationDbContext _context;

    public ErrorLogsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        int totalLogs = await _context.ErrorLogs.CountAsync();

        // round up, same idea as the teams list
        int totalPages = (totalLogs + PageSize - 1) / PageSize;

        if (page < 1)
        {
            page = 1;
        }

        if (page > totalPages && totalPages > 0)
        {
            page = totalPages;
        }

        List<ErrorLog> logs = await _context.ErrorLogs
            .AsNoTracking()
            .OrderByDescending(log => log.CreatedOn)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalLogs = totalLogs;

        return View(logs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        await _context.ErrorLogs.ExecuteDeleteAsync();

        TempData["SuccessMessage"] =
            "Журналът на грешките беше изчистен.";

        return RedirectToAction(nameof(Index));
    }
}
