using ClosedXML.Excel;
using GamingTournament.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GamingTournament.Services;

// turns a ReportData into a PDF (QuestPDF) or an Excel file (ClosedXML).
// Static and without a database: it only formats what it is given
public static class ReportExporter
{
    static ReportExporter()
    {
        // QuestPDF is free for non-commercial / small projects, but the
        // license has to be chosen once before the first document
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ---------------------------------------------------------- PDF

    public static byte[] ToPdf(ReportData data)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);

                // Arial has Cyrillic; the default QuestPDF font may not
                page.DefaultTextStyle(style =>
                    style.FontSize(10).FontFamily("Arial"));

                page.Header().Column(column =>
                {
                    column.Item()
                        .Text("GamingTournament - справка")
                        .FontSize(18)
                        .Bold();

                    column.Item()
                        .Text($"Генерирана на {data.GeneratedOn:dd.MM.yyyy HH:mm}")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(10).Column(column =>
                {
                    column.Spacing(14);

                    AddTable(
                        column,
                        "Обобщение",
                        new[] { "Показател", "Стойност" },
                        new float[] { 3, 1 },
                        SummaryRows(data)
                            .Select(row => new[]
                            {
                                row[0]!.ToString()!,
                                row[1]!.ToString()!
                            })
                            .ToList());

                    AddTable(
                        column,
                        "Изиграни мачове по месеци",
                        new[] { "Месец", "Мачове" },
                        new float[] { 2, 1 },
                        data.MatchesByMonth
                            .Select(item => new[]
                            {
                                item.Label,
                                item.Count.ToString()
                            })
                            .ToList());

                    AddTable(
                        column,
                        "Най-играни игри",
                        new[] { "Игра", "Турнири", "Мачове" },
                        new float[] { 3, 1, 1 },
                        data.Games
                            .Select(item => new[]
                            {
                                item.Game,
                                item.Tournaments.ToString(),
                                item.Matches.ToString()
                            })
                            .ToList());

                    AddTable(
                        column,
                        "Заявки по месеци (играчи и треньори)",
                        new[] { "Месец", "Одобрени", "Отхвърлени", "Чакащи" },
                        new float[] { 2, 1, 1, 1 },
                        data.RequestsByMonth
                            .Select(item => new[]
                            {
                                item.Label,
                                item.Approved.ToString(),
                                item.Rejected.ToString(),
                                item.Pending.ToString()
                            })
                            .ToList());

                    string tournamentName =
                        data.SelectedTournament?.Name ?? "-";

                    AddTable(
                        column,
                        $"Класиране - {tournamentName}",
                        new[] { "#", "Отбор", "М", "П", "Р", "З", "Голове", "Точки" },
                        new float[] { 0.5f, 3, 0.7f, 0.7f, 0.7f, 0.7f, 1.2f, 1 },
                        data.Standings
                            .Select((row, index) => new[]
                            {
                                (index + 1).ToString(),
                                row.TeamName,
                                row.Played.ToString(),
                                row.Wins.ToString(),
                                row.Draws.ToString(),
                                row.Losses.ToString(),
                                $"{row.GoalsFor}:{row.GoalsAgainst}",
                                row.Points.ToString()
                            })
                            .ToList());

                    AddTable(
                        column,
                        $"Мачове - {tournamentName}",
                        new[] { "Дата", "Домакин", "Резултат", "Гост", "Статус" },
                        new float[] { 1.6f, 2.2f, 1, 2.2f, 1.4f },
                        data.Matches
                            .Select(match => new[]
                            {
                                match.ScheduledOn.ToString("dd.MM.yyyy HH:mm"),
                                match.HomeTeam?.Name ?? "-",
                                ScoreText(match),
                                match.AwayTeam?.Name ?? "-",
                                StatusText(match)
                            })
                            .ToList());
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Страница ");
                    text.CurrentPageNumber();
                    text.Span(" от ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void AddTable(
        ColumnDescriptor column,
        string title,
        string[] headers,
        float[] widths,
        List<string[]> rows)
    {
        column.Item().Text(title).FontSize(13).Bold();

        if (rows.Count == 0)
        {
            column.Item()
                .Text("Няма данни.")
                .FontColor(Colors.Grey.Darken1);

            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (float width in widths)
                {
                    columns.RelativeColumn(width);
                }
            });

            table.Header(header =>
            {
                foreach (string text in headers)
                {
                    header.Cell()
                        .Background(Colors.Grey.Lighten2)
                        .Padding(4)
                        .Text(text)
                        .Bold();
                }
            });

            foreach (string[] row in rows)
            {
                foreach (string value in row)
                {
                    table.Cell()
                        .BorderBottom(0.5f)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(4)
                        .Text(value);
                }
            }
        });
    }

    // --------------------------------------------------------- Excel

    public static byte[] ToExcel(ReportData data)
    {
        using XLWorkbook workbook = new XLWorkbook();

        WriteSheet(
            workbook,
            "Обобщение",
            new[] { "Показател", "Стойност" },
            SummaryRows(data));

        WriteSheet(
            workbook,
            "Класиране",
            new[] { "#", "Отбор", "Мачове", "Победи", "Равенства",
                    "Загуби", "Вкарани", "Получени", "Разлика", "Точки" },
            data.Standings.Select((row, index) => new object?[]
            {
                index + 1,
                row.TeamName,
                row.Played,
                row.Wins,
                row.Draws,
                row.Losses,
                row.GoalsFor,
                row.GoalsAgainst,
                row.GoalDifference,
                row.Points
            }));

        WriteSheet(
            workbook,
            "Мачове",
            new[] { "Дата", "Турнир", "Домакин", "Резултат",
                    "Гост", "Статус" },
            data.Matches.Select(match => new object?[]
            {
                match.ScheduledOn,
                data.SelectedTournament?.Name,
                match.HomeTeam?.Name,
                ScoreText(match),
                match.AwayTeam?.Name,
                StatusText(match)
            }));

        WriteSheet(
            workbook,
            "По месеци",
            new[] { "Месец", "Изиграни мачове" },
            data.MatchesByMonth.Select(item => new object?[]
            {
                item.Label,
                item.Count
            }));

        WriteSheet(
            workbook,
            "Игри",
            new[] { "Игра", "Турнири", "Мачове" },
            data.Games.Select(item => new object?[]
            {
                item.Game,
                item.Tournaments,
                item.Matches
            }));

        WriteSheet(
            workbook,
            "Заявки",
            new[] { "Месец", "Одобрени", "Отхвърлени", "Чакащи" },
            data.RequestsByMonth.Select(item => new object?[]
            {
                item.Label,
                item.Approved,
                item.Rejected,
                item.Pending
            }));

        WriteSheet(
            workbook,
            "Отбори",
            new[] { "Отбор", "Държава", "Основан", "Треньор", "Играчи" },
            data.Teams.Select(team => new object?[]
            {
                team.Name,
                team.Country,
                team.FoundedYear,
                team.Coach?.UserName,
                string.Join(", ", team.Members
                    .Select(member => member.UserName))
            }));

        using MemoryStream stream = new MemoryStream();

        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    private static void WriteSheet(
        XLWorkbook workbook,
        string name,
        string[] headers,
        IEnumerable<object?[]> rows)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add(name);

        for (int column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }

        IXLRange headerRow = sheet.Range(1, 1, 1, headers.Length);

        headerRow.Style.Font.Bold = true;
        headerRow.Style.Font.FontColor = XLColor.White;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#7c5cff");

        int rowNumber = 2;

        foreach (object?[] row in rows)
        {
            for (int column = 0; column < row.Length; column++)
            {
                SetCell(sheet.Cell(rowNumber, column + 1), row[column]);
            }

            rowNumber++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    // numbers stay numbers and dates stay dates, so Excel can
    // sort and sum them - not everything as text
    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;

            case int number:
                cell.Value = number;
                break;

            case DateTime date:
                cell.Value = date;
                cell.Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
                break;

            default:
                cell.Value = value.ToString() ?? "";
                break;
        }
    }

    // -------------------------------------------------------- shared

    private static List<object?[]> SummaryRows(ReportData data)
    {
        return new List<object?[]>
        {
            new object?[] { "Отбори", data.TeamCount },
            new object?[] { "Играчи в отбори", data.PlayerCount },
            new object?[] { "Турнири", data.TournamentCount },
            new object?[] { "Изиграни мачове", data.FinishedMatchCount },
            new object?[] { "Предстоящи мачове", data.ScheduledMatchCount }
        };
    }

    private static string ScoreText(Match match)
    {
        return match.Status == MatchStatus.Finished
            ? $"{match.HomeScore}:{match.AwayScore}"
            : "-";
    }

    private static string StatusText(Match match)
    {
        return match.Status switch
        {
            MatchStatus.Scheduled => "Предстоящ",
            MatchStatus.Cancelled => "Отменен",
            _ => match.WinnerTeamId == null ? "Равен" : "Приключил"
        };
    }
}
