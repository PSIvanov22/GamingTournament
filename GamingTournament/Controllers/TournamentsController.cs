using GamingTournament.Data;
using GamingTournament.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GamingTournament.Controllers;

// everyone sees the tournaments, only Admin creates them
public class TournamentsController : Controller
{
    // generated matches: first round at 18:00, more rounds
    // on the same day follow every 2 hours (18:00, 20:00, 22:00)
    private const int FirstMatchHour = 18;

    private const int HoursBetweenRounds = 2;

    private const int MaxRoundsPerDay = 3;

    private readonly ApplicationDbContext _context;

    private readonly ILogger<TournamentsController> _logger;

    public TournamentsController(
        ApplicationDbContext context,
        ILogger<TournamentsController> logger)
    {
        _context = context;
        _logger = logger;
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

    // Създава програмата "всеки срещу всеки" за всички отбори.
    // Критична операция с транзакция: ако един кръг не може да се
    // запише (например отбор има друг мач в същия час), се връщат
    // и вече записаните кръгове - няма наполовина генерирана програма
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateMatches(int id)
    {
        Tournament? tournament = await _context.Tournaments
            .FindAsync(id);

        if (tournament == null)
        {
            return NotFound();
        }

        bool hasMatches = await _context.Matches
            .AnyAsync(match => match.TournamentId == id);

        if (hasMatches)
        {
            TempData["ErrorMessage"] =
                "Турнирът вече има мачове - програмата не може да се генерира отново.";

            return RedirectToAction(nameof(Index));
        }

        List<int> teamIds = await _context.Teams
            .OrderBy(team => team.Name)
            .Select(team => team.Id)
            .ToListAsync();

        if (teamIds.Count < 2)
        {
            TempData["ErrorMessage"] =
                "Трябват поне два отбора, за да се генерира програма.";

            return RedirectToAction(nameof(Index));
        }

        // EndDate is the whole last day, so +1
        int days = (tournament.EndDate.Date - tournament.StartDate.Date)
            .Days + 1;

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            string? error = await CreateRoundRobinAsync(
                tournament, teamIds, days);

            if (error != null)
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] = error;

                return RedirectToAction(nameof(Index));
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();

            throw;
        }

        int matchCount = teamIds.Count * (teamIds.Count - 1) / 2;

        _logger.LogInformation(
            "Генерирана програма за турнир {TournamentId}: {Teams} отбора, {Matches} мача",
            id,
            teamIds.Count,
            matchCount);

        TempData["SuccessMessage"] =
            $"Генерирани са {matchCount} мача.";

        return RedirectToAction(nameof(Index));
    }

    // Circle method: the first team stays, the others rotate one
    // place every round. With an odd number of teams one slot is
    // empty (null) - the team facing it rests that round.
    // Returns an error text, or null when everything is saved.
    private async Task<string?> CreateRoundRobinAsync(
        Tournament tournament,
        List<int> teamIds,
        int days)
    {
        List<int?> slots = teamIds
            .Select(teamId => (int?)teamId)
            .ToList();

        if (slots.Count % 2 == 1)
        {
            slots.Add(null);
        }

        int count = slots.Count;
        int rounds = count - 1;

        // round up: 7 rounds in 3 days -> 3 rounds per day
        int roundsPerDay = (rounds + days - 1) / days;

        if (roundsPerDay > MaxRoundsPerDay)
        {
            return $"Турнирът е твърде кратък: за {teamIds.Count} отбора " +
                   $"трябват {rounds} кръга, а има само {days} дни.";
        }

        for (int round = 0; round < rounds; round++)
        {
            DateTime scheduledOn = tournament.StartDate.Date
                .AddDays(round / roundsPerDay)
                .AddHours(FirstMatchHour +
                          round % roundsPerDay * HoursBetweenRounds);

            for (int i = 0; i < count / 2; i++)
            {
                int? home = slots[i];
                int? away = slots[count - 1 - i];

                // the empty slot: this team has no match this round
                if (home == null || away == null)
                {
                    continue;
                }

                // the first team would always be at home otherwise
                if (i == 0 && round % 2 == 1)
                {
                    (home, away) = (away, home);
                }

                int homeTeamId = home.Value;
                int awayTeamId = away.Value;

                // same rule as MatchesController.Create:
                // a team can't play two matches at the same moment
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
                    return "Отбор вече има мач на " +
                           $"{scheduledOn:dd.MM.yyyy HH:mm}. " +
                           "Нищо не е записано.";
                }

                _context.Matches.Add(new Match
                {
                    TournamentId = tournament.Id,
                    HomeTeamId = homeTeamId,
                    AwayTeamId = awayTeamId,
                    ScheduledOn = scheduledOn
                });
            }

            // one save per round: a problem in round 3 has to undo
            // rounds 1 and 2 too - that is what the transaction is for
            await _context.SaveChangesAsync();

            // rotate: the last one moves to the second place
            int? last = slots[count - 1];

            slots.RemoveAt(count - 1);
            slots.Insert(1, last);
        }

        return null;
    }
}
