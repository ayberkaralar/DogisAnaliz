namespace DogisAnaliz.Services;

/// <summary>
/// UI'da lig isimlerini Türkçe gösterir.
/// DB'deki orijinal isimler korunur — sadece görsel katman.
/// </summary>
public static class LeagueDisplayHelper
{
    private static readonly Dictionary<string, (string DisplayName, string Flag)> _map =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Birinci Ligler ──
        ["Premier League"]     = ("İngiltere Premier Lig",    "🏴󠁧󠁢󠁥󠁮󠁧󠁿"),
        ["La Liga"]            = ("İspanya La Liga",          "🇪🇸"),
        ["Bundesliga"]         = ("Almanya Bundesliga",       "🇩🇪"),
        ["Serie A"]            = ("İtalya Serie A",           "🇮🇹"),
        ["Ligue 1"]            = ("Fransa Ligue 1",           "🇫🇷"),
        ["Süper Lig"]          = ("Türkiye Süper Lig",        "🇹🇷"),
        ["Eredivisie"]         = ("Hollanda Eredivisie",      "🇳🇱"),
        ["Primeira Liga"]      = ("Portekiz Liga",            "🇵🇹"),

        // ── İkinci Ligler ──
        ["2. Bundesliga"]      = ("Almanya 2. Lig",           "🇩🇪"),
        ["La Liga 2"]          = ("İspanya 2. Lig",           "🇪🇸"),
        ["Eerste Divisie"]     = ("Hollanda 2. Lig",          "🇳🇱"),

        // ── Diğer Ülkeler ──
        ["Allsvenskan"]        = ("İsveç 1. Lig",            "🇸🇪"),
        ["Super League"]       = ("İsviçre Süper Lig",       "🇨🇭"),

        // ── Jupiler / Belçika ──
        ["Jupiler Pro League"] = ("Belçika 1. Lig",          "🇧🇪"),
        ["Belgian Pro League"] = ("Belçika 1. Lig",          "🇧🇪"),

        // ── Arjantin ──
        ["Liga Profesional Argentina"] = ("Arjantin 1. Lig", "🇦🇷"),
    };

    // Avusturya Bundesliga — country ile ayırt edilir
    private static readonly Dictionary<string, (string DisplayName, string Flag)> _countryOverrides =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["Austria_Bundesliga"]  = ("Avusturya Bundesliga",    "🇦🇹"),
    };

    /// <summary>
    /// Lig adını Türkçe karşılığıyla gösterir.
    /// Country parametresi opsiyonel — aynı isimli ligleri ayırt etmek için.
    /// </summary>
    public static string ToTurkish(string? leagueName, string? country = null)
    {
        if (string.IsNullOrWhiteSpace(leagueName)) return "-";

        // Önce country+name combo dene (Avusturya Bundesliga vs Almanya Bundesliga)
        if (!string.IsNullOrWhiteSpace(country))
        {
            string key = $"{country}_{leagueName}";
            if (_countryOverrides.TryGetValue(key, out var co))
                return co.DisplayName;
        }

        if (_map.TryGetValue(leagueName, out var entry))
            return entry.DisplayName;

        return leagueName; // mapping yoksa orijinal ad
    }

    /// <summary>
    /// Lig bayrağını döndürür.
    /// </summary>
    public static string GetFlag(string? leagueName, string? country = null)
    {
        if (string.IsNullOrWhiteSpace(leagueName)) return "⚽";

        if (!string.IsNullOrWhiteSpace(country))
        {
            string key = $"{country}_{leagueName}";
            if (_countryOverrides.TryGetValue(key, out var co))
                return co.Flag;
        }

        if (_map.TryGetValue(leagueName, out var entry))
            return entry.Flag;

        return "⚽";
    }

    /// <summary>
    /// Bayrak + Türkçe lig adı — UI'da en çok kullanılacak format.
    /// </summary>
    public static string ToDisplay(string? leagueName, string? country = null)
    {
        return $"{GetFlag(leagueName, country)} {ToTurkish(leagueName, country)}";
    }

    // ── Sol menü sıralaması ────────────────────────────────────────────────
    // Tier 0: Büyük 5 (sabit sırayla) → Tier 1: diğer 1. ligler (alfabetik) →
    // Tier 2: 2. ligler / alt ligler (alfabetik, en altta).
    // Name+Country combo ile eşleşir (aynı isimli iki lig — örn. Bundesliga
    // Almanya/Avusturya — country ile ayrılır).
    private static readonly string[] _big5Order =
    {
        "England_Premier League",
        "Germany_Bundesliga",
        "Almanya_Bundesliga",   // DB'de bazı eski kayıtlarda country Türkçe "Almanya" olabilir
        "France_Ligue 1",
        "Spain_La Liga",
        "Italy_Serie A",
    };

    private static readonly HashSet<string> _secondTierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "2. Bundesliga", "La Liga 2", "Segunda División", "Eerste Divisie"
    };

    /// <summary>
    /// Sol menüdeki lig sıralaması için (tier, sıraNo) döner — küçük olan önce gelir.
    /// tier 0 = Büyük 5 (belirtilen sabit sırada), 1 = diğer 1. ligler, 2 = 2. ligler.
    /// sıraNo: tier 0 için sabit sıra index'i, tier 1/2 için 0 (view'da Türkçe ada göre
    /// alfabetik sıralanır — burada sadece grup belirlenir).
    /// </summary>
    public static (int Tier, int Order) GetSidebarRank(string? leagueName, string? country)
    {
        if (string.IsNullOrWhiteSpace(leagueName)) return (1, 0);

        string key = $"{country}_{leagueName}";
        int big5Index = Array.IndexOf(_big5Order, key);
        if (big5Index >= 0) return (0, big5Index);

        if (_secondTierNames.Contains(leagueName)) return (2, 0);

        return (1, 0);
    }
}
