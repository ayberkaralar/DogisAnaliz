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

    // Görünüm modu: "surprise" (varsayılan, mevcut sürpriz ekranı) | "fixtures" (tüm maçlar / fikstür)
    public string ViewMode { get; set; } = "surprise";

    // surprise modu: ALL, TURNAROUND, HIGH_GOAL, DOUBLE
    // fixtures modu: ALL, SURPRISE, TURNAROUND, HIGH_GOAL, NORMAL
    public string SelectedFilter { get; set; } = "ALL";
    public string SortOrder { get; set; } = "week_asc";  // week_asc, week_desc, date_asc, date_desc

    // KPI Kartları — surprise modu
    public int TotalMatchesCount   { get; set; } // fixtures modunda "toplam maç" anlamına gelir
    public int TurnaroundCount     { get; set; }
    public int HighGoalCount       { get; set; }
    public int DoubleSurpriseCount { get; set; }

    // KPI Kartları — fixtures modu (ek)
    public int HomeWinCount { get; set; }
    public int DrawCount    { get; set; }
    public int AwayWinCount { get; set; }
    public double AvgGoals  { get; set; }

    // Tablo Verisi
    public List<MatchRowDto> Matches { get; set; } = new();

    // fixtures modunda bir takım seçiliyse: o takımın seçili sezondaki özeti
    public TeamFixtureStatsDto? TeamStats { get; set; }
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
    public int    MatchCount    { get; set; } // fixtures modunda seçili sezonda oynanan maç sayısı
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
    public int      FtHome       { get; set; }
    public int      FtAway       { get; set; }
    public string   MsResultCode { get; set; } = string.Empty; // "1" | "X" | "2" — FT skorundan
    public string   IyMsCode     { get; set; } = string.Empty;
    public int      TotalGoals   { get; set; }
    public string   SurpriseType { get; set; } = "NONE";
    // Takım filtresi aktifken hangi takım (ev sahibi/deplasman) olduğunu vurgular
    public bool     IsHomeTeam   { get; set; }
}

// fixtures modunda seçili takımın seçili sezondaki fikstür özeti (bellek içinde hesaplanır)
public class TeamFixtureStatsDto
{
    public string TeamName { get; set; } = string.Empty;

    public int Played { get; set; }
    public int Wins   { get; set; }
    public int Draws  { get; set; }
    public int Losses { get; set; }

    public int GoalsFor     { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDiff => GoalsFor - GoalsAgainst;

    public double AvgTotalGoals { get; set; } // takımın maçlarındaki ortalama toplam gol

    public int HomePlayed { get; set; }
    public int HomeWins   { get; set; }
    public int HomeDraws  { get; set; }
    public int HomeLosses { get; set; }

    public int AwayPlayed { get; set; }
    public int AwayWins   { get; set; }
    public int AwayDraws  { get; set; }
    public int AwayLosses { get; set; }

    public int HighGoalCount   { get; set; } // toplam gol >= 6
    public int TurnaroundCount { get; set; } // MatchSurprise.IsTurnaround
    public int SurpriseCount   { get; set; } // SurpriseType != "NONE"
}
