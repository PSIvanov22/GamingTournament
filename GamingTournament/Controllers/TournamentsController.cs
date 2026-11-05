using GamingTournament.Data;
using GamingTournament.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamingTournament.Controllers;

// everyone sees the tournaments, only Admin creates them
public class TournamentsController : Controller
{
    private readonly ApplicationDbContext _context;

    public TournamentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        // matches are loaded only to count them in the view
        List<Tournament> tournaments = await _context.Tournaments
            .Include(tournament => tournament.Matches)
            .OrderByDescending(tournament => tournament.StartDate)
            .ToListAsync();

        return View(tournaments);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult Create()
    {
        // sensible defaults instead of 01.01.0001
        Tournament tournament = new Tournament
        {
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(7)
        };

        return View(tournament);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        // Bind: only the form fields, Id and Matches can't be posted
        [Bind("Name,Game,StartDate,EndDate")] Tournament tournament)
    {
        // same rule as CK_Tournaments_Dates in the database
        if (tournament.EndDate < tournament.StartDate)
        {
            ModelState.AddModelError(
                nameof(tournament.EndDate),
                "Краят не може да е преди началото.");
        }

        // Name is UNIQUE in the database
        bool nameTaken = await _context.Tournaments
            .AnyAsync(item => item.Name == tournament.Name);

        if (nameTaken)
        {
            ModelState.AddModelError(
                nameof(tournament.Name),
                "Вече има турнир с това име.");
        }

        if (!ModelState.IsValid)
        {
            return View(tournament);
        }

        _context.Tournaments.Add(tournament);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Турнирът беше създаден успешно.";

        return RedirectToAction(nameof(Index));
    }
}
