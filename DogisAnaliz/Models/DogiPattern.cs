namespace DogisAnaliz.Models;

public class DogiPattern
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public int LeagueId { get; set; }
    public League League { get; set; } = null!;

    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    // 1. tetikleyici maç (5-1/1-5 veya 2-2)
    public int TriggerMatch1Id { get; set; }
    public Match TriggerMatch1 { get; set; } = null!;

    // 2. tetikleyici maç (2-2 veya 5-1/1-5)
    public int TriggerMatch2Id { get; set; }
    public Match TriggerMatch2 { get; set; } = null!;

    // 3. maç — sürpriz adayı (null = henüz oynanmamış)
    public int? AlertMatchId { get; set; }
    public Match? AlertMatch { get; set; }

    // "HIGH_DRAW" = (5-1/1-5 → 2-2)  |  "DRAW_HIGH" = (2-2 → 5-1/1-5)
    public string PatternType { get; set; } = string.Empty;

    // "PENDING" = oynanmadı  |  "SURPRISE" = sürpriz bitti  |  "NORMAL" = normal bitti
    public string AlertResult { get; set; } = "PENDING";

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
