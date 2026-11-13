using GamingTournament.Data;
using GamingTournament.Models;
using Microsoft.EntityFrameworkCore;

namespace GamingTournament.Services;

public interface IReportService
{
    // tournamentId == null -> the newest tournament
    Task<ReportData> BuildAsync(int? tournamentId);
}

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;

    public ReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportData> BuildAsync(int? tournamentId)
    {
        ReportData data = new ReportData
        {
            TeamCount = await _context.Teams.CountAsync(),

            PlayerCount = await _context.Users
                .CountAsync(user => user.TeamId != null),

            TournamentCount = await _context.Tournaments.CountAsync(),

            FinishedMatchCount = await _context.Matches
                .CountAsync(match =>
                    match.Status == MatchStatus.Finished),

            ScheduledMatchCount = await _context.Matches
                .CountAsync(match =>
                    match.Status == MatchStatus.Scheduled)
        };

        data.MatchesByMonth = await GetMatchesByMonthAsync();
        data.Games = await GetGameStatsAsync();
        data.RequestsByMonth = await GetRequestsByMonthAsync();

        data.Tournaments = await _context.Tournaments
            .AsNoTracking()
            .OrderByDescending(tournament => tournament.StartDate)
            .ToListAsync();

        data.SelectedTournament = tournamentId == null
            ? data.Tournaments.FirstOrDefault()
            : data.Tournaments.FirstOrDefault(
                tournament => tournament.Id == tournamentId);

        if (data.SelectedTournament != null)
        {
            int selectedId = data.SelectedTournament.Id;

            data.Matches = await _context.Matches
                .AsNoTracking()
                .Include(match => match.HomeTeam)
                .Include(match => match.AwayTeam)
                .Where(match => match.TournamentId == selectedId)
                .OrderBy(match => match.ScheduledOn)
                .ToListAsync();

            data.Standings = BuildStandings(data.Matches);
        }

        data.Teams = await _context.Teams
            .AsNoTracking()
            .Include(team => team.Coach)
            .Include(team => team.Members)
            .OrderBy(team => team.Name)
            .ToListAsync();

        return data;
    }

    // Pure calculation (no database), so it is easy to unit test.
    // 3 points for a win, 1 for a draw. Cancelled matches are ignored,
    // scheduled ones only add the team to the table with 0 played.
    public static List<StandingRow> BuildStandings(
        IEnumerable<Match> matches)
    {
        Dictionary<int, StandingRow> rows =
            new Dictionary<int, StandingRow>();

        foreach (Match match in matches)
        {
            if (match.Status == MatchStatus.Cancelled)
            {
                continue;
            }

            StandingRow home = GetRow(
                rows, match.HomeTeamId, match.HomeTeam?.Name);

            StandingRow away = GetRow(
                rows, match.AwayTeamId, match.AwayTeam?.Name);

            if (match.Status != MatchStatus.Finished ||
                match.HomeScore == null ||
                match.AwayScore == null)
            {
                continue;
            }

            int homeScore = match.HomeScore.Value;
            int awayScore = match.AwayScore.Value;

            home.Played++;
            away.Played++;

            home.GoalsFor += homeScore;
            home.GoalsAgainst += awayScore;
            away.GoalsFor += awayScore;
            away.GoalsAgainst += homeScore;

            if (homeScore > awayScore)
            {
                home.Wins++;
                away.Losses++;
            }
            else if (homeScore < awayScore)
            {
                away.Wins++;
                home.Losses++;
            }
            else
            {
                home.Draws++;
                away.Draws++;
            }
        }

        return rows.Values
            .OrderByDescending(row => row.Points)
            .ThenByDescending(row => row.GoalDifference)
            .ThenByDescending(row => row.GoalsFor)
            .ThenBy(row => row.TeamName)
            .ToList();
    }

    private static StandingRow GetRow(
        Dictionary<int, StandingRow> rows,
        int teamId,
        string? teamName)
    {
        if (!rows.TryGetValue(teamId, out StandingRow? row))
        {
            row = new StandingRow
            {
                TeamId = teamId,
                TeamName = teamName ?? ("Отбор " + teamId)
            };

            rows[teamId] = row;
        }

        return row;
    }

    // finished matches per month (by the scheduled date).
    // Grouped in memory: SQLite can't group by year+month in SQL
    public async Task<List<MonthlyCount>> GetMatchesByMonthAsync()
    {
        List<DateTime> dates = await _context.Matches
            .AsNoTracking()
            .Where(match => match.Status == MatchStatus.Finished)
            .Select(match => match.ScheduledOn)
            .ToListAsync();

        return dates
            .GroupBy(date => new DateTime(date.Year, date.Month, 1))
            .OrderBy(group => group.Key)
            .Select(group => new MonthlyCount
            {
                Month = group.Key,
                Count = group.Count()
            })
            .ToList();
    }

    // which games are played the most
    public async Task<List<GameStat>> GetGameStatsAsync()
    {
        var tournaments = await _context.Tournaments
            .AsNoTracking()
            .Select(tournament => new
            {
                tournament.Game,
                MatchCount = tournament.Matches.Count(match =>
                    match.Status != MatchStatus.Cancelled)
            })
            .ToListAsync();

        return tournaments
            .GroupBy(item => item.Game)
            .Select(group => new GameStat
            {
                Game = group.Key,
                Tournaments = group.Count(),
                Matches = group.Sum(item => item.MatchCount)
            })
            .OrderByDescending(stat => stat.Matches)
            .ThenBy(stat => stat.Game)
            .ToList();
    }

    // player join requests and coach requests together, per month
    public async Task<List<RequestMonthStat>> GetRequestsByMonthAsync()
    {
        var joinRequests = await _context.TeamJoinRequests
            .AsNoTracking()
            .Select(request => new
            {
                request.CreatedOn,
                request.Status
            })
            .ToListAsync();

        var coachRequests = await _context.CoachRequests
            .AsNoTracking()
            .Select(request => new
            {
                request.CreatedOn,
                request.Status
            })
            .ToListAsync();

        return joinRequests
            .Concat(coachRequests)
            .GroupBy(request => new DateTime(
                request.CreatedOn.Year,
                request.CreatedOn.Month,
                1))
            .OrderBy(group => group.Key)
            .Select(group => new RequestMonthStat
            {
                Month = group.Key,
                Approved = group.Count(item => item.Status == "Approved"),
                Rejected = group.Count(item => item.Status == "Rejected"),
                Pending = group.Count(item => item.Status == "Pending")
            })
            .ToList();
    }
}
