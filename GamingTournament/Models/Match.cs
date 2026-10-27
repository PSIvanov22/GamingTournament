using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GamingTournament.Models;

public class Match
{
    public int Id { get; set; }

    // two FKs to the same table (Teams) -
    // the mapping is in ApplicationDbContext
    public int HomeTeamId { get; set; }

    public Team? HomeTeam { get; set; }

    public int AwayTeamId { get; set; }

    public Team? AwayTeam { get; set; }

    public DateTime ScheduledOn { get; set; }

    // null until the result is entered
    [Range(0, 999)]
    public int? HomeScore { get; set; }

    [Range(0, 999)]
    public int? AwayScore { get; set; }

    public MatchStatus Status { get; set; } =
        MatchStatus.Scheduled;

    // computed, not a column.
    // null = no winner (not played yet or a draw)
    [NotMapped]
    public int? WinnerTeamId
    {
        get
        {
            if (Status != MatchStatus.Finished ||
                HomeScore == null ||
                AwayScore == null ||
                HomeScore == AwayScore)
            {
                return null;
            }

            return HomeScore > AwayScore
                ? HomeTeamId
                : AwayTeamId;
        }
    }
}
