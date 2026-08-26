using System.Text.Json;
using DogisAnaliz.Data;
using DogisAnaliz.Models;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Services;

public class FootballApiService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;

    public FootballApiService(HttpClient httpClient, AppDbContext context, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _context = context;

        string apiKey = configuration["FootballApi:ApiKey"] ?? "";
        _httpClient.BaseAddress = new Uri("https://v3.football.api-sports.io/");
        _httpClient.DefaultRequestHeaders.Add("x-apisports-key", apiKey);
    }

    public async Task<int> SyncLeagueSeasonAsync(int apiLeagueId, int seasonYear)
    {
        var response = await _httpClient.GetAsync($"fixtures?league={apiLeagueId}&season={seasonYear}&status=FT");

        if (!response.IsSuccessStatusCode)
            return 0;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("response", out var fixtures) || fixtures.GetArrayLength() == 0)
            return 0;

        var existingTeams = await _context.Teams.ToDictionaryAsync(t => t.Name, t => t);
        var existingSeasons = await _context.Seasons.ToDictionaryAsync(s => s.SeasonName, s => s);
        var existingLeagues = await _context.Leagues.ToDictionaryAsync(l => l.Name, l => l);

        int processedCount = 0;

        foreach (var item in fixtures.EnumerateArray())
        {
            try
            {
                var fixture = item.GetProperty("fixture");
                var leagueData = item.GetProperty("league");
                var teamsData = item.GetProperty("teams");
                var scoreData = item.GetProperty("score");

                string leagueName = leagueData.GetProperty("name").GetString()!;
                string country = leagueData.GetProperty("country").GetString()!;
                string seasonName = $"{seasonYear}-{seasonYear + 1}";

                string homeTeamName = teamsData.GetProperty("home").GetProperty("name").GetString()!;
                string awayTeamName = teamsData.GetProperty("away").GetProperty("name").GetString()!;

                int htHome = scoreData.GetProperty("halftime").GetProperty("home").GetInt32();
                int htAway = scoreData.GetProperty("halftime").GetProperty("away").GetInt32();
                int ftHome = scoreData.GetProperty("fulltime").GetProperty("home").GetInt32();
                int ftAway = scoreData.GetProperty("fulltime").GetProperty("away").GetInt32();

                // Hafta bilgisini al (Örn: "Regular Season - 1" -> 1)
                string roundStr = leagueData.GetProperty("round").GetString() ?? "";
                int week = 1;
                var matchRound = System.Text.RegularExpressions.Regex.Match(roundStr, @"\d+");
                if (matchRound.Success) week = int.Parse(matchRound.Value);

                // Lig Kontrolü
                if (!existingLeagues.TryGetValue(leagueName, out var league))
                {
                    league = new League { Name = leagueName, Country = country };
                    _context.Leagues.Add(league);
                    await _context.SaveChangesAsync();
                    existingLeagues[leagueName] = league;
                }

                // Sezon Kontrolü
                if (!existingSeasons.TryGetValue(seasonName, out var season))
                {
                    season = new Season { SeasonName = seasonName, StartYear = seasonYear, EndYear = seasonYear + 1 };
                    _context.Seasons.Add(season);
                    await _context.SaveChangesAsync();
                    existingSeasons[seasonName] = season;
                }

                // Ev Sahibi Takım
                if (!existingTeams.TryGetValue(homeTeamName, out var homeTeam))
                {
                    homeTeam = new Team { Name = homeTeamName };
                    _context.Teams.Add(homeTeam);
                    await _context.SaveChangesAsync();
                    existingTeams[homeTeamName] = homeTeam;
                }

                // Deplasman Takımı
                if (!existingTeams.TryGetValue(awayTeamName, out var awayTeam))
                {
                    awayTeam = new Team { Name = awayTeamName };
                    _context.Teams.Add(awayTeam);
                    await _context.SaveChangesAsync();
                    existingTeams[awayTeamName] = awayTeam;
                }

                DateTime rawDate = fixture.GetProperty("date").GetDateTime();
                DateTime matchDateUtc = DateTime.SpecifyKind(rawDate, DateTimeKind.Utc);

                bool exists = await _context.Matches.AnyAsync(m =>
                    m.HomeTeamId == homeTeam.Id &&
                    m.AwayTeamId == awayTeam.Id &&
                    m.SeasonId == season.Id);

                if (exists) continue;

                var match = new DogisAnaliz.Models.Match
                {
                    LeagueId = league.Id,
                    SeasonId = season.Id,
                    HomeTeamId = homeTeam.Id,
                    AwayTeamId = awayTeam.Id,
                    Week = week,
                    MatchDate = matchDateUtc,
                    HtHomeScore = htHome,
                    HtAwayScore = htAway,
                    FtHomeScore = ftHome,
                    FtAwayScore = ftAway
                };

                // Sürpriz Motoru
                var surprise = new MatchSurprise();
                surprise.CalculateSurprise(htHome, htAway, ftHome, ftAway);
                match.Surprise = surprise;

                _context.Matches.Add(match);
                processedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Maç İşleme Hatası]: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return processedCount;
    }
}