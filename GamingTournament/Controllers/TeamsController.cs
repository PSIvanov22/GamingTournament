using GamingTournament.Data;
using GamingTournament.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamingTournament.Controllers;

public class TeamsController : Controller
{
    private readonly ApplicationDbContext _context;

    private readonly UserManager<ApplicationUser>
        _userManager;

    public TeamsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        List<Team> teams = await _context.Teams
            .Include(team => team.Members)
            .OrderBy(team => team.Name)
            .ToListAsync();

        HashSet<int> requestedTeamIds =
            new HashSet<int>();

        if (User.Identity != null &&
            User.Identity.IsAuthenticated)
        {
            string? playerId =
                _userManager.GetUserId(User);

            if (playerId != null)
            {
                requestedTeamIds =
                    (await _context.TeamJoinRequests
                        .Where(request =>
                            request.PlayerId == playerId &&
                            request.Status == "Pending")
                        .Select(request => request.TeamId)
                        .ToListAsync())
                    .ToHashSet();

                ApplicationUser? currentUser =
                    await _userManager.GetUserAsync(User);

                ViewBag.CurrentTeamId =
                    currentUser?.TeamId;
            }
        }

        ViewBag.RequestedTeamIds =
            requestedTeamIds;

        return View(teams);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Team team)
    {
        if (!ModelState.IsValid)
        {
            return View(team);
        }

        _context.Teams.Add(team);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Отборът беше създаден успешно.";

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Player")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(
        int teamId)
    {
        ApplicationUser? player =
            await _userManager.GetUserAsync(User);

        if (player == null)
        {
            return Challenge();
        }

        if (player.TeamId != null)
        {
            TempData["ErrorMessage"] =
                "Вече участваш в отбор.";

            return RedirectToAction(nameof(Index));
        }

        Team? team =
            await _context.Teams.FindAsync(teamId);

        if (team == null)
        {
            return NotFound();
        }

        bool requestExists =
            await _context.TeamJoinRequests
                .AnyAsync(request =>
                    request.PlayerId == player.Id &&
                    request.TeamId == teamId &&
                    request.Status == "Pending");

        if (requestExists)
        {
            TempData["ErrorMessage"] =
                "Вече имаш чакаща заявка към този отбор.";

            return RedirectToAction(nameof(Index));
        }

        TeamJoinRequest joinRequest =
            new TeamJoinRequest
            {
                PlayerId = player.Id,
                TeamId = teamId,
                Status = "Pending",
                CreatedOn = DateTime.Now
            };

        _context.TeamJoinRequests.Add(joinRequest);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Заявката беше изпратена успешно.";

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Requests()
    {
        List<TeamJoinRequest> requests =
            await _context.TeamJoinRequests
                .Include(request => request.Player)
                .Include(request => request.Team)
                .Where(request =>
                    request.Status == "Pending")
                .OrderBy(request =>
                    request.CreatedOn)
                .ToListAsync();

        return View(requests);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(
        int id)
    {
        TeamJoinRequest? request =
            await _context.TeamJoinRequests
                .Include(item => item.Player)
                .FirstOrDefaultAsync(item =>
                    item.Id == id);

        if (request == null)
        {
            return NotFound();
        }

        if (request.Status != "Pending")
        {
            TempData["ErrorMessage"] =
                "Тази заявка вече е обработена.";

            return RedirectToAction(
                nameof(Requests)
            );
        }

        if (request.Player == null)
        {
            return NotFound();
        }

        if (request.Player.TeamId != null)
        {
            request.Status = "Rejected";

            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] =
                "Играчът вече е в друг отбор.";

            return RedirectToAction(
                nameof(Requests)
            );
        }

        request.Player.TeamId = request.TeamId;
        request.Status = "Approved";

        List<TeamJoinRequest> otherRequests =
            await _context.TeamJoinRequests
                .Where(item =>
                    item.PlayerId ==
                    request.PlayerId &&
                    item.Id != request.Id &&
                    item.Status == "Pending")
                .ToListAsync();

        foreach (TeamJoinRequest otherRequest
                 in otherRequests)
        {
            otherRequest.Status = "Rejected";
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Играчът беше добавен към отбора.";

        return RedirectToAction(nameof(Requests));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(
        int id)
    {
        TeamJoinRequest? request =
            await _context.TeamJoinRequests
                .FindAsync(id);

        if (request == null)
        {
            return NotFound();
        }

        if (request.Status == "Pending")
        {
            request.Status = "Rejected";

            await _context.SaveChangesAsync();
        }

        TempData["SuccessMessage"] =
            "Заявката беше отказана.";

        return RedirectToAction(nameof(Requests));
    }
}