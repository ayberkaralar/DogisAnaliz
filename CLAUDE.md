BU BİR FUTBOL İDDİA ANALİZİ GÖSTERME, BULMA UYGULAMASIDIR.
C# diliyle ASP.NET Core MVC ile geliştirilmiştir. Bu uygulama, futbol maçlarının analizini yaparak kullanıcıya sürpriz maçları ve olası sonuçları sunmayı amaçlamaktadır.
SÜRPİZ MAÇIN TANIMI NEDİR? 
Sürpriz diye nitelendirilen 2 maç türü vardır:
1) Maçın +6 golle bitmesi
2)İlk yarıyı önde bitiren takımın maçı kaybetmesi durumu.
- 1/2: Ev sahibi ilk yarıda önde, deplasman maçı kazandı
- 2/1: Deplasman ilk yarıda önde, ev sahibi maçı kazandı
- İlk yarı berabereyse (X) bu kategoriye girmez
- İlk yarıyı önde bitirip maçı berabere bitirmek bu kategoriye GİRMEZ (sadece kayıp sayılır) 
PROJENİN YAPISI
Controller: Admin Controller: Admin sayfasındaki işlemleri yönetir. Kullanıcıların ve maçların yönetimi gibi işlevleri içerir.
Dogi Controller: Dogi Analizidiye bir page var. Bu pagede dogi diye bir analiz türü var. Bu analizin içeriği şudur:
Sıralarından bağımsız ve örnek olarak düşün: Eğer bir takım 10. haftayı 1-5 ve 5-1 skor ile tamamlamışsa 11. haftayı 2-2 tamamlamışsa diğer maçın yani 12.maçın 1/2 veya 2/1 olma olasılığı yüksektir.
Ayrıca bu analizde 10. hafta maçı 2-2 11. hafta maçı 1-5 veya 5-1 olursa diğer maçın 1/2 veya 2/1 olma olasılığı yüksektir. Bizim temel amacımız burada 1/2-2/1 olan maçı veya maçları yakalamak.
Bunun örneklerini bu sayfada gösteriyoruz. Models sayfasındaki DogiPattern'de buna yardımcı oluyor.
Models Klasörü:
Veri tabanı yapısının ve modellerin çalıştığı yerdir.
Services Klasörü: Veri alma ve verileri optimize eden servislerin bulunduğu yerdir. Bunların içinde DogiService DogiAnalizinin veri tabanından verileri DogiAnalizine göre 
filtlemeyi amaçlıyor.
FootbolApiServices:https://v3.football.api-sports.io sitesinden verileri istediğimiz şekilde çekebiliyoruz. Verileri APİ'den çekip veritabanına kaydediyoruz. Daha sonra çekiyoruz. Yai umarım öyledir.
MatchSurprise: Burada maçın sürpriz olup olmadığına göre filtrelemeyle verileri çekiyoruz.


# DogisAnaliz — Proje Talimatları

Futbol maçlarında "sürpriz" sonuçları (İY/MS dönüşü, yüksek gol, çoklu maç desenleri)
tespit edip görselleştiren bir ASP.NET Core MVC uygulaması.

## Teknoloji yığını

- **.NET 10**, ASP.NET Core MVC (Razor Views, controller bazlı, SPA değil)
- **PostgreSQL** + **EF Core 10** (Npgsql sağlayıcısı)
- **ExcelDataReader**: manuel Excel içe aktarma
- Solution dosyası `.slnx` formatında (yeni tip, XML tabanlı)

## Çalıştırma / build komutları

```bash
dotnet build
dotnet run --project DogisAnaliz          # http://localhost'ta ayağa kalkar
dotnet ef migrations add <İsim> --project DogisAnaliz
dotnet ef database update --project DogisAnaliz
```

`Program.cs` içinde uygulama her başlangıçta otomatik olarak veri senkronizasyonu
(api-sports.io + OpenFootball GitHub kaynağı) ve Dogi pattern taramasını çalıştırır.
Bu yüzden `dotnet run` ilk seferde biraz uzun sürebilir — normal.

