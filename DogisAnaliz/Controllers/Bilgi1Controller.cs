using DogisAnaliz.Data;
using DogisAnaliz.Models;
using DogisAnaliz.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Controllers;

/// <summary>
/// BİLGİ 1 — fikstür deseni analizi (tüm ligler). Anlık hesaplanır (kalıcı tablo yok).
/// Oynanmış maçlar <c>Matches</c>'ten, oynanmamış (aktif sezon) maçlar <c>FixtureSchedules</c>'ten
/// gelir; henüz oynanmamış ama deseni tutan pivot maçlar "tahmin" olarak listelenir.
/// Tetikleyici (X-Y dönüş maçı) yalnızca OYNANMIŞ maçlardan sayılır.
/// Tanım için bkz. <see cref="Bilgi1ViewModel"/>.
/// </summary>
public class Bilgi1Controller : Controller
{
    private readonly AppDbContext _context;

    public Bilgi1Controller(AppDbContext context) => _context = context;

    private sealed record Fx(
        int SeasonId, string SeasonName, int Week, int HomeId, int AwayId,
        string HomeName, string AwayName,
        bool Played, bool Turn, string Ht, string Ft, string IyMs,
        DateTime Date, DateTime? Kickoff);

    private sealed record ScanResult(
        List<Bilgi1RowDto> Rows, List<Bilgi1RowDto> PendingRows,
        int EligiblePivotCount, int EligibleTurnaroundCount,
        int Bilgi1Count, int Bilgi1TurnaroundCount);

    public async Task<IActionResult> Index(int? leagueId, int? seasonId)
    {
        var vm = new Bilgi1ViewModel { SelectedSeasonId = seasonId };

        var leagueCounts = await _context.Matches.AsNoTracking()
            .GroupBy(m => m.LeagueId).Select(g => new { LeagueId = g.Key, Count = g.Count() }).ToListAsync();
        var lids = leagueCounts.Select(x => x.LeagueId).ToList();
        var leagueEntities = await _context.Leagues.AsNoTracking().Where(l => lids.Contains(l.Id)).ToListAsync();

        vm.Leagues = leagueEntities.Select(l => new Bilgi1LeagueDto
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

        var matchSeasonIds = await _context.Matches.AsNoTracking()
            .Where(m => m.LeagueId == leagueId.Value).Select(m => m.SeasonId).Distinct().ToListAsync();
        var schedSeasonIds = await _context.FixtureSchedules.AsNoTracking()
            .Where(f => f.LeagueId == leagueId.Value).Select(f => f.SeasonId).Distinct().ToListAsync();
        var allSeasonIds = matchSeasonIds.Concat(schedSeasonIds).Distinct().ToList();
        vm.Seasons = await _context.Seasons.AsNoTracking()
            .Where(s => allSeasonIds.Contains(s.Id)).OrderByDescending(s => s.StartYear).ToListAsync();

        vm.AllSeasons = !seasonId.HasValue;
        vm.SelectedSeasonName = seasonId.HasValue
            ? vm.Seasons.FirstOrDefault(s => s.Id == seasonId.Value)?.SeasonName ?? ""
            : "Tüm sezonlar";

        var result = await ScanLeagueAsync(leagueId.Value, seasonId);
        vm.Rows = result.Rows;
        vm.PendingRows = result.PendingRows;
        vm.EligiblePivotCount = result.EligiblePivotCount;
        vm.EligibleTurnaroundCount = result.EligibleTurnaroundCount;
        vm.Bilgi1Count = result.Bilgi1Count;
        vm.Bilgi1TurnaroundCount = result.Bilgi1TurnaroundCount;

        // --- Tüm liglerde bekleyen tahminler (seçili ligden bağımsız) ---
        // "Bu hafta Süper Lig'de desen tutan bir maç var ama ben Premier Lig'i açık tutuyorum,
        // hiç görmedim" şikayetine karşı: aktif sezonun tüm ligleri taranır, sadece önümüzdeki
        // ~14 gün içindeki bekleyen (henüz oynanmamış) satırlar toplanıp tek listede gösterilir.
        var activeSeason = await _context.Seasons.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SeasonName == LeagueCatalog.ActiveSeasonName);
        if (activeSeason != null)
        {
            var cutoff = DateTime.UtcNow.AddDays(14);
            var crossLeague = new List<Bilgi1RowDto>();
            foreach (var lg in vm.Leagues)
            {
                var r = lg.Id == leagueId.Value && seasonId == activeSeason.Id
                    ? result // aynı lig+sezon zaten tarandıysa tekrar tarama
                    : await ScanLeagueAsync(lg.Id, activeSeason.Id);

                foreach (var row in r.PendingRows)
                {
                    if (row.KickoffUtc.HasValue && row.KickoffUtc.Value <= cutoff)
                    {
                        row.LeagueName = lg.Name;
                        crossLeague.Add(row);
                    }
                }
            }
            vm.CrossLeaguePending = crossLeague.OrderBy(r => r.KickoffUtc ?? DateTime.MaxValue).ToList();
        }

        return View(vm);
    }

