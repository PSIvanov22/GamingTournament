namespace GamingTournament.Models;

// enum instead of "Pending"-style strings:
// a typo here won't compile
public enum MatchStatus
{
    Scheduled,
    Finished,
    Cancelled
}
