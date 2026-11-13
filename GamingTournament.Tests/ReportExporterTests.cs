using ClosedXML.Excel;
using GamingTournament.Models;
using GamingTournament.Services;

namespace GamingTournament.Tests;

public class ReportExporterTests
{
    private static ReportData SampleData()
    {
        Team alpha = new Team { Id = 1, Name = "Alpha" };
        Team beta = new Team { Id = 2, Name = "Beta" };

        List<Match> matches = new List<Match>
        {
            new Match
            {
                HomeTeamId = 1,
                AwayTeamId = 2,
                HomeTeam = alpha,
                AwayTeam = beta,
                HomeScore = 2,
                AwayScore = 1,
                Status = MatchStatus.Finished,
                ScheduledOn = new DateTime(2026, 1, 5, 18, 0, 0)
            }
        };

        return new ReportData
        {
            TeamCount = 2,
            FinishedMatchCount = 1,
            SelectedTournament = new Tournament
            {
                Id = 1,
                Name = "Winter Cup",
                Game = "CS2"
            },
            Matches = matches,
            Standings = ReportService.BuildStandings(matches),
            MatchesByMonth = new List<MonthlyCount>
            {
                new MonthlyCount
                {
                    Month = new DateTime(2026, 1, 1),
                    Count = 1
                }
            },
            Games = new List<GameStat>
            {
                new GameStat { Game = "CS2", Tournaments = 1, Matches = 1 }
            },
            Teams = new List<Team> { alpha, beta }
        };
    }

    [Fact]
    public void ToPdf_ReturnsAPdfFile()
    {
        byte[] file = ReportExporter.ToPdf(SampleData());

        // every PDF starts with "%PDF"
        Assert.True(file.Length > 4);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(file, 0, 4));
    }

    [Fact]
    public void ToPdf_WorksWithoutAnyData()
    {
        byte[] file = ReportExporter.ToPdf(new ReportData());

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(file, 0, 4));
    }

    [Fact]
    public void ToExcel_ContainsTheSheetsAndTheStandings()
    {
        byte[] file = ReportExporter.ToExcel(SampleData());

        using MemoryStream stream = new MemoryStream(file);
        using XLWorkbook workbook = new XLWorkbook(stream);

        Assert.True(workbook.Worksheets.Contains("Класиране"));
        Assert.True(workbook.Worksheets.Contains("Мачове"));
        Assert.True(workbook.Worksheets.Contains("Отбори"));

        IXLWorksheet standings = workbook.Worksheet("Класиране");

        // row 1 = headers, row 2 = the leader
        Assert.Equal("Alpha", standings.Cell(2, 2).GetString());

        // points are a real number, not text
        Assert.Equal(3, standings.Cell(2, 10).GetDouble());
    }

    [Fact]
    public void ToExcel_WorksWithoutAnyData()
    {
        byte[] file = ReportExporter.ToExcel(new ReportData());

        // an .xlsx is a zip archive: starts with "PK"
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(file, 0, 2));
    }
}
