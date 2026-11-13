using GamingTournament.Data;
using GamingTournament.Middleware;
using GamingTournament.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace GamingTournament.Tests;

public class ExceptionHandlingMiddlewareTests
{
    // runs the middleware with a "next" that fails on the first call;
    // the second call is the re-executed /Home/Error, which succeeds
    private static async Task<(DefaultHttpContext Context, int Calls)> RunAsync(
        TestDb db, Exception exception, string environment)
    {
        ServiceProvider provider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(db.Connection))
            .BuildServiceProvider();

        int calls = 0;

        RequestDelegate next = _ =>
        {
            calls++;

            if (calls == 1)
            {
                throw exception;
            }

            return Task.CompletedTask;
        };

        ExceptionHandlingMiddleware middleware =
            new ExceptionHandlingMiddleware(
                next,
                NullLogger<ExceptionHandlingMiddleware>.Instance,
                provider.GetRequiredService<IServiceScopeFactory>(),
                new FakeEnvironment(environment));

        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/Matches/Create";
        context.Request.Method = "POST";

        await middleware.InvokeAsync(context);

        return (context, calls);
    }

    [Fact]
    public async Task UnhandledException_Returns500_AndPreparesErrorPage()
    {
        using TestDb db = new TestDb();

        (DefaultHttpContext context, int calls) = await RunAsync(
            db, new InvalidOperationException("boom"), "Production");

        // the failing request + the re-executed error page
        Assert.Equal(2, calls);
        Assert.Equal(500, context.Response.StatusCode);

        ErrorViewModel model = Assert.IsType<ErrorViewModel>(
            context.Items[ExceptionHandlingMiddleware.ErrorItemKey]);

        Assert.Equal(500, model.StatusCode);
        Assert.Equal(context.TraceIdentifier, model.RequestId);

        // the user never sees the exception text in Production
        Assert.Null(model.Details);

        // the original request is restored after the re-execute
        Assert.Equal("/Matches/Create", context.Request.Path.Value);
        Assert.Equal("POST", context.Request.Method);
    }

    [Fact]
    public async Task UnhandledException_IsSavedInTheErrorLogTable()
    {
        using TestDb db = new TestDb();

        (DefaultHttpContext context, _) = await RunAsync(
            db, new InvalidOperationException("boom"), "Production");

        ErrorLog log = Assert.Single(db.Context.ErrorLogs);

        Assert.Equal("boom", log.Message);
        Assert.Equal("InvalidOperationException", log.ExceptionType);
        Assert.Equal("/Matches/Create", log.Path);
        Assert.Equal("POST", log.Method);
        Assert.Equal(500, log.StatusCode);
        Assert.Equal(context.TraceIdentifier, log.TraceId);
    }

    [Fact]
    public async Task DatabaseUpdateException_Returns409()
    {
        using TestDb db = new TestDb();

        (DefaultHttpContext context, _) = await RunAsync(
            db, new DbUpdateException("duplicate"), "Production");

        Assert.Equal(409, context.Response.StatusCode);
    }

    [Fact]
    public async Task ConcurrencyException_Returns409()
    {
        using TestDb db = new TestDb();

        (DefaultHttpContext context, _) = await RunAsync(
            db, new DbUpdateConcurrencyException("changed"), "Production");

        Assert.Equal(409, context.Response.StatusCode);
    }

    [Fact]
    public async Task InDevelopment_ErrorPageGetsTheExceptionDetails()
    {
        using TestDb db = new TestDb();

        (DefaultHttpContext context, _) = await RunAsync(
            db, new InvalidOperationException("boom"), "Development");

        ErrorViewModel model = Assert.IsType<ErrorViewModel>(
            context.Items[ExceptionHandlingMiddleware.ErrorItemKey]);

        Assert.Contains("boom", model.Details);
    }

    [Fact]
    public async Task RequestWithoutErrors_PassesThroughUntouched()
    {
        using TestDb db = new TestDb();

        ServiceProvider provider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(db.Connection))
            .BuildServiceProvider();

        int calls = 0;

        ExceptionHandlingMiddleware middleware =
            new ExceptionHandlingMiddleware(
                _ =>
                {
                    calls++;
                    return Task.CompletedTask;
                },
                NullLogger<ExceptionHandlingMiddleware>.Instance,
                provider.GetRequiredService<IServiceScopeFactory>(),
                new FakeEnvironment("Production"));

        DefaultHttpContext context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(1, calls);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Empty(db.Context.ErrorLogs);
    }
}

// the smallest possible IHostEnvironment for the middleware
class FakeEnvironment : IHostEnvironment
{
    public FakeEnvironment(string environmentName)
    {
        EnvironmentName = environmentName;
    }

    public string EnvironmentName { get; set; }

    public string ApplicationName { get; set; } = "GamingTournament.Tests";

    public string ContentRootPath { get; set; } = string.Empty;

    public IFileProvider ContentRootFileProvider { get; set; } =
        new NullFileProvider();
}
