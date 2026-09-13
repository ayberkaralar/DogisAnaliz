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

    public async Task<IActionResult> Index(int? leagueId, int? seasonId, int? teamId, string filter = "ALL", string sort = "week_asc", string view = "surprise")
    {
        bool fixturesMode = view == "fixtures";

        var viewModel = new DashboardViewModel
        {
            ViewMode       = fixturesMode ? "fixtures" : "surprise",
            SelectedFilter = filter,
            SortOrder      = sort,
            SelectedTeamId = teamId
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
            Id            = l.Id,
            Name          = l.Name,
            Country       = l.Country,
            SurpriseCount = surpriseCounts.TryGetValue(l.Id, out int count) ? count : 0
        })
        .OrderByDescending(l => l.SurpriseCount)
        .ToList();

        // 2. Varsayılan Lig
        if (!leagueId.HasValue && viewModel.Leagues.Any())
            leagueId = viewModel.Leagues.First().Id;

        viewModel.SelectedLeagueId = leagueId;

        // 3. Sezonları Al — sadece seçili ligde maçı olan sezonlar
        if (leagueId.HasValue)
        {
            var seasonIdsForLeague = await _context.Matches
                .AsNoTracking()
                .Where(m => m.LeagueId == leagueId.Value)
                .Select(m => m.SeasonId).Distinct().ToListAsync();

            viewModel.Seasons = await _context.Seasons.AsNoTracking()
                .Where(s => seasonIdsForLeague.Contains(s.Id))
                .OrderByDescending(s => s.StartYear).ToListAsync();
        }
        else
        {
            viewModel.Seasons = await _context.Seasons.AsNoTracking()
                .OrderByDescending(s => s.StartYear).ToListAsync();
        }

        if (leagueId.HasValue)
        {
            var currentLeague = viewModel.Leagues.FirstOrDefault(l => l.Id == leagueId.Value);
            viewModel.SelectedLeagueName = currentLeague?.Name ?? "Lig";

            // Varsayılan sezon: en güncel
            if (!seasonId.HasValue)
            {
                seasonId = await _context.Matches
                    .Where(m => m.LeagueId == leagueId.Value)
                    .OrderByDescending(m => m.SeasonId)
                    .Select(m => (int?)m.SeasonId)
                    .FirstOrDefaultAsync()
                    ?? viewModel.Seasons.FirstOrDefault()?.Id;
            }
            viewModel.SelectedSeasonId = seasonId;

            // 4. Seçili ligdeki takımları al
            if (fixturesMode)
            {
                // Fikstür modu: seçili lig + seçili sezonda maçı olan TÜM takımlar (oynanan maç sayısıyla)
                var teamMatchesHome = await _context.Matches.AsNoTracking()
                    .Where(m => m.LeagueId == leagueId.Value && (!seasonId.HasValue || m.SeasonId == seasonId.Value))
                    .GroupBy(m => new { m.HomeTeamId, m.HomeTeam!.Name })
                    .Select(g => new { g.Key.HomeTeamId, g.Key.Name, Count = g.Count() })
                    .ToListAsync();

                var teamMatchesAway = await _context.Matches.AsNoTracking()
                    .Where(m => m.LeagueId == leagueId.Value && (!seasonId.HasValue || m.SeasonId == seasonId.Value))
                    .GroupBy(m => new { m.AwayTeamId, m.AwayTeam!.Name })
                    .Select(g => new { g.Key.AwayTeamId, g.Key.Name, Count = g.Count() })
                    .ToListAsync();

                var tDict = new Dictionary<int, (string Name, int Count)>();
                foreach (var t in teamMatchesHome)
                    tDict[t.HomeTeamId] = (t.Name, t.Count);
                foreach (var t in teamMatchesAway)
                {
                    if (tDict.TryGetValue(t.AwayTeamId, out var existing))
                        tDict[t.AwayTeamId] = (existing.Name, existing.Count + t.Count);
                    else
                        tDict[t.AwayTeamId] = (t.Name, t.Count);
                }

                viewModel.Teams = tDict
                    .Select(kv => new TeamNavDto { Id = kv.Key, Name = kv.Value.Name, MatchCount = kv.Value.Count })
                    .OrderBy(t => t.Name)
                    .ToList();
            }
            else
            {
                // Sürpriz modu: sürpriz sayısına göre — sezon bağımsız tüm sezonlar
                var teamSurprisesHome = await _context.Matches.AsNoTracking()
                    .Where(m => m.LeagueId == leagueId.Value && m.Surprise != null && m.Surprise.SurpriseType != "NONE")
                    .GroupBy(m => new { m.HomeTeamId, m.HomeTeam!.Name })
                    .Select(g => new { g.Key.HomeTeamId, g.Key.Name, Count = g.Count() })
                    .ToListAsync();

                var teamSurprisesAway = await _context.Matches.AsNoTracking()
                    .Where(m => m.LeagueId == leagueId.Value && m.Surprise != null && m.Surprise.SurpriseType != "NONE")
                    .GroupBy(m => new { m.AwayTeamId, m.AwayTeam!.Name })
                    .Select(g => new { g.Key.AwayTeamId, g.Key.Name, Count = g.Count() })
                    .ToListAsync();

                var teamDict = new Dictionary<int, (string Name, int Count)>();
                foreach (var t in teamSurprisesHome)
                    teamDict[t.HomeTeamId] = (t.Name, t.Count);
                foreach (var t in teamSurprisesAway)
                {
                    if (teamDict.TryGetValue(t.AwayTeamId, out var existing))
                        teamDict[t.AwayTeamId] = (existing.Name, existing.Count + t.Count);
                    else
                        teamDict[t.AwayTeamId] = (t.Name, t.Count);
                }

                viewModel.Teams = teamDict
                    .Select(kv => new TeamNavDto { Id = kv.Key, Name = kv.Value.Name, SurpriseCount = kv.Value.Count })
                    .OrderBy(t => t.Name)
                    .ToList();
            }

            // 5. Maçları sorgula
            var query = _context.Matches.AsNoTracking()
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Surprise)
                .Where(m => m.LeagueId == leagueId.Value);

            // Sürpriz modunda sadece sürpriz maçlar; fikstür modunda tüm maçlar
            if (!fixturesMode)
                query = query.Where(m => m.Surprise != null && m.Surprise.SurpriseType != "NONE");

            if (seasonId.HasValue)
                query = query.Where(m => m.SeasonId == seasonId.Value);

            // 6. Takım filtresi
            if (teamId.HasValue)
                query = query.Where(m => m.HomeTeamId == teamId.Value || m.AwayTeamId == teamId.Value);

            // 7. KPI Kartları (takım filtresi dahil, tür filtresinden önce)
            if (fixturesMode)
            {
                viewModel.TotalMatchesCount = await query.CountAsync();
                viewModel.HomeWinCount      = await query.CountAsync(m => m.FtHomeScore > m.FtAwayScore);
                viewModel.DrawCount         = await query.CountAsync(m => m.FtHomeScore == m.FtAwayScore);
                viewModel.AwayWinCount      = await query.CountAsync(m => m.FtHomeScore < m.FtAwayScore);
                viewModel.TurnaroundCount   = await query.CountAsync(m => m.Surprise != null && m.Surprise.IsTurnaround);
                viewModel.HighGoalCount     = await query.CountAsync(m => m.Surprise != null && m.Surprise.IsHighGoal);
                viewModel.AvgGoals = viewModel.TotalMatchesCount > 0
                    ? await query.AverageAsync(m => (double)(m.FtHomeScore + m.FtAwayScore))
                    : 0;
            }
            else
            {
                viewModel.TotalMatchesCount   = await query.CountAsync();
                viewModel.TurnaroundCount     = await query.CountAsync(m => m.Surprise!.IsTurnaround);
                viewModel.HighGoalCount       = await query.CountAsync(m => m.Surprise!.IsHighGoal);
                viewModel.DoubleSurpriseCount = await query.CountAsync(m => m.Surprise!.IsDoubleSurprise);
            }

            // 8. Tür filtresi
            if (fixturesMode)
            {
                if (filter == "SURPRISE")
                    query = query.Where(m => m.Surprise != null && m.Surprise.SurpriseType != "NONE");
                else if (filter == "TURNAROUND")
                    query = query.Where(m => m.Surprise != null && m.Surprise.IsTurnaround);
                else if (filter == "HIGH_GOAL")
                    query = query.Where(m => m.Surprise != null && m.Surprise.IsHighGoal);
                else if (filter == "NORMAL")
                    query = query.Where(m => m.Surprise == null || m.Surprise.SurpriseType == "NONE");
            }
            else
            {
                if (filter == "TURNAROUND")
                    query = query.Where(m => m.Surprise!.IsTurnaround);
                else if (filter == "HIGH_GOAL")
                    query = query.Where(m => m.Surprise!.IsHighGoal);
                else if (filter == "DOUBLE")
                    query = query.Where(m => m.Surprise!.IsDoubleSurprise);
            }

            // 9. Sıralama
            query = sort switch
            {
                "week_asc"  => query.OrderBy(m => m.Week).ThenBy(m => m.MatchDate),
                "week_desc" => query.OrderByDescending(m => m.Week).ThenByDescending(m => m.MatchDate),
                "date_asc"  => query.OrderBy(m => m.MatchDate).ThenBy(m => m.Week),
                _           => query.OrderByDescending(m => m.MatchDate).ThenByDescending(m => m.Week),
            };

            // 10. Tabloyu doldur
            viewModel.Matches = (await query
                .Select(m => new
                {
                    m.Id, m.Week, m.MatchDate,
                    HomeTeamName = m.HomeTeam != null ? m.HomeTeam.Name : "-",
                    AwayTeamName = m.AwayTeam != null ? m.AwayTeam.Name : "-",
                    m.HtHomeScore, m.HtAwayScore, m.FtHomeScore, m.FtAwayScore,
                    IyMsCode     = m.Surprise != null ? m.Surprise.IyMsCode     : "-",
                    TotalGoals   = m.Surprise != null ? m.Surprise.TotalGoals   : (m.FtHomeScore + m.FtAwayScore),
                    SurpriseType = m.Surprise != null ? m.Surprise.SurpriseType : "NONE",
                    m.HomeTeamId
                })
                .ToListAsync())
                .Select(m => new MatchRowDto
                {
                    Id           = m.Id,
                    Week         = m.Week,
                    MatchDate    = m.MatchDate,
                    HomeTeam     = m.HomeTeamName,
                    AwayTeam     = m.AwayTeamName,
                    HtScore      = $"{m.HtHomeScore}-{m.HtAwayScore}",
                    FtScore      = $"{m.FtHomeScore}-{m.FtAwayScore}",
                    FtHome       = m.FtHomeScore,
                    FtAway       = m.FtAwayScore,
                    MsResultCode = m.FtHomeScore > m.FtAwayScore ? "1" : (m.FtHomeScore < m.FtAwayScore ? "2" : "X"),
                    IyMsCode     = m.IyMsCode,
                    TotalGoals   = m.TotalGoals,
                    SurpriseType = m.SurpriseType,
                    IsHomeTeam   = teamId.HasValue && m.HomeTeamId == teamId.Value
                })
                .ToList();

            // 11. Fikstür modu + takım seçili → takım sezon özeti (bellek içinde, ekstra sorgu yok)
            if (fixturesMode && teamId.HasValue && viewModel.Matches.Count > 0)
            {
                var stats = new TeamFixtureStatsDto
                {
                    TeamName = viewModel.Teams.FirstOrDefault(t => t.Id == teamId.Value)?.Name ?? "-"
                };

                int totalGoalsInMatches = 0;
                foreach (var m in viewModel.Matches)
                {
                    bool home = m.IsHomeTeam;
                    int gf = home ? m.FtHome : m.FtAway;
                    int ga = home ? m.FtAway : m.FtHome;

                    stats.Played++;
                    stats.GoalsFor += gf;
                    stats.GoalsAgainst += ga;
                    totalGoalsInMatches += m.FtHome + m.FtAway;

                    bool win  = gf > ga;
                    bool draw = gf == ga;

                    if (win) stats.Wins++;
                    else if (draw) stats.Draws++;
                    else stats.Losses++;

                    if (home)
                    {
                        stats.HomePlayed++;
                        if (win) stats.HomeWins++; else if (draw) stats.HomeDraws++; else stats.HomeLosses++;
                    }
                    else
                    {
                        stats.AwayPlayed++;
                        if (win) stats.AwayWins++; else if (draw) stats.AwayDraws++; else stats.AwayLosses++;
                    }

                    if (m.TotalGoals >= 6) stats.HighGoalCount++;
                    if (m.SurpriseType == "TURNAROUND" || m.SurpriseType == "DOUBLE_SURPRISE") stats.TurnaroundCount++;
                    if (m.SurpriseType != "NONE") stats.SurpriseCount++;
                }

                stats.AvgTotalGoals = stats.Played > 0
                    ? Math.Round((double)totalGoalsInMatches / stats.Played, 2)
                    : 0;
                viewModel.TeamStats = stats;
            }
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