## ⚠️ Güvenlik — önce bunu düzelt

`appsettings.json` içinde **düz metin PostgreSQL şifresi ve FootballApi API key'i var**
ve repo public. Yeni bir özellik eklemeden önce:
1. Her iki credential'ı da rotate et (şifreyi değiştir, API key'i yenile).
2. `appsettings.json`'ı `.gitignore`'a ekle, `appsettings.Example.json` gibi
   placeholder'lı bir örnek dosya bırak.
3. Gerçek değerleri `dotnet user-secrets` (local) veya ortam değişkeni (prod) ile ver.

Claude Code bu dosyayı düzenlerken **asla gerçek credential değerlerini commit'e
eklemesin veya chat'te tekrar yazmasın.**

## Domain modeli

### `Match` (Models/Match.cs)
Bir maçın temel verisi: `LeagueId`, `SeasonId`, `Week`, `MatchDate`, `HomeTeamId`,
`AwayTeamId`, `HtHomeScore`, `HtAwayScore`, `FtHomeScore`, `FtAwayScore`.
1:1 ilişkiler: `Surprise` (MatchSurprise) ve `Detail` (MatchDetail).

### `MatchSurprise` (Models/MatchSurprise.cs) — "Sürpriz" tanımının kaynağı
`CalculateSurprise(htHome, htAway, ftHome, ftAway)` metodu şunu hesaplar:

- `IyMsCode`: `"1/2"`, `"2/1"`, `"X/1"` vb. (İY sonucu / MS sonucu)
- `IsTurnaround`: `IyMsCode == "1/2"` veya `"2/1"` ise `true`
  → **Bu, kullanıcının tanımladığı "sürpriz": ilk yarıyı önde bitiren takım
  maçı kaybetti.** İlk yarı berabere (X) ise veya önde bitirip maçı da
  berabere bitirirse `IsTurnaround = false` sayılır (sadece galibiyet→mağlubiyet
  dönüşü sürpriz kabul edilir, galibiyet→beraberlik değil).
- `IsHighGoal`: toplam gol (FT) `>= 6` ise `true`
- `IsDoubleSurprise`: hem `IsTurnaround` hem `IsHighGoal` ise `true`
- `SurpriseType`: `"NONE" | "TURNAROUND" | "HIGH_GOAL" | "DOUBLE_SURPRISE"`

Yeni bir sürpriz tanımı eklerken bu enum/flag desenini takip et: yeni bir
`bool IsXxx` alanı + `SurpriseType` string'ine yeni bir case ekle, `CalculateSurprise`
içinde hesapla.

### `DogiPattern` (Models/DogiPattern.cs) — çoklu maç deseni
Bir takımın **ardışık 2 maçında** belirli bir skor deseni yakalanırsa, **3. maçın**
sürpriz olup olmadığını izler:

- `PatternType`: `"HIGH_DRAW"` (1. maç 5-1/1-5 → 2. maç 2-2) veya
  `"DRAW_HIGH"` (1. maç 2-2 → 2. maç 5-1/1-5)
- `TriggerMatch1Id` / `TriggerMatch2Id`: tetikleyici iki maç
- `AlertMatchId`: 3. (izlenen) maç — henüz oynanmadıysa `null`
- `AlertResult`: `"PENDING" | "SURPRISE" | "NORMAL"` — 3. maç oynandığında
  `MatchSurprise.SurpriseType != "NONE"` ise `SURPRISE`

Desen tespiti `Services/DogiService.cs` → `DetectPattern(m1, m2)` içinde;
tarama mantığı `ScanTeamAsync` (yeni desenleri bulur) ve
`UpdatePendingPatternsAsync` (PENDING kayıtları günceller) metotlarında.

Yeni bir çoklu-maç deseni eklerken bu yapıyı örnek al: `DogiPattern`'a benzer
yeni bir entity + `DogiService`'e benzer bir tarama servisi + migration.

