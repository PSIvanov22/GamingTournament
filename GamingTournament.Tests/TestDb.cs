using GamingTournament.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GamingTournament.Tests;

// in-memory SQLite, the same idea as in MatchesControllerTests:
// unlike the EF InMemory provider it enforces the CHECK constraints,
// unique indexes and foreign keys of the real database.
// The database lives as long as the connection is open.
public sealed class TestDb : IDisposable
{
    public SqliteConnection Connection { get; }

    public ApplicationDbContext Context { get; }

    public TestDb()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(Connection)
                .Options;

        Context = new ApplicationDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        Connection.Dispose();
    }
}
