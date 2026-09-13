using DogisAnaliz.Data;
using DogisAnaliz.Models;
using DogisAnaliz.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Controllers;

public class AdminController : Controller
{
    private readonly AppDbContext _context;
    public AdminController(AppDbContext context) => _context = context;

    // ─────────────────────────────────────────────────────────
    // GET /Admin/MergeTeams — duplicate takımları listele
    // ─────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> MergeTeams()
    {
        var teams = await _context.Teams.AsNoTracking().OrderBy(t => t.Name).ToListAsync();

        // Lowercase key'e göre gruplama — aynı ismin farklı case'leri
        var duplicates = teams
            .GroupBy(t => t.Name.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.OrderBy(t => t.Id).ToList())
            .ToList();

        // FC eki olanları da tespit et: "Arsenal" vs "Arsenal FC"
        var baseKeyGroups = teams
            .GroupBy(t => TeamNameNormalizer.ToBaseKey(t.Name))
            .Where(g => g.Count() > 1)
            .Select(g => g.OrderBy(t => t.Id).ToList())
            .ToList();

        // İkisini birleştir, unique gruplar
        var allDuplicates = duplicates
            .Concat(baseKeyGroups)
            .GroupBy(g => string.Join(",", g.Select(t => t.Id).OrderBy(id => id)))
            .Select(g => g.First())
            .OrderByDescending(g => g.Count)
            .ToList();

        ViewBag.Duplicates = allDuplicates;
        ViewBag.TotalTeams = teams.Count;
        ViewBag.AllTeams   = teams; // Manuel merge için tüm takım listesi
        return View();
    }

    // ─────────────────────────────────────────────────────────
    // POST /Admin/MergeTeam — keepId takımını koru, deleteId'yi sil
    // ─────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> MergeTeam(int keepId, int deleteId)
    {
        if (keepId == deleteId)
            return RedirectToAction(nameof(MergeTeams));

        var keepTeam   = await _context.Teams.FindAsync(keepId);
        var deleteTeam = await _context.Teams.FindAsync(deleteId);
        if (keepTeam == null || deleteTeam == null)
            return RedirectToAction(nameof(MergeTeams));

        // Tüm maçlardaki referansları güncelle
        var homeMatches = await _context.Matches
            .Where(m => m.HomeTeamId == deleteId).ToListAsync();
        var awayMatches = await _context.Matches
            .Where(m => m.AwayTeamId == deleteId).ToListAsync();

        foreach (var m in homeMatches) m.HomeTeamId = keepId;
        foreach (var m in awayMatches) m.AwayTeamId = keepId;

        // DogiPatterns'deki referansları da güncelle
        var dogiPatterns = await _context.DogiPatterns
            .Where(d => d.TeamId == deleteId).ToListAsync();
        foreach (var d in dogiPatterns) d.TeamId = keepId;

        // FixtureSchedules'daki (fikstür takvimi) referansları da güncelle
        var fixtureHome = await _context.FixtureSchedules
            .Where(f => f.HomeTeamId == deleteId).ToListAsync();
        var fixtureAway = await _context.FixtureSchedules
            .Where(f => f.AwayTeamId == deleteId).ToListAsync();
        foreach (var f in fixtureHome) f.HomeTeamId = keepId;
        foreach (var f in fixtureAway) f.AwayTeamId = keepId;

        await _context.SaveChangesAsync();

        // Duplicate takımı sil
        _context.Teams.Remove(deleteTeam);
        await _context.SaveChangesAsync();

        // İki takım birleşince, aynı gerçek maçı temsil eden iki Match satırı (aynı lig+sezon+hafta+ev+dep)
        // artık birebir aynı hale gelmiş olabilir — bunları otomatik temizle.
        var (dupGroups, dupDeleted) = await DedupeMatchesAsync();

        TempData["Success"] = $"'{deleteTeam.Name}' → '{keepTeam.Name}' ile birleştirildi. " +
                              $"({homeMatches.Count + awayMatches.Count} maç, {dogiPatterns.Count} Dogi, " +
                              $"{fixtureHome.Count + fixtureAway.Count} fikstür kaydı güncellendi)" +
                              (dupDeleted > 0
                                  ? $" ⚠️ Birleştirme {dupGroups} kopya maç grubu oluşturdu, {dupDeleted} fazladan kayıt otomatik silindi."
                                  : "");

        return RedirectToAction(nameof(MergeTeams));
    }

