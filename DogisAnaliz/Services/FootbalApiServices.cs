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
        // Pro plan: status filtresi kaldırıldı — tüm maçlar çekilir,
        // null skor olanlar (oynanmamış maçlar) kod içinde zaten atlanır.
        var response = await _httpClient.GetAsync(
            $"fixtures?league={apiLeagueId}&season={seasonYear}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[API Hata] HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            return 0;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("response", out var fixtures) || fixtures.GetArrayLength() == 0)
        {
            Console.WriteLine($"[Bilgi] {seasonYear}-{seasonYear + 1} sezonu için tamamlanmış maç (FT) bulunamadı. Sezon henüz başlamış veya veri yok.");
            return 0;
        }

        // Takım dictionary'si lowercase key ile — case-insensitive eşleşme için.
        // GroupBy + First (ToDictionaryAsync değil): DB'de henüz birleştirilmemiş aynı isimli
        // duplicate kayıt varsa çökmek yerine ilkini kullan.
        var existingTeams = (await _context.Teams.ToListAsync())
            .GroupBy(t => t.Name.ToLower()).ToDictionary(g => g.Key, g => g.First());

        var existingSeasons = (await _context.Seasons.ToListAsync())
            .GroupBy(s => s.SeasonName).ToDictionary(g => g.Key, g => g.First());
        var existingLeagues = (await _context.Leagues.ToListAsync())
            .GroupBy(l => l.Name).ToDictionary(g => g.Key, g => g.First());

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

                string homeTeamName = TeamNameNormalizer.Normalize(
                    teamsData.GetProperty("home").GetProperty("name").GetString()!);
                string awayTeamName = TeamNameNormalizer.Normalize(
                    teamsData.GetProperty("away").GetProperty("name").GetString()!);
                int apiHomeTeamId = teamsData.GetProperty("home").GetProperty("id").GetInt32();
                int apiAwayTeamId = teamsData.GetProperty("away").GetProperty("id").GetInt32();

                var htHomeEl = scoreData.GetProperty("halftime").GetProperty("home");
                var htAwayEl = scoreData.GetProperty("halftime").GetProperty("away");
                var ftHomeEl = scoreData.GetProperty("fulltime").GetProperty("home");
                var ftAwayEl = scoreData.GetProperty("fulltime").GetProperty("away");

                // Null skor = maç tamamlanmamış veya veri eksik, bu maçı atla
                if (htHomeEl.ValueKind == JsonValueKind.Null || htAwayEl.ValueKind == JsonValueKind.Null ||
                    ftHomeEl.ValueKind == JsonValueKind.Null || ftAwayEl.ValueKind == JsonValueKind.Null)
                {
                    Console.WriteLine($"[Atlandı] Skor verisi null olan maç geçildi (fixture id: {fixture.GetProperty("id").GetInt32()})");
                    continue;
                }

                int htHome = htHomeEl.GetInt32();
                int htAway = htAwayEl.GetInt32();
                int ftHome = ftHomeEl.GetInt32();
                int ftAway = ftAwayEl.GetInt32();

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

                // Ev Sahibi Takım — normalize key ile ara; tam eşleşme yoksa güvenli kısa/uzun ad
                // eşleşmesine bak (bkz. TeamNameNormalizer.FindSafeFuzzyMatch) — yoksa api-sports
                // farklı bir ad döndürdüğünde her senkronizasyonda aynı duplicate takım geri gelir.
                if (!existingTeams.TryGetValue(homeTeamName.ToLower(), out var homeTeam))
                {
                    homeTeam = TeamNameNormalizer.FindSafeFuzzyMatch(homeTeamName, existingTeams.Values);
                    if (homeTeam == null)
                    {
                        homeTeam = new Team { Name = homeTeamName };
                        _context.Teams.Add(homeTeam);
                        await _context.SaveChangesAsync();
                    }
                    existingTeams[homeTeamName.ToLower()] = homeTeam;
                }
                if (homeTeam.ApiTeamId == null) homeTeam.ApiTeamId = apiHomeTeamId; // bkz. TeamSeasonStat/FixturePrediction

                // Deplasman Takımı — normalize key ile ara (aynı desen)
                if (!existingTeams.TryGetValue(awayTeamName.ToLower(), out var awayTeam))
                {
                    awayTeam = TeamNameNormalizer.FindSafeFuzzyMatch(awayTeamName, existingTeams.Values);
                    if (awayTeam == null)
                    {
                        awayTeam = new Team { Name = awayTeamName };
                        _context.Teams.Add(awayTeam);
                        await _context.SaveChangesAsync();
                    }
                    existingTeams[awayTeamName.ToLower()] = awayTeam;
                }
                if (awayTeam.ApiTeamId == null) awayTeam.ApiTeamId = apiAwayTeamId;

                // ÖNEMLİ: api-sports tarihi genelde sıfır olmayan bir offset ile döner (örn. "+01:00" İngiltere
                // yaz saati). System.Text.Json bu durumda GetDateTime()'ı SUNUCUNUN yerel saatine çevirip
                // Kind=Local işaretler (offset "+00:00"/"Z" ise Kind=Utc kalır, değer değişmez). Önceden burada
                // doğrudan SpecifyKind(...,Utc) ile "yerel saat" değeri UTC diye etiketleniyordu — bu da
                // MatchDate'in gerçek UTC'den sunucunun UTC farkı kadar (örn. +3 saat, TR sunucusunda) kaymasına
                // yol açıyordu. Sonuç: senkronize edilen maçların tarihi/haftası, FixtureSchedule'daki
                // (doğru hesaplanan) KickoffUtc ile tutarsız kalıyor, Gol Beklentisi'nin hafta penceresi maçı
                // "oynanmış" olarak eşleştiremiyordu. ToUniversalTime() ile önce gerçek UTC'ye çevrilir, SONRA
                // Kind=Utc etiketlenir — FixtureScheduleService.SyncScheduleAsync'teki desenle birebir aynı.
                DateTime matchDateUtc = DateTime.SpecifyKind(
                    fixture.GetProperty("date").GetDateTime().ToUniversalTime(), DateTimeKind.Utc);

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