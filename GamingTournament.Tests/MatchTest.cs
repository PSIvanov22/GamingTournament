using GamingTournament.Models;

namespace GamingTournament.Tests;

public class MatchTests
{
    [Fact]
    public void WinnerTeamId_HomeScoresMore_ReturnsHomeTeam()
    {
        var match = new Match
        {
            HomeTeamId = 1, AwayTeamId = 2,
            HomeScore = 3, AwayScore = 1,
            Status = MatchStatus.Finished
        };

        Assert.Equal(1, match.WinnerTeamId);
    }

    [Fact]
    public void WinnerTeamId_AwayScoresMore_ReturnsAwayTeam()
    {
        var match = new Match
        {
            HomeTeamId = 1, AwayTeamId = 2,
            HomeScore = 0, AwayScore = 2,
            Status = MatchStatus.Finished
        };

        Assert.Equal(2, match.WinnerTeamId);
    }

    [Fact]
    public void WinnerTeamId_Draw_ReturnsNull()
    {
        var match = new Match
        {
            HomeScore = 2, AwayScore = 2,
            Status = MatchStatus.Finished
        };

        Assert.Null(match.WinnerTeamId);
    }

    [Theory]
    [InlineData(MatchStatus.Scheduled)]
    [InlineData(MatchStatus.Cancelled)]
    public void WinnerTeamId_NotFinished_ReturnsNull(MatchStatus status)
    {
        var match = new Match
        {
            HomeScore = 5, AwayScore = 0,
            Status = status
        };

        Assert.Null(match.WinnerTeamId);
    }
}