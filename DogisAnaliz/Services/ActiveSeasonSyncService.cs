using DogisAnaliz.Data;

namespace DogisAnaliz.Services;

/// <summary>
/// Uygulama açılışında (web sunucusu ayağa kalktıktan birkaç saniye sonra, ARKA PLANDA)
/// aktif sezonu tüm ligler için bir kez günceller. Sunucunun başlamasını bloklamaz.
/// </summary>
public class ActiveSeasonSyncService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<ActiveSeasonSyncService> _logger;

    public ActiveSeasonSyncService(IServiceProvider sp, ILogger<ActiveSeasonSyncService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Sunucu ayağa kalksın, açılış senkronizasyon blokları bitsin
            await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

            using var scope = _sp.CreateScope();
            var ctx  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var api  = scope.ServiceProvider.GetRequiredService<FootballApiService>();
            var dogi = scope.ServiceProvider.GetRequiredService<DogiService>();

            _logger.LogInformation("[Otomatik Güncelle] Aktif sezon ({Season}-{Next}) güncelleniyor...",
                LeagueCatalog.ActiveSeasonYear, LeagueCatalog.ActiveSeasonYear + 1);

            var r = await ActiveSeasonRefresher.RefreshAsync(
                ctx, api, dogi,
                log: msg => _logger.LogInformation("[Otomatik Güncelle] {Msg}", msg),
                ct: stoppingToken);

            _logger.LogInformation(
                "[Otomatik Güncelle] Bitti — {Leagues} lig, {Matches} yeni maç, {Err} hata.",
                r.LeaguesProcessed, r.MatchesAdded, r.Errors.Count);
        }
        catch (OperationCanceledException) { /* uygulama kapanıyor */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Otomatik Güncelle] Beklenmeyen hata.");
        }
    }
}
