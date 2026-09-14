using DogisAnaliz.Data;
using DogisAnaliz.Models;
using DogisAnaliz.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Controllers;

/// <summary>
/// GOL BEKLENTİSİ — çok gollü maç tahmini. Anlık hesaplanır. Tanım için bkz. <see cref="GolBeklentisiViewModel"/>.
/// </summary>
public class GolBeklentisiController : Controller
{
    private readonly AppDbContext _context;
    private const int RecentN = 10;

    public GolBeklentisiController(AppDbContext context) => _context = context;

    // Birleşik gol beklentisi -> kova etiketi
    private static string Bucket(double combined) => combined switch
    {
        < 5.0 => "< 5,0",
        < 5.4 => "5,0 – 5,4",
        < 5.8 => "5,4 – 5,8",
        < 6.2 => "5,8 – 6,2",
        _     => "≥ 6,2",
    };
    private static readonly string[] BucketOrder = { "< 5,0", "5,0 – 5,4", "5,4 – 5,8", "5,8 – 6,2", "≥ 6,2" };

    private static double H2hWeight(int n) => Math.Min(0.5, 0.09 * n);
    private static double Combine(double teamPart, double h2hAvg, int h2hN)
    {
        if (h2hN < 3) return teamPart;
        double w = H2hWeight(h2hN);
        return w * (2.0 * h2hAvg) + (1 - w) * teamPart;
    }

