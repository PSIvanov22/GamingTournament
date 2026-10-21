using System.ComponentModel.DataAnnotations;

namespace GamingTournament.Models;

public class CoachRequest
{
    public int Id { get; set; }

    [Required]
    public string PlayerId { get; set; } = string.Empty;

    public ApplicationUser? Player { get; set; }

    public int TeamId { get; set; }

    public Team? Team { get; set; }

    [Required]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedOn { get; set; } = DateTime.Now;
}