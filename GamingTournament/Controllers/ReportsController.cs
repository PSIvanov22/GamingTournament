using GamingTournament.Models;
using GamingTournament.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamingTournament.Controllers;

// statistics and exports - only for the administrator
[Authorize(Roles = "Admin")]
public class ReportsController : Controller
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IReportService _reports;

    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IReportService reports,
        ILogger<ReportsController> logger)
    {
        _reports = reports;
        _logger = logger;
    }

    // tournamentId: which tournament the table and the match list
    // are about; without it the newest tournament is used
    public async Task<IActionResult> Index(int? tournamentId)
    {
        ReportData data = await _reports.BuildAsync(tournamentId);

        if (tournamentId != null && data.SelectedTournament == null)
        {
            return NotFound();
        }

        return View(data);
    }

    public async Task<IActionResult> ExportPdf(int? tournamentId)
    {
        ReportData data = await _reports.BuildAsync(tournamentId);

        if (tournamentId != null && data.SelectedTournament == null)
        {
            return NotFound();
        }

        byte[] file = ReportExporter.ToPdf(data);

        _logger.LogInformation(
            "Експорт PDF: {User}, турнир {TournamentId}, {Bytes} байта",
            User.Identity?.Name,
            data.SelectedTournament?.Id,
            file.Length);

        return File(
            file,
            "application/pdf",
            $"report-{DateTime.Now:yyyyMMdd-HHmm}.pdf");
    }

    public async Task<IActionResult> ExportExcel(int? tournamentId)
    {
        ReportData data = await _reports.BuildAsync(tournamentId);

        if (tournamentId != null && data.SelectedTournament == null)
        {
            return NotFound();
        }

        byte[] file = ReportExporter.ToExcel(data);

        _logger.LogInformation(
            "Експорт Excel: {User}, турнир {TournamentId}, {Bytes} байта",
            User.Identity?.Name,
            data.SelectedTournament?.Id,
            file.Length);

        return File(
            file,
            ExcelContentType,
            $"report-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }
}
