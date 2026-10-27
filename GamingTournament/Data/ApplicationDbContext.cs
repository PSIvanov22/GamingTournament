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

        // second line of defence, the controller checks this too
        builder.Entity<Match>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_Matches_DifferentTeams",
                "HomeTeamId <> AwayTeamId"));
    }
}