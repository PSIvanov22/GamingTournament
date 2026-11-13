using GamingTournament.Models;
using GamingTournament.Services;

namespace GamingTournament.Tests;

public class ReportServiceTests
{
    // ----- BuildStandings: pure logic, no database -----

    private static Match Finished(
        int home, int away, int homeScore, int awayScore)
    {
        return new Match
        {
            HomeTeamId = home,
            AwayTeamId = away,
            HomeScore = homeScore,
            AwayScore = awayScore,
            Status = MatchStatus.Finished
        };
    }

    [Fact]
    public void BuildStandings_WinIsThreePoints_DrawIsOne()
    {
        List<Match> matches = new List<Match>
        {
            Finished(1, 2, 3, 1), // team 1 wins
            Finished(1, 3, 2, 2)  // draw
        };

        List<StandingRow> table = ReportService.BuildStandings(matches);

        StandingRow first = table.Single(row => row.TeamId == 1);

        Assert.Equal(2, first.Played);
        Assert.Equal(1, first.Wins);
        Assert.Equal(1, first.Draws);
        Assert.Equal(4, first.Points);
        Assert.Equal(5, first.GoalsFor);
        Assert.Equal(3, first.GoalsAgainst);

        Assert.Equal(1, table.Single(row => row.TeamId == 3).Points);
        Assert.Equal(0, table.Single(row => row.TeamId == 2).Points);
    }

    [Fact]
    public void BuildStandings_SortsByPointsThenGoalDifference()
    {
        // 1 and 2 both win once, but team 2 wins by more goals
        List<Match> matches = new List<Match>
        {
            Finished(1, 3, 1, 0),
            Finished(2, 4, 5, 0)
        };

        List<StandingRow> table = ReportService.BuildStandings(matches);

        Assert.Equal(2, table[0].TeamId);
        Assert.Equal(1, table[1].TeamId);
    }

    [Fact]
    public void BuildStandings_CancelledMatchIsIgnored()
    {
        List<Match> matches = new List<Match>
        {
            new Match
            {
                HomeTeamId = 1,
                AwayTeamId = 2,
                Status = MatchStatus.Cancelled
            }
        };

        Assert.Empty(ReportService.BuildStandings(matches));
    }

    [Fact]
    public void BuildStandings_ScheduledMatchAddsTeamsWithZeroPlayed()
    {
        List<Match> matches = new List<Match>
        {
            new Match { HomeTeamId = 1, AwayTeamId = 2 }
        };

        List<StandingRow> table = ReportService.BuildStandings(matches);

        Assert.Equal(2, table.Count);
        Assert.All(table, row => Assert.Equal(0, row.Played));
    }

    [Fact]
    public void BuildStandings_UsesTeamNameWhenLoaded()
    {
        Match match = Finished(1, 2, 1, 0);
        match.HomeTeam = new Team { Id = 1, Name = "Alpha" };

        List<StandingRow> table =
            ReportService.BuildStandings(new[] { match });

        Assert.Equal("Alpha", table.Single(row => row.TeamId == 1).TeamName);
    }

    // ----- queries: real SQL on in-memory SQLite -----

