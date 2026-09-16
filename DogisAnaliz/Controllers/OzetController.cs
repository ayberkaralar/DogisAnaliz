using DogisAnaliz.Data;
using DogisAnaliz.Models;
using DogisAnaliz.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Controllers;

/// <summary>
/// Uygulamanın yeni ilk açılış sayfası. Tanım/tasarım gerekçesi için bkz. <see cref="OzetViewModel"/>.
/// NOT: GOL BEKLENTİSİ kullanıcı talebiyle diğer 4 analizle (BİLGİ 1/4, DOGİ, LİG FİKSTÜR)
/// AYNI birleşik listeye karıştırılmaz — kendi ayrı listesinde (<see cref="OzetViewModel.GolBeklentisiMatches"/>)
/// tutulur ve sayfada ayrı bir bölümde gösterilir.
/// </summary>
public class OzetController : Controller
{
    private readonly AppDbContext _context;
    public OzetController(AppDbContext context) => _context = context;

    private const int WindowDays = 14;

    public async Task<IActionResult> Index()
    {
        var vm = new OzetViewModel();
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(WindowDays);
        vm.WindowFrom = now;
        vm.WindowTo = cutoff;

        // Anahtar: (Lig, Ev, Deplasman) — aynı maç birden fazla analizden gelirse TEK satırda
        // birleşir (rozetler eklenir). Hafta/tarih ilk görülen kaynaktan alınır.
        var byKey = new Dictionary<(string League, string Home, string Away), OzetMatchDto>();

        OzetMatchDto GetOrAdd(string league, string home, string away, int week, DateTime kickoff)
        {
            var key = (league, home, away);
            if (!byKey.TryGetValue(key, out var m))
            {
                m = new OzetMatchDto { LeagueName = league, HomeTeam = home, AwayTeam = away, Week = week, KickoffUtc = kickoff };
                byKey[key] = m;
            }
            return m;
        }

        // --- BİLGİ 1 ---
        // "Bunların dönüş oranı" (LeaguePatternTurnaroundRate/LeaguePatternCount) zaten
        // Bilgi1Controller.BuildAsync tarafından hesaplanıp CrossLeaguePending satırına
        // dolduruluyor (bkz. Bilgi1/Index.cshtml'deki aynı gösterim) — burada TEKRAR
        // HESAPLANMAZ, sadece rozete taşınır. Kullanıcı talebiyle sadece bu oran gösterilir,
        // ayrı bir "tutma oranı" hesaplaması eklenmez.
        var b1 = await Bilgi1Controller.BuildAsync(_context, null, null);
        foreach (var r in b1.CrossLeaguePending)
        {
            if (!r.KickoffUtc.HasValue || r.KickoffUtc.Value > cutoff) continue;
            var m = GetOrAdd(r.LeagueName, r.TeamA, r.TeamB, r.Week, r.KickoffUtc.Value);
            string rate1 = r.LeaguePatternCount > 0 ? $"%{r.LeaguePatternTurnaroundRate} (n={r.LeaguePatternCount})" : "veri yok";
            m.Tags.Add(new OzetTagDto
            {
                Source = "BİLGİ 1", Color = "#7dd3fc", Short = rate1,
                Detail = $"{r.OppX} & {r.OppY} önceki hafta karşılaşıp dönüş çıkarmıştı (H{r.TriggerWeek}, {r.TriggerIyMs}) — bu ligde BİLGİ 1 deseninin tuttuğu maçların dönüş oranı: {rate1}"
            });
            vm.Bilgi1Count++;
        }

        // --- BİLGİ 4 ---
        var b4 = await Bilgi4Controller.BuildAsync(_context, null, null);
        foreach (var r in b4.CrossLeaguePending)
        {
            if (!r.KickoffUtc.HasValue || r.KickoffUtc.Value > cutoff) continue;
            var m = GetOrAdd(r.LeagueName, r.TeamA, r.TeamB, r.Week, r.KickoffUtc.Value);
            string rate4 = r.LeaguePatternCount > 0 ? $"%{r.LeaguePatternTurnaroundRate} (n={r.LeaguePatternCount})" : "veri yok";
            m.Tags.Add(new OzetTagDto
            {
                Source = "BİLGİ 4", Color = "#c4b5fd", Short = rate4,
                Detail = $"Ortak rakipler: {r.SharedOpp1} + {r.SharedOpp2} ({r.Direction}) — bu ligde BİLGİ 4 deseninin tuttuğu maçların dönüş oranı: {rate4}"
            });
            vm.Bilgi4Count++;
        }

        // --- LİG FİKSTÜR ---
        var lf = await LigFiksturController.BuildAsync(_context, null, null);
        foreach (var round in lf.LivePending)
        {
            foreach (var c in round.Candidates)
            {
                if (c.KickoffUtc > cutoff) continue;
                var m = GetOrAdd(round.LeagueName, c.HomeTeam, c.AwayTeam, round.Week, c.KickoffUtc);
                var evidence = string.Join(", ", round.PlayedMatches
                    .Where(pm => pm.Is22 || pm.SurpriseType != "NONE")
                    .Select(pm => pm.Is22 ? $"{pm.HomeTeam}-{pm.AwayTeam} 2-2" : $"{pm.HomeTeam}-{pm.AwayTeam} {pm.SurpriseType}"));
                m.Tags.Add(new OzetTagDto
                {
                    Source = "LİG FİKSTÜR", Color = "#f472b6",
                    Detail = $"Bu haftanın oynanan maçlarında zaten 2-2 + sürpriz var ({evidence}) — takip et"
                });
                vm.LigFiksturCount++;
            }
        }

        // --- DOGİ ---
        // AlertMatchId==null olan PENDING pattern'ler için "3. maç" DogiPattern'de henüz
        // bilinmiyor (bkz. DogiService — sadece oynanmış maçlara bakar, FixtureSchedule'a
        // hiç bakmaz). Burada, sadece bu özet için, o takımın bir sonraki (henüz oynanmamış)
        // fikstürünü FixtureSchedule'dan buluyoruz — DogiPattern/DogiService'in kendisi
        // DEĞİŞTİRİLMEDİ, salt-okunur bir eşleştirme.
        var pendingDogi = await _context.DogiPatterns.AsNoTracking()
            .Include(d => d.Team).Include(d => d.League).Include(d => d.Season)
            .Include(d => d.TriggerMatch2)
            .Where(d => d.AlertResult == "PENDING" && d.AlertMatchId == null)
            .ToListAsync();

        if (pendingDogi.Count > 0)
        {
            var fxAll = await _context.FixtureSchedules.AsNoTracking()
                .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
                .Where(f => f.KickoffUtc > now && f.KickoffUtc <= cutoff)
                .Select(f => new { f.LeagueId, f.SeasonId, f.HomeTeamId, f.AwayTeamId,
                    HomeName = f.HomeTeam.Name, AwayName = f.AwayTeam.Name, f.KickoffUtc, f.Week })
                .ToListAsync();

            foreach (var d in pendingDogi)
            {
                var next = fxAll
                    .Where(f => f.LeagueId == d.LeagueId && f.SeasonId == d.SeasonId
                             && (f.HomeTeamId == d.TeamId || f.AwayTeamId == d.TeamId)
                             && f.KickoffUtc > d.TriggerMatch2.MatchDate)
                    .OrderBy(f => f.KickoffUtc)
                    .FirstOrDefault();
                if (next == null) continue;

                var m = GetOrAdd(d.League.Name, next.HomeName, next.AwayName, next.Week, next.KickoffUtc);
                string patternText = d.PatternType == "HIGH_DRAW" ? "5-1/1-5 → 2-2" : "2-2 → 5-1/1-5";
                m.Tags.Add(new OzetTagDto
                {
                    Source = "DOGİ", Color = "#fbbf24",
                    Detail = $"{d.Team.Name} son 2 maçında {patternText} deseni gösterdi — 3. maçta sürpriz (dönüş/+6) beklenir"
                });
                vm.DogiCount++;
            }
        }

        // --- GOL BEKLENTİSİ ---
        // Kullanıcı talebiyle diğer 4 analizden AYRI tutulur — yukarıdaki `byKey`'e KARIŞMAZ,
        // kendi bağımsız listesine (vm.GolBeklentisiMatches) yazılır; sayfada ayrı bir bölümde
        // gösterilir. Tam takvim hesaplaması (GolBeklentisiController) burada TEKRAR EDİLMEZ —
        // sadece "combined skor ÜST 2 kovada mı" sorusuna cevap için career/recent/h2h hesaplanır
        // (aynı formül: bkz. Services/GoalExpectationCalculator.cs). Tarihsel kalibrasyon
        // (kova → +6 oranı) burada YOK, sadece GolBeklentisi sayfasında var — bu özet sadece
        // "yüksek kovaya düştü" diye işaretler, oranı görmek için o sayfaya yönlendirir.
        vm.GolBeklentisiMatches = await BuildGoalExpectationMatchesAsync(now, cutoff, () => vm.GolBeklentisiCount++);

        vm.Matches = byKey.Values
            .OrderBy(m => m.KickoffUtc)
            .ToList();

        return View(vm);
    }