## Mimari akış

```
Controllers/ (HomeController, DogiController, SyncController, ImportController, AdminController,
              Bilgi1Controller, Bilgi4Controller, FixturesController, GolBeklentisiController)
    ↓
Services/ (DogiService, ExcelImportService, FootballApiService, OpenFootballService,
           FixtureScheduleService, LeagueCatalog, ActiveSeasonRefresher, ActiveSeasonSyncService,
           LeagueDisplayHelper, TeamNameNormalizer)
    ↓
Data/AppDbContext.cs (EF Core, PostgreSQL)
    ↓
Models/ (Match, MatchSurprise, MatchDetail, DogiPattern, FixtureSchedule, League, Season, Team
         + ViewModel'ler: DashboardViewModel, DogiViewModel, Bilgi1ViewModel, Bilgi4ViewModel,
           GolBeklentisiViewModel, FixtureCalendarViewModel)
```

Views, controller adlarıyla birebir eşleşir (standart MVC convention):
`Views/{ControllerAdı}/{ActionAdı}.cshtml`.

### Veri kaynakları (3 farklı giriş noktası)
1. **`FootballApiService`** — api-sports.io, API key gerektirir. Artık sadece Süper Lig
   değil, `Services/LeagueCatalog.cs`'teki **14 ligin tamamı** için kullanılıyor
   (Premier League, La Liga, Bundesliga, Serie A, Ligue 1, Süper Lig, Eredivisie,
   Primeira Liga, 2. Bundesliga, La Liga 2, Eerste Divisie, Allsvenskan, Avusturya
   Bundesliga, Super League). Hem manuel `/Sync` ekranından hem "Senkronize Et"
   butonundan (bkz. aşağı) tetiklenir. **Artık tek geçerli canlı veri kaynağı budur.**
2. **`OpenFootballService`** — GitHub'daki openfootball/football.json, ücretsiz,
   limitsiz. Sadece **2020-21 ve 2021-22** sezonları için (bazı ligler 2024-25'e kadar)
   kullanıldı; `Program.cs` başlangıcında hâlâ taranır ama hedeflediği her lig+sezon
   zaten dolu olduğundan **pratikte artık hiçbir şey çekmiyor** (idempotent skip her
   zaman devreye giriyor). Yeni/güncel veri için kullanılmamalı — bkz. "Veri bütünlüğü"
   bölümü, iki kaynağın karışması duplicate takım/maç sorununun kök sebebiydi.
3. **`ExcelImportService`** — manuel Excel yükleme (Admin/Import ekranından).

Her ikisi de (API/OpenFootball) `Program.cs` başlangıcında **idempotent** çalışır:
DB'de o lig+sezon zaten varsa tekrar çekmez (`ctx.Matches.AnyAsync(...)` kontrolü).
Yeni bir veri kaynağı eklerken bu idempotency pattern'ini koru.

### Takım adı normalizasyonu
`TeamNameNormalizer` — farklı kaynaklardan gelen takım isimlerindeki
("Arsenal" vs "Arsenal FC", case farklılıkları) tutarsızlığı önlemek için
kullanılıyor. Yeni bir veri kaynağı eklerken bu normalizer'ı mutlaka kullan,
yoksa `Team` tablosunda duplicate kayıtlar oluşur (bkz. `AdminController.MergeTeams`
— zaten var olan duplicate temizleme ekranı).

## Kod stili / konvansiyonlar

- Domain terimleri ve yorumlar **Türkçe**, C# üye/sınıf isimleri **İngilizce PascalCase**.
- `AsNoTracking()` salt-okunur sorgularda tutarlı şekilde kullanılıyor — yeni
  read-only query'lerde de kullan.
- Foreign key'lerde `DeleteBehavior.Restrict` tercih ediliyor (cascade delete yok) —
  yeni ilişkilerde bu davranışı koru.
- View'lardaki filtre/sıralama parametreleri string literal (`"ALL"`, `"TURNAROUND"`,
  `week_asc` vb.) — yeni filtre eklerken enum yerine bu string convention'ı takip et.

