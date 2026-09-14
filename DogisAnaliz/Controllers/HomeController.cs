using DogisAnaliz.Data;
using DogisAnaliz.Models;
using DogisAnaliz.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DogisAnaliz.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public HomeController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(int? leagueId, int? seasonId, int? teamId, string filter = "ALL", string sort = "week_asc", string view = "surprise")
    {
        bool fixturesMode = view == "fixtures";
        bool standingsMode = view == "standings";

        var viewModel = new DashboardViewModel
        {
            ViewMode       = standingsMode ? "standings" : fixturesMode ? "fixtures" : "surprise",
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

        // Sol menü sıralaması: Büyük 5 (Premier Lig, Almanya, Fransa, İspanya, İtalya — sabit sırada),
        // sonra diğer 1. ligler alfabetik (Türkçe ad), en altta 2. ligler alfabetik.
        viewModel.Leagues = leaguesRaw
            .Select(l => (League: l, Rank: LeagueDisplayHelper.GetSidebarRank(l.Name, l.Country)))
            .OrderBy(x => x.Rank.Tier).ThenBy(x => x.Rank.Order)
            .ThenBy(x => LeagueDisplayHelper.ToTurkish(x.League.Name, x.League.Country))
            .Select(x => new LeagueNavDto
            {
                Id            = x.League.Id,
                Name          = x.League.Name,
                Country       = x.League.Country,
                Tier          = x.Rank.Tier,
                SurpriseCount = surpriseCounts.TryGetValue(x.League.Id, out int count) ? count : 0
            })
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

            // Varsayılan sezon: önce aktif sezonu (LeagueCatalog.ActiveSeasonYear) dene,
            // o ligde henüz yoksa en güncel sezona düş.
            if (!seasonId.HasValue)
            {
                seasonId = viewModel.Seasons.FirstOrDefault(s => s.SeasonName == LeagueCatalog.ActiveSeasonName)?.Id
                    ?? viewModel.Seasons.FirstOrDefault()?.Id;
            }
            viewModel.SelectedSeasonId = seasonId;

            // 4. Seçili lig + seçili SEZONDAKİ takımlar (hem maç sayısı hem sürpriz sayısı birlikte —
            // sol menüde her zaman görünür liste + hangi sekmede olursa olsun takıma tıklayınca
            // fikstür VE sürpriz bilgisi birlikte gösterilebilsin diye ikisi de hesaplanır).
            var teamMatchesHome = await _context.Matches.AsNoTracking()
                .Where(m => m.LeagueId == leagueId.Value && (!seasonId.HasValue || m.SeasonId == seasonId.Value))
                .GroupBy(m => new { m.HomeTeamId, m.HomeTeam!.Name })
                .Select(g => new { g.Key.HomeTeamId, g.Key.Name, Count = g.Count(),
                    Surprise = g.Count(x => x.Surprise != null && x.Surprise.SurpriseType != "NONE") })
                .ToListAsync();

            var teamMatchesAway = await _context.Matches.AsNoTracking()
                .Where(m => m.LeagueId == leagueId.Value && (!seasonId.HasValue || m.SeasonId == seasonId.Value))
                .GroupBy(m => new { m.AwayTeamId, m.AwayTeam!.Name })
                .Select(g => new { g.Key.AwayTeamId, g.Key.Name, Count = g.Count(),
                    Surprise = g.Count(x => x.Surprise != null && x.Surprise.SurpriseType != "NONE") })
                .ToListAsync();

            var tDict = new Dictionary<int, (string Name, int Matches, int Surprises)>();
            foreach (var t in teamMatchesHome)
                tDict[t.HomeTeamId] = (t.Name, t.Count, t.Surprise);
            foreach (var t in teamMatchesAway)
            {
                if (tDict.TryGetValue(t.AwayTeamId, out var existing))
                    tDict[t.AwayTeamId] = (existing.Name, existing.Matches + t.Count, existing.Surprises + t.Surprise);
                else
                    tDict[t.AwayTeamId] = (t.Name, t.Count, t.Surprise);
            }

            viewModel.Teams = tDict
                .Select(kv => new TeamNavDto
                {
                    Id = kv.Key, Name = kv.Value.Name,
                    MatchCount = kv.Value.Matches, SurpriseCount = kv.Value.Surprises
                })
                .OrderBy(t => t.Name)
                .ToList();

            if (standingsMode)
            {
                // Puan durumu modu: Match/FixtureSchedule sorgu hattının tamamı atlanır —
                // sadece Standing tablosundan (Senkronize Et ile tazelenir) seçili lig+sezonun
                // güncel tablosu okunur, rank'a göre sıralı.
                if (seasonId.HasValue)
                {
                    var standingRows = await _context.Standings.AsNoTracking()
                        .Include(s => s.Team)
                        .Where(s => s.LeagueId == leagueId.Value && s.SeasonId == seasonId.Value)
                        .OrderBy(s => s.Rank)
                        .ToListAsync();

                    viewModel.Standings = standingRows.Select(s => new StandingRowDto
                    {
                        TeamId = s.TeamId, TeamName = s.Team.Name,
                        Rank = s.Rank, Points = s.Points, GoalsDiff = s.GoalsDiff,
                        Form = s.Form, Description = s.Description,
                        Played = s.Played, Win = s.Win, Draw = s.Draw, Lose = s.Lose,
                        GoalsFor = s.GoalsFor, GoalsAgainst = s.GoalsAgainst,
                        HomePlayed = s.HomePlayed, HomeWin = s.HomeWin, HomeDraw = s.HomeDraw, HomeLose = s.HomeLose,
                        HomeGoalsFor = s.HomeGoalsFor, HomeGoalsAgainst = s.HomeGoalsAgainst,
                        AwayPlayed = s.AwayPlayed, AwayWin = s.AwayWin, AwayDraw = s.AwayDraw, AwayLose = s.AwayLose,
                        AwayGoalsFor = s.AwayGoalsFor, AwayGoalsAgainst = s.AwayGoalsAgainst
                    }).ToList();

                    viewModel.StandingsUpdatedAt = standingRows.Select(s => s.UpdatedAt).DefaultIfEmpty().Max();
                }
            }
            else
            {

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

            // 10b. Fikstür modunda: sezonun HENÜZ OYNANMAMIŞ maçlarını da (FixtureSchedule) tabloya
            // ekle — "Fikstür" sekmesi kullanıcının bir sezon boyunca kiminle oynayacağını
            // göstermeli, sadece geçmiş sonuçları değil (bkz. CLAUDE.md). Sayı (UpcomingCount)
            // her filtrede doğru görünsün diye HER ZAMAN hesaplanır; tabloya EKLENMESİ ise sonuç
            // gerektiren filtrelerde (SURPRISE/TURNAROUND/HIGH_GOAL/NORMAL) anlamsız olduğundan
            // sadece "ALL" (hepsi bir arada) ve "PENDING" (sadece kalanlar) iken yapılır.
            if (fixturesMode && seasonId.HasValue)
            {
                if (filter == "PENDING")
                    viewModel.Matches.Clear();

                // Aynı gerçek maç FixtureSchedule'da hâlâ "NS" görünüp Matches'te zaten oynanmış
                // olabilir (Senkronize Et fikstür takvimini güncellemiyor) — bu yüzden oynanmış
                // (hafta, ev, dep) anahtarlarını çıkarıp FixtureSchedule'dan sadece GERÇEKTEN
                // kalanları alıyoruz (aynı desen: GolBeklentisiController/Bilgi1/Bilgi4).
                var playedKeys = (await _context.Matches.AsNoTracking()
                    .Where(m => m.LeagueId == leagueId.Value && m.SeasonId == seasonId.Value)
                    .Select(m => new { m.Week, m.HomeTeamId, m.AwayTeamId })
                    .ToListAsync())
                    .Select(m => (m.Week, Math.Min(m.HomeTeamId, m.AwayTeamId), Math.Max(m.HomeTeamId, m.AwayTeamId)))
                    .ToHashSet();

                var fxQuery = _context.FixtureSchedules.AsNoTracking()
                    .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
                    .Where(f => f.LeagueId == leagueId.Value && f.SeasonId == seasonId.Value);
                if (teamId.HasValue)
                    fxQuery = fxQuery.Where(f => f.HomeTeamId == teamId.Value || f.AwayTeamId == teamId.Value);

                var upcoming = (await fxQuery.ToListAsync())
                    .Where(f => !playedKeys.Contains((f.Week, Math.Min(f.HomeTeamId, f.AwayTeamId), Math.Max(f.HomeTeamId, f.AwayTeamId))))
                    .Select(f => new MatchRowDto
                    {
                        Id           = -f.Id, // negatif: oynanmış Match.Id'leriyle çakışmasın (sadece görüntü — DB'ye yazılmaz)
                        Week         = f.Week,
                        MatchDate    = f.KickoffUtc,
                        HomeTeam     = f.HomeTeam.Name,
                        AwayTeam     = f.AwayTeam.Name,
                        HtScore      = "-",
                        FtScore      = "-",
                        FtHome       = 0,
                        FtAway       = 0,
                        MsResultCode = "-",
                        IyMsCode     = "-",
                        TotalGoals   = 0,
                        SurpriseType = "NONE",
                        IsHomeTeam   = teamId.HasValue && f.HomeTeamId == teamId.Value,
                        IsPlayed     = false
                    })
                    .ToList();

                viewModel.UpcomingCount = upcoming.Count;

                if (filter == "ALL" || filter == "PENDING")
                {
                    viewModel.Matches.AddRange(upcoming);

                    // SQL tarafında sıralanmış oynanmışlarla bellek-içi eklenen kalanları birlikte
                    // yeniden sırala — aksi halde eklenenler listenin sonuna yığılır, hafta/tarih
                    // sırası bozulur.
                    viewModel.Matches = sort switch
                    {
                        "week_asc"  => viewModel.Matches.OrderBy(m => m.Week).ThenBy(m => m.MatchDate).ToList(),
                        "week_desc" => viewModel.Matches.OrderByDescending(m => m.Week).ThenByDescending(m => m.MatchDate).ToList(),
                        "date_asc"  => viewModel.Matches.OrderBy(m => m.MatchDate).ThenBy(m => m.Week).ToList(),
                        _           => viewModel.Matches.OrderByDescending(m => m.MatchDate).ThenByDescending(m => m.Week).ToList(),
                    };
                }
            }

            // 11. Takım seçili → takım sezon özeti (fikstür + sürpriz bilgisi BİRLİKTE) — hangi
            // sekmede olunursa olsun (Sürpriz sekmesinde tablo sadece sürpriz maçlarla filtrelendiği
            // için buradaki özet ayrı, filtresiz bir sorguyla hesaplanır — aksi halde "38 maç"
            // yerine yanlışlıkla "3 sürpriz maçı" gibi görünürdü).
            if (teamId.HasValue && seasonId.HasValue)
            {
                var teamSeasonMatches = await _context.Matches.AsNoTracking()
                    .Include(m => m.Surprise)
                    .Where(m => m.LeagueId == leagueId.Value && m.SeasonId == seasonId.Value
                             && (m.HomeTeamId == teamId.Value || m.AwayTeamId == teamId.Value))
                    .Select(m => new
                    {
                        m.FtHomeScore, m.FtAwayScore, IsHomeTeam = m.HomeTeamId == teamId.Value,
                        SurpriseType = m.Surprise != null ? m.Surprise.SurpriseType : "NONE"
                    })
                    .ToListAsync();

                var stats = new TeamFixtureStatsDto
                {
                    TeamName = viewModel.Teams.FirstOrDefault(t => t.Id == teamId.Value)?.Name ?? "-"
                };

                int totalGoalsInMatches = 0;
                foreach (var m in teamSeasonMatches)
                {
                    bool home = m.IsHomeTeam;
                    int gf = home ? m.FtHomeScore : m.FtAwayScore;
                    int ga = home ? m.FtAwayScore : m.FtHomeScore;

                    stats.Played++;
                    stats.GoalsFor += gf;
                    stats.GoalsAgainst += ga;
                    totalGoalsInMatches += m.FtHomeScore + m.FtAwayScore;

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

                    if (gf + ga >= 6) stats.HighGoalCount++;
                    if (m.SurpriseType == "TURNAROUND" || m.SurpriseType == "DOUBLE_SURPRISE") stats.TurnaroundCount++;
                    if (m.SurpriseType != "NONE") stats.SurpriseCount++;
                }

                stats.AvgTotalGoals = stats.Played > 0
                    ? Math.Round((double)totalGoalsInMatches / stats.Played, 2)
                    : 0;

                // Lig tablosu (Standing) — api-sports'tan, "Senkronize Et" ile tazelenir. Yoksa
                // (bu lig+sezon hiç senkronize edilmemişse) sessizce null kalır, panel gizlenir.
                var standing = await _context.Standings.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.LeagueId == leagueId.Value && s.SeasonId == seasonId.Value && s.TeamId == teamId.Value);
                if (standing != null)
                {
                    stats.LeagueRank = standing.Rank;
                    stats.LeaguePoints = standing.Points;
                    stats.LeagueForm = standing.Form;
                }

                viewModel.TeamStats = stats;
            }
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
        // GÜVENLİK: API key artık appsettings.json'dan okunuyor, kaynak koda gömülü değil
        // (bkz. CLAUDE.md "Güvenlik" bölümü — bu repo public, credential asla koda yazılmamalı).
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://v3.football.api-sports.io/");
        httpClient.DefaultRequestHeaders.Add("x-apisports-key", _configuration["FootballApi:ApiKey"] ?? "");

        // Ligin API'deki tüm mevcut sezonlarını ve kapsamını döner
        var response = await httpClient.GetAsync($"leagues?id={leagueId}");
        var json = await response.Content.ReadAsStringAsync();

        return Content(json, "application/json");
    }
}