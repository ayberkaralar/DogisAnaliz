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
    private readonly FixtureScheduleService _schedule;

    // Desteklenen ligler/sezonlar Services/LeagueCatalog.cs içine taşındı
    public static LeagueConfig[] KnownLeagues => LeagueCatalog.KnownLeagues;
    public static int[] KnownSeasons => LeagueCatalog.KnownSeasons;

    public SyncController(AppDbContext context, FootballApiService api, DogiService dogi, FixtureScheduleService schedule)
    {
        _context  = context;
        _api      = api;
        _dogi     = dogi;
        _schedule = schedule;
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

        // Fikstür takvimi (oynanmamış maçlar dahil) sayıları
        var scheduleCounts = await _context.FixtureSchedules
            .AsNoTracking()
            .GroupBy(f => new { f.LeagueId, f.SeasonId })
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
                int scheduleCount = 0;
                if (dbLeague != null && dbSeason != null)
                {
                    var mc = matchCounts.FirstOrDefault(m =>
                        m.LeagueId == dbLeague.Id && m.SeasonId == dbSeason.Id);
                    matchCount = mc?.Count ?? 0;

                    var sc = scheduleCounts.FirstOrDefault(s =>
                        s.LeagueId == dbLeague.Id && s.SeasonId == dbSeason.Id);
                    scheduleCount = sc?.Count ?? 0;
                }

                rows.Add(new SyncStatusRow(
                    lc.ApiId, lc.Name, lc.Country, sy, seasonName, matchCount, scheduleCount));
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
    // POST /Sync/Schedule — bir lig+sezonun FİKSTÜR TAKVİMİNİ çek
    // (oynanmamış NS maçlar dahil). FixtureSchedule tablosuna upsert.
    // ─────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Schedule(int apiLeagueId, int seasonYear, string leagueName)
    {
        try
        {
            Console.WriteLine($"[Sync] {leagueName} {seasonYear}-{seasonYear + 1} FİKSTÜR TAKVİMİ çekiliyor...");
            var (added, updated, total) = await _schedule.SyncScheduleAsync(apiLeagueId, seasonYear);
            TempData["SyncResult"] =
                $"✅ {leagueName} {seasonYear}-{seasonYear + 1} fikstür takvimi: {total} maç işlendi " +
                $"({added} yeni, {updated} güncellendi). Oynanmamış maçlar dahil.";
        }
        catch (Exception ex)
        {
            TempData["SyncResult"] = $"❌ Fikstür takvimi hatası: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────
    // POST /Sync/RefreshAllActive — TÜM liglerin AKTİF sezonunu güncelle
    // (Ana Sayfa'daki "Senkronize Et" butonu buraya gönderir)
    // ─────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> RefreshAllActive(string? returnUrl = null)
    {
        try
        {
            Console.WriteLine("[Sync] Tüm liglerin aktif sezonu güncelleniyor...");
            var r = await ActiveSeasonRefresher.RefreshAsync(
                _context, _api, _dogi,
                log: msg => Console.WriteLine($"[Sync] {msg}"));

            var msg = $"✅ Aktif sezon güncellendi — {r.LeaguesProcessed} lig tarandı, {r.MatchesAdded} yeni maç eklendi.";
            if (r.MatchesAdded > 0) msg += " Dogi taraması yenilendi.";
            if (r.Errors.Count > 0) msg += $" ⚠️ Hatalar: {string.Join(", ", r.Errors)}";
            TempData["SyncResult"] = msg;
        }
        catch (Exception ex)
        {
            TempData["SyncResult"] = $"❌ Senkronizasyon hatası: {ex.Message}";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
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
public record SyncStatusRow(int ApiId, string LeagueName, string Country,
    int SeasonYear, string SeasonName, int MatchCount, int ScheduleCount = 0)
{
    public bool IsSynced  => MatchCount > 0;
    public bool IsActive  => SeasonYear == 2026;
    public bool HasSchedule => ScheduleCount > 0;
}
