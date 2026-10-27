using System.ComponentModel.DataAnnotations;

namespace GamingTournament.Models;

// only what the form is allowed to send.
// binding Match directly would let someone post Status or scores
public class MatchCreateViewModel
{
    [Required(ErrorMessage = "Избери домакин.")]
    [Display(Name = "Домакин")]
    public int? HomeTeamId { get; set; }

    [Required(ErrorMessage = "Избери гост.")]
    [Display(Name = "Гост")]
    public int? AwayTeamId { get; set; }

    [Required(ErrorMessage = "Избери дата и час.")]
    [Display(Name = "Дата и час")]
    public DateTime? ScheduledOn { get; set; }
}
