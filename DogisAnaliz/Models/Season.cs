namespace DogisAnaliz.Models;

public class Season
{
    public int Id { get; set; }
    public string SeasonName { get; set; } = string.Empty; // Örn: "2019-2020", "2025-2026"
    public int StartYear { get; set; }
    public int EndYear { get; set; }

    public ICollection<Match> Matches { get; set; } = new List<Match>();
}