## Yeni bir analiz eklerken izlenecek yol

1. `Models/` altına gerekli entity'yi ekle (varsa `MatchSurprise` veya `DogiPattern`
   şablonunu örnek al).
2. `AppDbContext.OnModelCreating` içine ilişki/index tanımlarını ekle.
3. `dotnet ef migrations add <İsim>` ile migration oluştur.
4. Hesaplama mantığını `Services/` altında ayrı bir servis olarak yaz (DogiService
   örneğindeki gibi: tarama + pending güncelleme ayrı metotlar).
5. Gerekirse yeni bir Controller + View ekle, mevcut `DashboardViewModel` /
   `DogiViewModel` desenini takip et (KPI kartları + filtrelenebilir tablo).

Not: Yukarıdaki 5 adımlı yol **kalıcı tabloya yazılan** desenler (DogiPattern gibi)
içindir. Aşağıdaki "fikstür deseni" analizleri (BİLGİ 1, BİLGİ 4) kalıcı tablo
kullanmıyor — anlık (on-the-fly) hesaplanıyor; bunlar için 1-3. adımlar gerekmez,
doğrudan Controller içinde hesaplanır (bkz. aşağı).

## Fikstür Takvimi ve Aktif Sezon Senkronizasyonu

### `FixtureSchedule` (Models/FixtureSchedule.cs) — oynanmamış maçlar dahil tüm takvim
`Match` tablosu sadece **oynanmış** maçları tutar (skor zorunlu alan). Bir sezonun
**tam fikstürünü** (oynanmamış maçlar dahil) göstermek/tahmin yapmak için ayrı bir
tablo: `FixtureSchedule` — `LeagueId, SeasonId, Week, KickoffUtc, HomeTeamId,
AwayTeamId, Status (api-sports "NS"/"FT"/…), HomeGoals?, AwayGoals?`.
`ApiFixtureId` ile **upsert** edilir (erteleme/tarih değişikliği güncellenir, silinmez).
Doldurulması: `Services/FixtureScheduleService.SyncScheduleAsync` — `/Sync` ekranındaki
"📅 Fikstür Takvimi" butonuyla lig+sezon bazında manuel tetiklenir (otomatik/başlangıç
senkronuna dahil değil). `Controllers/FixturesController.cs` + `Views/Fixtures/Index.cshtml`
bunu lig/sezon/hafta/takım/durum filtreleriyle listeler.

