using System.Text.Json;
using DogisAnaliz.Data;
using DogisAnaliz.Models;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Services;

/// <summary>
/// api-sports "fixtures" ucundan bir lig+sezonun TÜM fikstürünü (oynanmamış NS maçlar dahil)
/// çeker ve <see cref="FixtureSchedule"/> tablosuna upsert eder.
/// Skor/sürpriz hesabı YOK — sadece takvim: kim-kiminle-hangi hafta-ne zaman.
/// İdempotent: <see cref="FixtureSchedule.ApiFixtureId"/> anahtarıyla var olan kayıt güncellenir.
/// Takım adları <see cref="TeamNameNormalizer"/> ile normalize edilir (duplicate takım oluşmasın).
/// </summary>
public class FixtureScheduleService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;

    public FixtureScheduleService(HttpClient httpClient, AppDbContext context, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _context = context;

        string apiKey = configuration["FootballApi:ApiKey"] ?? "";
        _httpClient.BaseAddress = new Uri("https://v3.football.api-sports.io/");
        _httpClient.DefaultRequestHeaders.Add("x-apisports-key", apiKey);
    }

    /// <returns>(eklenen, güncellenen, toplam işlenen) fikstür sayısı</returns>
    public async Task<(int Added, int Updated, int Total)> SyncScheduleAsync(int apiLeagueId, int seasonYear)
    {
        var response = await _httpClient.GetAsync($"fixtures?league={apiLeagueId}&season={seasonYear}");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[FikstürTakvimi Hata] HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            return (0, 0, 0);
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("response", out var fixtures) || fixtures.GetArrayLength() == 0)
        {
            Console.WriteLine($"[FikstürTakvimi] {seasonYear}-{seasonYear + 1} için fikstür bulunamadı.");
            return (0, 0, 0);
        }

        // Referans verileri tek seferde belleğe al (FootballApiService ile aynı desen).
        // GroupBy + First (ToDictionaryAsync değil): DB'de henüz birleştirilmemiş aynı isimli
        // duplicate kayıt varsa çökmek yerine ilkini kullan.
        var existingTeams = (await _context.Teams.ToListAsync())
            .GroupBy(t => t.Name.ToLower()).ToDictionary(g => g.Key, g => g.First());
        var existingSeasons = (await _context.Seasons.ToListAsync())
            .GroupBy(s => s.SeasonName).ToDictionary(g => g.Key, g => g.First());
        var existingLeagues = (await _context.Leagues.ToListAsync())
            .GroupBy(l => l.Name).ToDictionary(g => g.Key, g => g.First());
        var existingSchedule = (await _context.FixtureSchedules.ToListAsync())
            .GroupBy(f => f.ApiFixtureId).ToDictionary(g => g.Key, g => g.First());

        string seasonName = $"{seasonYear}-{seasonYear + 1}";
        int added = 0, updated = 0, total = 0;

        foreach (var item in fixtures.EnumerateArray())
        {
            try
            {
                var fixture  = item.GetProperty("fixture");
                var leagueEl = item.GetProperty("league");
                var teamsEl  = item.GetProperty("teams");
                var goalsEl  = item.GetProperty("goals");

                int apiFixtureId = fixture.GetProperty("id").GetInt32();
                string status = fixture.GetProperty("status").GetProperty("short").GetString() ?? "NS";
                DateTime kickoffUtc = DateTime.SpecifyKind(
                    fixture.GetProperty("date").GetDateTime().ToUniversalTime(), DateTimeKind.Utc);

                string leagueName = leagueEl.GetProperty("name").GetString()!;
                string country    = leagueEl.GetProperty("country").GetString()!;
                string round      = leagueEl.GetProperty("round").GetString() ?? "";
                int week = 0;
                var mw = System.Text.RegularExpressions.Regex.Match(round, @"\d+");
                if (mw.Success) week = int.Parse(mw.Value);

                string homeName = TeamNameNormalizer.Normalize(
                    teamsEl.GetProperty("home").GetProperty("name").GetString()!);
                string awayName = TeamNameNormalizer.Normalize(
                    teamsEl.GetProperty("away").GetProperty("name").GetString()!);
                int apiHomeTeamId = teamsEl.GetProperty("home").GetProperty("id").GetInt32();
                int apiAwayTeamId = teamsEl.GetProperty("away").GetProperty("id").GetInt32();

                var ghEl = goalsEl.GetProperty("home");
                var gaEl = goalsEl.GetProperty("away");
                int? homeGoals = ghEl.ValueKind == JsonValueKind.Null ? null : ghEl.GetInt32();
                int? awayGoals = gaEl.ValueKind == JsonValueKind.Null ? null : gaEl.GetInt32();

                // Lig / Sezon / Takım get-or-create
                if (!existingLeagues.TryGetValue(leagueName, out var league))
                {
                    league = new League { Name = leagueName, Country = country };
                    _context.Leagues.Add(league);
                    await _context.SaveChangesAsync();
                    existingLeagues[leagueName] = league;
                }
                if (!existingSeasons.TryGetValue(seasonName, out var season))
                {
                    season = new Season { SeasonName = seasonName, StartYear = seasonYear, EndYear = seasonYear + 1 };
                    _context.Seasons.Add(season);
                    await _context.SaveChangesAsync();
                    existingSeasons[seasonName] = season;
                }
                // Tam eşleşme yoksa güvenli kısa/uzun ad eşleşmesine bak (bkz.
                // TeamNameNormalizer.FindSafeFuzzyMatch) — yoksa api-sports farklı bir ad
                // döndürdüğünde her senkronizasyonda aynı duplicate takım geri gelir.
                if (!existingTeams.TryGetValue(homeName.ToLower(), out var homeTeam))
                {
                    homeTeam = TeamNameNormalizer.FindSafeFuzzyMatch(homeName, existingTeams.Values);
                    if (homeTeam == null)
                    {
                        homeTeam = new Team { Name = homeName };
                        _context.Teams.Add(homeTeam);
                        await _context.SaveChangesAsync();
                    }
                    existingTeams[homeName.ToLower()] = homeTeam;
                }
                if (homeTeam.ApiTeamId == null) homeTeam.ApiTeamId = apiHomeTeamId;
                if (!existingTeams.TryGetValue(awayName.ToLower(), out var awayTeam))
                {
                    awayTeam = TeamNameNormalizer.FindSafeFuzzyMatch(awayName, existingTeams.Values);
                    if (awayTeam == null)
                    {
                        awayTeam = new Team { Name = awayName };
                        _context.Teams.Add(awayTeam);
                        await _context.SaveChangesAsync();
                    }
                    existingTeams[awayName.ToLower()] = awayTeam;
                }
                if (awayTeam.ApiTeamId == null) awayTeam.ApiTeamId = apiAwayTeamId;

                total++;

                if (!existingSchedule.TryGetValue(apiFixtureId, out var row))
                {
                    _context.FixtureSchedules.Add(new FixtureSchedule
                    {
                        ApiFixtureId = apiFixtureId,
                        LeagueId     = league.Id,
                        SeasonId     = season.Id,
                        Week         = week,
                        Round        = round,
                        KickoffUtc   = kickoffUtc,
                        HomeTeamId   = homeTeam.Id,
                        AwayTeamId   = awayTeam.Id,
                        Status       = status,
                        HomeGoals    = homeGoals,
                        AwayGoals    = awayGoals,
                        UpdatedAt    = DateTime.UtcNow
                    });
                    added++;
                }
                else
                {
                    row.LeagueId   = league.Id;
                    row.SeasonId   = season.Id;
                    row.Week       = week;
                    row.Round      = round;
                    row.KickoffUtc = kickoffUtc;
                    row.HomeTeamId = homeTeam.Id;
                    row.AwayTeamId = awayTeam.Id;
                    row.Status     = status;
                    row.HomeGoals  = homeGoals;
                    row.AwayGoals  = awayGoals;
                    row.UpdatedAt  = DateTime.UtcNow;
                    updated++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FikstürTakvimi işleme hatası]: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return (added, updated, total);
    }
}
