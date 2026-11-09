using GamingTournament.Controllers;
using GamingTournament.Data;
using GamingTournament.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GamingTournament.Tests;

public class MatchesControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;

    public MatchesControllerTests()
    {
        // the database lives only as long as this connection is open
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private MatchesController CreateController()
    {
        var controller = new MatchesController(_context);

        // the controller writes to TempData, which is null in a test
        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), new FakeTempDataProvider());

        return controller;
    }

    // one tournament (1-10 Jan 2026) and two teams
    private (Tournament, Team, Team) SeedBasics()
    {
        var tournament = new Tournament
        {
            Name = "Winter Cup", Game = "CS2",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 10)
        };
        var teamA = new Team { Name = "Alpha" };
        var teamB = new Team { Name = "Beta" };

        _context.AddRange(tournament, teamA, teamB);
        _context.SaveChanges();

        return (tournament, teamA, teamB);
    }

    [Fact]
    public async Task Create_ValidMatch_SavesAndRedirects()
    {
        var (tournament, a, b) = SeedBasics();
        var controller = CreateController();

        var model = new MatchCreateViewModel
        {
            TournamentId = tournament.Id,
            HomeTeamId = a.Id,
            AwayTeamId = b.Id,
            ScheduledOn = new DateTime(2026, 1, 5, 18, 0, 0)
        };

        var result = await controller.Create(model);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, await _context.Matches.CountAsync());
    }

    [Fact]
    public async Task Create_SameTeamTwice_ReturnsViewWithError()
    {
        var (tournament, a, _) = SeedBasics();
        var controller = CreateController();

        var model = new MatchCreateViewModel
        {
            TournamentId = tournament.Id,
            HomeTeamId = a.Id,
            AwayTeamId = a.Id,
            ScheduledOn = new DateTime(2026, 1, 5, 18, 0, 0)
        };

        var result = await controller.Create(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(0, await _context.Matches.CountAsync());
    }

    [Fact]
    public async Task Create_DateOutsideTournament_ReturnsViewWithError()
    {
        var (tournament, a, b) = SeedBasics();
        var controller = CreateController();

        var model = new MatchCreateViewModel
        {
            TournamentId = tournament.Id,
            HomeTeamId = a.Id,
            AwayTeamId = b.Id,
            ScheduledOn = new DateTime(2026, 2, 1, 18, 0, 0)
        };

        var result = await controller.Create(model);

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.ContainsKey(
            nameof(model.ScheduledOn)));
        Assert.Equal(0, await _context.Matches.CountAsync());
    }

    [Fact]
    public async Task Cancel_ScheduledMatch_SetsStatusCancelled()
    {
        var (tournament, a, b) = SeedBasics();
        var match = new Match
        {
            TournamentId = tournament.Id,
            HomeTeamId = a.Id, AwayTeamId = b.Id,
            ScheduledOn = new DateTime(2026, 1, 5, 18, 0, 0)
        };
        _context.Matches.Add(match);
        _context.SaveChanges();

        var result = await CreateController().Cancel(match.Id);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(MatchStatus.Cancelled,
            (await _context.Matches.FindAsync(match.Id))!.Status);
    }

    [Fact]
    public async Task Cancel_FinishedMatch_DoesNotChangeStatus()
    {
        var (tournament, a, b) = SeedBasics();
        var match = new Match
        {
            TournamentId = tournament.Id,
            HomeTeamId = a.Id, AwayTeamId = b.Id,
            ScheduledOn = new DateTime(2026, 1, 5, 18, 0, 0),
            HomeScore = 1, AwayScore = 0,
            Status = MatchStatus.Finished
        };
        _context.Matches.Add(match);
        _context.SaveChanges();

        await CreateController().Cancel(match.Id);

        Assert.Equal(MatchStatus.Finished,
            (await _context.Matches.FindAsync(match.Id))!.Status);
    }

    [Fact]
    public async Task Cancel_UnknownId_ReturnsNotFound()
    {
        var result = await CreateController().Cancel(999);

        Assert.IsType<NotFoundResult>(result);
    }
}

// minimal stand-in so TempData works without a web host
class FakeTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context)
        => new Dictionary<string, object>();

    public void SaveTempData(
        HttpContext context, IDictionary<string, object> values) { }
}