namespace GamingTournament.Models;

// everything the statistics page and both exports (PDF, Excel) show.
// One object, so the page and the files can never disagree
public class ReportData
{
    public DateTime GeneratedOn { get; set; } = DateTime.Now;

    public int TeamCount { get; set; }

    public int PlayerCount { get; set; }

    public int TournamentCount { get; set; }

    public int FinishedMatchCount { get; set; }

    public int ScheduledMatchCount { get; set; }

    public List<MonthlyCount> MatchesByMonth { get; set; } = new();

    public List<GameStat> Games { get; set; } = new();

    public List<RequestMonthStat> RequestsByMonth { get; set; } = new();

    // for the dropdown, newest first
    public List<Tournament> Tournaments { get; set; } = new();

    public Tournament? SelectedTournament { get; set; }

    // table and match list of the selected tournament
    public List<StandingRow> Standings { get; set; } = new();

    public List<Match> Matches { get; set; } = new();

    // all teams with their coach and players (Excel sheet)
    public List<Team> Teams { get; set; } = new();
}

// one month on a chart; Month is always the 1st day
public class MonthlyCount
{
    public DateTime Month { get; set; }

    public int Count { get; set; }

    public string Label => Month.ToString("MM.yyyy");
}

// "most requested" analogue: which games are played the most
public class GameStat
{
    public string Game { get; set; } = string.Empty;

    public int Tournaments { get; set; }

    // cancelled matches are not counted
    public int Matches { get; set; }
}

// team + coach join requests of one month
public class RequestMonthStat
{
    public DateTime Month { get; set; }

    public int Approved { get; set; }

    public int Rejected { get; set; }

    public int Pending { get; set; }

    public string Label => Month.ToString("MM.yyyy");
}

public class StandingRow
{
    public const int WinPoints = 3;

    public const int DrawPoints = 1;

    public int TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;

    public int Played { get; set; }

    public int Wins { get; set; }

    public int Draws { get; set; }

    public int Losses { get; set; }

    public int GoalsFor { get; set; }

    public int GoalsAgainst { get; set; }

    public int GoalDifference => GoalsFor - GoalsAgainst;

    public int Points => Wins * WinPoints + Draws * DrawPoints;
}
