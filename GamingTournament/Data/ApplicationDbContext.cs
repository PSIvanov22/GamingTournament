using GamingTournament.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GamingTournament.Data;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Team> Teams { get; set; } = null!;

    public DbSet<TeamJoinRequest> TeamJoinRequests
    {
        get;
        set;
    } = null!;

    public DbSet<CoachRequest> CoachRequests
    {
        get;
        set;
    } = null!;

    public DbSet<Match> Matches { get; set; } = null!;

    public DbSet<Tournament> Tournaments { get; set; } = null!;

    protected override void OnModelCreating(
        ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Играчите в отбора
        builder.Entity<ApplicationUser>()
            .HasOne(user => user.Team)
            .WithMany(team => team.Members)
            .HasForeignKey(user => user.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        // Треньорът на отбора
        builder.Entity<Team>()
            .HasOne(team => team.Coach)
            .WithOne()
            .HasForeignKey<Team>(team => team.CoachId)
            .OnDelete(DeleteBehavior.SetNull);

        // Заявки за влизане като играч
        builder.Entity<TeamJoinRequest>()
            .HasOne(request => request.Player)
            .WithMany(player => player.TeamJoinRequests)
            .HasForeignKey(request => request.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TeamJoinRequest>()
            .HasOne(request => request.Team)
            .WithMany(team => team.JoinRequests)
            .HasForeignKey(request => request.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Заявки за треньор
        builder.Entity<CoachRequest>()
            .HasOne(request => request.Player)
            .WithMany(player => player.CoachRequests)
            .HasForeignKey(request => request.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CoachRequest>()
            .HasOne(request => request.Team)
            .WithMany(team => team.CoachRequests)
            .HasForeignKey(request => request.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Matches
        // Match -> Teams twice, so EF can't guess on its own
        // which FK belongs to which navigation - spell it out.
        // Restrict: deleting a team must not silently wipe
        // its match history
        builder.Entity<Match>()
            .HasOne(match => match.HomeTeam)
            .WithMany()
            .HasForeignKey(match => match.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Match>()
            .HasOne(match => match.AwayTeam)
            .WithMany()
            .HasForeignKey(match => match.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // store "Scheduled" instead of 0 - readable in the DB
        builder.Entity<Match>()
            .Property(match => match.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // CHECK constraints: second line of defence, the controllers
        // and [Range] attributes check the same things first
        builder.Entity<Match>()
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Matches_DifferentTeams",
                    "[HomeTeamId] <> [AwayTeamId]");

                table.HasCheckConstraint(
                    "CK_Matches_Scores",
                    "([HomeScore] IS NULL OR [HomeScore] >= 0) AND " +
                    "([AwayScore] IS NULL OR [AwayScore] >= 0)");

                // a finished match without a score makes no sense
                table.HasCheckConstraint(
                    "CK_Matches_FinishedHasScores",
                    "[Status] <> 'Finished' OR " +
                    "([HomeScore] IS NOT NULL AND [AwayScore] IS NOT NULL)");

                // the enum is stored as text - only its 3 names allowed
                table.HasCheckConstraint(
                    "CK_Matches_Status",
                    "[Status] IN ('Scheduled', 'Finished', 'Cancelled')");
            });

        // Tournaments
        builder.Entity<Tournament>()
            .HasIndex(tournament => tournament.Name)
            .IsUnique();

        builder.Entity<Tournament>()
            .Property(tournament => tournament.StartDate)
            .HasColumnType("date");

        builder.Entity<Tournament>()
            .Property(tournament => tournament.EndDate)
            .HasColumnType("date");

        builder.Entity<Tournament>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_Tournaments_Dates",
                "[EndDate] >= [StartDate]"));

        // Restrict, same reason as the teams: keep the match history
        builder.Entity<Match>()
            .HasOne(match => match.Tournament)
            .WithMany(tournament => tournament.Matches)
            .HasForeignKey(match => match.TournamentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Teams
        builder.Entity<Team>()
            .HasIndex(team => team.Name)
            .IsUnique();

        builder.Entity<Team>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_Teams_FoundedYear",
                "[FoundedYear] IS NULL OR " +
                "[FoundedYear] BETWEEN 1990 AND 2100"));

        // Requests: the status strings the controllers use
        builder.Entity<TeamJoinRequest>()
            .Property(request => request.Status)
            .HasMaxLength(20);

        builder.Entity<TeamJoinRequest>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_TeamJoinRequests_Status",
                "[Status] IN ('Pending', 'Approved', 'Rejected')"));

        builder.Entity<CoachRequest>()
            .Property(request => request.Status)
            .HasMaxLength(20);

        builder.Entity<CoachRequest>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_CoachRequests_Status",
                "[Status] IN ('Pending', 'Approved', 'Rejected')"));
    }
}