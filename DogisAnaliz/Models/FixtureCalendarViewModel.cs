namespace DogisAnaliz.Models;

public class FixtureCalendarViewModel
{
    public List<FixtureLeagueDto> Leagues { get; set; } = new();
    public List<Season> Seasons { get; set; } = new();
    public List<FixtureTeamDto> Teams { get; set; } = new();
    public List<int> Weeks { get; set; } = new();

    public int? SelectedLeagueId { get; set; }
    public int? SelectedSeasonId { get; set; }
    public int? SelectedTeamId   { get; set; }
    public int? SelectedWeek     { get; set; }
    public string SelectedStatus { get; set; } = "ALL"; // ALL | NS (oynanmamış) | PLAYED (oynanmış)
    public string SelectedLeagueName { get; set; } = string.Empty;
    public string SelectedSeasonName { get; set; } = string.Empty;

    // Özet
    public int TotalCount    { get; set; }
    public int PlayedCount   { get; set; }
    public int NotPlayedCount { get; set; }

    public List<FixtureRowDto> Rows { get; set; } = new();
}

public class FixtureLeagueDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class FixtureTeamDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class FixtureRowDto
{
    public int Week { get; set; }
    public DateTime KickoffUtc { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public string Status { get; set; } = "NS";
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }

    public bool IsPlayed => Status == "FT" || Status == "AET" || Status == "PEN";
    public bool IsHomeTeam { get; set; } // takım filtresi aktifken vurgulama

    // Türkiye saati (UTC+3, DST yok)
    public DateTime KickoffTr => KickoffUtc.AddHours(3);
    public string ScoreText => (HomeGoals.HasValue && AwayGoals.HasValue)
        ? $"{HomeGoals}-{AwayGoals}"
        : "–";
}
