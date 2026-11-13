using System.ComponentModel.DataAnnotations;

namespace GamingTournament.Models;

// one row per unhandled exception, written by
// ExceptionHandlingMiddleware (the admin reads them in /ErrorLogs)
public class ErrorLog
{
    public int Id { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.Now;

    // the same id is shown to the user on the error page,
    // so a screenshot is enough to find the row and the log line
    [Required]
    [StringLength(100)]
    public string TraceId { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    [Required]
    [StringLength(200)]
    public string ExceptionType { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Message { get; set; } = string.Empty;

    public string? StackTrace { get; set; }

    [Required]
    [StringLength(500)]
    public string Path { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Method { get; set; } = string.Empty;

    // null for guests
    [StringLength(256)]
    public string? UserName { get; set; }
}
