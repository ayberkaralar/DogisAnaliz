using DogisAnaliz.Data;
using DogisAnaliz.Models;
using DogisAnaliz.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Controllers;

/// <summary>
/// BİLGİ 4 — fikstür deseni analizi (tüm ligler). Anlık hesaplanır (kalıcı tablo yok).
/// Oynanmış maçlar <c>Matches</c>'ten, oynanmamış (aktif sezon) maçlar <c>FixtureSchedules</c>'ten
/// gelir; desen ikisinin birleşiminde aranır. Henüz oynanmamış pivot maçlar "tahmin" olarak listelenir.
/// Tanım için bkz. <see cref="Bilgi4ViewModel"/>.
///
/// Hesaplama mantığı <see cref="BuildAsync"/>'te STATIC — bkz. Bilgi1Controller'daki aynı not,
/// <c>OzetController</c> bunu tekrar yazmadan çağırır.
/// </summary>
public class Bilgi4Controller : Controller
{
    private readonly AppDbContext _context;

    public Bilgi4Controller(AppDbContext context)
    {
        _context = context;
    }

    // Birleşik fikstür satırı (oynanmış veya oynanmamış)
    private sealed record Fx(
        int SeasonId, string SeasonName, int Week, int HomeId, int AwayId,
        string HomeName, string AwayName,
        bool Played, bool Turn, string Ht, string Ft, string IyMs,
        DateTime Date, DateTime? Kickoff);

    internal sealed record ScanResult(
        List<Bilgi4RowDto> Rows, List<Bilgi4RowDto> PendingRows,
        int EligiblePivotCount, int EligibleTurnaroundCount,
        int Bilgi4Count, int Bilgi4TurnaroundCount);

    public async Task<IActionResult> Index(int? leagueId, int? seasonId)
        => View(await BuildAsync(_context, leagueId, seasonId));

