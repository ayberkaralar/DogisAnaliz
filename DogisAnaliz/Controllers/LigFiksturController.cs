using DogisAnaliz.Data;
using DogisAnaliz.Models;
using DogisAnaliz.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Controllers;

/// <summary>
/// LİG FİKSTÜR — "aynı haftanın farklı maçları birbirini etkiler mi?" deseni. Anlık hesaplanır,
/// kalıcı tablo yok (bkz. CLAUDE.md "Yeni bir analiz eklerken izlenecek yol" notu — BİLGİ 1/4 ile
/// aynı kategori). Tüm ligleri tek seferde tarar (kullanıcı talebiyle — lig bazlı kırılım gerekiyor).
/// Tanım ve tetikleyici/hedef mantığı için bkz. <see cref="LigFiksturViewModel"/>.
/// </summary>
public class LigFiksturController : Controller
{
    private readonly AppDbContext _context;
    public LigFiksturController(AppDbContext context) => _context = context;

    private sealed record MatchRow(
        int LeagueId, int SeasonId, int Week, string LeagueName, string SeasonName,
        int FtHome, int FtAway, string SurpriseType, DateTime MatchDate,
        string HomeName, string AwayName);

    public async Task<IActionResult> Index(int? leagueId, int? seasonId)
    {
        var vm = new LigFiksturViewModel { SelectedLeagueId = leagueId, SelectedSeasonId = seasonId };

        // --- Filtre dropdown'ları: TÜM ligler / TÜM sezonlar (boş bırakılırsa hepsi birleşik
        // taranır — varsayılan). Lig seçilince sezon listesi o lige daralır. ---
        var leagueCounts = await _context.Matches.AsNoTracking()
            .GroupBy(m => m.LeagueId).Select(g => g.Key).ToListAsync();
        vm.Leagues = (await _context.Leagues.AsNoTracking()
                .Where(l => leagueCounts.Contains(l.Id)).ToListAsync())
            .Select(l => new LigFiksturLeagueDto { Id = l.Id, Name = l.Name, Country = l.Country })
            .OrderBy(l => LeagueDisplayHelper.ToTurkish(l.Name, l.Country))
            .ToList();

        var seasonIdsQuery = _context.Matches.AsNoTracking().AsQueryable();
        if (leagueId.HasValue) seasonIdsQuery = seasonIdsQuery.Where(m => m.LeagueId == leagueId.Value);
        var seasonIds = await seasonIdsQuery.Select(m => m.SeasonId).Distinct().ToListAsync();
        vm.Seasons = await _context.Seasons.AsNoTracking()
            .Where(s => seasonIds.Contains(s.Id)).OrderByDescending(s => s.StartYear).ToListAsync();

        // --- Oynanmış maçlar — seçili lig/sezon filtresiyle (boşsa tüm ligler/sezonlar). ---
        var mq = _context.Matches.AsNoTracking().AsQueryable();
        if (leagueId.HasValue) mq = mq.Where(m => m.LeagueId == leagueId.Value);
        if (seasonId.HasValue) mq = mq.Where(m => m.SeasonId == seasonId.Value);
        var all = await mq
            .Select(m => new MatchRow(
                m.LeagueId, m.SeasonId, m.Week, m.League.Name, m.Season.SeasonName,
                m.FtHomeScore, m.FtAwayScore,
                m.Surprise != null ? m.Surprise.SurpriseType : "NONE",
                m.MatchDate, m.HomeTeam.Name, m.AwayTeam.Name))
            .ToListAsync();

        if (all.Count == 0) return View(vm);

        // --- Fikstür takvimindeki round başına toplam maç sayısı — "bu round hâlâ devam ediyor
        // mu" (bazı maçları henüz oynanmadı) tespiti için. Sadece aktif sezon/senkronize edilmiş
        // ligler için veri var; olmayan (eski) sezonlarda round zaten "tamamlanmış" sayılır. ---
        var scheduleCounts = await _context.FixtureSchedules.AsNoTracking()
            .GroupBy(f => new { f.LeagueId, f.SeasonId, f.Week })
            .Select(g => new { g.Key.LeagueId, g.Key.SeasonId, g.Key.Week, Count = g.Count() })
            .ToDictionaryAsync(x => (x.LeagueId, x.SeasonId, x.Week), x => x.Count);

        int totalRounds = 0, baseHitCount = 0, triggerCount = 0, triggerHitCount = 0;
        var leagueStats = new Dictionary<int, LigFiksturLeagueStatDto>();
        var triggerRoundsDetail = new List<LigFiksturRoundDto>();
        var incompleteTriggerRounds = new List<(int LeagueId, int SeasonId, int Week, string LeagueName, string SeasonName, List<MatchRow> Played)>();

        foreach (var g in all.GroupBy(m => (m.LeagueId, m.SeasonId, m.Week)))
        {
            var matches = g.ToList();
            var first = matches[0];

            bool isComplete = true;
            if (scheduleCounts.TryGetValue((first.LeagueId, first.SeasonId, first.Week), out var schedCount)
                && schedCount > matches.Count)
                isComplete = false; // round'da hâlâ oynanmamış maç var

            bool has22 = matches.Any(m => m.FtHome == 2 && m.FtAway == 2);

            // Sürpriz "çeşidi" önemli — kullanıcı düzeltmesi: +6 gol + 2-2 varsa, İSABET için
            // ayrıca (FARKLI bir maçta) DÖNÜŞ olmalı; dönüş + 2-2 varsa, İSABET için ayrıca +6
            // gol olmalı. "Aynı çeşitten 2 sürpriz" (örn. iki ayrı +6'lı maç) İSABET SAYILMAZ —
            // sadece "toplam sürpriz sayısı ≥2" yeterli değil, iki FARKLI ÇEŞİT gerekiyor.
            var goalIdx = new List<int>();
            var turnIdx = new List<int>();
            for (int i = 0; i < matches.Count; i++)
            {
                var st = matches[i].SurpriseType;
                if (st is "HIGH_GOAL" or "DOUBLE_SURPRISE") goalIdx.Add(i);
                if (st is "TURNAROUND" or "DOUBLE_SURPRISE") turnIdx.Add(i);
            }
            bool hasGoalFlavor = goalIdx.Count > 0;
            bool hasTurnFlavor = turnIdx.Count > 0;
            // Tek bir ÇİFTE SÜRPRİZ maçı hem "gol" hem "dönüş" listesinde görünür ama kendi
            // kendini kanıtlayamaz — iki listenin TEK VE AYNI maçtan oluşmadığından emin ol.
            bool bothFlavorsFromDistinctMatches = hasGoalFlavor && hasTurnFlavor
                && !(goalIdx.Count == 1 && turnIdx.Count == 1 && goalIdx[0] == turnIdx[0]);

            bool isTrigger = has22 && (hasGoalFlavor || hasTurnFlavor);
            int surpriseCount = matches.Count(m => m.SurpriseType != "NONE");

            if (!isComplete)
            {
                // Devam eden round — sadece "tetikleyici şimdiden oluştu mu" ilgimizi çekiyor,
                // istatistiklere (taban/oran) KARIŞTIRILMAZ (eksik veri yanlış oran üretir).
                if (isTrigger)
                    incompleteTriggerRounds.Add((first.LeagueId, first.SeasonId, first.Week, first.LeagueName, first.SeasonName, matches));
                continue;
            }

            totalRounds++;
            // Taban oranı da AYNI kritere göre: rastgele bir haftada (2-2 şartı OLMADAN) hem gol
            // hem dönüş çeşidinden sürpriz birlikte bulunma oranı.
            bool isHit = bothFlavorsFromDistinctMatches;
            if (isHit) baseHitCount++;

            if (!leagueStats.TryGetValue(first.LeagueId, out var ls))
            {
                ls = new LigFiksturLeagueStatDto { LeagueName = first.LeagueName };
                leagueStats[first.LeagueId] = ls;
            }
            ls.RoundsScanned++;

            if (isTrigger)
            {
                triggerCount++;
                ls.TriggerRounds++;
                if (isHit) { triggerHitCount++; ls.HitRounds++; }

                triggerRoundsDetail.Add(new LigFiksturRoundDto
                {
                    LeagueName = first.LeagueName,
                    SeasonName = first.SeasonName,
                    Week = first.Week,
                    SurpriseCount = surpriseCount,
                    IsHit = isHit,
                    Matches = matches.OrderBy(m => m.MatchDate).Select(m => new LigFiksturMatchDto
                    {
                        HomeTeam = m.HomeName, AwayTeam = m.AwayName, MatchDate = m.MatchDate,
                        FtScore = $"{m.FtHome}-{m.FtAway}", TotalGoals = m.FtHome + m.FtAway,
                        SurpriseType = m.SurpriseType, Is22 = m.FtHome == 2 && m.FtAway == 2
                    }).ToList()
                });
            }
        }

        vm.TotalRoundsScanned = totalRounds;
        vm.BaseMultiSurpriseCount = baseHitCount;
        vm.TriggerRoundCount = triggerCount;
        vm.TriggerHitCount = triggerHitCount;
        vm.LeagueStats = leagueStats.Values
            .Where(l => l.TriggerRounds > 0)
            .OrderByDescending(l => l.TriggerHitRate)
            .ThenByDescending(l => l.TriggerRounds)
            .ToList();
        vm.TriggerRounds = triggerRoundsDetail
            .OrderByDescending(r => r.SeasonName).ThenBy(r => r.LeagueName).ThenBy(r => r.Week)
            .ToList();

        // --- Devam eden round'larda zaten tetikleyici oluşmuşsa: o round'un henüz oynanmamış
        // maçlarını (FixtureSchedule'dan) "aday" olarak göster. ---
        if (incompleteTriggerRounds.Count > 0)
        {
            foreach (var r in incompleteTriggerRounds)
            {
                var playedTeamPairs = r.Played
                    .Select(m => (m.HomeName, m.AwayName)) // isimle eşleştirmek yeterli (aynı round içinde)
                    .ToHashSet();

                var fx = await _context.FixtureSchedules.AsNoTracking()
                    .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
                    .Where(f => f.LeagueId == r.LeagueId && f.SeasonId == r.SeasonId && f.Week == r.Week)
                    .Select(f => new { f.HomeTeam.Name, AwayName = f.AwayTeam.Name, f.KickoffUtc })
                    .ToListAsync();

                var candidates = fx
                    .Where(f => !playedTeamPairs.Contains((f.Name, f.AwayName)))
                    .Select(f => new LigFiksturCandidateDto { HomeTeam = f.Name, AwayTeam = f.AwayName, KickoffUtc = f.KickoffUtc })
                    .OrderBy(c => c.KickoffUtc)
                    .ToList();

                if (candidates.Count == 0) continue; // fikstür verisi yoksa gösterecek bir şey yok

                vm.LivePending.Add(new LigFiksturLiveRoundDto
                {
                    LeagueName = r.LeagueName,
                    SeasonName = r.SeasonName,
                    Week = r.Week,
                    PlayedMatches = r.Played.OrderBy(m => m.MatchDate).Select(m => new LigFiksturMatchDto
                    {
                        HomeTeam = m.HomeName, AwayTeam = m.AwayName, MatchDate = m.MatchDate,
                        FtScore = $"{m.FtHome}-{m.FtAway}", TotalGoals = m.FtHome + m.FtAway,
                        SurpriseType = m.SurpriseType, Is22 = m.FtHome == 2 && m.FtAway == 2
                    }).ToList(),
                    Candidates = candidates
                });
            }

            vm.LivePending = vm.LivePending.OrderBy(r => r.Candidates.Min(c => c.KickoffUtc)).ToList();
        }

        return View(vm);
    }
}
