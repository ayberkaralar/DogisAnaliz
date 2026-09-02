namespace DogisAnaliz.Models;

public class DogiViewModel
{
    public int TotalPatterns { get; set; }
    public int SurpriseCount { get; set; }
    public int NormalCount   { get; set; }
    public int PendingCount  { get; set; }

    public double SurpriseRate => (TotalPatterns - PendingCount) > 0
        ? Math.Round((double)SurpriseCount / (TotalPatterns - PendingCount) * 100, 1)
        : 0;

    public List<League> Leagues { get; set; } = new();
    public List<Season> Seasons { get; set; } = new();
    public int? SelectedLeagueId { get; set; }
    public int? SelectedSeasonId { get; set; }
    public string SelectedResult  { get; set; } = "ALL";

    public List<DogiRowDto> Patterns      { get; set; } = new();
    public List<DogiRowDto> PendingAlerts { get; set; } = new();
}

public class DogiRowDto
{
    public int    Id          { get; set; }
    public string TeamName    { get; set; } = "";
    public string LeagueName  { get; set; } = "";
    public string SeasonName  { get; set; } = "";
    public string PatternType { get; set; } = "";
    public string AlertResult { get; set; } = "";

    public string   Match1Teams { get; set; } = "";
    public string   Match1Score { get; set; } = "";
    public DateTime Match1Date  { get; set; }
    public int      Match1Week  { get; set; }

    public string   Match2Teams { get; set; } = "";
    public string   Match2Score { get; set; } = "";
    public DateTime Match2Date  { get; set; }
    public int      Match2Week  { get; set; }

    public string?   Match3Teams       { get; set; }
    public string?   Match3Score       { get; set; }
    public DateTime? Match3Date        { get; set; }
    public int?      Match3Week        { get; set; }
    public string?   Match3SurpriseType { get; set; }
}
