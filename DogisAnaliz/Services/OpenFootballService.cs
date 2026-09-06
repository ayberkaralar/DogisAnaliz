using System.Text.Json;
using DogisAnaliz.Data;
using DogisAnaliz.Models;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Services;

public class OpenFootballService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;

    private const string BaseUrl = "https://raw.githubusercontent.com/openfootball/football.json/master/";

    public OpenFootballService(HttpClient httpClient, AppDbContext context)
    {
        _httpClient = httpClient;
        _context    = context;
    }

    // Çekilecek tüm lig/sezon kombinasyonları
    public static readonly (string Season, string Code, string LeagueName, string Country, string SeasonName, int StartYear, int EndYear)[] LeagueSeasons =
    {
        // ── Mevcut 4 Lig — Sadece OpenFootball'da olan 2020-21 ve 2021-22 ──
        ("2020-21", "en.1", "Premier League", "England", "2020-2021", 2020, 2021),
        ("2020-21", "de.1", "Bundesliga",     "Germany", "2020-2021", 2020, 2021),
        ("2020-21", "it.1", "Serie A",        "Italy",   "2020-2021", 2020, 2021),
        ("2020-21", "fr.1", "Ligue 1",        "France",  "2020-2021", 2020, 2021),
        ("2021-22", "en.1", "Premier League", "England", "2021-2022", 2021, 2022),
        ("2021-22", "de.1", "Bundesliga",     "Germany", "2021-2022", 2021, 2022),
        ("2021-22", "it.1", "Serie A",        "Italy",   "2021-2022", 2021, 2022),
        ("2021-22", "fr.1", "Ligue 1",        "France",  "2021-2022", 2021, 2022),

        // ── La Liga — Tüm 5 sezon OpenFootball'da mevcut ──
        ("2020-21", "es.1", "La Liga",        "Spain",   "2020-2021", 2020, 2021),
        ("2021-22", "es.1", "La Liga",        "Spain",   "2021-2022", 2021, 2022),
        ("2022-23", "es.1", "La Liga",        "Spain",   "2022-2023", 2022, 2023),
        ("2023-24", "es.1", "La Liga",        "Spain",   "2023-2024", 2023, 2024),
        ("2024-25", "es.1", "La Liga",        "Spain",   "2024-2025", 2024, 2025),

        // ── Eredivisie — Tüm 5 sezon OpenFootball'da mevcut ──
        ("2020-21", "nl.1", "Eredivisie",     "Netherlands", "2020-2021", 2020, 2021),
        ("2021-22", "nl.1", "Eredivisie",     "Netherlands", "2021-2022", 2021, 2022),
        ("2022-23", "nl.1", "Eredivisie",     "Netherlands", "2022-2023", 2022, 2023),
        ("2023-24", "nl.1", "Eredivisie",     "Netherlands", "2023-2024", 2023, 2024),
        ("2024-25", "nl.1", "Eredivisie",     "Netherlands", "2024-2025", 2024, 2025),

        // ── Portekiz Primeira Liga — Tüm 5 sezon OpenFootball'da mevcut ──
        ("2020-21", "pt.1", "Primeira Liga",  "Portugal", "2020-2021", 2020, 2021),
        ("2021-22", "pt.1", "Primeira Liga",  "Portugal", "2021-2022", 2021, 2022),
        ("2022-23", "pt.1", "Primeira Liga",  "Portugal", "2022-2023", 2022, 2023),
        ("2023-24", "pt.1", "Primeira Liga",  "Portugal", "2023-2024", 2023, 2024),
        ("2024-25", "pt.1", "Primeira Liga",  "Portugal", "2024-2025", 2024, 2025),

        // ── Belçika Pro Lig — OpenFootball'da sadece 2020-21 ve 2024-25 var ──
        // 2021-22: YOK (Excel gerekli)  |  2022-24: api-sports.io (ID: 144)
        ("2020-21", "be.1", "Belgian Pro League", "Belgium", "2020-2021", 2020, 2021),
        ("2024-25", "be.1", "Belgian Pro League", "Belgium", "2024-2025", 2024, 2025),
    };

    public async Task<int> SyncAsync(string githubSeason, string leagueCode,
        string leagueName, string country, string seasonName, int startYear, int endYear)
    {
        var url  = $"{BaseUrl}{githubSeason}/{leagueCode}.json";
        var json = await _httpClient.GetStringAsync(url);

        var matches = ParseMatches(json);
        if (matches.Count == 0)
        {
            Console.WriteLine($"[Bilgi] {leagueName} {seasonName}: Maç parse edilemedi.");
            return 0;
        }

        // Takım dictionary'si lowercase key ile — case-insensitive eşleşme için
        var existingTeams   = await _context.Teams
            .ToDictionaryAsync(t => t.Name.ToLower(), t => t);
        var existingSeasons = await _context.Seasons.ToDictionaryAsync(s => s.SeasonName, s => s);
        var existingLeagues = await _context.Leagues.ToDictionaryAsync(l => l.Name, l => l);

        // Lig
        if (!existingLeagues.TryGetValue(leagueName, out var league))
        {
            league = new League { Name = leagueName, Country = country };
            _context.Leagues.Add(league);
            await _context.SaveChangesAsync();
            existingLeagues[leagueName] = league;
        }

        // Sezon
        if (!existingSeasons.TryGetValue(seasonName, out var season))
        {
            season = new Season { SeasonName = seasonName, StartYear = startYear, EndYear = endYear };
            _context.Seasons.Add(season);
            await _context.SaveChangesAsync();
            existingSeasons[seasonName] = season;
        }

        int processedCount = 0;

        foreach (var m in matches)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(m.Team1) || string.IsNullOrWhiteSpace(m.Team2)) continue;
                if (m.Score?.Ft == null || m.Score.Ft.Count < 2) continue;

                int htHome = m.Score.Ht?.Count >= 2 ? m.Score.Ht[0] : 0;
                int htAway = m.Score.Ht?.Count >= 2 ? m.Score.Ht[1] : 0;
                int ftHome = m.Score.Ft[0];
                int ftAway = m.Score.Ft[1];

                int week = 1;
                if (!string.IsNullOrEmpty(m.Round))
                {
                    var wm = System.Text.RegularExpressions.Regex.Match(m.Round, @"\d+");
                    if (wm.Success) week = int.Parse(wm.Value);
                }

                DateTime matchDate = DateTime.UtcNow;
                if (DateTime.TryParse(m.Date, out var parsed))
                    matchDate = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

                // Takım adlarını normalize et
                string team1Name = TeamNameNormalizer.Normalize(m.Team1);
                string team2Name = TeamNameNormalizer.Normalize(m.Team2);

                if (!existingTeams.TryGetValue(team1Name.ToLower(), out var homeTeam))
                {
                    homeTeam = new Team { Name = team1Name };
                    _context.Teams.Add(homeTeam);
                    await _context.SaveChangesAsync();
                    existingTeams[team1Name.ToLower()] = homeTeam;
                }
                if (!existingTeams.TryGetValue(team2Name.ToLower(), out var awayTeam))
                {
                    awayTeam = new Team { Name = team2Name };
                    _context.Teams.Add(awayTeam);
                    await _context.SaveChangesAsync();
                    existingTeams[team2Name.ToLower()] = awayTeam;
                }

                bool exists = await _context.Matches.AnyAsync(x =>
                    x.HomeTeamId == homeTeam.Id &&
                    x.AwayTeamId == awayTeam.Id &&
                    x.SeasonId   == season.Id);

                if (exists) continue;

                var match = new DogisAnaliz.Models.Match
                {
                    LeagueId    = league.Id,
                    SeasonId    = season.Id,
                    HomeTeamId  = homeTeam.Id,
                    AwayTeamId  = awayTeam.Id,
                    Week        = week,
                    MatchDate   = matchDate,
                    HtHomeScore = htHome,
                    HtAwayScore = htAway,
                    FtHomeScore = ftHome,
                    FtAwayScore = ftAway
                };

                var surprise = new MatchSurprise();
                surprise.CalculateSurprise(htHome, htAway, ftHome, ftAway);
                match.Surprise = surprise;

                _context.Matches.Add(match);
                processedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Maç Hatası]: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return processedCount;
    }

    // ---------------------------------------------------------------
    // JSON Parse — tek veya çok JSON objeli dosyaları destekler
    // ---------------------------------------------------------------
    private List<OFMatch> ParseMatches(string json)
    {
        var result  = new List<OFMatch>();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Satır satır parse (çok objeli dosya)
        foreach (var line in json.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim();
            if (!t.StartsWith("{")) continue;
            try
            {
                var obj = JsonSerializer.Deserialize<OFRoot>(t, options);
                if (obj?.Matches != null) result.AddRange(obj.Matches);
            }
            catch { }
        }

        // Tek büyük obje dene
        if (result.Count == 0)
        {
            try
            {
                var obj = JsonSerializer.Deserialize<OFRoot>(json.Trim(), options);
                if (obj?.Matches != null) result.AddRange(obj.Matches);
            }
            catch { }
        }

        return result;
    }

    private class OFRoot  { public string? Name { get; set; } public List<OFMatch>? Matches { get; set; } }
    private class OFMatch { public string? Round { get; set; } public string? Date { get; set; } public string? Team1 { get; set; } public string? Team2 { get; set; } public OFScore? Score { get; set; } }
    private class OFScore { public List<int>? Ht { get; set; } public List<int>? Ft { get; set; } }
}
