namespace DogisAnaliz.Models;

public class League
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    public ICollection<Match> Matches { get; set; } = new List<Match>();
}