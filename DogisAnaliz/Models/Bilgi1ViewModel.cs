namespace DogisAnaliz.Models;

/// <summary>
/// BİLGİ 1 — fikstür deseni analizi.
/// Pivot maç: A vs B, hafta N. X = A'nın N-1'deki rakibi, Y = B'nin N-1'deki rakibi.
/// X ile Y, N'den önceki HERHANGİ bir haftada kendi aralarında DÖNÜŞ (İY/MS 1/2 veya 2/1)
/// oynamışsa desen tutar. İzlenen çıktı: pivot maçın DÖNÜŞ olması. +6 gol dahil değildir.
/// </summary>
public class Bilgi1ViewModel
{
    public List<Bilgi1LeagueDto> Leagues { get; set; } = new();
    public List<Season> Seasons { get; set; } = new();
    public int? SelectedLeagueId { get; set; }
    public int? SelectedSeasonId { get; set; }
    public string SelectedLeagueName { get; set; } = string.Empty;
    public string SelectedSeasonName { get; set; } = string.Empty;
    public bool AllSeasons { get; set; }

    public int EligiblePivotCount { get; set; }
    public int EligibleTurnaroundCount { get; set; }
    public int Bilgi1Count { get; set; }
    public int Bilgi1TurnaroundCount { get; set; }

    public double BaseTurnaroundRate => EligiblePivotCount > 0
        ? Math.Round(100.0 * EligibleTurnaroundCount / EligiblePivotCount, 1) : 0;
    public double Bilgi1TurnaroundRate => Bilgi1Count > 0
        ? Math.Round(100.0 * Bilgi1TurnaroundCount / Bilgi1Count, 1) : 0;
    public double RateDiff => Math.Round(Bilgi1TurnaroundRate - BaseTurnaroundRate, 1);

    public List<Bilgi1RowDto> Rows { get; set; } = new();

    /// <summary>Deseni tutan ama henüz OYNANMAMIŞ pivot maçlar (fikstür takviminden — tahmin).</summary>
    public List<Bilgi1RowDto> PendingRows { get; set; } = new();
}

public class Bilgi1LeagueDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int MatchCount { get; set; }
}

public class Bilgi1RowDto
{
    public string SeasonName { get; set; } = string.Empty;
    public int Week { get; set; }
    public DateTime MatchDate { get; set; }
    public string TeamA { get; set; } = string.Empty;
    public string TeamB { get; set; } = string.Empty;
    public string HtScore { get; set; } = string.Empty;
    public string FtScore { get; set; } = string.Empty;
    public string IyMsCode { get; set; } = string.Empty;
    public bool IsTurnaround { get; set; }

    // Tetikleyici: N-1'deki iki rakip ve onların dönüş maçı
    public string OppX { get; set; } = string.Empty;
    public string OppY { get; set; } = string.Empty;
    public int TriggerWeek { get; set; }
    public string TriggerIyMs { get; set; } = string.Empty;
    public string TriggerFt { get; set; } = string.Empty;

    /// <summary>Pivot maç henüz oynanmadı (fikstür takviminden geldi) — tahmin satırı.</summary>
    public bool IsPending { get; set; }
    public DateTime? KickoffUtc { get; set; }
    public DateTime? KickoffTr => KickoffUtc?.AddHours(3);
}
