using System.Globalization;
using System.Text.Json;
using DogisAnaliz.Data;
using DogisAnaliz.Models;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Services;

/// <summary>
/// api-sports "predictions" ucundan MAÇ BAŞINA api-sports'un kendi tahmini (kazanan yüzdeleri,
/// tavsiye metni) çekilir — bizim modellerimizi (BİLGİ 1/4, Gol Beklentisi) DEĞİŞTİRMEZ, sadece
/// karşılaştırma/benchmark amaçlıdır (bkz. <see cref="FixturePrediction"/>). Maliyeti fixture
/// başına 1 çağrı olduğundan sadece YAKIN VADELİ (varsayılan: gelecek <see cref="DefaultWeeksAhead"/>
/// hafta) oynanmamış maçlar için çekilir — "/Sync" ekranında ayrı, manuel bir buton.
/// </summary>
public class PredictionService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;

    public const int DefaultWeeksAhead = 14; // ~2 hafta (gün bazlı süzme aşağıda yapılır)

    public PredictionService(HttpClient httpClient, AppDbContext context, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _context = context;

        string apiKey = configuration["FootballApi:ApiKey"] ?? "";
        _httpClient.BaseAddress = new Uri("https://v3.football.api-sports.io/");
        _httpClient.DefaultRequestHeaders.Add("x-apisports-key", apiKey);
    }

    private static double ParsePercent(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.String) return 0;
        var s = (el.GetString() ?? "").TrimEnd('%');
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>Seçili lig+sezonun, bugünden itibaren <paramref name="daysAhead"/> gün içindeki
    /// OYNANMAMIŞ maçları için api-sports tahminini çeker. Zaten kaydı olup güncel (24 saatten
    /// yeni) olanları tekrar çekmez — gereksiz API çağrısını önler.</summary>
    /// <returns>(çekilen, atlanan zaten-güncel, hata)</returns>
    public async Task<(int Synced, int Skipped, int Errors)> SyncUpcomingAsync(int leagueId, int seasonId, int daysAhead = DefaultWeeksAhead)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        var upcoming = await _context.FixtureSchedules.AsNoTracking()
            .Where(f => f.LeagueId == leagueId && f.SeasonId == seasonId
                     && f.Status == "NS" && f.KickoffUtc <= cutoff && f.KickoffUtc >= DateTime.UtcNow.AddDays(-1))
            .ToListAsync();

        var existing = (await _context.FixturePredictions
                .Where(p => p.LeagueId == leagueId && p.SeasonId == seasonId).ToListAsync())
            .ToDictionary(p => p.ApiFixtureId);

        // Kazananı (api-sports'un döndürdüğü api takım id'sini) bizim TeamId'mize çevirmek için
        // önceden bir kerede yükle — döngü içinde tekrar tekrar sorgu atmamak için.
        var involvedTeamIds = upcoming.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().ToList();
        var apiTeamIdByTeamId = await _context.Teams.AsNoTracking()
            .Where(t => involvedTeamIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.ApiTeamId);

        int synced = 0, skipped = 0, errors = 0;
        var freshCutoff = DateTime.UtcNow.AddHours(-24);

        foreach (var fx in upcoming)
        {
            if (existing.TryGetValue(fx.ApiFixtureId, out var already) && already.UpdatedAt > freshCutoff)
            {
                skipped++;
                continue;
            }

            try
            {
                var response = await _httpClient.GetAsync($"predictions?fixture={fx.ApiFixtureId}");
                if (!response.IsSuccessStatusCode) { errors++; continue; }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("response", out var respArr) || respArr.GetArrayLength() == 0)
                {
                    errors++;
                    continue;
                }

                var pred = respArr[0].GetProperty("predictions");

                int? winnerTeamId = null;
                string? winnerComment = null;
                if (pred.TryGetProperty("winner", out var winnerEl) && winnerEl.ValueKind == JsonValueKind.Object)
                {
                    if (winnerEl.TryGetProperty("id", out var wIdEl) && wIdEl.ValueKind == JsonValueKind.Number)
                    {
                        int apiWinnerId = wIdEl.GetInt32();
                        int? apiHomeId = apiTeamIdByTeamId.GetValueOrDefault(fx.HomeTeamId);
                        int? apiAwayId = apiTeamIdByTeamId.GetValueOrDefault(fx.AwayTeamId);
                        winnerTeamId = apiWinnerId == apiHomeId ? fx.HomeTeamId
                            : apiWinnerId == apiAwayId ? fx.AwayTeamId : (int?)null;
                    }
                    winnerComment = winnerEl.TryGetProperty("comment", out var c) && c.ValueKind == JsonValueKind.String
                        ? c.GetString() : null;
                }

                var percent = pred.GetProperty("percent");

                if (!existing.TryGetValue(fx.ApiFixtureId, out var row))
                {
                    row = new FixturePrediction
                    {
                        ApiFixtureId = fx.ApiFixtureId, LeagueId = leagueId, SeasonId = seasonId,
                        HomeTeamId = fx.HomeTeamId, AwayTeamId = fx.AwayTeamId
                    };
                    _context.FixturePredictions.Add(row);
                    existing[fx.ApiFixtureId] = row;
                }

                row.WinnerTeamId = winnerTeamId;
                row.WinnerComment = winnerComment;
                row.WinOrDraw = pred.TryGetProperty("win_or_draw", out var wod) && wod.ValueKind == JsonValueKind.True;
                row.UnderOver = pred.TryGetProperty("under_over", out var uo) && uo.ValueKind == JsonValueKind.String ? uo.GetString() : null;
                if (pred.TryGetProperty("goals", out var goalsEl) && goalsEl.ValueKind == JsonValueKind.Object)
                {
                    row.GoalsHome = goalsEl.TryGetProperty("home", out var gh) && gh.ValueKind == JsonValueKind.String ? gh.GetString() : null;
                    row.GoalsAway = goalsEl.TryGetProperty("away", out var ga) && ga.ValueKind == JsonValueKind.String ? ga.GetString() : null;
                }
                row.Advice = pred.TryGetProperty("advice", out var adv) && adv.ValueKind == JsonValueKind.String ? adv.GetString() : null;
                row.PercentHome = ParsePercent(percent.GetProperty("home"));
                row.PercentDraw = ParsePercent(percent.GetProperty("draw"));
                row.PercentAway = ParsePercent(percent.GetProperty("away"));
                row.UpdatedAt = DateTime.UtcNow;

                synced++;
                await Task.Delay(300); // rate limit'e karşı — maç başına 1 çağrı
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Predictions işleme hatası] fixture {fx.ApiFixtureId}: {ex.Message}");
                errors++;
            }
        }

        await _context.SaveChangesAsync();
        return (synced, skipped, errors);
    }
}
