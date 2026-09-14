namespace DogisAnaliz.Models;

/// <summary>
/// LİG FİKSTÜR deseni — BİLGİ 1/4'ten farklı olarak takımın kendi ardışık haftalarına değil,
/// AYNI HAFTANIN (bir ligin bir round'unun) FARKLI maçlarına bakar (kullanıcının deyimiyle
/// "sırayla değil birbiri arasında"). Anlık hesaplanır, kalıcı tablo yok (bkz. CLAUDE.md).
///
/// Tetikleyici: bir round'da (lig+sezon+hafta) EN AZ bir maç tam skoru 2-2 bitmiş VE o round'da
/// (farklı bir maçta) EN AZ bir sürpriz (dönüş veya +6 gol) zaten var. "Tuttu" = o round'da
/// TOPLAM sürpriz sayısı ≥ 2 — yani tetikleyicinin sağladığı 1 sürprizin ÖTESİNDE bir sürpriz
/// daha var. Taban oranı: TÜM (tamamlanmış) round'ların kaçında ≥2 sürpriz var — bununla
/// kıyaslanır (bkz. BİLGİ 1/4'teki aynı disiplin: "öngörü gücü olduğunu varsayma").
/// </summary>
public class LigFiksturViewModel
{
    // Filtreler — boşsa (null) "tüm ligler / tüm sezonlar" birleşik taranır (varsayılan, ilk sürüm).
    public List<LigFiksturLeagueDto> Leagues { get; set; } = new();
    public List<Season> Seasons { get; set; } = new();
    public int? SelectedLeagueId { get; set; }
    public int? SelectedSeasonId { get; set; }

    public int TotalRoundsScanned { get; set; }
    public int BaseMultiSurpriseCount { get; set; }
    public double BaseMultiSurpriseRate => TotalRoundsScanned > 0
        ? Math.Round(100.0 * BaseMultiSurpriseCount / TotalRoundsScanned, 1) : 0;

    public int TriggerRoundCount { get; set; }
    public int TriggerHitCount { get; set; }
    public double TriggerHitRate => TriggerRoundCount > 0
        ? Math.Round(100.0 * TriggerHitCount / TriggerRoundCount, 1) : 0;

    public double RateDiff => Math.Round(TriggerHitRate - BaseMultiSurpriseRate, 1);

    public List<LigFiksturLeagueStatDto> LeagueStats { get; set; } = new();

    /// <summary>Geçmiş, TAMAMLANMIŞ, tetikleyici olan round'lar — detay tablo için.</summary>
    public List<LigFiksturRoundDto> TriggerRounds { get; set; } = new();

    /// <summary>Şu an DEVAM EDEN (bazı maçları oynanmış bazıları bekleyen) ama oynanan kısmında
    /// zaten tetikleyici oluşmuş round'lar — kalan (henüz oynanmamış) maçları "aday" olarak gösterir.</summary>
    public List<LigFiksturLiveRoundDto> LivePending { get; set; } = new();
}

public class LigFiksturLeagueDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class LigFiksturLeagueStatDto
{
    public string LeagueName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int RoundsScanned { get; set; }
    public int TriggerRounds { get; set; }
    public int HitRounds { get; set; }
    public double TriggerHitRate => TriggerRounds > 0 ? Math.Round(100.0 * HitRounds / TriggerRounds, 1) : 0;
}

/// <summary>Bir round içindeki tek maç — hem geçmiş detay tablosunda hem canlı bölümde kullanılır.</summary>
public class LigFiksturMatchDto
{
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public DateTime MatchDate { get; set; }
    public string FtScore { get; set; } = string.Empty;
    public int TotalGoals { get; set; }
    public string SurpriseType { get; set; } = "NONE";
    public bool Is22 { get; set; }
}

public class LigFiksturRoundDto
{
    public string LeagueName { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public int Week { get; set; }
    public List<LigFiksturMatchDto> Matches { get; set; } = new();
    public int SurpriseCount { get; set; }
    public bool IsHit { get; set; }
}

public class LigFiksturLiveRoundDto
{
    public string LeagueName { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public int Week { get; set; }

    /// <summary>O round'da şimdiye kadar oynanmış maçlar (tetikleyici kanıtı burada görünür).</summary>
    public List<LigFiksturMatchDto> PlayedMatches { get; set; } = new();

    /// <summary>Aynı round'un henüz oynanmamış maçları — "aday" (sürpriz olabilir).</summary>
    public List<LigFiksturCandidateDto> Candidates { get; set; } = new();
}

public class LigFiksturCandidateDto
{
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public DateTime KickoffUtc { get; set; }
    public DateTime KickoffTr => KickoffUtc.AddHours(3);
}
