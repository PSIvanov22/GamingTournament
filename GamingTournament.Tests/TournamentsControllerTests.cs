using GamingTournament.Controllers;
using GamingTournament.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GamingTournament.Tests;

// GenerateMatches: round-robin schedule inside a transaction
public class TournamentsControllerTests
{
    private static TournamentsController CreateController(TestDb db)
    {
        TournamentsController controller = new TournamentsController(
            db.Context,
            NullLogger<TournamentsController>.Instance);

        // FakeTempDataProvider lives in MatchesControllerTests.cs
        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), new FakeTempDataProvider());

        return controller;
    }

    private static Tournament AddTournament(
        TestDb db, string name, int days)
    {
        Tournament tournament = new Tournament
        {
            Name = name,
            Game = "CS2",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 1).AddDays(days - 1)
        };

        db.Context.Tournaments.Add(tournament);
        db.Context.SaveChanges();

        return tournament;
    }

    private static List<Team> AddTeams(TestDb db, int count)
    {
        List<Team> teams = Enumerable.Range(1, count)
            .Select(number => new Team { Name = "Team " + number })
            .ToList();

        db.Context.Teams.AddRange(teams);
        db.Context.SaveChanges();

        return teams;
    }

    [Fact]
    public async Task GenerateMatches_FourTeams_EveryPairPlaysOnce()
    {
        using TestDb db = new TestDb();
        Tournament tournament = AddTournament(db, "Cup", days: 10);
        AddTeams(db, 4);

        IActionResult result = await CreateController(db)
            .GenerateMatches(tournament.Id);

        Assert.IsType<RedirectToActionResult>(result);

        List<Match> matches = await db.Context.Matches.ToListAsync();

        // 4 teams -> 6 different pairs
        Assert.Equal(6, matches.Count);

        int distinctPairs = matches
            .Select(match => (
                Math.Min(match.HomeTeamId, match.AwayTeamId),
                Math.Max(match.HomeTeamId, match.AwayTeamId)))
            .Distinct()
            .Count();

        Assert.Equal(6, distinctPairs);

        // all inside the tournament dates, nobody plays against himself
        Assert.All(matches, match =>
        {
            Assert.True(match.ScheduledOn.Date >= tournament.StartDate);
            Assert.True(match.ScheduledOn.Date <= tournament.EndDate);
            Assert.NotEqual(match.HomeTeamId, match.AwayTeamId);
            Assert.Equal(MatchStatus.Scheduled, match.Status);
        });
    }

    [Fact]
    public async Task GenerateMatches_NoTeamPlaysTwiceAtTheSameTime()
    {
        using TestDb db = new TestDb();
        Tournament tournament = AddTournament(db, "Cup", days: 10);
        AddTeams(db, 6);

        await CreateController(db).GenerateMatches(tournament.Id);

        List<Match> matches = await db.Context.Matches.ToListAsync();

        // (team, time) pairs: home and away side together
        var slots = matches
            .SelectMany(match => new[]
            {
                (match.HomeTeamId, match.ScheduledOn),
                (match.AwayTeamId, match.ScheduledOn)
            })
            .ToList();

        Assert.Equal(slots.Count, slots.Distinct().Count());
    }

    [Fact]
    public async Task GenerateMatches_OddNumberOfTeams_OneRestsEachRound()
    {
        using TestDb db = new TestDb();
        Tournament tournament = AddTournament(db, "Cup", days: 10);
        AddTeams(db, 5);

        await CreateController(db).GenerateMatches(tournament.Id);

        // 5 teams -> 10 pairs
        Assert.Equal(10, await db.Context.Matches.CountAsync());
    }

    [Fact]
    public async Task GenerateMatches_TournamentTooShort_SavesNothing()
    {
        using TestDb db = new TestDb();

        // 8 teams need 7 rounds; one day fits at most 3
        Tournament tournament = AddTournament(db, "Short Cup", days: 1);
        AddTeams(db, 8);

        TournamentsController controller = CreateController(db);

        await controller.GenerateMatches(tournament.Id);

        Assert.Equal(0, await db.Context.Matches.CountAsync());
        Assert.NotNull(controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task GenerateMatches_TournamentAlreadyHasMatches_IsRefused()
    {
        using TestDb db = new TestDb();
        Tournament tournament = AddTournament(db, "Cup", days: 10);
        List<Team> teams = AddTeams(db, 4);

        db.Context.Matches.Add(new Match
        {
            TournamentId = tournament.Id,
            HomeTeamId = teams[0].Id,
            AwayTeamId = teams[1].Id,
            ScheduledOn = new DateTime(2026, 1, 3, 18, 0, 0)
        });
        db.Context.SaveChanges();

        TournamentsController controller = CreateController(db);

        await controller.GenerateMatches(tournament.Id);

        Assert.Equal(1, await db.Context.Matches.CountAsync());
        Assert.NotNull(controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task GenerateMatches_UnknownTournament_ReturnsNotFound()
    {
        using TestDb db = new TestDb();
        AddTeams(db, 4);

        IActionResult result =
            await CreateController(db).GenerateMatches(999);

        Assert.IsType<NotFoundResult>(result);
    }

    // the transaction test: round 1 is already saved when round 2
    // hits a conflict - the whole schedule must be rolled back
    [Fact]
    public async Task GenerateMatches_ConflictInLaterRound_RollsBackEverything()
    {
        using TestDb db = new TestDb();

        Tournament tournament = AddTournament(db, "Winter Cup", days: 10);
        Tournament other = AddTournament(db, "Other Cup", days: 10);
        List<Team> teams = AddTeams(db, 4);

        // Team 1 already plays on day 2 at 18:00 (the time of round 2)
        db.Context.Matches.Add(new Match
        {
            TournamentId = other.Id,
            HomeTeamId = teams[0].Id,
            AwayTeamId = teams[1].Id,
            ScheduledOn = new DateTime(2026, 1, 2, 18, 0, 0)
        });
        db.Context.SaveChanges();

        TournamentsController controller = CreateController(db);

        IActionResult result =
            await controller.GenerateMatches(tournament.Id);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(controller.TempData["ErrorMessage"]);

        // round 1 was saved first, but the rollback removed it again
        Assert.Equal(0, await db.Context.Matches
            .CountAsync(match => match.TournamentId == tournament.Id));

        // only the match that existed before is left
        Assert.Equal(1, await db.Context.Matches.CountAsync());
    }
}
