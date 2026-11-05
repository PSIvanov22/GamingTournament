using System.ComponentModel.DataAnnotations;

namespace GamingTournament.Models;

public class Team
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Името на отбора е задължително.")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(80)]
    public string? Country { get; set; }

    // same range as CK_Teams_FoundedYear in the database
    [Range(1990, 2100, ErrorMessage = "Годината трябва да е между 1990 и 2100.")]
    public int? FoundedYear { get; set; }

    public string? CoachId { get; set; }

    public ApplicationUser? Coach { get; set; }

    public List<ApplicationUser> Members { get; set; }
        = new List<ApplicationUser>();

    public List<TeamJoinRequest> JoinRequests { get; set; }
        = new List<TeamJoinRequest>();

    public List<CoachRequest> CoachRequests { get; set; }
        = new List<CoachRequest>();
}