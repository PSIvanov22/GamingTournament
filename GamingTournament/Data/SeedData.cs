using GamingTournament.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GamingTournament.Data;

// Starting data for an empty database: teams, players, coaches,
// requests and matches. Runs on every start from Program.cs,
// but only does something while there are no teams yet.
//
// Users go through UserManager (not HasData) so the passwords
// get hashed properly - and this code doesn't care which
// database is behind the context.
public static class SeedData
{
    // every seeded player/coach logs in with this
    private const string SeedPassword = "Player123!";

    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        // someone already has data here - don't mix ours in
        if (await context.Teams.AnyAsync())
        {
            return;
        }

        // teams first, the players need their Ids
        Team wolves = new Team { Name = "Sofia Wolves", Country = "България", FoundedYear = 2019 };
        Team phoenix = new Team { Name = "Plovdiv Phoenix", Country = "България", FoundedYear = 2020 };
        Team sharks = new Team { Name = "Varna Sharks", Country = "България", FoundedYear = 2018 };
        Team titans = new Team { Name = "Balkan Titans", Country = "Сърбия", FoundedYear = 2017 };
        Team dragons = new Team { Name = "Danube Dragons", Country = "Румъния", FoundedYear = 2021 };
        Team storm = new Team { Name = "Aegean Storm", Country = "Гърция", FoundedYear = 2022 };

        // 6 teams on purpose: 5 per page -> the list gets a second page
        context.Teams.AddRange(wolves, phoenix, sharks, titans, dragons, storm);
        await context.SaveChangesAsync();

        // players already in a team
        ApplicationUser ivan = await CreateUserAsync(userManager, "ivan@gaming.com", wolves.Id);
        ApplicationUser georgi = await CreateUserAsync(userManager, "georgi@gaming.com", wolves.Id);
        ApplicationUser maria = await CreateUserAsync(userManager, "maria@gaming.com", phoenix.Id);
        ApplicationUser nikolay = await CreateUserAsync(userManager, "nikolay@gaming.com", phoenix.Id);
        ApplicationUser elena = await CreateUserAsync(userManager, "elena@gaming.com", sharks.Id);
        ApplicationUser dimitar = await CreateUserAsync(userManager, "dimitar@gaming.com", titans.Id);

        // players without a team - they have requests waiting / refused
        ApplicationUser stefan = await CreateUserAsync(userManager, "stefan@gaming.com", null);
        ApplicationUser petya = await CreateUserAsync(userManager, "petya@gaming.com", null);

        // coaches are never members (TeamId stays null),
        // the link goes the other way: Team.CoachId
        ApplicationUser coachPetrov = await CreateUserAsync(userManager, "coach.petrov@gaming.com", null);
        ApplicationUser coachIvanova = await CreateUserAsync(userManager, "coach.ivanova@gaming.com", null);
        ApplicationUser coachGeorgiev = await CreateUserAsync(userManager, "coach.georgiev@gaming.com", null);

        wolves.CoachId = coachPetrov.Id;
        sharks.CoachId = coachIvanova.Id;

        DateTime now = DateTime.Now;

        // every member got into the team through an approved request,
        // so the history matches what the app would have produced
        context.TeamJoinRequests.AddRange(
            JoinRequest(ivan, wolves, "Approved", now.AddDays(-30)),
            JoinRequest(georgi, wolves, "Approved", now.AddDays(-29)),
            JoinRequest(maria, phoenix, "Approved", now.AddDays(-28)),
            JoinRequest(nikolay, phoenix, "Approved", now.AddDays(-27)),
            JoinRequest(elena, sharks, "Approved", now.AddDays(-26)),
            JoinRequest(dimitar, titans, "Approved", now.AddDays(-25)),
            // waiting for the admin - shows up on /Teams/Requests
            JoinRequest(stefan, sharks, "Pending", now.AddDays(-2)),
            JoinRequest(stefan, dragons, "Pending", now.AddDays(-1)),
            JoinRequest(petya, wolves, "Rejected", now.AddDays(-10)));

        context.CoachRequests.AddRange(
            CoachRequest(coachPetrov, wolves, "Approved", now.AddDays(-31)),
            CoachRequest(coachIvanova, sharks, "Approved", now.AddDays(-31)),
            // waiting - shows up on /Teams/CoachRequests
            CoachRequest(coachGeorgiev, phoenix, "Pending", now.AddDays(-3)));

        // dates relative to today, so "upcoming" stays upcoming
        // no matter when the database gets created
        DateTime today = DateTime.Today;

        context.Matches.AddRange(
            Finished(wolves, phoenix, today.AddDays(-10).AddHours(18), 3, 1),
            Finished(sharks, titans, today.AddDays(-7).AddHours(19), 2, 2),
            Finished(dragons, wolves, today.AddDays(-5).AddHours(18), 0, 2),
            Finished(phoenix, storm, today.AddDays(-3).AddHours(20), 1, 0),
            new Match
            {
                HomeTeamId = titans.Id,
                AwayTeamId = storm.Id,
                ScheduledOn = today.AddDays(-2).AddHours(18),
                Status = MatchStatus.Cancelled
            },
            Scheduled(wolves, sharks, today.AddDays(2).AddHours(18)),
            Scheduled(phoenix, titans, today.AddDays(4).AddHours(19)),
            Scheduled(storm, dragons, today.AddDays(7).AddHours(20)));

        // one save for coaches, requests and matches
        await context.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        int? teamId)
    {
        // the teams can be deleted and the seed run again -
        // the users from last time are still there then
        ApplicationUser? user = await userManager.FindByEmailAsync(email);

        if (user != null)
        {
            return user;
        }

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            TeamId = teamId
        };

        IdentityResult result = await userManager.CreateAsync(user, SeedPassword);

        // fail loudly - a half-seeded database is worse than a crash
        if (!result.Succeeded)
        {
            string errors = string.Join(", ", result.Errors.Select(error => error.Description));

            throw new InvalidOperationException($"Seed user {email}: {errors}");
        }

        return user;
    }

    private static TeamJoinRequest JoinRequest(
        ApplicationUser player, Team team, string status, DateTime createdOn)
    {
        return new TeamJoinRequest
        {
            PlayerId = player.Id,
            TeamId = team.Id,
            Status = status,
            CreatedOn = createdOn
        };
    }

    private static CoachRequest CoachRequest(
        ApplicationUser player, Team team, string status, DateTime createdOn)
    {
        return new CoachRequest
        {
            PlayerId = player.Id,
            TeamId = team.Id,
            Status = status,
            CreatedOn = createdOn
        };
    }

    private static Match Finished(
        Team home, Team away, DateTime scheduledOn, int homeScore, int awayScore)
    {
        return new Match
        {
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            ScheduledOn = scheduledOn,
            HomeScore = homeScore,
            AwayScore = awayScore,
            Status = MatchStatus.Finished
        };
    }

    private static Match Scheduled(Team home, Team away, DateTime scheduledOn)
    {
        return new Match
        {
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            ScheduledOn = scheduledOn,
            Status = MatchStatus.Scheduled
        };
    }
}
