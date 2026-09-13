using DogisAnaliz.Data;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Services;

/// <summary>
/// Tek düğme / tek işlem: <b>aktif oynanan sezonun</b> (bkz. <see cref="LeagueCatalog.ActiveSeasonYear"/>)
/// maçlarını <b>tüm takip edilen ligler</b> için api-sports'tan çeker.
/// <see cref="FootballApiService.SyncLeagueSeasonAsync"/> idempotenttir — var olan maç atlanır,
/// yalnızca yeni oynanan maçlar <c>Matches</c>'e eklenir. Yeni maç geldiyse Dogi taraması yenilenir.
///
/// Hem Ana Sayfa'daki "Senkronize Et" butonu hem açılışta çalışan <see cref="ActiveSeasonSyncService"/>
/// bunu kullanır.
/// </summary>
public static class ActiveSeasonRefresher
{
    public sealed record Result(int MatchesAdded, int LeaguesProcessed, List<string> Errors);

    public static async Task<Result> RefreshAsync(
        AppDbContext ctx,
        FootballApiService api,
        DogiService dogi,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        int season = LeagueCatalog.ActiveSeasonYear;

        // Yalnızca DB'de zaten bulunan ligleri güncelle
        var tracked = await ctx.Leagues.AsNoTracking().Select(l => l.Name).ToListAsync(ct);
        var trackedSet = new HashSet<string>(tracked, StringComparer.OrdinalIgnoreCase);

        int matchesAdded = 0, processed = 0;
        var errors = new List<string>();

        // Aynı isimli ligleri (ör. "Bundesliga" Almanya + Avusturya) teke indir —
        // FootballApiService ligi yalnızca isimle eşleştirdiğinden yanlış lige veri yazılmasın.
        foreach (var lc in LeagueCatalog.KnownLeagues.DistinctBy(l => l.Name))
        {
            if (ct.IsCancellationRequested) break;
            if (!trackedSet.Contains(lc.Name)) continue;

            try
            {
                int m = await api.SyncLeagueSeasonAsync(lc.ApiId, season);
                matchesAdded += m;
                processed++;
                if (m > 0) log?.Invoke($"{lc.Name}: {m} yeni maç");
                await Task.Delay(700, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                errors.Add($"{lc.Name}: {ex.Message}");
                log?.Invoke($"HATA {lc.Name}: {ex.Message}");
            }
        }

        if (matchesAdded > 0)
        {
            log?.Invoke($"Dogi taraması yenileniyor ({matchesAdded} yeni maç)...");
            await dogi.ScanAllAsync();
        }

        return new Result(matchesAdded, processed, errors);
    }
}
