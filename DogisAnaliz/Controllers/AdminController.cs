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

        await _context.SaveChangesAsync();

        // Duplicate takımı sil
        _context.Teams.Remove(deleteTeam);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"'{deleteTeam.Name}' → '{keepTeam.Name}' ile birleştirildi. " +
                              $"({homeMatches.Count + awayMatches.Count} maç, {dogiPatterns.Count} Dogi kaydı güncellendi)";

        return RedirectToAction(nameof(MergeTeams));
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