    /// <summary>Tek bir lig+sezon (veya tüm sezonlar, seasonId=null) için BİLGİ 1 taramasını
    /// çalıştırır. Hem seçili-lig detay ekranı hem tüm-liglerde-bekleyenler digest'i bunu kullanır.</summary>
    private async Task<ScanResult> ScanLeagueAsync(int leagueId, int? seasonId)
    {
        var mq = _context.Matches.AsNoTracking()
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

        var fq = _context.FixtureSchedules.AsNoTracking()
            .Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.Season)
            .Where(f => f.LeagueId == leagueId);
        if (seasonId.HasValue) fq = fq.Where(f => f.SeasonId == seasonId.Value);
        var sched = await fq.Select(f => new
        {
            f.SeasonId, SeasonName = f.Season.SeasonName, f.Week, f.HomeTeamId, f.AwayTeamId,
            HomeName = f.HomeTeam.Name, AwayName = f.AwayTeam.Name, f.KickoffUtc
        }).ToListAsync();

        var playedKey = played
            .Select(p => (p.SeasonId, Math.Min(p.HomeId, p.AwayId), Math.Max(p.HomeId, p.AwayId), p.Week))
            .ToHashSet();

        var fixtures = new List<Fx>(played);
        foreach (var s in sched)
        {
            var k = (s.SeasonId, Math.Min(s.HomeTeamId, s.AwayTeamId), Math.Max(s.HomeTeamId, s.AwayTeamId), s.Week);
            if (playedKey.Contains(k)) continue;
            fixtures.Add(new Fx(s.SeasonId, s.SeasonName, s.Week, s.HomeTeamId, s.AwayTeamId,
                s.HomeName, s.AwayName, false, false, "", "", "-", s.KickoffUtc, s.KickoffUtc));
        }

        var rows = new List<Bilgi1RowDto>();
        var pendingRows = new List<Bilgi1RowDto>();
        int eligiblePivotCount = 0, eligibleTurnaroundCount = 0, bilgi1Count = 0, bilgi1TurnaroundCount = 0;

        foreach (var g in fixtures.GroupBy(r => r.SeasonId))
        {
            var seasonRows = g.ToList();

            var byTeamWeek = new Dictionary<int, Dictionary<int, HashSet<int>>>();
            var pivots = new Dictionary<(int, int, int), Fx>();
            // Tetikleyici dönüş çiftleri — SADECE oynanmış maçlardan
            var turnPairs = new Dictionary<(int, int), List<(int wk, string iyms, string ft)>>();
            var teamNameById = new Dictionary<int, string>();

            foreach (var r in seasonRows)
            {
                teamNameById[r.HomeId] = r.HomeName;
                teamNameById[r.AwayId] = r.AwayName;

                void Add(int t, int w, int opp)
                {
                    if (!byTeamWeek.TryGetValue(t, out var wk)) { wk = new(); byTeamWeek[t] = wk; }
                    if (!wk.TryGetValue(w, out var set)) { set = new(); wk[w] = set; }
                    set.Add(opp);
                }
                Add(r.HomeId, r.Week, r.AwayId);
                Add(r.AwayId, r.Week, r.HomeId);

                var key = (Math.Min(r.HomeId, r.AwayId), Math.Max(r.HomeId, r.AwayId), r.Week);
                if (!pivots.TryGetValue(key, out var ex)) pivots[key] = r;
                else if (!ex.Played && r.Played) pivots[key] = r;

                if (r.Played && r.Turn)
                {
                    var pk = (Math.Min(r.HomeId, r.AwayId), Math.Max(r.HomeId, r.AwayId));
                    if (!turnPairs.TryGetValue(pk, out var lst)) { lst = new(); turnPairs[pk] = lst; }
                    lst.Add((r.Week, r.IyMs, r.Ft));
                }
            }

            int? OppAt(int team, int w) =>
                byTeamWeek.TryGetValue(team, out var wk) && wk.TryGetValue(w, out var s) && s.Count == 1
                    ? s.First() : (int?)null;

            foreach (var (a0, b0, n) in pivots.Keys)
            {
                if (n < 2) continue;
                var x = OppAt(a0, n - 1);
                var y = OppAt(b0, n - 1);
                if (x is null || y is null) continue;
                if (x == y) continue;
                if (x == a0 || x == b0 || y == a0 || y == b0) continue;

                var p = pivots[(a0, b0, n)];

                if (p.Played)
                {
                    eligiblePivotCount++;
                    if (p.Turn) eligibleTurnaroundCount++;
                }

                var pairKey = (Math.Min(x.Value, y.Value), Math.Max(x.Value, y.Value));
                if (!turnPairs.TryGetValue(pairKey, out var meets)) continue;
                var priorTurn = meets.Where(mm => mm.wk < n).OrderByDescending(mm => mm.wk).ToList();
                if (priorTurn.Count == 0) continue;

                var xy = priorTurn.First();
                var row = new Bilgi1RowDto
                {
                    SeasonName = p.SeasonName,
                    Week = n,
                    MatchDate = p.Date,
                    TeamA = teamNameById.GetValueOrDefault(a0, "?"),
                    TeamB = teamNameById.GetValueOrDefault(b0, "?"),
                    HtScore = p.Ht,
                    FtScore = p.Ft,
                    IyMsCode = p.IyMs,
                    IsTurnaround = p.Turn,
                    OppX = teamNameById.GetValueOrDefault(x.Value, "?"),
                    OppY = teamNameById.GetValueOrDefault(y.Value, "?"),
                    TriggerWeek = xy.wk,
                    TriggerIyMs = xy.iyms,
                    TriggerFt = xy.ft,
                    IsPending = !p.Played,
                    KickoffUtc = p.Kickoff
                };

                if (p.Played)
                {
                    bilgi1Count++;
                    if (p.Turn) bilgi1TurnaroundCount++;
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

        return new ScanResult(rows, pendingRows, eligiblePivotCount, eligibleTurnaroundCount, bilgi1Count, bilgi1TurnaroundCount);
    }
}
