using DogisAnaliz.Data;
using DogisAnaliz.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<FootballApiService>();
builder.Services.AddControllersWithViews();

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

// --- SÜPER LİG (2025 ve 2026 Sezonları) AKTARIMI ---
using (var scope = app.Services.CreateScope())
{
    var apiService = scope.ServiceProvider.GetRequiredService<FootballApiService>();

    int superLigId = 203;
    // 2025 -> 2025-2026 Sezonu (Tamamlanmış maçlar)
    // 2026 -> 2026-2027 Sezonu (Günümüze kadar oynanıp biten maçlar)
    int[] targetSeasons = new int[] { 2025, 2026 };

    Console.WriteLine("\n=======================================================");
    Console.WriteLine("--> Süper Lig 2025-2026 ve 2026-2027 Sezonları Çekiliyor...");
    Console.WriteLine("=======================================================");

    foreach (var seasonYear in targetSeasons)
    {
        try
        {
            Console.WriteLine($"--> Süper Lig ({seasonYear}-{seasonYear + 1}) sezonu çekiliyor...");
            int count = await apiService.SyncLeagueSeasonAsync(superLigId, seasonYear);
            Console.WriteLine($"--> Başarılı! {count} maç ve analiz kaydedildi.");

            // Rate Limit koruması
            await Task.Delay(1500);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Hata - Sezon {seasonYear}]: {ex.Message}");
        }
    }

    Console.WriteLine("=======================================================");
    Console.WriteLine("--> Aktarım Tamamlandı!");
    Console.WriteLine("=======================================================\n");
}
// ---------------------------------------------------






app.Run();