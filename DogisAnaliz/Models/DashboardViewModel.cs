namespace DogisAnaliz.Models;

public class DashboardViewModel
{
    // Navigasyon Verileri
    public List<LeagueNavDto> Leagues { get; set; } = new();
    public List<Season>       Seasons { get; set; } = new();
    public List<TeamNavDto>   Teams   { get; set; } = new(); // seçili ligdeki takımlar
    public int? SelectedLeagueId { get; set; }
    public int? SelectedSeasonId { get; set; }
    public int? SelectedTeamId   { get; set; }
    public string SelectedLeagueName { get; set; } = string.Empty;
    public string SelectedFilter { get; set; } = "ALL"; // ALL, TURNAROUND, HIGH_GOAL, DOUBLE
    public string SortOrder { get; set; } = "week_asc";  // week_asc, week_desc, date_asc, date_desc

    // KPI Kartları
    public int TotalMatchesCount   { get; set; }
    public int TurnaroundCount     { get; set; }
    public int HighGoalCount       { get; set; }
    public int DoubleSurpriseCount { get; set; }

    // Tablo Verisi
    public List<MatchRowDto> Matches { get; set; } = new();
}

public class LeagueNavDto
{
    public int    Id            { get; set; }
    public string Name          { get; set; } = string.Empty;
    public string Country       { get; set; } = string.Empty;
    public int    SurpriseCount { get; set; }
}

public class TeamNavDto
{
    public int    Id            { get; set; }
    public string Name          { get; set; } = string.Empty;
    public int    SurpriseCount { get; set; } // Seçili sezondaki sürpriz sayısı
}

public class MatchRowDto
{
    public int      Id           { get; set; }
    public int      Week         { get; set; }
    public DateTime MatchDate    { get; set; }
    public string   HomeTeam     { get; set; } = string.Empty;
    public string   AwayTeam     { get; set; } = string.Empty;
    public string   HtScore      { get; set; } = string.Empty;
    public string   FtScore      { get; set; } = string.Empty;
    public string   IyMsCode     { get; set; } = string.Empty;
    public int      TotalGoals   { get; set; }
    public string   SurpriseType { get; set; } = "NONE";
    // Takım filtresi aktifken hangi takım (ev sahibi/deplasman) olduğunu vurgular
    public bool     IsHomeTeam   { get; set; }
}