    public async Task<IActionResult> Index(int? leagueId, int? seasonId, DateTime? date, int? teamId)
    {
        var vm = new GolBeklentisiViewModel { SelectedSeasonId = seasonId, SelectedDate = date?.Date, SelectedTeamId = teamId };

        var leagueCounts = await _context.Matches.AsNoTracking()
            .GroupBy(m => m.LeagueId).Select(g => new { LeagueId = g.Key, Count = g.Count() }).ToListAsync();
        var lids = leagueCounts.Select(x => x.LeagueId).ToList();
        var leagueEntities = await _context.Leagues.AsNoTracking().Where(l => lids.Contains(l.Id)).ToListAsync();
        vm.Leagues = leagueEntities.Select(l => new GbLeagueDto
        {
            Id = l.Id, Name = l.Name, Country = l.Country,
            MatchCount = leagueCounts.First(c => c.LeagueId == l.Id).Count
        }).OrderBy(l => l.Name).ToList();
        if (vm.Leagues.Count == 0) return View(vm);

        if (!leagueId.HasValue)
            leagueId = (vm.Leagues.FirstOrDefault(l => l.Name == "Premier League")
                        ?? vm.Leagues.OrderByDescending(l => l.MatchCount).First()).Id;
        vm.SelectedLeagueId = leagueId;
        vm.SelectedLeagueName = vm.Leagues.FirstOrDefault(l => l.Id == leagueId)?.Name ?? "Lig";

        // Sezonlar: takvimi olan VEYA maçı olan sezonlar; varsayılan aktif (takvimli) sezon
        var schedSeasons = await _context.FixtureSchedules.AsNoTracking()
            .Where(f => f.LeagueId == leagueId.Value).Select(f => f.SeasonId).Distinct().ToListAsync();
        var matchSeasons = await _context.Matches.AsNoTracking()
            .Where(m => m.LeagueId == leagueId.Value).Select(m => m.SeasonId).Distinct().ToListAsync();
        var allSeasonIdsForDropdown = schedSeasons.Concat(matchSeasons).Distinct().ToList();
        vm.Seasons = await _context.Seasons.AsNoTracking()
            .Where(s => allSeasonIdsForDropdown.Contains(s.Id)).OrderByDescending(s => s.StartYear).ToListAsync();
        if (!seasonId.HasValue)
            seasonId = vm.Seasons.FirstOrDefault(s => s.SeasonName == LeagueCatalog.ActiveSeasonName)?.Id
                ?? (schedSeasons.Count > 0 ? vm.Seasons.FirstOrDefault(s => schedSeasons.Contains(s.Id)) : vm.Seasons.FirstOrDefault())?.Id;
        vm.SelectedSeasonId = seasonId;
        vm.SelectedSeasonName = vm.Seasons.FirstOrDefault(s => s.Id == seasonId)?.SeasonName ?? "-";

        // --- Ligin tüm oynanmış maçları, kronolojik (kalibrasyon + takım formu + pencere için) ---
        var ms = await _context.Matches.AsNoTracking()
            .Where(m => m.LeagueId == leagueId.Value)
            .Select(m => new { m.SeasonId, m.Week, m.MatchDate, m.HomeTeamId, m.AwayTeamId,
                HomeName = m.HomeTeam.Name, AwayName = m.AwayTeam.Name,
                m.FtHomeScore, m.FtAwayScore })
            .ToListAsync();
        ms = ms.OrderBy(x => x.MatchDate).ToList();
        double leagueAvgHalf = ms.Count > 0 ? ms.Average(x => x.FtHomeScore + x.FtAwayScore) : 2.8;

        // --- Seçili sezonun fikstür takvimi (oynanmamışları bulmak için) ---
        var schedForSeason = seasonId.HasValue
            ? await _context.FixtureSchedules.AsNoTracking()
                .Where(f => f.LeagueId == leagueId.Value && f.SeasonId == seasonId.Value)
                .Select(f => new { f.Week, f.KickoffUtc, f.HomeTeamId, f.AwayTeamId,
                    HomeName = f.HomeTeam.Name, AwayName = f.AwayTeam.Name })
                .ToListAsync()
            : new();

        var playedKeySeason = seasonId.HasValue
            ? ms.Where(m => m.SeasonId == seasonId.Value)
                .Select(m => (Math.Min(m.HomeTeamId, m.AwayTeamId), Math.Max(m.HomeTeamId, m.AwayTeamId), m.Week))
                .ToHashSet()
            : new HashSet<(int, int, int)>();

        // Seçili sezonun TÜM fikstürleri (oynanmış + oynanmamış) — hafta pencerelerini bulmak için
        var seasonFx = new List<(int Week, DateTime Date, int HomeId, int AwayId, string HomeName, string AwayName, bool Played)>();
        if (seasonId.HasValue)
        {
            seasonFx.AddRange(ms.Where(m => m.SeasonId == seasonId.Value)
                .Select(m => (m.Week, m.MatchDate, m.HomeTeamId, m.AwayTeamId, m.HomeName, m.AwayName, true)));
            foreach (var s in schedForSeason)
            {
                var key = (Math.Min(s.HomeTeamId, s.AwayTeamId), Math.Max(s.HomeTeamId, s.AwayTeamId), s.Week);
                if (playedKeySeason.Contains(key)) continue;
                seasonFx.Add((s.Week, s.KickoffUtc, s.HomeTeamId, s.AwayTeamId, s.HomeName, s.AwayName, false));
            }
        }

        // --- Hafta penceresi: seçilen tarihin haftası + bir sonraki hafta ---
        int windowWeekStart = 0, windowWeekEnd = 0;
        DateTime? windowFrom = null, windowTo = null;
        var windowWeeks = new HashSet<int>();
        if (seasonFx.Count > 0)
        {
            var weekRanges = seasonFx.GroupBy(f => f.Week)
                .Select(g => new { Week = g.Key, Min = g.Min(x => x.Date), Max = g.Max(x => x.Date) })
                .OrderBy(x => x.Week).ToList();

            DateTime refDate = (vm.SelectedDate ?? DateTime.UtcNow.AddHours(3)).Date;
            var upcomingWeek = weekRanges.FirstOrDefault(w => w.Max.Date >= refDate);
            int targetWeek = upcomingWeek?.Week ?? weekRanges.Last().Week;

            windowWeekStart = targetWeek;
            windowWeekEnd = targetWeek + 1;
            windowWeeks.Add(windowWeekStart);
            windowWeeks.Add(windowWeekEnd);

            windowFrom = weekRanges.FirstOrDefault(w => w.Week == windowWeekStart)?.Min;
            windowTo = weekRanges.FirstOrDefault(w => w.Week == windowWeekEnd)?.Max
                       ?? weekRanges.FirstOrDefault(w => w.Week == windowWeekStart)?.Max;
        }
        vm.WindowWeekStart = windowWeekStart;
        vm.WindowWeekEnd = windowWeekEnd;
        vm.WindowFrom = windowFrom;
        vm.WindowTo = windowTo;

        // Pencereye giren OYNANMIŞ maçların anahtarları — yürüyen taramada "o anki" (maç öncesi)
        // tahmini yakalamak için. Takım filtresi seçiliyse pencere artık 2 hafta değil o takımın
        // TÜM sezonu olduğundan, "o anki" tahmin de takımın oynadığı HER hafta için yakalanmalı.
        var windowPlayedKeys = seasonId.HasValue
            ? seasonFx.Where(f => f.Played && (teamId.HasValue
                    ? (f.HomeId == teamId.Value || f.AwayId == teamId.Value)
                    : windowWeeks.Contains(f.Week)))
                .Select(f => (f.Week, Math.Min(f.HomeId, f.AwayId), Math.Max(f.HomeId, f.AwayId)))
                .ToHashSet()
            : new HashSet<(int, int, int)>();

        // --- Takım -> son N maç + kariyer toplam ; H2H ---
        var recent = new Dictionary<int, Queue<int>>();
        var career = new Dictionary<int, (long g, int n)>();
        var h2h = new Dictionary<(int, int), (long g, int n)>();
        var names = new Dictionary<int, string>();

        var bucketTally = BucketOrder.ToDictionary(b => b, _ => (n: 0, p6: 0, p5: 0, g: 0L));
        int totN = 0, totP6 = 0, totP5 = 0; long totG = 0;

        // Pencerede oynanmış maçların "o anki" (maç öncesi) tahmini
        var windowPlayedPredictions = new Dictionary<(int week, int minId, int maxId),
            (double hr, double ar, double teamPart, int h2hN, double h2hAvg, double combined, string bucket, bool lowData)>();

        double BlendedAt(int n, long g, Queue<int>? recQ)
        {
            double car = n > 0 ? (double)g / n : leagueAvgHalf;
            double rec = (recQ != null && recQ.Count > 0) ? recQ.Average() : car;
            return n < 6 ? leagueAvgHalf : 0.5 * car + 0.5 * rec;
        }

        foreach (var m in ms)
        {
            names[m.HomeTeamId] = m.HomeName; names[m.AwayTeamId] = m.AwayName;
            int tot = m.FtHomeScore + m.FtAwayScore;
            var cH = career.GetValueOrDefault(m.HomeTeamId);
            var cA = career.GetValueOrDefault(m.AwayTeamId);
            var recH = recent.GetValueOrDefault(m.HomeTeamId);
            var recA = recent.GetValueOrDefault(m.AwayTeamId);
            var pk = (Math.Min(m.HomeTeamId, m.AwayTeamId), Math.Max(m.HomeTeamId, m.AwayTeamId));
            var ph = h2h.GetValueOrDefault(pk);

            bool eligible = cH.n >= 8 && cA.n >= 8;
            if (eligible)
            {
                double teamPart = (double)cH.g / cH.n + (double)cA.g / cA.n;
                double h2hAvgE = ph.n > 0 ? (double)ph.g / ph.n : 0;
                double comb = Combine(teamPart, h2hAvgE, ph.n);
                var b = Bucket(comb);
                var t = bucketTally[b];
                bucketTally[b] = (t.n + 1, t.p6 + (tot >= 6 ? 1 : 0), t.p5 + (tot >= 5 ? 1 : 0), t.g + tot);
                totN++; totP6 += tot >= 6 ? 1 : 0; totP5 += tot >= 5 ? 1 : 0; totG += tot;
            }

            var winKey = (m.Week, pk.Item1, pk.Item2);
            if (windowPlayedKeys.Contains(winKey))
            {
                double hrAny = BlendedAt(cH.n, cH.g, recH);
                double arAny = BlendedAt(cA.n, cA.g, recA);
                double tpAny = hrAny + arAny;
                double h2hAvgAny = ph.n > 0 ? (double)ph.g / ph.n : 0;
                double combAny = Combine(tpAny, h2hAvgAny, ph.n);
                windowPlayedPredictions[winKey] = (hrAny, arAny, tpAny, ph.n, h2hAvgAny, combAny, Bucket(combAny), !eligible);
            }

            void Upd(int id)
            {
                if (!recent.TryGetValue(id, out var q)) { q = new(); recent[id] = q; }
                q.Enqueue(tot); if (q.Count > RecentN) q.Dequeue();
                var c = career.GetValueOrDefault(id); career[id] = (c.g + tot, c.n + 1);
            }
            Upd(m.HomeTeamId); Upd(m.AwayTeamId);
            h2h[pk] = (ph.g + tot, ph.n + 1);
        }

        vm.BasePlus6 = totN > 0 ? Math.Round(100.0 * totP6 / totN, 1) : 0;
        vm.BasePlus5 = totN > 0 ? Math.Round(100.0 * totP5 / totN, 1) : 0;
        vm.BaseAvgGoals = totN > 0 ? Math.Round((double)totG / totN, 2) : 0;
        vm.Buckets = BucketOrder.Select(b =>
        {
            var t = bucketTally[b];
            return new GbBucketDto
            {
                Label = b, N = t.n,
                Plus6Rate = t.n > 0 ? Math.Round(100.0 * t.p6 / t.n, 1) : 0,
                Plus5Rate = t.n > 0 ? Math.Round(100.0 * t.p5 / t.n, 1) : 0,
                AvgGoals = t.n > 0 ? Math.Round((double)t.g / t.n, 2) : 0
            };
        }).ToList();
        var bucketByLabel = vm.Buckets.ToDictionary(b => b.Label, b => b);

        // Takım oranları (nihai, "şu an itibarıyla")
        double FinalBlended(int id, out bool low, out double car, out double rec, out int pl)
        {
            var c = career.GetValueOrDefault(id);
            pl = c.n;
            car = c.n > 0 ? (double)c.g / c.n : leagueAvgHalf;
            var q = recent.GetValueOrDefault(id);
            rec = (q != null && q.Count > 0) ? q.Average() : car;
            low = c.n < 6;
            return low ? leagueAvgHalf : 0.5 * car + 0.5 * rec;
        }
        vm.Teams = names.Keys.Select(id =>
        {
            double bl = FinalBlended(id, out bool low, out double car, out double rec, out int pl);
            return new GbTeamDto { Id = id, Name = names[id], CareerAvg = Math.Round(car, 2),
                RecentAvg = Math.Round(rec, 2), Blended = Math.Round(bl, 2), Played = pl, LowData = low };
        }).OrderByDescending(t => t.Blended).ToList();
        if (teamId.HasValue)
            vm.SelectedTeamName = names.GetValueOrDefault(teamId.Value, "-");

        // TeamSeasonStat (api-sports "teams/statistics") — senkronize edildiyse ev/deplasman
        // ayrı ortalamayı takım tablosuna ekle (bizim Blended hesabımıza EK/karşılaştırma).
        if (seasonId.HasValue)
        {
            var apiStats = await _context.TeamSeasonStats.AsNoTracking()
                .Where(s => s.LeagueId == leagueId.Value && s.SeasonId == seasonId.Value)
                .ToDictionaryAsync(s => s.TeamId);
            foreach (var t in vm.Teams)
            {
                if (apiStats.TryGetValue(t.Id, out var st))
                {
                    t.ApiGoalsForAvgHome = st.GoalsForAvgHome;
                    t.ApiGoalsForAvgAway = st.GoalsForAvgAway;
                }
            }
        }
        var blendedById = vm.Teams.ToDictionary(t => t.Id, t => t.Blended);
        var lowById = vm.Teams.ToDictionary(t => t.Id, t => t.LowData);

        // api-sports'un KENDİ tahmini (Sync ekranındaki "🔮 Tahminler" ile, sadece yakın vadeli
        // maçlar için çekilir) — varsa (HomeTeamId,AwayTeamId) ile eşleştirip karşılaştırma
        // amacıyla ekleriz; bizim Combined/Bucket hesabımızı DEĞİŞTİRMEZ.
        var apiPredictions = seasonId.HasValue
            ? (await _context.FixturePredictions.AsNoTracking()
                .Where(p => p.LeagueId == leagueId.Value && p.SeasonId == seasonId.Value)
                .ToListAsync())
                .ToDictionary(p => (p.HomeTeamId, p.AwayTeamId))
            : new Dictionary<(int, int), FixturePrediction>();

        // --- Pencere: oynanmış + oynanmamış maçlar birlikte ---
        // Takım filtresi YOKSA: global 2 haftalık pencere (bugünün haftası + sonraki).
        // Takım filtresi VARSA: o takımın SEZON BOYUNCA tüm maçları (geçmiş + kalan) — "maçlar
        // güncellenmiyor" şikayetinin sebebi buydu: 2 haftalık pencere kullanıcının takip ettiği
        // takımın maçını çoğu zaman kapsamıyordu. Artık takım seçilince tüm sezonu gösteriyoruz.
        if (seasonId.HasValue && (teamId.HasValue || windowWeeks.Count > 0))
        {
            // GroupBy + First (ToDictionary değil): takım birleştirmesinden sonra aynı hafta/ikili için
            // birden fazla (kopya) Match satırı kalmış olabilir — bu durumda çökmek yerine ilkini kullan.

            var msSeasonByKey = ms.Where(x => x.SeasonId == seasonId.Value)
                .GroupBy(x => (x.Week, Math.Min(x.HomeTeamId, x.AwayTeamId), Math.Max(x.HomeTeamId, x.AwayTeamId)))
                .ToDictionary(g => g.Key, g => g.First());

            var fixturesToShow = teamId.HasValue
                ? seasonFx.Where(f => f.HomeId == teamId.Value || f.AwayId == teamId.Value)
                : seasonFx.Where(f => windowWeeks.Contains(f.Week));

            foreach (var f in fixturesToShow)
            {
                var key = (f.Week, Math.Min(f.HomeId, f.AwayId), Math.Max(f.HomeId, f.AwayId));
                if (f.Played)
                {
                    var match = msSeasonByKey.GetValueOrDefault(key);
                    bool hasPred = windowPlayedPredictions.TryGetValue(key, out var pred);
                    var bl = hasPred ? pred.bucket : Bucket(leagueAvgHalf * 2);
                    var bk = bucketByLabel.GetValueOrDefault(bl);
                    vm.WindowFixtures.Add(new GbFixtureDto
                    {
                        Week = f.Week, KickoffUtc = f.Date,
                        Home = f.HomeName, Away = f.AwayName,
                        IsPlayed = true, FtHome = match?.FtHomeScore, FtAway = match?.FtAwayScore,
                        HomeRate = hasPred ? Math.Round(pred.hr, 2) : 0,
                        AwayRate = hasPred ? Math.Round(pred.ar, 2) : 0,
                        TeamPart = hasPred ? Math.Round(pred.teamPart, 2) : 0,
                        H2hCount = hasPred ? pred.h2hN : 0,
                        H2hAvg = hasPred && pred.h2hN > 0 ? Math.Round(pred.h2hAvg, 2) : 0,
                        Combined = hasPred ? Math.Round(pred.combined, 2) : 0,
                        BucketLabel = bl, Plus6Rate = bk?.Plus6Rate ?? 0, Plus5Rate = bk?.Plus5Rate ?? 0,
                        LowData = !hasPred || pred.lowData
                    });
                }
                else
                {
                    double hr = blendedById.GetValueOrDefault(f.HomeId, leagueAvgHalf);
                    double ar = blendedById.GetValueOrDefault(f.AwayId, leagueAvgHalf);
                    double teamPart = hr + ar;
                    var ph = h2h.GetValueOrDefault((Math.Min(f.HomeId, f.AwayId), Math.Max(f.HomeId, f.AwayId)));
                    double h2hAvg = ph.n > 0 ? (double)ph.g / ph.n : 0;
                    double comb = Combine(teamPart, h2hAvg, ph.n);
                    var bl = Bucket(comb);
                    var bk = bucketByLabel.GetValueOrDefault(bl);
                    apiPredictions.TryGetValue((f.HomeId, f.AwayId), out var apiPred);
                    vm.WindowFixtures.Add(new GbFixtureDto
                    {
                        Week = f.Week, KickoffUtc = f.Date,
                        Home = f.HomeName, Away = f.AwayName,
                        IsPlayed = false,
                        HomeRate = Math.Round(hr, 2), AwayRate = Math.Round(ar, 2),
                        TeamPart = Math.Round(teamPart, 2),
                        H2hCount = ph.n, H2hAvg = ph.n > 0 ? Math.Round(h2hAvg, 2) : 0,
                        Combined = Math.Round(comb, 2), BucketLabel = bl,
                        Plus6Rate = bk?.Plus6Rate ?? 0, Plus5Rate = bk?.Plus5Rate ?? 0,
                        LowData = lowById.GetValueOrDefault(f.HomeId) || lowById.GetValueOrDefault(f.AwayId),
                        ApiPercentHome = apiPred?.PercentHome, ApiPercentDraw = apiPred?.PercentDraw,
                        ApiPercentAway = apiPred?.PercentAway, ApiAdvice = apiPred?.Advice
                    });
                }
            }
            vm.WindowFixtures = vm.WindowFixtures.OrderBy(x => x.Week).ThenBy(x => x.KickoffUtc).ToList();
        }

        return View(vm);
    }
}
