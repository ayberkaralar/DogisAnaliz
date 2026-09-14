using System.Globalization;
using System.Text.Json;
using DogisAnaliz.Data;
using DogisAnaliz.Models;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Services;

/// <summary>
/// api-sports "teams/statistics" ucundan bir takımın lig+sezon özet istatistiklerini çeker.
/// Bu uç TAKIM BAŞINA ayrı çağrı gerektirir (fixtures/standings gibi ligin tamamını tek
/// seferde vermez) — bu yüzden <see cref="ActiveSeasonRefresher"/>'a (Senkronize Et) DAHİL
/// DEĞİL, "/Sync" ekranında ayrı, manuel bir buton (lig+sezon bazında, ~20 çağrı/lig).
/// Sadece <see cref="Team.ApiTeamId"/> dolu takımlar için çalışır (fixtures/fikstür senkronu
/// hiç çalışmamış bir lig+sezon için ApiTeamId boş olur — önce en az bir maç/fikstür senkronu
/// gerekir).
/// </summary>
public class TeamStatisticsService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;

    public TeamStatisticsService(HttpClient httpClient, AppDbContext context, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _context = context;

        string apiKey = configuration["FootballApi:ApiKey"] ?? "";
        _httpClient.BaseAddress = new Uri("https://v3.football.api-sports.io/");
        _httpClient.DefaultRequestHeaders.Add("x-apisports-key", apiKey);
    }

    private static double ParseAvg(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.String) return 0;
        var s = el.GetString();
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static int SumIntervalTotals(JsonElement intervalObj)
    {
        int sum = 0;
        foreach (var prop in intervalObj.EnumerateObject())
            if (prop.Value.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number)
                sum += t.GetInt32();
        return sum;
    }

    /// <returns>(işlenen takım sayısı, ApiTeamId'si olmadığı için atlanan takım sayısı)</returns>
    public async Task<(int Synced, int Skipped)> SyncLeagueSeasonAsync(int apiLeagueId, int seasonYear)
    {
        string seasonName = $"{seasonYear}-{seasonYear + 1}";

        // Lig/sezon önceden var olmalı (fixtures senkronundan) — LeagueCatalog'tan apiLeagueId'nin
        // Name+Country'sini bulup öyle eşleştiriyoruz (aynı isimli iki lig olabilir, bkz. CLAUDE.md).
        var leagueConfig = LeagueCatalog.KnownLeagues.FirstOrDefault(k => k.ApiId == apiLeagueId);
        if (leagueConfig == null) return (0, 0);

        var leagueRow = await _context.Leagues.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == leagueConfig.Name && l.Country == leagueConfig.Country);
        var seasonRow = await _context.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.SeasonName == seasonName);
        if (leagueRow == null || seasonRow == null) return (0, 0);

        // Bu lig+sezonda maçı/fikstürü olan, ApiTeamId'si dolu takımlar. SelectMany(m => new[]
        // {home,away}) EF Core'da SQL'e çevrilemiyor ("could not be translated") — bunun yerine
        // ev/deplasman ayrı ayrı çekilip client tarafında birleştirilir (aynı desen HomeController'da da var).
        var homeIds1 = await _context.Matches.AsNoTracking()
            .Where(m => m.LeagueId == leagueRow.Id && m.SeasonId == seasonRow.Id)
            .Select(m => m.HomeTeamId).Distinct().ToListAsync();
        var awayIds1 = await _context.Matches.AsNoTracking()
            .Where(m => m.LeagueId == leagueRow.Id && m.SeasonId == seasonRow.Id)
            .Select(m => m.AwayTeamId).Distinct().ToListAsync();
        var homeIds2 = await _context.FixtureSchedules.AsNoTracking()
            .Where(f => f.LeagueId == leagueRow.Id && f.SeasonId == seasonRow.Id)
            .Select(f => f.HomeTeamId).Distinct().ToListAsync();
        var awayIds2 = await _context.FixtureSchedules.AsNoTracking()
            .Where(f => f.LeagueId == leagueRow.Id && f.SeasonId == seasonRow.Id)
            .Select(f => f.AwayTeamId).Distinct().ToListAsync();
        var teamIds = homeIds1.Concat(awayIds1).Concat(homeIds2).Concat(awayIds2).Distinct().ToList();

        var teams = await _context.Teams.Where(t => teamIds.Contains(t.Id)).ToListAsync();
        var withApiId = teams.Where(t => t.ApiTeamId.HasValue).ToList();
        int skipped = teams.Count - withApiId.Count;

        var existingStats = (await _context.TeamSeasonStats
                .Where(s => s.LeagueId == leagueRow.Id && s.SeasonId == seasonRow.Id).ToListAsync())
            .ToDictionary(s => s.TeamId);

        int synced = 0;
        foreach (var team in withApiId)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"teams/statistics?league={apiLeagueId}&season={seasonYear}&team={team.ApiTeamId}");
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[TeamStats Hata] {team.Name}: HTTP {(int)response.StatusCode}");
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("response", out var r) || r.ValueKind != JsonValueKind.Object)
                    continue;

                var goalsFor = r.GetProperty("goals").GetProperty("for").GetProperty("average");
                var goalsAgainst = r.GetProperty("goals").GetProperty("against").GetProperty("average");
                var cleanSheet = r.GetProperty("clean_sheet");
                var failedToScore = r.GetProperty("failed_to_score");
                var penalty = r.GetProperty("penalty");
                var cardsYellow = r.GetProperty("cards").GetProperty("yellow");
                var cardsRed = r.GetProperty("cards").GetProperty("red");

                if (!existingStats.TryGetValue(team.Id, out var stat))
                {
                    stat = new TeamSeasonStat { LeagueId = leagueRow.Id, SeasonId = seasonRow.Id, TeamId = team.Id };
                    _context.TeamSeasonStats.Add(stat);
                    existingStats[team.Id] = stat;
                }

                stat.Form = r.TryGetProperty("form", out var formEl) && formEl.ValueKind == JsonValueKind.String
                    ? formEl.GetString() ?? "" : "";

                stat.GoalsForAvgHome = ParseAvg(goalsFor.GetProperty("home"));
                stat.GoalsForAvgAway = ParseAvg(goalsFor.GetProperty("away"));
                stat.GoalsForAvgTotal = ParseAvg(goalsFor.GetProperty("total"));
                stat.GoalsAgainstAvgHome = ParseAvg(goalsAgainst.GetProperty("home"));
                stat.GoalsAgainstAvgAway = ParseAvg(goalsAgainst.GetProperty("away"));
                stat.GoalsAgainstAvgTotal = ParseAvg(goalsAgainst.GetProperty("total"));

                stat.CleanSheetsHome = cleanSheet.GetProperty("home").GetInt32();
                stat.CleanSheetsAway = cleanSheet.GetProperty("away").GetInt32();
                stat.CleanSheetsTotal = cleanSheet.GetProperty("total").GetInt32();
                stat.FailedToScoreHome = failedToScore.GetProperty("home").GetInt32();
                stat.FailedToScoreAway = failedToScore.GetProperty("away").GetInt32();
                stat.FailedToScoreTotal = failedToScore.GetProperty("total").GetInt32();

                stat.PenaltyScored = penalty.GetProperty("scored").GetProperty("total").GetInt32();
                stat.PenaltyMissed = penalty.GetProperty("missed").GetProperty("total").GetInt32();

                stat.CardsYellowTotal = SumIntervalTotals(cardsYellow);
                stat.CardsRedTotal = SumIntervalTotals(cardsRed);

                stat.UpdatedAt = DateTime.UtcNow;
                synced++;

                await Task.Delay(350); // rate limit'e karşı nazik ol — takım başına 1 çağrı, N takım
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TeamStats işleme hatası] {team.Name}: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return (synced, skipped);
    }
}