Bir maç `FixtureSchedule`'da NS (oynanmamış) olarak görünüp `Match` tablosunda karşılığı
varsa (aynı LeagueId+SeasonId+HomeTeamId+AwayTeamId+Week), **`Match` esas alınır** —
oynanmışsa gerçek sonuç odur; `FixtureSchedule.Status` senkronlanana kadar geride
kalabilir (aşağıdaki "Senkronize Et" fikstür takvimini güncellemez, sadece `Match`'i).

### `LeagueCatalog` (Services/LeagueCatalog.cs) — desteklenen 14 lig + sezon listesi
`SyncController`'daki `KnownLeagues`/`KnownSeasons` buraya taşındı (ortak kullanım için).
`ActiveSeasonYear` sabiti = içinde bulunulan sezonun başlangıç yılı (örn. 2026 →
"2026-2027"). Yeni bir lig eklerken buraya ekle, `SyncController` ve
`ActiveSeasonRefresher` otomatik kullanır. **Dikkat:** aynı isimli iki lig olabilir
(örn. "Bundesliga" Almanya + Avusturya) — isim eşleştirmesi yapan her yerde
`.DistinctBy(l => l.Name)` ile teke indirilmeli, yoksa yanlış lige veri yazılır
(`FootballApiService` ligi sadece isimle eşleştiriyor).

### "Senkronize Et" — tek düğmeyle tüm liglerin aktif sezonunu güncelle
Ana Sayfa'daki **"🔄 Senkronize Et"** butonu → `POST /Sync/RefreshAllActive` →
`Services/ActiveSeasonRefresher.RefreshAsync`: DB'de zaten kayıtlı **her ligin**
aktif sezonunu (`LeagueCatalog.ActiveSeasonYear`) `FootballApiService.SyncLeagueSeasonAsync`
ile tek tek çeker (idempotent — sadece yeni oynanan maçlar eklenir), yeni maç
geldiyse Dogi taramasını yeniler. **Sadece `Matches`'i günceller, `FixtureSchedule`'ı
değil** (o ayrı, `/Sync`'teki "📅 Fikstür Takvimi" butonuyla).
Aynı işlem `Services/ActiveSeasonSyncService` (bir `BackgroundService`) tarafından
**uygulama açılışından ~8 sn sonra arka planda** bir kez otomatik çalıştırılır —
sunucunun açılışını bloklamaz.

## Fikstür Deseni Analizleri — BİLGİ 1 / BİLGİ 4 (anlık hesaplanır, kalıcı tablo yok)

İkisi de `Controllers/BilgiXController.cs` + `Models/BilgiXViewModel.cs` +
`Views/BilgiX/Index.cshtml` üçlüsü, lig+sezon filtresiyle, sezona göre bölünmüş
tablo. Aday maçları hem `Matches` (oynanmış) hem `FixtureSchedule` (oynanmamış,
aktif sezon) üzerinden tarar; deseni tutan ama henüz oynanmamış pivot maçlar
sayfanın üstünde **"🔮 Bekleyen Tahminler"** olarak listelenir — "Senkronize Et"
sonrası oynanan maç otomatik olarak gerçek sonucuyla alttaki (çözülmüş) tabloya geçer.

- **BİLGİ 4**: pivot maç A–B (hafta N). A'nın **N-1 & N-2**'deki rakip ikilisi,
  B'nin **N+1 & N+2**'deki rakip ikilisiyle aynıysa (yön serbest) desen tutar.
  Sıra/ev-deplasman önemsiz.
- **BİLGİ 1**: pivot maç A–B (hafta N). X = A'nın **N-1**'deki rakibi, Y = B'nin
  **N-1**'deki rakibi. X ile Y, N'den önce kendi aralarında **DÖNÜŞ** (İY/MS 1/2
  veya 2/1) oynamışsa desen tutar.
- **İzlenen çıktı ikisinde de**: pivot maçın DÖNÜŞ olması (+6 gol dahil değil).
- **Bulgu (tüm liglerde test edildi): ikisinin de kayda değer öngörü gücü yok.**
  Taban dönüş oranı ~%5,2; desen tuttuğunda BİLGİ 4 ~%5,3-7,4, BİLGİ 1 ~%5,6 —
  fark gürültü sınırında. Yeni "BİLGİ N" fikstür deseni istenirse bu ikisi şablon
  alınabilir ama **öngörü gücü olduğunu varsayma — her seferinde taban oranla
  (aynı koşullar altındaki tüm maçların dönüş oranıyla) kıyaslayıp doğrula.**

## Gol Beklentisi — tek gerçek öngörü sinyali (Controllers/GolBeklentisiController.cs)

BİLGİ 1/4'ün aksine **gerçek, tutarlı bir sinyal**: çok gollü (+6 / toplam≥5) maç
tahmini. Skor iki parçadan: **(1)** iki takımın harmanlanmış gol ortalaması
(kariyer maç başı toplam gol %50 + son 10 maç %50), **(2)** varsa aralarındaki
**H2H** (ikili geçmiş maçları) ortalaması — H2H karşılaşma sayısı arttıkça ağırlığı
artar (maks %50, ≥3 karşılaşma şartı). Bu, "Fenerbahçe–Galatasaray genel gol
ortalaması yüksek ama aralarında kısır geçiyor" gibi durumları düzeltir.

Kalibrasyon: bu birleşik skor **yürüyen (lookahead'siz)** şekilde 5 kovaya bölünüp
her kovanın tarihsel +6/+5 oranı hesaplanır — en düşük kova ~%3, en yüksek ~%11 (+6
için), **monoton ve her ligde tutarlı.** Sayfada:
- Kalibrasyon tablosu (kova → tarihsel oran),
- **Tarih filtresi**: girilen günün haftası + bir sonraki hafta penceresi (varsayılan
  bugün),
- O pencerede **oynanmış + oynanmamış maçlar birlikte**: oynanmamışsa tahmin,
  oynanmışsa **maç öncesi** (o ana kadarki veriyle, kendi skorunu katmadan) hesaplanmış
  tahmin + gerçek skor yan yana — tutarlılığı gözle takip etmek için.
- Takım gol ortalamaları tablosu (kariyer/son 10/harman).

Yeni bir "gerçek sinyal" araştırırken bu dosyadaki **yürüyen kalibrasyon** desenini
örnek al: `career`/`recent` (Queue, son N) / `h2h` sözlüklerini kronolojik sırayla
güncelleyip her maçı **kendinden önceki** veriyle değerlendir — lookahead'e düşme.

## Veri bütünlüğü — takım/lig/maç tekrarları (tekrar karşına çıkabilir, dikkat)

**Kök sebep:** iki farklı içe aktarma kaynağı (OpenFootball + api-sports) aynı gerçek
takımı farklı yazımla (aksan: "Nürnberg"/"Nurnberg"; önek/sonek: "Tottenham"/
"Tottenham Hotspur FC") ayrı `Team` satırı olarak oluşturabiliyor.
`TeamNameNormalizer` sadece case/whitespace farkını çözer, **aksan ve önek/sonek
farkını çözmez** — bu yüzden hâlâ elle birleştirme gerekebilir (`Admin/MergeTeams`).

**Önemli riskler ve çözümleri:**
- İki takım birleştirilince (`AdminController.MergeTeam`), aynı gerçek maçı temsil
  eden iki `Match` satırı (farklı kaynaktan, farklı takım ID'siyle) **birebir aynı
  hale gelebilir** (aynı LeagueId+SeasonId+Week+HomeTeamId+AwayTeamId). Bu artık
  **otomatik** temizleniyor: `MergeTeam` sonunda `DedupeMatchesAsync()` çalışır
  (en düşük Id'yi koru, gerisini bağlı MatchSurprise/MatchDetail/DogiPattern'lerle
  birlikte sil). Elle tetiklemek için Takım Düzelt sayfasında **"🧹 Duplicate
  Maçları Temizle"** butonu (`POST /Admin/DedupeMatches`) var.
- **Kritik kod kuralı**: takım/lig/sezon adı ya da `(hafta, evTakımı, depTakımı)`
  gibi "muhtemelen tekil ama garantisi olmayan" bir alanı anahtar yaparak ASLA
  ham `.ToDictionary()` / `.ToDictionaryAsync()` yazma — duplicate varsa
  `System.ArgumentException: An item with the same key has already been added`
  ile **tüm sayfa çöker**. Bunun yerine daima:
  ```csharp
  var dict = items.GroupBy(x => key).ToDictionary(g => g.Key, g => g.First());
  ```
  (ilkini sessizce tutar, çökmez). Bu düzeltme zaten `FootballApiService`,
  `OpenFootballService`, `FixtureScheduleService` ve `GolBeklentisiController`'da
  uygulandı — **yeni bir servis/controller yazarken bu deseni koru.**
- Lig tekrarı da olabilir (örn. "Jupiler Pro League" / "Belgian Pro League" aynı
  lig, iki kaynaktan geldi) — bunlar için henüz kalıcı bir UI yok, elle SQL/script
  ile birleştirildi.


