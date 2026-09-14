using DogisAnaliz.Data;
using DogisAnaliz.Models;
using DogisAnaliz.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Controllers;

/// <summary>
/// Fikstür Takvimi — FixtureSchedule tablosunu (oynanmamış NS maçlar dahil)
/// lig / sezon / hafta / takım / durum filtreleriyle gösterir. Manuel analiz içindir.
/// </summary>
public class FixturesController : Controller
{
    private readonly AppDbContext _context;

    public FixturesController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? leagueId, int? seasonId, int? teamId, int? week, string status = "ALL")
    {
        var vm = new FixtureCalendarViewModel
        {
            SelectedTeamId = teamId,
            SelectedWeek   = week,
            SelectedStatus = status
        };

        // 1. Takvim kaydı olan ligler
        var leagueCounts = await _context.FixtureSchedules.AsNoTracking()
            .GroupBy(f => f.LeagueId)
            .Select(g => new { LeagueId = g.Key, Count = g.Count() })
            .ToListAsync();

        var leagueIds = leagueCounts.Select(x => x.LeagueId).ToList();
        var leagueEntities = await _context.Leagues.AsNoTracking()
            .Where(l => leagueIds.Contains(l.Id))
            .ToListAsync();

        vm.Leagues = leagueEntities
            .Select(l => new FixtureLeagueDto
            {
                Id = l.Id,
                Name = l.Name,
                Country = l.Country,
                Count = leagueCounts.First(c => c.LeagueId == l.Id).Count
            })
            .OrderBy(l => l.Name)
            .ToList();

        if (vm.Leagues.Count == 0)
            return View(vm); // henüz hiç takvim çekilmemiş

        // 2. Varsayılan lig: Premier League varsa o, yoksa en çok kayıt olan
        if (!leagueId.HasValue)
            leagueId = (vm.Leagues.FirstOrDefault(l => l.Name == "Premier League")
                        ?? vm.Leagues.OrderByDescending(l => l.Count).First()).Id;
        vm.SelectedLeagueId = leagueId;
        vm.SelectedLeagueName = vm.Leagues.FirstOrDefault(l => l.Id == leagueId)?.Name ?? "Lig";

        // 3. Bu ligde takvim kaydı olan sezonlar
        var seasonIds = await _context.FixtureSchedules.AsNoTracking()
            .Where(f => f.LeagueId == leagueId.Value)
            .Select(f => f.SeasonId).Distinct().ToListAsync();

        vm.Seasons = await _context.Seasons.AsNoTracking()
            .Where(s => seasonIds.Contains(s.Id))
            .OrderByDescending(s => s.StartYear)
            .ToListAsync();

        // 4. Varsayılan sezon: önce aktif sezon (LeagueCatalog.ActiveSeasonYear), yoksa en güncel
        if (!seasonId.HasValue)
            seasonId = vm.Seasons.FirstOrDefault(s => s.SeasonName == LeagueCatalog.ActiveSeasonName)?.Id
                ?? vm.Seasons.FirstOrDefault()?.Id;
        vm.SelectedSeasonId = seasonId;
        vm.SelectedSeasonName = vm.Seasons.FirstOrDefault(s => s.Id == seasonId)?.SeasonName ?? "";

        if (!seasonId.HasValue)
            return View(vm);

        // 5. Lig+sezondaki takımlar (fikstürden türetilir)
        var homeTeams = await _context.FixtureSchedules.AsNoTracking()
            .Where(f => f.LeagueId == leagueId.Value && f.SeasonId == seasonId.Value)
            .Select(f => new { f.HomeTeamId, Name = f.HomeTeam.Name })
            .Distinct().ToListAsync();
        var awayTeams = await _context.FixtureSchedules.AsNoTracking()
            .Where(f => f.LeagueId == leagueId.Value && f.SeasonId == seasonId.Value)
            .Select(f => new { f.AwayTeamId, Name = f.AwayTeam.Name })
            .Distinct().ToListAsync();

        vm.Teams = homeTeams.Select(t => new FixtureTeamDto { Id = t.HomeTeamId, Name = t.Name })
            .Concat(awayTeams.Select(t => new FixtureTeamDto { Id = t.AwayTeamId, Name = t.Name }))
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .OrderBy(t => t.Name)
            .ToList();

        // 6. Haftalar
        vm.Weeks = await _context.FixtureSchedules.AsNoTracking()
            .Where(f => f.LeagueId == leagueId.Value && f.SeasonId == seasonId.Value)
            .Select(f => f.Week).Distinct().OrderBy(w => w).ToListAsync();

        // 7. Ana sorgu
        var query = _context.FixtureSchedules.AsNoTracking()
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => f.LeagueId == leagueId.Value && f.SeasonId == seasonId.Value);

        if (teamId.HasValue)
            query = query.Where(f => f.HomeTeamId == teamId.Value || f.AwayTeamId == teamId.Value);

        if (week.HasValue)
            query = query.Where(f => f.Week == week.Value);

        // Özet (durum filtresinden önce)
        vm.TotalCount = await query.CountAsync();
        vm.PlayedCount = await query.CountAsync(f => f.HomeGoals != null && f.AwayGoals != null);
        vm.NotPlayedCount = vm.TotalCount - vm.PlayedCount;

        if (status == "NS")
            query = query.Where(f => f.HomeGoals == null || f.AwayGoals == null);
        else if (status == "PLAYED")
            query = query.Where(f => f.HomeGoals != null && f.AwayGoals != null);

        vm.Rows = (await query
            .OrderBy(f => f.Week).ThenBy(f => f.KickoffUtc)
            .Select(f => new
            {
                f.Week, f.KickoffUtc, f.Status,
                Home = f.HomeTeam.Name, Away = f.AwayTeam.Name,
                f.HomeGoals, f.AwayGoals, f.HomeTeamId
            })
            .ToListAsync())
            .Select(f => new FixtureRowDto
            {
                Week = f.Week,
                KickoffUtc = f.KickoffUtc,
                HomeTeam = f.Home,
                AwayTeam = f.Away,
                Status = f.Status,
                HomeGoals = f.HomeGoals,
                AwayGoals = f.AwayGoals,
                IsHomeTeam = teamId.HasValue && f.HomeTeamId == teamId.Value
            })
            .ToList();

        return View(vm);
    }
}
