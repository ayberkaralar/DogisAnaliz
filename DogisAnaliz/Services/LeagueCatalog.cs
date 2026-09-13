namespace DogisAnaliz.Services;

/// <summary>Bir ligin api-sports kimliği ve gösterim bilgisi.</summary>
public record LeagueConfig(int ApiId, string Name, string Country, bool IsPriority);

/// <summary>
/// Desteklenen ligler ve sezonlar — hem /Sync ekranı hem otomatik güncelleme bunu kullanır.
/// </summary>
public static class LeagueCatalog
{
    /// <summary>Aktif (içinde bulunulan) sezonun başlangıç yılı. 2026 → "2026-2027".</summary>
    public const int ActiveSeasonYear = 2026;

    public static readonly LeagueConfig[] KnownLeagues =
    {
        // ── Birinci Ligler ──
        new(39,  "Premier League",     "England",     true),
        new(140, "La Liga",            "Spain",       false),
        new(78,  "Bundesliga",         "Germany",     false),
        new(135, "Serie A",            "Italy",       false),
        new(61,  "Ligue 1",            "France",      false),
        new(203, "Süper Lig",          "Turkey",      false),
        new(88,  "Eredivisie",         "Netherlands", false),
        new(94,  "Primeira Liga",      "Portugal",    false),

        // ── İkinci / Diğer Ligler ──
        new(79,  "2. Bundesliga",      "Germany",     false),
        new(141, "La Liga 2",          "Spain",       false),
        new(89,  "Eerste Divisie",     "Netherlands", false),

        // ── Diğer Ülkeler ──
        new(113, "Allsvenskan",        "Sweden",      false),
        new(218, "Bundesliga",         "Austria",     false),
        new(207, "Super League",       "Switzerland", false),
    };

    // Sezonlar: 2020-21 → 2026-27 (aktif)
    public static readonly int[] KnownSeasons = { 2020, 2021, 2022, 2023, 2024, 2025, 2026 };
}
