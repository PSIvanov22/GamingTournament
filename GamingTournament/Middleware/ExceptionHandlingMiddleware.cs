using GamingTournament.Data;
using GamingTournament.Models;
using Microsoft.EntityFrameworkCore;

namespace GamingTournament.Middleware;

// global error handling: every exception that escapes a controller
// ends up here. It (1) writes it to the log file, (2) saves it in the
// ErrorLogs table and (3) shows the user a friendly page instead of
// a stack trace. Must be registered first in Program.cs.
public class ExceptionHandlingMiddleware
{
    // HomeController.Error reads the prepared model from here
    public const string ErrorItemKey = "GamingTournament.ErrorInfo";

    private const string ErrorPath = "/Home/Error";

    private readonly RequestDelegate _next;

    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private readonly IServiceScopeFactory _scopeFactory;

    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            // the browser closed the tab mid-request - not a bug
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                // headers are already sent, a new page is impossible
                _logger.LogError(
                    exception,
                    "Грешка след началото на отговора: {Path}",
                    context.Request.Path);

                throw;
            }

            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(
        HttpContext context,
        Exception exception)
    {
        string traceId = context.TraceIdentifier;

        (int statusCode, string title, string message) =
            Classify(exception);

        _logger.LogError(
            exception,
            "Необработена грешка {TraceId}: {Method} {Path}",
            traceId,
            context.Request.Method,
            context.Request.Path);

        await SaveToDatabaseAsync(
            context, exception, statusCode, traceId);

        ErrorViewModel model = new ErrorViewModel
        {
            RequestId = traceId,
            StatusCode = statusCode,
            Title = title,
            Message = message,

            // technical details only on the developer's machine
            Details = _environment.IsDevelopment()
                ? exception.ToString()
                : null
        };

        await ReExecuteAsync(context, model);
    }

    // runs the pipeline again for /Home/Error, the same idea as
    // the built-in UseExceptionHandler, but with our own model
    private async Task ReExecuteAsync(
        HttpContext context,
        ErrorViewModel model)
    {
        PathString originalPath = context.Request.Path;
        QueryString originalQuery = context.Request.QueryString;
        string originalMethod = context.Request.Method;

        try
        {
            context.Items[ErrorItemKey] = model;

            context.Response.Clear();
            context.Response.StatusCode = model.StatusCode;

            context.Request.Path = ErrorPath;
            context.Request.QueryString = QueryString.Empty;
            context.Request.Method = HttpMethods.Get;

            // routing skips matching when an endpoint is already set
            context.SetEndpoint(null);

            await _next(context);
        }
        finally
        {
            context.Request.Path = originalPath;
            context.Request.QueryString = originalQuery;
            context.Request.Method = originalMethod;
        }
    }

    // a NEW scope and DbContext: the request's own context may be
    // exactly what failed and can still hold broken entities
    private async Task SaveToDatabaseAsync(
        HttpContext context,
        Exception exception,
        int statusCode,
        string traceId)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();

            ApplicationDbContext database = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            database.ErrorLogs.Add(new ErrorLog
            {
                CreatedOn = DateTime.Now,
                TraceId = traceId,
                StatusCode = statusCode,
                ExceptionType = Truncate(
                    exception.GetType().Name, 200),
                Message = Truncate(exception.Message, 2000),
                StackTrace = Truncate(exception.ToString(), 8000),
                Path = Truncate(context.Request.Path.Value ?? "", 500),
                Method = Truncate(context.Request.Method, 10),
                UserName = context.User.Identity?.Name
            });

            await database.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception loggingException)
        {
            // writing the log must never create a second error
            _logger.LogError(
                loggingException,
                "Грешката {TraceId} не можа да се запише в базата.",
                traceId);
        }
    }

    // DbUpdateConcurrencyException derives from DbUpdateException,
    // so it has to be matched first
    private static (int StatusCode, string Title, string Message)
        Classify(Exception exception)
    {
        return exception switch
        {
            DbUpdateConcurrencyException => (
                409,
                "Конфликт при запис",
                "Данните са били променени от друг потребител. " +
                "Презареди страницата и опитай отново."),

            DbUpdateException => (
                409,
                "Записът не е възможен",
                "Операцията нарушава правило в базата данни " +
                "(например дублирано име). Промените не са запазени."),

            _ => (
                500,
                "Нещо се обърка",
                "Възникна неочаквана грешка. Промените не са " +
                "запазени. Опитай отново след малко.")
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength);
    }
}
