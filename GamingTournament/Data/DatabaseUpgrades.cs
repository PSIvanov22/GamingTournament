using Microsoft.EntityFrameworkCore;

namespace GamingTournament.Data;

// the project uses EnsureCreated (no migrations for SQLite).
// EnsureCreated does nothing when the .db file already exists,
// so a table added later has to be created by hand - otherwise
// a developer with an old gamingtournament.db gets "no such table"
public static class DatabaseUpgrades
{
    public static async Task EnsureErrorLogsTableAsync(
        ApplicationDbContext context)
    {
        if (!context.Database.IsSqlite())
        {
            return;
        }

        // same columns as the ErrorLog model;
        // IF NOT EXISTS: harmless on a fresh database
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""ErrorLogs"" (
                ""Id"" INTEGER NOT NULL
                    CONSTRAINT ""PK_ErrorLogs"" PRIMARY KEY AUTOINCREMENT,
                ""CreatedOn"" TEXT NOT NULL,
                ""TraceId"" TEXT NOT NULL,
                ""StatusCode"" INTEGER NOT NULL,
                ""ExceptionType"" TEXT NOT NULL,
                ""Message"" TEXT NOT NULL,
                ""StackTrace"" TEXT NULL,
                ""Path"" TEXT NOT NULL,
                ""Method"" TEXT NOT NULL,
                ""UserName"" TEXT NULL
            );");

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS ""IX_ErrorLogs_CreatedOn""
            ON ""ErrorLogs"" (""CreatedOn"");");
    }
}