    /// <summary>Bilgi4Controller.Index'in tüm hesaplama mantığı — bkz. Bilgi1Controller.BuildAsync.</summary>
    internal static async Task<Bilgi4ViewModel> BuildAsync(AppDbContext context, int? leagueId, int? seasonId)
    {
        var vm = new Bilgi4ViewModel { SelectedSeasonId = seasonId };

        var leagueCounts = await context.Matches.AsNoTracking()
            .GroupBy(m => m.LeagueId)
            .Select(g => new { LeagueId = g.Key, Count = g.Count() })
            .ToListAsync();
        var lids = leagueCounts.Select(x => x.LeagueId).ToList();
        var leagueEntities = await context.Leagues.AsNoTracking()
            .Where(l => lids.Contains(l.Id)).ToListAsync();

        vm.Leagues = leagueEntities.Select(l => new Bilgi4LeagueDto
        {
            Id = l.Id, Name = l.Name, Country = l.Country,
            MatchCount = leagueCounts.First(c => c.LeagueId == l.Id).Count
        }).OrderBy(l => l.Name).ToList();

        if (vm.Leagues.Count == 0) return vm;

        if (!leagueId.HasValue)
            leagueId = (vm.Leagues.FirstOrDefault(l => l.Name == "Premier League")
                        ?? vm.Leagues.OrderByDescending(l => l.MatchCount).First()).Id;
        vm.SelectedLeagueId = leagueId;
        vm.SelectedLeagueName = vm.Leagues.FirstOrDefault(l => l.Id == leagueId)?.Name ?? "Lig";

        // Sezon listesi: maçı OLAN + fikstür takvimi OLAN sezonlar
        var matchSeasonIds = await context.Matches.AsNoTracking()
            .Where(m => m.LeagueId == leagueId.Value).Select(m => m.SeasonId).Distinct().ToListAsync();
        var schedSeasonIds = await context.FixtureSchedules.AsNoTracking()
            .Where(f => f.LeagueId == leagueId.Value).Select(f => f.SeasonId).Distinct().ToListAsync();
        var allSeasonIds = matchSeasonIds.Concat(schedSeasonIds).Distinct().ToList();
        vm.Seasons = await context.Seasons.AsNoTracking()
            .Where(s => allSeasonIds.Contains(s.Id))
            .OrderByDescending(s => s.StartYear).ToListAsync();

        vm.AllSeasons = !seasonId.HasValue;
        vm.SelectedSeasonName = seasonId.HasValue
            ? vm.Seasons.FirstOrDefault(s => s.Id == seasonId.Value)?.SeasonName ?? ""
            : "Tüm sezonlar";

        var result = await ScanLeagueAsync(context, leagueId.Value, seasonId);
        vm.Rows = result.Rows;
        vm.PendingRows = result.PendingRows;
        vm.EligiblePivotCount = result.EligiblePivotCount;
        vm.EligibleTurnaroundCount = result.EligibleTurnaroundCount;
        vm.Bilgi4Count = result.Bilgi4Count;
        vm.Bilgi4TurnaroundCount = result.Bilgi4TurnaroundCount;

        // --- Tüm liglerde bekleyen tahminler (seçili ligden bağımsız) ---
        // Bkz. Bilgi1Controller — aynı gerekçe: seçili lig ne olursa olsun, önümüzdeki ~14 gün
        // içinde desen tutan hiçbir maç kaçırılmasın.
        var activeSeason = await context.Seasons.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SeasonName == LeagueCatalog.ActiveSeasonName);
        if (activeSeason != null)
        {
            var cutoff = DateTime.UtcNow.AddDays(14);
            var crossLeague = new List<Bilgi4RowDto>();
            foreach (var lg in vm.Leagues)
            {
                var r = lg.Id == leagueId.Value && seasonId == activeSeason.Id
                    ? result
                    : await ScanLeagueAsync(context, lg.Id, activeSeason.Id);

                var nearTerm = r.PendingRows
                    .Where(row => row.KickoffUtc.HasValue && row.KickoffUtc.Value <= cutoff)
                    .ToList();
                if (nearTerm.Count == 0) continue;

                // Oran sütunları TÜM ZAMANLARIN verisiyle (bkz. Bilgi1Controller — aynı gerekçe:
                // sezon henüz birkaç hafta ilerlediyse aktif-sezon-bazlı oran neredeyse hep n=0).
                var allTime = lg.Id == leagueId.Value && !seasonId.HasValue
                    ? result
                    : await ScanLeagueAsync(context, lg.Id, null);

                double leagueBaseRate = allTime.EligiblePivotCount > 0
                    ? Math.Round(100.0 * allTime.EligibleTurnaroundCount / allTime.EligiblePivotCount, 1) : 0;
                double leaguePatternRate = allTime.Bilgi4Count > 0
                    ? Math.Round(100.0 * allTime.Bilgi4TurnaroundCount / allTime.Bilgi4Count, 1) : 0;

                foreach (var row in nearTerm)
                {
                    row.LeagueName = lg.Name;
                    row.LeagueBaseTurnaroundRate = leagueBaseRate;
                    row.LeagueEligiblePivotCount = allTime.EligiblePivotCount;
                    row.LeaguePatternTurnaroundRate = leaguePatternRate;
                    row.LeaguePatternCount = allTime.Bilgi4Count;
                    crossLeague.Add(row);
                }
            }
            vm.CrossLeaguePending = crossLeague.OrderBy(r => r.KickoffUtc ?? DateTime.MaxValue).ToList();
        }

