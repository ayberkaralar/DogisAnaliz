using DogisAnaliz.Data;
using DogisAnaliz.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<FootballApiService>();
builder.Services.AddHttpClient<OpenFootballService>();
builder.Services.AddHttpClient<FixtureScheduleService>();
builder.Services.AddScoped<DogiService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddControllersWithViews();

// Açılışta arka planda aktif sezonu güncelle (sunucuyu bloklamaz)
builder.Services.AddHostedService<ActiveSeasonSyncService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// ---------------------------------------------------------------
// api-sports.io — Sadece OpenFootball'da eksik olan ligler
// Belçika: 2021-22 hariç 2022-2024 buradan çekilir
// Süper Lig: OpenFootball'da yok, tamamen buradan
// ---------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var apiService = scope.ServiceProvider.GetRequiredService<FootballApiService>();
    var ctx        = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var leagues = new (int Id, string Name)[]
    {
        (203, "Süper Lig"),          // OpenFootball'da YOK → tüm sezonlar buradan
        // Not: Belçika → Jupiler Pro League zaten çalışıyor, Belgian Pro League kaldırıldı
    };

    int[] seasons = new int[] { 2022, 2023, 2024 };

    Console.WriteLine("\n=======================================================");
    Console.WriteLine("--> [API-Sports] Tüm Ligler (2022-2025) Kontrol Ediliyor...");
    Console.WriteLine("=======================================================");

    foreach (var league in leagues)
    {
        foreach (var seasonYear in seasons)
        {
            try
            {
                string seasonName = $"{seasonYear}-{seasonYear + 1}";

                // DB'de bu lig+sezon için veri var mı? Varsa HTTP isteği atma
                var leagueEntity = await ctx.Leagues.FirstOrDefaultAsync(l => l.Name == league.Name);
                var seasonEntity = await ctx.Seasons.FirstOrDefaultAsync(s => s.SeasonName == seasonName);
                if (leagueEntity != null && seasonEntity != null)
                {
                    bool hasData = await ctx.Matches.AnyAsync(m =>
                        m.LeagueId == leagueEntity.Id && m.SeasonId == seasonEntity.Id);
                    if (hasData)
                    {
                        Console.WriteLine($"--> {league.Name} ({seasonName}) zaten mevcut, atlanıyor.");
                        continue;
                    }
                }

                Console.WriteLine($"--> {league.Name} ({seasonName}) çekiliyor...");
                int count = await apiService.SyncLeagueSeasonAsync(league.Id, seasonYear);
                Console.WriteLine($"--> Başarılı! {count} maç kaydedildi.");
                await Task.Delay(1500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hata - {league.Name} {seasonYear}]: {ex.Message}");
            }
        }
    }

    Console.WriteLine("\n=======================================================");
    Console.WriteLine("--> [API-Sports] Tamamlandı!");
    Console.WriteLine("=======================================================\n");
}
// ---------------------------------------------------------------

// ---------------------------------------------------------------
// 2020-2021 ve 2021-2022 AKTARIMI (OpenFootball - GitHub)
// Limit yok, ücretsiz, Türkiye'den erişilebilir
// ---------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var ofService = scope.ServiceProvider.GetRequiredService<OpenFootballService>();
    var ctx       = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    Console.WriteLine("\n=======================================================");
    Console.WriteLine("--> [OpenFootball] 2020-2022 Sezonları Kontrol Ediliyor...");
    Console.WriteLine("=======================================================");

    foreach (var ls in OpenFootballService.LeagueSeasons)
    {
        try
        {
            // DB'de bu lig+sezon için veri var mı? Varsa GitHub'a istek atma
            var leagueEntity = await ctx.Leagues.FirstOrDefaultAsync(l => l.Name == ls.LeagueName);
            var seasonEntity = await ctx.Seasons.FirstOrDefaultAsync(s => s.SeasonName == ls.SeasonName);
            if (leagueEntity != null && seasonEntity != null)
            {
                bool hasData = await ctx.Matches.AnyAsync(m =>
                    m.LeagueId == leagueEntity.Id && m.SeasonId == seasonEntity.Id);
                if (hasData)
                {
                    Console.WriteLine($"--> {ls.LeagueName} ({ls.SeasonName}) zaten mevcut, atlanıyor.");
                    continue;
                }
            }

            Console.WriteLine($"--> {ls.LeagueName} ({ls.SeasonName}) çekiliyor...");
            int count = await ofService.SyncAsync(ls.Season, ls.Code, ls.LeagueName, ls.Country, ls.SeasonName, ls.StartYear, ls.EndYear);
            Console.WriteLine($"--> Başarılı! {count} maç kaydedildi.");
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Hata - {ls.LeagueName} {ls.SeasonName}]: {ex.Message}");
        }
    }

    Console.WriteLine("\n=======================================================");
    Console.WriteLine("--> [OpenFootball] Tamamlandı!");
    Console.WriteLine("=======================================================\n");
}
// ----------------------------------------------------------------------------------------------

// ---------------------------------------------------------------
// DOGİ PATTERN TARAMASI
// Her çalıştırmada: PENDING'leri günceller + yeni maçları tarar
// ---------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var dogiService = scope.ServiceProvider.GetRequiredService<DogiService>();
    Console.WriteLine("\n=======================================================");
    Console.WriteLine("--> [Dogi] Pattern taraması başlıyor...");
    Console.WriteLine("=======================================================");
    try
    {
        await dogiService.ScanAllAsync();
        Console.WriteLine("--> [Dogi] Tarama tamamlandı!\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Dogi Hata]: {ex.Message}\n");
    }
}
// ---------------------------------------------------------------

app.Run();