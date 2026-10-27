using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GamingTournament.Models;

public class MatchResultViewModel
{
    public int Id { get; set; }

    // display only - re-read from the DB on POST,
    // never trusted from the form.
    // [ValidateNever]: a non-nullable string counts as [Required],
    // and the form doesn't send these -> ModelState would fail
    [ValidateNever]
    public string HomeTeamName { get; set; } = string.Empty;

    [ValidateNever]
    public string AwayTeamName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Въведи резултат.")]
    [Range(0, 999, ErrorMessage = "Между 0 и 999.")]
    public int? HomeScore { get; set; }

    [Required(ErrorMessage = "Въведи резултат.")]
    [Range(0, 999, ErrorMessage = "Между 0 и 999.")]
    public int? AwayScore { get; set; }
}