        return vm;
    }

    /// <summary>Tek bir lig+sezon (veya tüm sezonlar, seasonId=null) için BİLGİ 4 taramasını
    /// çalıştırır. Hem seçili-lig detay ekranı hem tüm-liglerde-bekleyenler digest'i bunu kullanır.</summary>
    private static async Task<ScanResult> ScanLeagueAsync(AppDbContext context, int leagueId, int? seasonId)
    {
        var mq = context.Matches.AsNoTracking()
            .Include(m => m.HomeTeam).Include(m => m.AwayTeam).Include(m => m.Surprise).Include(m => m.Season)
            .Where(m => m.LeagueId == leagueId);
        if (seasonId.HasValue) mq = mq.Where(m => m.SeasonId == seasonId.Value);
        var played = await mq.Select(m => new Fx(
            m.SeasonId, m.Season.SeasonName, m.Week, m.HomeTeamId, m.AwayTeamId,
            m.HomeTeam.Name, m.AwayTeam.Name,
            true, m.Surprise != null && m.Surprise.IsTurnaround,
            $"{m.HtHomeScore}-{m.HtAwayScore}", $"{m.FtHomeScore}-{m.FtAwayScore}",
            m.Surprise != null ? m.Surprise.IyMsCode : "-",
            m.MatchDate, (DateTime?)null)).ToListAsync();

        var fq = context.FixtureSchedules.AsNoTracking()
            .Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.Season)
            .Where(f => f.LeagueId == leagueId);
        if (seasonId.HasValue) fq = fq.Where(f => f.SeasonId == seasonId.Value);
        var sched = await fq.Select(f => new
        {
            f.SeasonId, SeasonName = f.Season.SeasonName, f.Week, f.HomeTeamId, f.AwayTeamId,
            HomeName = f.HomeTeam.Name, AwayName = f.AwayTeam.Name, f.KickoffUtc
        }).ToListAsync();

        // Oynanmış maçların (sezon, minTakım, maxTakım, hafta) kümesi — hafta ile ayır ki
        // çift devreli ligde ikinci yarı fikstürleri elenmesin.
        var playedKey = played
            .Select(p => (p.SeasonId, Math.Min(p.HomeId, p.AwayId), Math.Max(p.HomeId, p.AwayId), p.Week))
            .ToHashSet();

        // Birleşik liste: tüm oynanmışlar + takvimdeki (henüz Matches'te olmayan) fikstürler
        var fixtures = new List<Fx>(played);
        foreach (var s in sched)
        {
            var k = (s.SeasonId, Math.Min(s.HomeTeamId, s.AwayTeamId), Math.Max(s.HomeTeamId, s.AwayTeamId), s.Week);
            if (playedKey.Contains(k)) continue; // zaten oynanmış olarak var
            fixtures.Add(new Fx(s.SeasonId, s.SeasonName, s.Week, s.HomeTeamId, s.AwayTeamId,
                s.HomeName, s.AwayName, false, false, "", "", "-", s.KickoffUtc, s.KickoffUtc));
        }

        var rows = new List<Bilgi4RowDto>();
        var pendingRows = new List<Bilgi4RowDto>();
        int eligiblePivotCount = 0, eligibleTurnaroundCount = 0, bilgi4Count = 0, bilgi4TurnaroundCount = 0;

        // --- Sezon sezon BİLGİ 4 taraması ---
        foreach (var g in fixtures.GroupBy(r => r.SeasonId))
        {
            var seasonRows = g.ToList();

            var byTeamWeek = new Dictionary<int, Dictionary<int, HashSet<int>>>();
            var pivots = new Dictionary<(int, int, int), Fx>();

            foreach (var r in seasonRows)
            {
                void Add(int t, int w, int opp)
                {
                    if (!byTeamWeek.TryGetValue(t, out var wk)) { wk = new(); byTeamWeek[t] = wk; }
                    if (!wk.TryGetValue(w, out var set)) { set = new(); wk[w] = set; }
                    set.Add(opp);
                }
                Add(r.HomeId, r.Week, r.AwayId);
                Add(r.AwayId, r.Week, r.HomeId);

                var key = (Math.Min(r.HomeId, r.AwayId), Math.Max(r.HomeId, r.AwayId), r.Week);
                if (!pivots.TryGetValue(key, out var ex))
                    pivots[key] = r;
                else if (!ex.Played && r.Played) // oynanmış kayıt önceliklidir
                    pivots[key] = r;
            }

            int? OppAt(int team, int w) =>
                byTeamWeek.TryGetValue(team, out var wk) && wk.TryGetValue(w, out var s) && s.Count == 1
                    ? s.First() : (int?)null;

            var teamNameById = seasonRows
                .SelectMany(r => new[] { (r.HomeId, r.HomeName), (r.AwayId, r.AwayName) })
                .GroupBy(x => x.Item1).ToDictionary(x => x.Key, x => x.First().Item2);

            foreach (var (a0, b0, n) in pivots.Keys)
            {
                var aP1 = OppAt(a0, n - 1); var aP2 = OppAt(a0, n - 2);
                var aN1 = OppAt(a0, n + 1); var aN2 = OppAt(a0, n + 2);
                var bP1 = OppAt(b0, n - 1); var bP2 = OppAt(b0, n - 2);
                var bN1 = OppAt(b0, n + 1); var bN2 = OppAt(b0, n + 2);
                if (aP1 is null || aP2 is null || aN1 is null || aN2 is null ||
                    bP1 is null || bP2 is null || bN1 is null || bN2 is null) continue;

                var aPrev = new HashSet<int> { aP1.Value, aP2.Value };
                var aNext = new HashSet<int> { aN1.Value, aN2.Value };
                var bPrev = new HashSet<int> { bP1.Value, bP2.Value };
                var bNext = new HashSet<int> { bN1.Value, bN2.Value };

                bool Valid(HashSet<int> s) => s.Count == 2 && !s.Contains(a0) && !s.Contains(b0);
                if (!Valid(aPrev) || !Valid(aNext) || !Valid(bPrev) || !Valid(bNext)) continue;

                bool dir1 = aPrev.SetEquals(bNext);
                bool dir2 = bPrev.SetEquals(aNext);

                var p = pivots[(a0, b0, n)];

                // KPI/taban yalnızca OYNANMIŞ pivotlar üzerinden
                if (p.Played)
                {
                    eligiblePivotCount++;
                    if (p.Turn) eligibleTurnaroundCount++;
                }

                if (!dir1 && !dir2) continue;

                var shared = (dir1 ? aPrev : bPrev).ToList();
                var row = new Bilgi4RowDto
                {
                    SeasonName = p.SeasonName,
                    Week = n,
                    MatchDate = p.Date,
                    TeamA = teamNameById.GetValueOrDefault(a0, "?"),
                    TeamB = teamNameById.GetValueOrDefault(b0, "?"),
                    SharedOpp1 = teamNameById.GetValueOrDefault(shared[0], "?"),
                    SharedOpp2 = teamNameById.GetValueOrDefault(shared[1], "?"),
                    Direction = dir1 && dir2 ? "çift yön" : dir1 ? "A→B" : "B→A",
                    HtScore = p.Ht,
                    FtScore = p.Ft,
                    IyMsCode = p.IyMs,
                    IsTurnaround = p.Turn,
                    IsPending = !p.Played,
                    KickoffUtc = p.Kickoff
                };

                if (p.Played)
                {
                    bilgi4Count++;
                    if (p.Turn) bilgi4TurnaroundCount++;
                    rows.Add(row);
                }
                else
                {
                    pendingRows.Add(row);
                }
            }
        }

        rows = rows
            .OrderByDescending(r => r.SeasonName)
            .ThenBy(r => r.Week)
            .ThenBy(r => r.MatchDate)
            .ToList();
        pendingRows = pendingRows
            .OrderBy(r => r.KickoffUtc ?? DateTime.MaxValue)
            .ThenBy(r => r.Week)
            .ToList();

        return new ScanResult(rows, pendingRows, eligiblePivotCount, eligibleTurnaroundCount, bilgi4Count, bilgi4TurnaroundCount);
    }
}
