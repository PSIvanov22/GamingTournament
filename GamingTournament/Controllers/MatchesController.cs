using GamingTournament.Data;
using GamingTournament.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GamingTournament.Controllers;

// everyone can view matches, only Admin manages them
// (same split as team requests in TeamsController)
public class MatchesController : Controller
{
    private readonly ApplicationDbContext _context;

    public MatchesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        // upcoming first, then the rest by date
        List<Match> matches = await _context.Matches
            .Include(match => match.HomeTeam)
            .Include(match => match.AwayTeam)
            .OrderBy(match =>
                match.Status == MatchStatus.Scheduled ? 0 : 1)
            .ThenBy(match => match.ScheduledOn)
            .ToListAsync();

        return View(matches);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await FillTeamsAsync();

        return View(new MatchCreateViewModel());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        MatchCreateViewModel model)
    {
        // null == null would be true too - skip that case,
        // [Required] already reports it
        if (model.HomeTeamId != null &&
            model.HomeTeamId == model.AwayTeamId)
        {
            ModelState.AddModelError(
                nameof(model.AwayTeamId),
                "Отборът не може да играе срещу себе си.");
        }

        if (!ModelState.IsValid)
        {
            // the dropdowns are not posted back, refill them
            await FillTeamsAsync();

            return View(model);
        }

        // [Required] passed, so .Value is safe from here on
        int homeTeamId = model.HomeTeamId!.Value;
        int awayTeamId = model.AwayTeamId!.Value;
        DateTime scheduledOn = model.ScheduledOn!.Value;

        // ids come from a <select>, but a POST can be forged -
        // check they really exist
        int existingTeams = await _context.Teams
            .CountAsync(team =>
                team.Id == homeTeamId ||
                team.Id == awayTeamId);

        if (existingTeams != 2)
        {
            return NotFound();
        }

        // one team can't be in two matches at the same moment
        bool timeTaken = await _context.Matches
            .AnyAsync(match =>
                match.Status == MatchStatus.Scheduled &&
                match.ScheduledOn == scheduledOn &&
                (match.HomeTeamId == homeTeamId ||
                 match.AwayTeamId == homeTeamId ||
                 match.HomeTeamId == awayTeamId ||
                 match.AwayTeamId == awayTeamId));

        if (timeTaken)
        {
            ModelState.AddModelError(
                nameof(model.ScheduledOn),
                "Някой от отборите вече има мач в този час.");

            await FillTeamsAsync();

            return View(model);
        }

        // past dates are allowed on purpose:
        // lets the admin record a match that was already played
        Match match = new Match
        {
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            ScheduledOn = scheduledOn
        };

        _context.Matches.Add(match);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Мачът беше създаден успешно.";

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Result(int id)
    {
        Match? match = await _context.Matches
            .Include(item => item.HomeTeam)
            .Include(item => item.AwayTeam)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (match == null)
        {
            return NotFound();
        }

        if (match.Status != MatchStatus.Scheduled)
        {
            TempData["ErrorMessage"] =
                "Резултат се въвежда само за предстоящ мач.";

            return RedirectToAction(nameof(Index));
        }

        MatchResultViewModel model = new MatchResultViewModel
        {
            Id = match.Id,
            HomeTeamName = match.HomeTeam?.Name ?? "",
            AwayTeamName = match.AwayTeam?.Name ?? ""
        };

        return View(model);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Result(
        MatchResultViewModel model)
    {
        Match? match = await _context.Matches
            .Include(item => item.HomeTeam)
            .Include(item => item.AwayTeam)
            .FirstOrDefaultAsync(item => item.Id == model.Id);

        if (match == null)
        {
            return NotFound();
        }

        // re-check on POST: the page could have been open
        // while someone else finished or cancelled the match
        if (match.Status != MatchStatus.Scheduled)
        {
            TempData["ErrorMessage"] =
                "Мачът вече е приключен или отменен.";

            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            // names come from the DB, not from hidden inputs
            model.HomeTeamName = match.HomeTeam?.Name ?? "";
            model.AwayTeamName = match.AwayTeam?.Name ?? "";

            return View(model);
        }

        match.HomeScore = model.HomeScore;
        match.AwayScore = model.AwayScore;
        match.Status = MatchStatus.Finished;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Резултатът беше записан.";

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        Match? match = await _context.Matches.FindAsync(id);

        if (match == null)
        {
            return NotFound();
        }

        if (match.Status != MatchStatus.Scheduled)
        {
            TempData["ErrorMessage"] =
                "Само предстоящ мач може да бъде отменен.";

            return RedirectToAction(nameof(Index));
        }

        match.Status = MatchStatus.Cancelled;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Мачът беше отменен.";

        return RedirectToAction(nameof(Index));
    }

    // dropdown data for Create (GET and failed POST)
    private async Task FillTeamsAsync()
    {
        List<Team> teams = await _context.Teams
            .OrderBy(team => team.Name)
            .ToListAsync();

        ViewBag.Teams = new SelectList(
            teams,
            nameof(Team.Id),
            nameof(Team.Name));
    }
}
