using Microsoft.AspNetCore.Identity;

namespace GamingTournament.Models;

public class ApplicationUser : IdentityUser
{
    public int? TeamId { get; set; }

    public Team? Team { get; set; }

    public List<TeamJoinRequest> TeamJoinRequests { get; set; }
        = new List<TeamJoinRequest>();

    public List<CoachRequest> CoachRequests { get; set; }
        = new List<CoachRequest>();
}