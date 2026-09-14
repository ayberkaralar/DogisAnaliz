namespace DogisAnaliz.Models;

/// <summary>
/// BİLGİ 4 — fikstür deseni analizi.
/// İki takımın (A, B) karşılaştığı "pivot maç" (hafta N) için:
/// A'nın N-1 & N-2'deki rakip ikilisi ile B'nin N+1 & N+2'deki rakip ikilisi AYNI ise
/// (ya da yön tersi: B'nin geçmişi = A'nın geleceği) desen tutar. Sıra önemsiz,
/// ev/deplasman önemsiz — sadece rakip takımların kimliği. İzlenen çıktı: pivot maçın
/// DÖNÜŞ (İY/MS 1/2 veya 2/1) olması. +6 gol bu analize dahil değildir.
/// </summary>
public class Bilgi4ViewModel
{
    public List<Bilgi4LeagueDto> Leagues { get; set; } = new();
    public List<Season> Seasons { get; set; } = new();
    public int? SelectedLeagueId { get; set; }
    public int? SelectedSeasonId { get; set; }
    public string SelectedLeagueName { get; set; } = string.Empty;
    public string SelectedSeasonName { get; set; } = string.Empty;
    public bool AllSeasons { get; set; } // seasonId verilmezse tüm sezonlar birleşik

    // KPI
    public int EligiblePivotCount { get; set; }       // 5 haftalık penceresi tam olan pivot maç
    public int EligibleTurnaroundCount { get; set; }   // bunların dönüş olanı (TABAN)
    public int Bilgi4Count { get; set; }               // desen tutan pivot maç
    public int Bilgi4TurnaroundCount { get; set; }     // desen tutup dönüş olan

    public double BaseTurnaroundRate => EligiblePivotCount > 0
        ? Math.Round(100.0 * EligibleTurnaroundCount / EligiblePivotCount, 1) : 0;
    public double Bilgi4TurnaroundRate => Bilgi4Count > 0
        ? Math.Round(100.0 * Bilgi4TurnaroundCount / Bilgi4Count, 1) : 0;

    public double RateDiff => Math.Round(Bilgi4TurnaroundRate - BaseTurnaroundRate, 1);

    public List<Bilgi4RowDto> Rows { get; set; } = new();

    /// <summary>Deseni tutan ama henüz OYNANMAMIŞ pivot maçlar (fikstür takviminden — tahmin).</summary>
    public List<Bilgi4RowDto> PendingRows { get; set; } = new();

    /// <summary>Seçili ligden BAĞIMSIZ — TÜM liglerde, önümüzdeki ~2 hafta içinde oynanacak,
    /// deseni tutan bekleyen maçlar. Bkz. Bilgi1ViewModel.CrossLeaguePending / CLAUDE.md.</summary>
    public List<Bilgi4RowDto> CrossLeaguePending { get; set; } = new();
}

public class Bilgi4LeagueDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int MatchCount { get; set; }
}

public class Bilgi4RowDto
{
    public string SeasonName { get; set; } = string.Empty;
    public int Week { get; set; }
    public DateTime MatchDate { get; set; }
    public string TeamA { get; set; } = string.Empty;
    public string TeamB { get; set; } = string.Empty;
    public string SharedOpp1 { get; set; } = string.Empty;
    public string SharedOpp2 { get; set; } = string.Empty;
    /// <summary>"A→B" (A'nın geçmişi = B'nin geleceği), "B→A", ya da "çift yön".</summary>
    public string Direction { get; set; } = string.Empty;
    public string HtScore { get; set; } = string.Empty;
    public string FtScore { get; set; } = string.Empty;
    public string IyMsCode { get; set; } = string.Empty;
    public bool IsTurnaround { get; set; }

    /// <summary>Pivot maç henüz oynanmadı (fikstür takviminden geldi) — tahmin satırı.</summary>
    public bool IsPending { get; set; }
    public DateTime? KickoffUtc { get; set; }
    public DateTime? KickoffTr => KickoffUtc?.AddHours(3);

    /// <summary>Sadece CrossLeaguePending listesinde doldurulur — hangi ligden geldiğini gösterir.</summary>
    public string LeagueName { get; set; } = string.Empty;
}
