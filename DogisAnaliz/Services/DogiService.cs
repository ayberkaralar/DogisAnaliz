using DogisAnaliz.Data;
using DogisAnaliz.Models;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Services;

public class DogiService
{
    private readonly AppDbContext _context;

    public DogiService(AppDbContext context)
    {
        _context = context;
    }

    // Tüm takımları tara, Dogi patternleri bul ve kaydet
    public async Task ScanAllAsync()
    {
        // Önce mevcut PENDING'leri güncelle (artık oynanmış olabilir)
        await UpdatePendingPatternsAsync();

        // Tüm takım ID'lerini çek
        var teamIds = await _context.Matches
            .Select(m => m.HomeTeamId)
            .Union(_context.Matches.Select(m => m.AwayTeamId))
            .Distinct()
            .ToListAsync();

        foreach (var teamId in teamIds)
        {
            await ScanTeamAsync(teamId);
        }
    }

    private async Task ScanTeamAsync(int teamId)
    {
        // Takımın tüm maçlarını tarihe göre sırala
        var matches = await _context.Matches
            .AsNoTracking()
            .Include(m => m.Surprise)
            .Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId)
            .OrderBy(m => m.MatchDate)
            .ThenBy(m => m.Id)
            .ToListAsync();

        if (matches.Count < 2) return;

        // Mevcut pattern çiftlerini bir kez çek (performans)
        var existingPairs = await _context.DogiPatterns
            .Where(d => d.TeamId == teamId)
            .Select(d => new { d.TriggerMatch1Id, d.TriggerMatch2Id })
            .ToListAsync();

        var existingPairSet = existingPairs
            .Select(p => (p.TriggerMatch1Id, p.TriggerMatch2Id))
            .ToHashSet();

        var newPatterns = new List<DogiPattern>();

        for (int i = 0; i < matches.Count - 1; i++)
        {
            var m1 = matches[i];
            var m2 = matches[i + 1];

            if (existingPairSet.Contains((m1.Id, m2.Id))) continue;

            string? patternType = DetectPattern(m1, m2);
            if (patternType == null) continue;

            // 3. maç var mı?
            Match? m3 = (i + 2 < matches.Count) ? matches[i + 2] : null;

            string alertResult = "PENDING";
            if (m3 != null)
            {
                alertResult = (m3.Surprise != null && m3.Surprise.SurpriseType != "NONE")
                    ? "SURPRISE"
                    : "NORMAL";
            }

            newPatterns.Add(new DogiPattern
            {
                TeamId          = teamId,
                LeagueId        = m1.LeagueId,
                SeasonId        = m1.SeasonId,
                TriggerMatch1Id = m1.Id,
                TriggerMatch2Id = m2.Id,
                AlertMatchId    = m3?.Id,
                PatternType     = patternType,
                AlertResult     = alertResult,
                DetectedAt      = DateTime.UtcNow
            });
        }

        if (newPatterns.Any())
        {
            _context.DogiPatterns.AddRange(newPatterns);
            await _context.SaveChangesAsync();
        }
    }

    private async Task UpdatePendingPatternsAsync()
    {
        // AlertMatchId olan ama PENDING kalan pattern'leri güncelle
        var pendingWithMatch = await _context.DogiPatterns
            .Include(d => d.AlertMatch)
                .ThenInclude(m => m!.Surprise)
            .Where(d => d.AlertResult == "PENDING" && d.AlertMatchId != null)
            .ToListAsync();

        foreach (var pattern in pendingWithMatch)
        {
            if (pattern.AlertMatch == null) continue;
            pattern.AlertResult = (pattern.AlertMatch.Surprise != null &&
                                   pattern.AlertMatch.Surprise.SurpriseType != "NONE")
                ? "SURPRISE"
                : "NORMAL";
        }

        // AlertMatchId null olan PENDING'lere artık 3. maç gelmiş mi bak
        var pendingNoMatch = await _context.DogiPatterns
            .Include(d => d.TriggerMatch2)
            .Where(d => d.AlertResult == "PENDING" && d.AlertMatchId == null)
            .ToListAsync();

        foreach (var pattern in pendingNoMatch)
        {
            var nextMatch = await _context.Matches
                .AsNoTracking()
                .Include(m => m.Surprise)
                .Where(m => (m.HomeTeamId == pattern.TeamId || m.AwayTeamId == pattern.TeamId)
                         && m.MatchDate > pattern.TriggerMatch2.MatchDate)
                .OrderBy(m => m.MatchDate)
                .ThenBy(m => m.Id)
                .FirstOrDefaultAsync();

            if (nextMatch == null) continue;

            pattern.AlertMatchId = nextMatch.Id;
            pattern.AlertResult  = (nextMatch.Surprise != null &&
                                    nextMatch.Surprise.SurpriseType != "NONE")
                ? "SURPRISE"
                : "NORMAL";
        }

        if (pendingWithMatch.Any() || pendingNoMatch.Any())
            await _context.SaveChangesAsync();
    }

    // ---------------------------------------------------------------
    // Desen tespiti
    // ---------------------------------------------------------------
    private static string? DetectPattern(Match m1, Match m2)
    {
        bool m1High = IsHighScore(m1);
        bool m1Draw = IsHighDraw(m1);
        bool m2High = IsHighScore(m2);
        bool m2Draw = IsHighDraw(m2);

        if (m1High && m2Draw) return "HIGH_DRAW";
        if (m1Draw && m2High) return "DRAW_HIGH";
        return null;
    }

    // Tam olarak 5-1 veya 1-5
    public static bool IsHighScore(Match m)
        => (m.FtHomeScore == 5 && m.FtAwayScore == 1)
        || (m.FtHomeScore == 1 && m.FtAwayScore == 5);

    // Tam olarak 2-2
    public static bool IsHighDraw(Match m)
        => m.FtHomeScore == 2 && m.FtAwayScore == 2;
}
