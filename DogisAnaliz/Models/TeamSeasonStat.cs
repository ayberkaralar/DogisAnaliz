namespace DogisAnaliz.Models;

/// <summary>
/// Bir takımın lig+sezon bazlı özet istatistikleri — api-sports "teams/statistics" ucundan
/// çekilir (<see cref="Services.TeamStatisticsService"/>). Bu uç TAKIM BAŞINA ayrı çağrı
/// gerektirir (ligin tamamını tek seferde vermez) — Senkronize Et'e değil, "/Sync" ekranındaki
/// ayrı manuel butona bağlıdır (maliyeti Matches/Standings'ten daha yüksek).
/// Gol Beklentisi'nin kendi hesapladığı (Matches tablosundan, ev/deplasman ayrımı YAPMADAN)
/// ortalamaya ek/karşılaştırma olarak kullanılmak üzere; ev/deplasman ayrı gol ortalaması burada.
/// (LeagueId, SeasonId, TeamId) anahtarıyla upsert edilir.
/// </summary>
public class TeamSeasonStat
{
    public int Id { get; set; }

    public int LeagueId { get; set; }
    public League League { get; set; } = null!;

    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    /// <summary>Son maçlar, örn. "WWDLL".</summary>
    public string Form { get; set; } = string.Empty;

    // Maç başı ortalama gol — ev/deplasman ayrı (api-sports "goals.for/against.average")
    public double GoalsForAvgHome { get; set; }
    public double GoalsForAvgAway { get; set; }
    public double GoalsForAvgTotal { get; set; }
    public double GoalsAgainstAvgHome { get; set; }
    public double GoalsAgainstAvgAway { get; set; }
    public double GoalsAgainstAvgTotal { get; set; }

    public int CleanSheetsHome { get; set; }
    public int CleanSheetsAway { get; set; }
    public int CleanSheetsTotal { get; set; }
    public int FailedToScoreHome { get; set; }
    public int FailedToScoreAway { get; set; }
    public int FailedToScoreTotal { get; set; }

    public int PenaltyScored { get; set; }
    public int PenaltyMissed { get; set; }

    /// <summary>Sezon toplamı — api-sports bunu 15 dakikalık aralıklara bölerek veriyor, biz topluyoruz.</summary>
    public int CardsYellowTotal { get; set; }
    public int CardsRedTotal { get; set; }

    public DateTime UpdatedAt { get; set; }
}