    // ─────────────────────────────────────────────────────────
    // POST /Admin/DedupeMatches — aynı lig+sezon+hafta+ev+deplasman'a sahip
    // kopya Match satırlarını temizle (elle de tetiklenebilir, MergeTeam sonrası otomatik çalışır)
    // ─────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> DedupeMatches()
    {
        var (groups, deleted) = await DedupeMatchesAsync();
        TempData["Success"] = deleted > 0
            ? $"🧹 {groups} kopya maç grubu bulundu, {deleted} fazladan kayıt silindi."
            : "🧹 Kopya maç bulunamadı — veri temiz.";
        return RedirectToAction(nameof(MergeTeams));
    }

    /// <summary>
    /// Aynı (LeagueId, SeasonId, Week, HomeTeamId, AwayTeamId) için birden fazla Match satırı varsa
    /// (iki takım birleştirildiğinde iki farklı kaynaktan gelen aynı maç birebir aynı hale gelebilir),
    /// en düşük Id'li satırı koru, gerisini (bağlı MatchSurprise/MatchDetail/DogiPattern dahil) sil.
    /// </summary>
    private async Task<(int Groups, int Deleted)> DedupeMatchesAsync()
    {
        var all = await _context.Matches.AsNoTracking()
            .Select(m => new { m.Id, m.LeagueId, m.SeasonId, m.Week, m.HomeTeamId, m.AwayTeamId })
            .ToListAsync();

        var dupGroups = all
            .GroupBy(m => (m.LeagueId, m.SeasonId, m.Week, m.HomeTeamId, m.AwayTeamId))
            .Where(g => g.Count() > 1)
            .ToList();

        var toDelete = dupGroups
            .SelectMany(g => g.OrderBy(x => x.Id).Skip(1)) // ilkini koru, gerisini sil
            .Select(x => x.Id)
            .ToList();

        if (toDelete.Count == 0) return (0, 0);

        await using var tx = await _context.Database.BeginTransactionAsync();

        await _context.DogiPatterns
            .Where(d => toDelete.Contains(d.TriggerMatch1Id) || toDelete.Contains(d.TriggerMatch2Id)
                     || (d.AlertMatchId != null && toDelete.Contains(d.AlertMatchId.Value)))
            .ExecuteDeleteAsync();
        await _context.MatchDetails.Where(md => toDelete.Contains(md.MatchId)).ExecuteDeleteAsync();
        await _context.MatchSurprises.Where(ms => toDelete.Contains(ms.MatchId)).ExecuteDeleteAsync();
        await _context.Matches.Where(m => toDelete.Contains(m.Id)).ExecuteDeleteAsync();

        await tx.CommitAsync();
        return (dupGroups.Count, toDelete.Count);
    }

    // ─────────────────────────────────────────────────────────
    // POST /Admin/NormalizeAllTeams — tüm takım adlarını normalize et
    // ─────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> NormalizeAllTeams()
    {
        var teams = await _context.Teams.ToListAsync();
        int changed = 0;
        foreach (var t in teams)
        {
            var normalized = TeamNameNormalizer.Normalize(t.Name);
            if (normalized != t.Name)
            {
                t.Name = normalized;
                changed++;
            }
        }
        await _context.SaveChangesAsync();
        TempData["Success"] = $"{changed} takım adı normalize edildi.";
        return RedirectToAction(nameof(MergeTeams));
    }
}
