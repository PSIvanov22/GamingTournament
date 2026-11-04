using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GamingTournament.Data;
using GamingTournament.Models;

namespace GamingTournament.Controllers;

public class HomeController : Controller
{
    // how many matches each list on the home page shows
    private const int ListSize = 5;

    private readonly ILogger<HomeController> _logger;

    private readonly ApplicationDbContext _context;

    public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        HomeIndexViewModel model = new HomeIndexViewModel
        {
            TeamCount = await _context.Teams.CountAsync(),

            PlayerCount = await _context.Users
                .CountAsync(user => user.TeamId != null),

            FinishedMatchCount = await _context.Matches
                .CountAsync(match =>
                    match.Status == MatchStatus.Finished),

            ScheduledMatchCount = await _context.Matches
                .CountAsync(match =>
                    match.Status == MatchStatus.Scheduled),

            // soonest first
            UpcomingMatches = await _context.Matches
                .Include(match => match.HomeTeam)
                .Include(match => match.AwayTeam)
                .Where(match =>
                    match.Status == MatchStatus.Scheduled)
                .OrderBy(match => match.ScheduledOn)
                .Take(ListSize)
                .ToListAsync(),

            // newest first
            RecentResults = await _context.Matches
                .Include(match => match.HomeTeam)
                .Include(match => match.AwayTeam)
                .Where(match =>
                    match.Status == MatchStatus.Finished)
                .OrderByDescending(match => match.ScheduledOn)
                .Take(ListSize)
                .ToListAsync()
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}