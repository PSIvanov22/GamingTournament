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
            .OnDelete(DeleteBehavior.Restrict);

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
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CoachRequest>()
            .HasOne(request => request.Team)
            .WithMany(team => team.CoachRequests)
            .HasForeignKey(request => request.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}