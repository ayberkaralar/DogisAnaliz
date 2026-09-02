using DogisAnaliz.Data;
using DogisAnaliz.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Controllers;

public class DogiController : Controller
{
    private readonly AppDbContext _context;

    public DogiController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? leagueId, int? seasonId, string result = "ALL")
    {
        var vm = new DogiViewModel
        {
            SelectedLeagueId = leagueId,
            SelectedSeasonId = seasonId,
            SelectedResult   = result
        };

        // Ligler
        vm.Leagues = await _context.Leagues.AsNoTracking()
            .OrderBy(l => l.Name)
            .ToListAsync();

        // Sezonlar — seçili lig varsa sadece o ligin sezonları
        if (leagueId.HasValue)
        {
            var seasonIds = await _context.DogiPatterns
                .Where(d => d.LeagueId == leagueId.Value)
                .Select(d => d.SeasonId).Distinct().ToListAsync();

            vm.Seasons = await _context.Seasons.AsNoTracking()
                .Where(s => seasonIds.Contains(s.Id))
                .OrderByDescending(s => s.StartYear)
                .ToListAsync();
        }
        else
        {
            vm.Seasons = await _context.Seasons.AsNoTracking()
                .OrderByDescending(s => s.StartYear).ToListAsync();
        }

        // İstatistikler (tüm filtre bağımsız — seçili lig/sezon)
        var statsQuery = _context.DogiPatterns.AsNoTracking();
        if (leagueId.HasValue) statsQuery = statsQuery.Where(d => d.LeagueId == leagueId.Value);
        if (seasonId.HasValue)  statsQuery = statsQuery.Where(d => d.SeasonId == seasonId.Value);

        vm.TotalPatterns = await statsQuery.CountAsync();
        vm.SurpriseCount = await statsQuery.CountAsync(d => d.AlertResult == "SURPRISE");
        vm.NormalCount   = await statsQuery.CountAsync(d => d.AlertResult == "NORMAL");
        vm.PendingCount  = await statsQuery.CountAsync(d => d.AlertResult == "PENDING");

        // PENDING Uyarılar (güncel — lig filtresi uygulanır, sezon yok)
        var pendingQuery = _context.DogiPatterns.AsNoTracking()
            .Include(d => d.Team)
            .Include(d => d.League)
            .Include(d => d.Season)
            .Include(d => d.TriggerMatch1).ThenInclude(m => m.HomeTeam)
            .Include(d => d.TriggerMatch1).ThenInclude(m => m.AwayTeam)
            .Include(d => d.TriggerMatch2).ThenInclude(m => m.HomeTeam)
            .Include(d => d.TriggerMatch2).ThenInclude(m => m.AwayTeam)
            .Include(d => d.AlertMatch).ThenInclude(m => m!.HomeTeam)
            .Include(d => d.AlertMatch).ThenInclude(m => m!.AwayTeam)
            .Include(d => d.AlertMatch).ThenInclude(m => m!.Surprise)
            .Where(d => d.AlertResult == "PENDING");

        if (leagueId.HasValue) pendingQuery = pendingQuery.Where(d => d.LeagueId == leagueId.Value);

        vm.PendingAlerts = (await pendingQuery
            .OrderByDescending(d => d.DetectedAt)
            .Take(50)
            .ToListAsync())
            .Select(ToDto).ToList();

        // Geçmiş Pattern Tablosu
        var patternQuery = _context.DogiPatterns.AsNoTracking()
            .Include(d => d.Team)
            .Include(d => d.League)
            .Include(d => d.Season)
            .Include(d => d.TriggerMatch1).ThenInclude(m => m.HomeTeam)
            .Include(d => d.TriggerMatch1).ThenInclude(m => m.AwayTeam)
            .Include(d => d.TriggerMatch2).ThenInclude(m => m.HomeTeam)
            .Include(d => d.TriggerMatch2).ThenInclude(m => m.AwayTeam)
            .Include(d => d.AlertMatch).ThenInclude(m => m!.HomeTeam)
            .Include(d => d.AlertMatch).ThenInclude(m => m!.AwayTeam)
            .Include(d => d.AlertMatch).ThenInclude(m => m!.Surprise)
            .Where(d => d.AlertResult != "PENDING");

        if (leagueId.HasValue) patternQuery = patternQuery.Where(d => d.LeagueId == leagueId.Value);
        if (seasonId.HasValue)  patternQuery = patternQuery.Where(d => d.SeasonId == seasonId.Value);
        if (result == "SURPRISE") patternQuery = patternQuery.Where(d => d.AlertResult == "SURPRISE");
        else if (result == "NORMAL") patternQuery = patternQuery.Where(d => d.AlertResult == "NORMAL");

        vm.Patterns = (await patternQuery
            .OrderByDescending(d => d.TriggerMatch2.MatchDate)
            .ToListAsync())
            .Select(ToDto).ToList();

        return View(vm);
    }

    private static DogiRowDto ToDto(DogiPattern d) => new()
    {
        Id          = d.Id,
        TeamName    = d.Team?.Name ?? "-",
        LeagueName  = d.League?.Name ?? "-",
        SeasonName  = d.Season?.SeasonName ?? "-",
        PatternType = d.PatternType,
        AlertResult = d.AlertResult,

        Match1Teams = $"{d.TriggerMatch1.HomeTeam?.Name} - {d.TriggerMatch1.AwayTeam?.Name}",
        Match1Score = $"{d.TriggerMatch1.FtHomeScore}-{d.TriggerMatch1.FtAwayScore}",
        Match1Date  = d.TriggerMatch1.MatchDate,
        Match1Week  = d.TriggerMatch1.Week,

        Match2Teams = $"{d.TriggerMatch2.HomeTeam?.Name} - {d.TriggerMatch2.AwayTeam?.Name}",
        Match2Score = $"{d.TriggerMatch2.FtHomeScore}-{d.TriggerMatch2.FtAwayScore}",
        Match2Date  = d.TriggerMatch2.MatchDate,
        Match2Week  = d.TriggerMatch2.Week,

        Match3Teams        = d.AlertMatch != null ? $"{d.AlertMatch.HomeTeam?.Name} - {d.AlertMatch.AwayTeam?.Name}" : null,
        Match3Score        = d.AlertMatch != null ? $"{d.AlertMatch.FtHomeScore}-{d.AlertMatch.FtAwayScore}" : null,
        Match3Date         = d.AlertMatch?.MatchDate,
        Match3Week         = d.AlertMatch?.Week,
        Match3SurpriseType = d.AlertMatch?.Surprise?.SurpriseType
    };
}