    private async Task<List<OzetMatchDto>> BuildGoalExpectationMatchesAsync(
        DateTime now, DateTime cutoff, Action countIncrement)
    {
        var byKey = new Dictionary<(string League, string Home, string Away), OzetMatchDto>();
        OzetMatchDto GetOrAdd(string league, string home, string away, int week, DateTime kickoff)
        {
            var key = (league, home, away);
            if (!byKey.TryGetValue(key, out var m))
            {
                m = new OzetMatchDto { LeagueName = league, HomeTeam = home, AwayTeam = away, Week = week, KickoffUtc = kickoff };
                byKey[key] = m;
            }
            return m;
        }

        await AddGoalExpectationTagsAsync(now, cutoff, GetOrAdd, countIncrement);

        return byKey.Values.OrderBy(m => m.KickoffUtc).ToList();
    }

    private async Task AddGoalExpectationTagsAsync(
        DateTime now, DateTime cutoff,
        Func<string, string, string, int, DateTime, OzetMatchDto> getOrAdd,
        Action countIncrement)
    {
        const int recentN = 10;

        var ms = await _context.Matches.AsNoTracking()
            .Select(m => new { m.LeagueId, LeagueName = m.League.Name, m.HomeTeamId, m.AwayTeamId, m.MatchDate,
                m.FtHomeScore, m.FtAwayScore })
            .ToListAsync();

        var fx = await _context.FixtureSchedules.AsNoTracking()
            .Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.League)
            .Where(f => f.KickoffUtc > now && f.KickoffUtc <= cutoff)
            .Select(f => new { f.LeagueId, LeagueName = f.League.Name, f.HomeTeamId, f.AwayTeamId,
                HomeName = f.HomeTeam.Name, AwayName = f.AwayTeam.Name, f.KickoffUtc, f.Week })
            .ToListAsync();
        if (fx.Count == 0) return;

