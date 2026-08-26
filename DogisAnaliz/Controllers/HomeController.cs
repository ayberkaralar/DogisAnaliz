using DogisAnaliz.Data;
using DogisAnaliz.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DogisAnaliz.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? leagueId, int? seasonId, string filter = "ALL", string sort = "date_desc")
    {
        var viewModel = new DashboardViewModel
        {
            SelectedFilter = filter,
            SortOrder = sort
        };

        // 1. Ligleri ve Sürpriz Sayılarını Al
        var leaguesRaw = await _context.Leagues.AsNoTracking().ToListAsync();
        var surpriseCounts = await _context.Matches
            .AsNoTracking()
            .Where(m => m.Surprise != null && m.Surprise.SurpriseType != "NONE")
            .GroupBy(m => m.LeagueId)
            .Select(g => new { LeagueId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LeagueId, x => x.Count);

        viewModel.Leagues = leaguesRaw.Select(l => new LeagueNavDto
        {
            Id = l.Id,
            Name = l.Name,
            Country = l.Country,
            SurpriseCount = surpriseCounts.TryGetValue(l.Id, out int count) ? count : 0
        })
        .OrderByDescending(l => l.SurpriseCount)
        .ToList();

        // 2. Sezonları Al
        viewModel.Seasons = await _context.Seasons.AsNoTracking().OrderByDescending(s => s.StartYear).ToListAsync();

        // 3. Varsayılan Lig ve Sezon
        if (!leagueId.HasValue && viewModel.Leagues.Any())
        {
            leagueId = viewModel.Leagues.First().Id;
        }

        viewModel.SelectedLeagueId = leagueId;

        if (leagueId.HasValue)
        {
            var currentLeague = viewModel.Leagues.FirstOrDefault(l => l.Id == leagueId.Value);
            viewModel.SelectedLeagueName = currentLeague?.Name ?? "Lig";

            if (!seasonId.HasValue)
            {
                var latestSeasonForLeague = await _context.Matches
                    .Where(m => m.LeagueId == leagueId.Value)
                    .OrderByDescending(m => m.SeasonId)
                    .Select(m => (int?)m.SeasonId)
                    .FirstOrDefaultAsync();

                seasonId = latestSeasonForLeague ?? viewModel.Seasons.FirstOrDefault()?.Id;
            }

            viewModel.SelectedSeasonId = seasonId;

            // 4. SADECE SÜRPRİZ OLAN MAÇLARI SORGULA
            var query = _context.Matches
                .AsNoTracking()
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Surprise)
                .Where(m => m.LeagueId == leagueId.Value && m.Surprise != null && m.Surprise.SurpriseType != "NONE");

            if (seasonId.HasValue)
            {
                query = query.Where(m => m.SeasonId == seasonId.Value);
            }

            // 5. KPI Kartları
            viewModel.TotalMatchesCount = await query.CountAsync();
            viewModel.TurnaroundCount = await query.CountAsync(m => m.Surprise != null && m.Surprise.IsTurnaround);
            viewModel.HighGoalCount = await query.CountAsync(m => m.Surprise != null && m.Surprise.IsHighGoal);
            viewModel.DoubleSurpriseCount = await query.CountAsync(m => m.Surprise != null && m.Surprise.IsDoubleSurprise);

            // 6. Filtreleme
            if (filter == "TURNAROUND")
                query = query.Where(m => m.Surprise != null && m.Surprise.IsTurnaround);
            else if (filter == "HIGH_GOAL")
                query = query.Where(m => m.Surprise != null && m.Surprise.IsHighGoal);
            else if (filter == "DOUBLE")
                query = query.Where(m => m.Surprise != null && m.Surprise.IsDoubleSurprise);

            // 7. TARİHSEL SIRALAMA
            if (sort == "date_asc")
            {
                query = query.OrderBy(m => m.MatchDate).ThenBy(m => m.Week);
            }
            else
            {
                // Varsayılan: En yeniden en eskiye
                query = query.OrderByDescending(m => m.MatchDate).ThenByDescending(m => m.Week);
            }

            // 8. Tabloyu Doldur
            viewModel.Matches = await query
                .Select(m => new MatchRowDto
                {
                    Id = m.Id,
                    Week = m.Week,
                    MatchDate = m.MatchDate,
                    HomeTeam = m.HomeTeam != null ? m.HomeTeam.Name : "-",
                    AwayTeam = m.AwayTeam != null ? m.AwayTeam.Name : "-",
                    HtScore = $"{m.HtHomeScore}-{m.HtAwayScore}",
                    FtScore = $"{m.FtHomeScore}-{m.FtAwayScore}",
                    IyMsCode = m.Surprise != null ? m.Surprise.IyMsCode : "-",
                    TotalGoals = m.Surprise != null ? m.Surprise.TotalGoals : (m.FtHomeScore + m.FtAwayScore),
                    SurpriseType = m.Surprise != null ? m.Surprise.SurpriseType : "NONE"
                }).ToListAsync();
        }

        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
    [HttpGet]
    public async Task<IActionResult> CheckLeagueSeasons(int leagueId = 203)
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://v3.football.api-sports.io/");
        httpClient.DefaultRequestHeaders.Add("x-apisports-key", "39bf4995dd31e7f8c7f33178c9c2101b");

        // Ligin API'deki tüm mevcut sezonlarını ve kapsamını döner
        var response = await httpClient.GetAsync($"leagues?id={leagueId}");
        var json = await response.Content.ReadAsStringAsync();

        return Content(json, "application/json");
    }
}