using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GamingTournament.Data;
using GamingTournament.Middleware;
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

    // 500 / 409: ExceptionHandlingMiddleware re-runs the request
    // here and leaves the prepared model in HttpContext.Items
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ErrorViewModel model =
            HttpContext.Items[ExceptionHandlingMiddleware.ErrorItemKey]
                as ErrorViewModel
            ?? new ErrorViewModel
            {
                RequestId = HttpContext.TraceIdentifier
            };

        Response.StatusCode = model.StatusCode;

        return View(model);
    }

    // 404, 403 and the rest: UseStatusCodePagesWithReExecute
    // sends them here (named HttpStatus, because Controller
    // already has a method called StatusCode)
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult HttpStatus(int code)
    {
        Response.StatusCode = code;

        ErrorViewModel model = new ErrorViewModel
        {
            RequestId = HttpContext.TraceIdentifier,
            StatusCode = code
        };

        switch (code)
        {
            case 404:
                return View("NotFound", model);

            case 403:
                return View("Forbidden", model);

            default:
                model.Title = "Грешка " + code;
                model.Message =
                    "Заявката не можа да бъде обработена.";

                return View("Error", model);
        }
    }

    // Development only: lets you see the 500 page on demand
    // (in Production the route simply doesn't exist -> 404)
    public IActionResult Crash(
        [FromServices] IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        throw new InvalidOperationException(
            "Тестова грешка от /Home/Crash.");
    }
}