        // Sadece fikstürü olan liglerin geçmişini işlemeye değer (performans).
        var relevantLeagueIds = fx.Select(f => f.LeagueId).Distinct().ToHashSet();

        foreach (var leagueGroup in ms.Where(m => relevantLeagueIds.Contains(m.LeagueId)).GroupBy(m => m.LeagueId))
        {
            var leagueMatches = leagueGroup.OrderBy(m => m.MatchDate).ToList();
            double leagueAvgHalf = leagueMatches.Count > 0
                ? leagueMatches.Average(x => x.FtHomeScore + x.FtAwayScore) : 2.8;

            var recent = new Dictionary<int, Queue<int>>();
            var career = new Dictionary<int, (long g, int n)>();
            var h2h = new Dictionary<(int, int), (long g, int n)>();

            foreach (var m in leagueMatches)
            {
                int tot = m.FtHomeScore + m.FtAwayScore;
                void Upd(int id)
                {
                    if (!recent.TryGetValue(id, out var q)) { q = new(); recent[id] = q; }
                    q.Enqueue(tot); if (q.Count > recentN) q.Dequeue();
                    var c = career.GetValueOrDefault(id); career[id] = (c.g + tot, c.n + 1);
                }
                Upd(m.HomeTeamId); Upd(m.AwayTeamId);
                var pk = (Math.Min(m.HomeTeamId, m.AwayTeamId), Math.Max(m.HomeTeamId, m.AwayTeamId));
                var ph = h2h.GetValueOrDefault(pk);
                h2h[pk] = (ph.g + tot, ph.n + 1);
            }

            double FinalBlended(int id, out bool low)
            {
                var c = career.GetValueOrDefault(id);
                double car = c.n > 0 ? (double)c.g / c.n : leagueAvgHalf;
                var q = recent.GetValueOrDefault(id);
                double rec = (q != null && q.Count > 0) ? q.Average() : car;
                // GolBeklentisiController'ın kendi kalibrasyon eğrisi (bkz. Kalibrasyon tablosu)
                // takım başına en az 8 maç şartıyla (cH.n>=8 && cA.n>=8) hesaplanıyor — burada 6
                // kullanmak, kalibrasyonun hiç test etmediği (az veri) takımları "yüksek kova"
                // sayıp Özet'i gereksiz kalabalıklaştırıyordu. Kalibrasyonla TUTARLI olsun diye 8'e
                // çekildi (bkz. CLAUDE.md "Gol Beklentisi" — Özet bölümü).
                low = c.n < 8;
                return low ? leagueAvgHalf : 0.5 * car + 0.5 * rec;
            }

            foreach (var f in fx.Where(x => x.LeagueId == leagueGroup.Key))
            {
                double hr = FinalBlended(f.HomeTeamId, out bool lowH);
                double ar = FinalBlended(f.AwayTeamId, out bool lowA);
                if (lowH || lowA) continue; // az veri — özet sayfasında gürültü yaratmasın

                double teamPart = hr + ar;
                var pk = (Math.Min(f.HomeTeamId, f.AwayTeamId), Math.Max(f.HomeTeamId, f.AwayTeamId));
                var ph = h2h.GetValueOrDefault(pk);
                double h2hAvg = ph.n > 0 ? (double)ph.g / ph.n : 0;
                double combined = GoalExpectationCalculator.Combine(teamPart, h2hAvg, ph.n);
                string bucket = GoalExpectationCalculator.Bucket(combined);

                // Sadece EN ÜST kova — gerçek kalibrasyon verisiyle doğrulandı (bkz. GolBeklentisi
                // sayfasındaki Kalibrasyon tablosu): "5,8 – 6,2" kovasının 6+ gol oranı ~%8, ligin
                // taban ortalamasına (~%6,6) çok yakın — gürültü sınırında, anlamlı bir eleme
                // sağlamıyor. "≥ 6,2" kovası ise ~%9,2 ile en yüksek ve en tutarlı sinyal. İkisini
                // birden almak tüm maçların ~%37'sini işaretliyordu (çok kalabalık); sadece üst
                // kovayla bu oran ~%17'ye iniyor — daha eleyici, daha anlamlı.
                if (bucket != "≥ 6,2") continue;

                var m = getOrAdd(f.LeagueName, f.HomeName, f.AwayName, f.Week, f.KickoffUtc);
                m.Tags.Add(new OzetTagDto
                {
                    Source = "GOL BEKLENTİSİ", Color = "#38bdf8",
                    Short = $"{combined:0.00} · \"{bucket}\"",
                    Detail = $"Birleşik gol gücü {combined:0.00} — \"{bucket}\" kovası (tarihsel olarak en gollü kova)"
                });
                countIncrement();
            }
        }
    }
}
