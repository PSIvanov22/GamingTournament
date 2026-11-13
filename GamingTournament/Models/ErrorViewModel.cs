namespace GamingTournament.Models;

public class ErrorViewModel
{
    // shown to the user; the same id is in the log file
    // and in the ErrorLogs table
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public int StatusCode { get; set; } = 500;

    public string Title { get; set; } = "Нещо се обърка";

    public string Message { get; set; } =
        "Възникна неочаквана грешка. Опитай отново след малко.";

    // exception text - filled only in the Development environment
    public string? Details { get; set; }
}
