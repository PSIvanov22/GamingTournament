using System.ComponentModel.DataAnnotations;

namespace GamingTournament.Models;

// a tournament groups matches: every match is played in one
public class Tournament
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Името на турнира е задължително.")]
    [StringLength(120)]
    [Display(Name = "Име")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Играта е задължителна.")]
    [StringLength(60)]
    [Display(Name = "Игра")]
    public string Game { get; set; } = string.Empty;

    // date only - stored as SQL "date", the time part is not used
    [DataType(DataType.Date)]
    [Display(Name = "Начало")]
    public DateTime StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Край")]
    public DateTime EndDate { get; set; }

    public List<Match> Matches { get; set; }
        = new List<Match>();
}
