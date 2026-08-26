namespace DogisAnaliz.Models;

public class MatchDetail
{
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;

    public string? Referee { get; set; }
    public string? AdditionalDataJson { get; set; } // JSONB formatında saklanır
}