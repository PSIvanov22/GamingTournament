namespace GamingTournament.Models;

// everything the home page shows, in one object
// instead of a pile of ViewBag values
public class HomeIndexViewModel
{
    public int TeamCount { get; set; }

    // only users that are in a team, not every account
    public int PlayerCount { get; set; }

    public int FinishedMatchCount { get; set; }

    public int ScheduledMatchCount { get; set; }

    public List<Match> UpcomingMatches { get; set; }
        = new List<Match>();

    public List<Match> RecentResults { get; set; }
        = new List<Match>();
}