    private static Tournament NewTournament(string name, string game)
    {
        return new Tournament
        {
            Name = name,
            Game = game,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31)
        };
    }

    [Fact]
    public async Task GetMatchesByMonth_CountsOnlyFinishedMatches()
    {
        using TestDb db = new TestDb();

        Tournament tournament = NewTournament("Cup", "CS2");
        Team alpha = new Team { Name = "Alpha" };
        Team beta = new Team { Name = "Beta" };

        db.Context.AddRange(tournament, alpha, beta);
        db.Context.SaveChanges();

        void AddMatch(DateTime when, MatchStatus status)
        {
            Match match = new Match
            {
                TournamentId = tournament.Id,
                HomeTeamId = alpha.Id,
                AwayTeamId = beta.Id,
                ScheduledOn = when,
                Status = status
            };

            if (status == MatchStatus.Finished)
            {
                match.HomeScore = 1;
                match.AwayScore = 0;
            }

            db.Context.Matches.Add(match);
        }

        AddMatch(new DateTime(2026, 1, 5, 18, 0, 0), MatchStatus.Finished);
        AddMatch(new DateTime(2026, 1, 20, 18, 0, 0), MatchStatus.Finished);
        AddMatch(new DateTime(2026, 2, 3, 18, 0, 0), MatchStatus.Finished);
        AddMatch(new DateTime(2026, 2, 9, 18, 0, 0), MatchStatus.Scheduled);
        db.Context.SaveChanges();

        ReportService service = new ReportService(db.Context);

        List<MonthlyCount> months = await service.GetMatchesByMonthAsync();

        Assert.Equal(2, months.Count);
        Assert.Equal(new DateTime(2026, 1, 1), months[0].Month);
        Assert.Equal(2, months[0].Count);
        Assert.Equal(new DateTime(2026, 2, 1), months[1].Month);
        Assert.Equal(1, months[1].Count);
        Assert.Equal("01.2026", months[0].Label);
    }

    [Fact]
    public async Task GetGameStats_GroupsTournamentsByGame()
    {
        using TestDb db = new TestDb();

        Team alpha = new Team { Name = "Alpha" };
        Team beta = new Team { Name = "Beta" };
        Tournament first = NewTournament("Cup 1", "CS2");
        Tournament second = NewTournament("Cup 2", "CS2");
        Tournament third = NewTournament("Cup 3", "Dota 2");

        db.Context.AddRange(alpha, beta, first, second, third);
        db.Context.SaveChanges();

        db.Context.Matches.AddRange(
            new Match
            {
                TournamentId = first.Id,
                HomeTeamId = alpha.Id,
                AwayTeamId = beta.Id,
                ScheduledOn = new DateTime(2026, 3, 1, 18, 0, 0)
            },
            new Match
            {
                TournamentId = second.Id,
                HomeTeamId = alpha.Id,
                AwayTeamId = beta.Id,
                ScheduledOn = new DateTime(2026, 4, 1, 18, 0, 0)
            },
            new Match
            {
                TournamentId = third.Id,
                HomeTeamId = alpha.Id,
                AwayTeamId = beta.Id,
                ScheduledOn = new DateTime(2026, 5, 1, 18, 0, 0),
                Status = MatchStatus.Cancelled
            });
        db.Context.SaveChanges();

        List<GameStat> games =
            await new ReportService(db.Context).GetGameStatsAsync();

        Assert.Equal(2, games.Count);

        // most matches first; the cancelled match is not counted
        Assert.Equal("CS2", games[0].Game);
        Assert.Equal(2, games[0].Tournaments);
        Assert.Equal(2, games[0].Matches);

        Assert.Equal("Dota 2", games[1].Game);
        Assert.Equal(0, games[1].Matches);
    }

    [Fact]
    public async Task GetRequestsByMonth_CombinesPlayerAndCoachRequests()
    {
        using TestDb db = new TestDb();

        Team team = new Team { Name = "Alpha" };
        ApplicationUser player = new ApplicationUser
        {
            UserName = "player@test.com",
            Email = "player@test.com"
        };

        db.Context.AddRange(team, player);
        db.Context.SaveChanges();

        db.Context.TeamJoinRequests.Add(new TeamJoinRequest
        {
            PlayerId = player.Id,
            TeamId = team.Id,
            Status = "Approved",
            CreatedOn = new DateTime(2026, 1, 5)
        });

        db.Context.CoachRequests.Add(new CoachRequest
        {
            PlayerId = player.Id,
            TeamId = team.Id,
            Status = "Rejected",
            CreatedOn = new DateTime(2026, 1, 9)
        });
        db.Context.SaveChanges();

        List<RequestMonthStat> months =
            await new ReportService(db.Context).GetRequestsByMonthAsync();

        RequestMonthStat january = Assert.Single(months);

        Assert.Equal(1, january.Approved);
        Assert.Equal(1, january.Rejected);
        Assert.Equal(0, january.Pending);
    }

    [Fact]
    public async Task Build_WithoutId_SelectsTheNewestTournament()
    {
        using TestDb db = new TestDb();

        Tournament older = NewTournament("Old Cup", "CS2");
        Tournament newer = NewTournament("New Cup", "CS2");
        newer.StartDate = new DateTime(2026, 6, 1);

        db.Context.AddRange(older, newer);
        db.Context.SaveChanges();

        ReportData data =
            await new ReportService(db.Context).BuildAsync(null);

        Assert.Equal("New Cup", data.SelectedTournament!.Name);
    }

    [Fact]
    public async Task Build_UnknownTournamentId_SelectsNothing()
    {
        using TestDb db = new TestDb();

        ReportData data =
            await new ReportService(db.Context).BuildAsync(999);

        Assert.Null(data.SelectedTournament);
        Assert.Empty(data.Standings);
    }
}
