using DogisAnaliz.Data;
using DogisAnaliz.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Controllers;

public class SyncController : Controller
{
    private readonly AppDbContext   _context;
    private readonly FootballApiService _api;
    private readonly DogiService    _dogi;

    // Desteklenen ligler — API ID'leri
    public static readonly LeagueConfig[] KnownLeagues =
    {
        // ── Büyük Ligler ──────────────────────────────────────
        new(39,  "Premier League",     "England",     true),
        new(140, "La Liga",            "Spain",       false),
        new(78,  "Bundesliga",         "Germany",     false),
        new(135, "Serie A",            "Italy",       false),
        new(61,  "Ligue 1",            "France",      false),
        new(203, "Süper Lig",          "Turkey",      false),
        new(88,  "Eredivisie",         "Netherlands", false),
        new(94,  "Primeira Liga",      "Portugal",    false),

        // ── 2. Ligler ─────────────────────────────────────────
        new(79,  "2. Bundesliga",      "Germany",     false),
        new(141, "La Liga 2",          "Spain",       false),
        new(89,  "Eerste Divisie",     "Netherlands", false),

        // ── Diğer Ligler ──────────────────────────────────────
        new(113, "Allsvenskan",        "Sweden",      false),
        new(218, "Bundesliga",         "Austria",     false),
        new(207, "Super League",       "Switzerland", false),
    };

    // Sezonlar: 2020-21 → 2026-27 (aktif)
    public static readonly int[] KnownSeasons = { 2020, 2021, 2022, 2023, 2024, 2025, 2026 };

    public SyncController(AppDbContext context, FootballApiService api, DogiService dogi)
    {
        _context = context;
        _api     = api;
        _dogi    = dogi;
    }

    // ─────────────────────────────────────────────────────────
    // GET /Sync — durum tablosu
    // ─────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rows = new List<SyncStatusRow>();

        // DB'deki mevcut maç sayılarını toplu çek (verimli)
        var matchCounts = await _context.Matches
            .AsNoTracking()
            .GroupBy(m => new { m.LeagueId, m.SeasonId })
            .Select(g => new { g.Key.LeagueId, g.Key.SeasonId, Count = g.Count() })
            .ToListAsync();

        var leagues = await _context.Leagues.AsNoTracking().ToListAsync();
        var seasons = await _context.Seasons.AsNoTracking().ToListAsync();

        foreach (var lc in KnownLeagues)
        {
            var dbLeague = leagues.FirstOrDefault(l =>
                string.Equals(l.Name, lc.Name, StringComparison.OrdinalIgnoreCase));

            foreach (var sy in KnownSeasons)
            {
                string seasonName = $"{sy}-{sy + 1}";
                var dbSeason = seasons.FirstOrDefault(s => s.SeasonName == seasonName);

                int matchCount = 0;
                if (dbLeague != null && dbSeason != null)
                {
                    var mc = matchCounts.FirstOrDefault(m =>
                        m.LeagueId == dbLeague.Id && m.SeasonId == dbSeason.Id);
                    matchCount = mc?.Count ?? 0;
                }

                rows.Add(new SyncStatusRow(
                    lc.ApiId, lc.Name, lc.Country, sy, seasonName, matchCount));
            }
        }

        if (TempData["SyncResult"] != null)
            ViewBag.SyncResult = TempData["SyncResult"];

        return View(rows);
    }

    // ─────────────────────────────────────────────────────────
    // POST /Sync/Season — tek sezon çek
    // ─────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Season(int apiLeagueId, int seasonYear, string leagueName)
    {
        try
        {
            Console.WriteLine($"[Sync] {leagueName} {seasonYear}-{seasonYear+1} çekiliyor...");
            int count = await _api.SyncLeagueSeasonAsync(apiLeagueId, seasonYear);

            // Yeni maçlar için Dogi taraması yenile
            if (count > 0)
            {
                Console.WriteLine($"[Sync] Dogi taraması başlatılıyor ({count} yeni maç)...");
                await _dogi.ScanAllAsync();
            }

            TempData["SyncResult"] = $"✅ {leagueName} {seasonYear}-{seasonYear+1}: {count} maç eklendi." +
                                     (count > 0 ? " Dogi taraması güncellendi." : "");
        }
        catch (Exception ex)
        {
            TempData["SyncResult"] = $"❌ Hata: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────
    // POST /Sync/LeagueAll — bir ligin tüm sezonlarını çek
    // ─────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> LeagueAll(int apiLeagueId, string leagueName)
    {
        int total = 0;
        var errors = new List<string>();

        foreach (var sy in KnownSeasons)
        {
            string seasonName = $"{sy}-{sy + 1}";
            try
            {
                // DB'de zaten var mı?
                var dbLeague = await _context.Leagues
                    .FirstOrDefaultAsync(l => l.Name.ToLower() == leagueName.ToLower());
                var dbSeason = await _context.Seasons
                    .FirstOrDefaultAsync(s => s.SeasonName == seasonName);
                if (dbLeague != null && dbSeason != null)
                {
                    bool hasData = await _context.Matches.AnyAsync(m =>
                        m.LeagueId == dbLeague.Id && m.SeasonId == dbSeason.Id);
                    if (hasData)
                    {
                        Console.WriteLine($"[Sync] {leagueName} {seasonName} zaten mevcut, atlanıyor.");
                        continue;
                    }
                }

                Console.WriteLine($"[Sync] {leagueName} {seasonName} çekiliyor...");
                int count = await _api.SyncLeagueSeasonAsync(apiLeagueId, sy);
                total += count;
                Console.WriteLine($"[Sync] {leagueName} {seasonName}: {count} maç eklendi.");
                await Task.Delay(800); // Rate limit arası bekleme
            }
            catch (Exception ex)
            {
                errors.Add($"{seasonName}: {ex.Message}");
                Console.WriteLine($"[Sync Hata] {leagueName} {seasonName}: {ex.Message}");
            }
        }

        TempData["SyncResult"] = errors.Any()
            ? $"✅ {leagueName} tamamlandı: {total} maç eklendi. ⚠️ Hatalar: {string.Join(", ", errors)}"
            : $"✅ {leagueName} tamamlandı: {total} maç eklendi. Dogi taraması güncellendi.";

        // Tüm yeni veriler için Dogi taramasını yenile
        if (total > 0)
        {
            Console.WriteLine($"[Sync] Dogi taraması başlatılıyor ({total} yeni maç)...");
            await _dogi.ScanAllAsync();
            Console.WriteLine("[Sync] Dogi taraması tamamlandı.");
        }

        return RedirectToAction(nameof(Index));
    }
}

// ─── Yardımcı record'lar ────────────────────────────────────
public record LeagueConfig(int ApiId, string Name, string Country, bool IsPriority);
public record SyncStatusRow(int ApiId, string LeagueName, string Country,
    int SeasonYear, string SeasonName, int MatchCount)
{
    public bool IsSynced  => MatchCount > 0;
    public bool IsActive  => SeasonYear == 2026;
}
