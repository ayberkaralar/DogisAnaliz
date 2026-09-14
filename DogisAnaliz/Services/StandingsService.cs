using System.Text.Json;
using DogisAnaliz.Data;
using DogisAnaliz.Models;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Services;

/// <summary>
/// api-sports "standings" ucundan bir lig+sezonun TÜM lig tablosunu (tek çağrıda) çeker ve
/// <see cref="Standing"/> tablosuna upsert eder. Puan durumu her hafta değiştiği için geçmiş
/// tutulmaz — (LeagueId,SeasonId,TeamId) başına tek, güncel satır. Ucuz bir uç (1 çağrı/lig)
/// olduğundan <see cref="ActiveSeasonRefresher"/> ("Senkronize Et") içine dahil edilir.
/// </summary>
public class StandingsService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;

    public StandingsService(HttpClient httpClient, AppDbContext context, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _context = context;

        string apiKey = configuration["FootballApi:ApiKey"] ?? "";
        _httpClient.BaseAddress = new Uri("https://v3.football.api-sports.io/");
        _httpClient.DefaultRequestHeaders.Add("x-apisports-key", apiKey);
    }

    public async Task<int> SyncStandingsAsync(int apiLeagueId, int seasonYear)
    {
        var response = await _httpClient.GetAsync($"standings?league={apiLeagueId}&season={seasonYear}");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[Standings Hata] HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            return 0;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("response", out var respArr) || respArr.GetArrayLength() == 0)
            return 0;

        var leagueEl = respArr[0].GetProperty("league");
        if (!leagueEl.TryGetProperty("standings", out var groupsEl) || groupsEl.GetArrayLength() == 0)
            return 0;

        string leagueName = leagueEl.GetProperty("name").GetString() ?? "";
        string country = leagueEl.GetProperty("country").GetString() ?? "";
        string seasonName = $"{seasonYear}-{seasonYear + 1}";

        var existingTeams = (await _context.Teams.ToListAsync())
            .GroupBy(t => t.Name.ToLower()).ToDictionary(g => g.Key, g => g.First());
        var existingLeagues = (await _context.Leagues.ToListAsync())
            .GroupBy(l => l.Name).ToDictionary(g => g.Key, g => g.First());
        var existingSeasons = (await _context.Seasons.ToListAsync())
            .GroupBy(s => s.SeasonName).ToDictionary(g => g.Key, g => g.First());

        if (!existingLeagues.TryGetValue(leagueName, out var league))
        {
            league = new League { Name = leagueName, Country = country };
            _context.Leagues.Add(league);
            await _context.SaveChangesAsync();
        }
        if (!existingSeasons.TryGetValue(seasonName, out var season))
        {
            season = new Season { SeasonName = seasonName, StartYear = seasonYear, EndYear = seasonYear + 1 };
            _context.Seasons.Add(season);
            await _context.SaveChangesAsync();
        }

        var existingStandings = (await _context.Standings
                .Where(s => s.LeagueId == league.Id && s.SeasonId == season.Id).ToListAsync())
            .ToDictionary(s => s.TeamId);

        int upserted = 0;

        // Bazı liglerde (playoff/grup usulü) birden fazla grup olabilir — hepsini düzleştir.
        foreach (var group in groupsEl.EnumerateArray())
        foreach (var row in group.EnumerateArray())
        {
            try
            {
                string teamName = TeamNameNormalizer.Normalize(row.GetProperty("team").GetProperty("name").GetString() ?? "");
                if (string.IsNullOrWhiteSpace(teamName)) continue;

                if (!existingTeams.TryGetValue(teamName.ToLower(), out var team))
                {
                    team = TeamNameNormalizer.FindSafeFuzzyMatch(teamName, existingTeams.Values);
                    if (team == null)
                    {
                        team = new Team { Name = teamName };
                        _context.Teams.Add(team);
                        await _context.SaveChangesAsync();
                    }
                    existingTeams[teamName.ToLower()] = team;
                }

                var all = row.GetProperty("all");
                var home = row.GetProperty("home");
                var away = row.GetProperty("away");

                if (!existingStandings.TryGetValue(team.Id, out var st))
                {
                    st = new Standing { LeagueId = league.Id, SeasonId = season.Id, TeamId = team.Id };
                    _context.Standings.Add(st);
                    existingStandings[team.Id] = st;
                }

                st.Rank = row.GetProperty("rank").GetInt32();
                st.Points = row.GetProperty("points").GetInt32();
                st.GoalsDiff = row.GetProperty("goalsDiff").GetInt32();
                st.Form = row.TryGetProperty("form", out var formEl) ? formEl.GetString() ?? "" : "";
                st.Description = row.TryGetProperty("description", out var descEl) && descEl.ValueKind != JsonValueKind.Null
                    ? descEl.GetString() : null;

                st.Played = all.GetProperty("played").GetInt32();
                st.Win = all.GetProperty("win").GetInt32();
                st.Draw = all.GetProperty("draw").GetInt32();
                st.Lose = all.GetProperty("lose").GetInt32();
                st.GoalsFor = all.GetProperty("goals").GetProperty("for").GetInt32();
                st.GoalsAgainst = all.GetProperty("goals").GetProperty("against").GetInt32();

                st.HomePlayed = home.GetProperty("played").GetInt32();
                st.HomeWin = home.GetProperty("win").GetInt32();
                st.HomeDraw = home.GetProperty("draw").GetInt32();
                st.HomeLose = home.GetProperty("lose").GetInt32();
                st.HomeGoalsFor = home.GetProperty("goals").GetProperty("for").GetInt32();
                st.HomeGoalsAgainst = home.GetProperty("goals").GetProperty("against").GetInt32();

                st.AwayPlayed = away.GetProperty("played").GetInt32();
                st.AwayWin = away.GetProperty("win").GetInt32();
                st.AwayDraw = away.GetProperty("draw").GetInt32();
                st.AwayLose = away.GetProperty("lose").GetInt32();
                st.AwayGoalsFor = away.GetProperty("goals").GetProperty("for").GetInt32();
                st.AwayGoalsAgainst = away.GetProperty("goals").GetProperty("against").GetInt32();

                st.UpdatedAt = DateTime.UtcNow;
                upserted++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Standings satır hatası]: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return upserted;
    }
}
