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
**Not:** Excel içe aktarma (`ExcelImportService`, `ImportController`, `Views/Import`,
`ExcelDataReader` paket referansı, navbar'daki "📥 Excel Aktar" sekmesi) **tamamen
kaldırıldı** — kullanıcı talebiyle, "sadece API verileri geçerli sayılabilir" kararı
kapsamında. Artık sadece yukarıdaki 2 kaynak var.

Her ikisi de (API/OpenFootball) `Program.cs` başlangıcında **idempotent** çalışır:
DB'de o lig+sezon zaten varsa tekrar çekmez (`ctx.Matches.AnyAsync(...)` kontrolü).
Yeni bir veri kaynağı eklerken bu idempotency pattern'ini koru.

### Takım adı normalizasyonu
`TeamNameNormalizer` — farklı kaynaklardan gelen takım isimlerindeki
("Arsenal" vs "Arsenal FC", case farklılıkları) tutarsızlığı önlemek için
kullanılıyor. Yeni bir veri kaynağı eklerken bu normalizer'ı mutlaka kullan,
yoksa `Team` tablosunda duplicate kayıtlar oluşur (bkz. `AdminController.MergeTeams`
— zaten var olan duplicate temizleme ekranı).

### Ana Sayfa sol menü: lig grupları + takım listesi (HomeController/Views/Home/Index.cshtml)
Sol menüde ligler `LeagueDisplayHelper.GetSidebarRank(name, country)` ile 3 gruba
ayrılır — her grup `<details>` (native, JS'siz açılır/kapanır) içinde:
- **Büyük 5** (açık): Premier Lig, Almanya Bundesliga, Fransa Ligue 1, İspanya La
  Liga, İtalya Serie A — bu sabit sırayla.
- **Diğer Ligler** (açık): kalan 1. ligler, Türkçe ada göre alfabetik.
- **2. Ligler** (kapalı): `_secondTierNames` içindeki 2. ligler (2. Bundesliga, La
  Liga 2/Segunda División, Eerste Divisie), alfabetik, en altta.

Yeni bir lig eklenince otomatik doğru gruba düşer; sadece "büyük 5"e yeni bir ülke
eklenecekse `LeagueDisplayHelper._big5Order`'a `"{Country}_{Name}"` anahtarını ekle.

Ligin altında **Takımlar** grubu — seçili lig + seçili sezondaki tüm takımlar
alfabetik listelenir (dropdown/arama değil, doğrudan tıklanabilir liste). Bir takıma
tıklamak `teamId`'yi set eder ve o anki sekmede (Sürpriz/Fikstür/Puan Durumu) kalır;
Sürpriz veya Fikstür'deyken takım seçiliyse **`TeamStats` paneli** (O/G/B/M, gol,
ev/deplasman kırılımı, +6 gol/dönüş/sürpriz sayısı, varsa **Lig Sırası**) her zaman
gösterilir — bu panel `viewModel.Matches`'ten (sekmenin filtrelediği alt küme) değil,
o takımın o sezondaki **filtresiz tüm maçlarından** ayrı bir sorguyla hesaplanır;
aksi halde Sürpriz sekmesinde "4 maç" yerine yanlışlıkla "1 sürpriz maçı" gibi görünürdü.

### 3. sekme: Puan Durumu (`view=standings`, Views/Home/_StandingsTable.cshtml)
Sürpriz/Fikstür'ün yanına eklenen 3. sekme — seçili lig+sezonun güncel lig tablosunu
(`Standing` tablosundan, bkz. "Ek veri kaynakları" bölümü) rank'a göre sıralı gösterir:
O/G/B/M/AG/YG/AV/Puan/Form + üç ihtimal rengi (yeşil=Şampiyonlar Ligi, mavi=Avrupa
kupaları, kırmızı=küme düşme — `Standing.Description` metnine göre heuristik). Sidebar'dan
seçili takım varsa o satır mavi çerçeveyle vurgulanır. `HomeController.Index`'te
`standingsMode` bloğu `Match`/`FixtureSchedule` sorgu hattının TAMAMINI atlar (KPI/filtre/
sıralama/TeamStats hiçbiri çalışmaz) — sadece `Standings` tablosunu okur; bu yüzden yeni bir
"3. sekme" eklerken bu izole-if-else desenini örnek al (KPI kartları + hızlı filtreler +
maç tablosu tek bir `@if (!standingsMode) { ... } else { <partial standings> }` bloğu
içinde). **Dikkat:** yeni bir sekme eklerken `!fixturesMode`'u "sürpriz sekmesi aktif"
anlamında kullanan her yeri (`class="main-tab @(!fixturesMode ? ...)"` gibi) yeni sekmeyi
de hariç tutacak şekilde güncelle — aksi halde iki sekme birden "active" görünür (bu hata
bir kez yapıldı, düzeltildi).

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

**Ana Sayfa'nın "📋 Fikstür" sekmesi de aynı veriyi kullanır** (`HomeController.Index`,
`fixturesMode` bloğu, adım 10b): sadece `Matches` (oynanmış) değil, seçili lig+sezonun
`FixtureSchedule` kayıtlarından henüz oynanmamış olanları da tabloya ekler — kullanıcı bir
takımı seçtiğinde sezon boyunca kalan rakiplerini de görebilsin diye (`filter=ALL` hepsini,
`filter=PENDING` sadece kalanları gösterir; sonuç gerektiren SURPRISE/TURNAROUND/HIGH_GOAL/
NORMAL filtrelerinde eklenmez). Aynı "oynanmış maç `FixtureSchedule`'da hâlâ NS görünebilir"
sorununa karşı aynı dedupe deseni kullanılır (hafta+ev+dep anahtarıyla). `MatchRowDto.IsPlayed`
bu satırları ayırt eder (view'da soluk/"🔜 Planlandı" gösterilir). Bu iki ekran (Fikstür
Takvimi sayfası ile Ana Sayfa'nın Fikstür sekmesi) artık aynı amaca hizmet ediyor gibi
görünse de öyle değil: Fikstür Takvimi sayfası ligler-arası ham takvim taraması içindir,
Ana Sayfa'nın Fikstür sekmesi ise sürpriz analiziyle aynı ekranda, takım bazlı bütünsel
görünüm sağlar — ikisini birbirinin yerine geçecek şekilde birleştirmeye çalışma.

Bir maç `FixtureSchedule`'da NS (oynanmamış) olarak görünüp `Match` tablosunda karşılığı
varsa (aynı LeagueId+SeasonId+HomeTeamId+AwayTeamId+Week), **`Match` esas alınır** —
oynanmışsa gerçek sonuç odur; `FixtureSchedule.Status` senkronlanana kadar geride
kalabilir (aşağıdaki "Senkronize Et" fikstür takvimini güncellemez, sadece `Match`'i).

### `LeagueCatalog` (Services/LeagueCatalog.cs) — desteklenen 14 lig + sezon listesi
`SyncController`'daki `KnownLeagues`/`KnownSeasons` buraya taşındı (ortak kullanım için).
`ActiveSeasonYear` sabiti = içinde bulunulan sezonun başlangıç yılı (örn. 2026 →
"2026-2027"). `ActiveSeasonName` (`"{year}-{year+1}"`) → `Season.SeasonName` ile
birebir eşleşir. Yeni bir lig eklerken buraya ekle, `SyncController` ve
`ActiveSeasonRefresher` otomatik kullanır. **Dikkat:** aynı isimli iki lig olabilir
(örn. "Bundesliga" Almanya + Avusturya) — isim eşleştirmesi yapan her yerde
`.DistinctBy(l => l.Name)` ile teke indirilmeli, yoksa yanlış lige veri yazılır
(`FootballApiService` ligi sadece isimle eşleştiriyor).

**Varsayılan sezon kuralı**: kullanıcı talebiyle, sezon seçimi olan tüm ekranlar
(`HomeController`, `FixturesController`, `GolBeklentisiController`) varsayılan olarak
önce `LeagueCatalog.ActiveSeasonName`'i (o lig+sezon kombinasyonu mevcutsa) seçer,
yoksa en güncel sezona düşer. Eskiden `HomeController` bunu `Matches.OrderByDescending
(m => m.SeasonId)` ile (StartYear değil, DB Id'sine göre) seçiyordu — sezonlar
kronolojik olmayan sırada eklenmişse yanlış sezon varsayılan gelebiliyordu; artık
`ActiveSeasonName` ile doğrudan eşleştiriliyor. Yeni bir sezon-filtreli ekran
eklerken bu deseni kullan.

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

**"🌍 Tüm Liglerde Bekleyen" (CrossLeaguePending)** — sayfanın en üstünde, seçili
ligden BAĞIMSIZ bir kutu: aktif sezonun TÜM liglerini tarayıp önümüzdeki ~14 gün
içinde kickoff'u olan bekleyen desenleri tek listede gösterir (lig adı sütunuyla).
Kullanıcı talebiyle eklendi — varsayılan lig (Premier Lig) açıkken başka bir ligde
(örn. Süper Lig) desen tutan bir maç fark edilmeden geçebiliyordu. Controller'daki
tarama mantığı `ScanLeagueAsync(leagueId, seasonId)` private metoduna çıkarıldı —
hem seçili-lig detay taraması hem bu digest AYNI metodu çağırır (kod tekrarı yok);
digest için tüm liglere `LeagueCatalog.ActiveSeasonName`'in Season.Id'si verilir
(zaten seçili lig+sezon aynıysa tekrar taramaz, mevcut sonucu kullanır). Her satırda
ayrıca 2 kıyas sütunu var:
- **"Ligin genel dönüş oranı"** (`LeagueBaseTurnaroundRate`/`LeagueEligiblePivotCount`) —
  o ligde rastgele bir maça bakılsa dönüş çıkma ihtimali (taban).
- **"Bunların dönüş oranı"** (`LeaguePatternTurnaroundRate`/`LeaguePatternCount`) —
  o ligde BİLGİ 1/4 DESENİNİN TUTTUĞU (ve oynanmış) maçların dönüş oranı (koşullu).

İkisi kıyaslanınca desenin o ligde gerçekten bir şey söyleyip söylemediği görülür
(bkz. kullanıcı sorusu: "ligin genel dönüş oranıyla bunların dönüş oranı arasındaki
fark nedir" — taban vs koşullu oran, aynı BİLGİ 1/4 sayfasındaki `RateDiff` mantığı,
sadece burada HER LİG için ayrı ayrı). **Önemli:** bu 2 oran `ScanLeagueAsync(lg.Id,
null)` ile **TÜM SEZONLAR** birleşik hesaplanır — `PendingRows` (hangi maçın yakın
vadede oynanacağı) hâlâ sadece aktif sezondan gelir, ama ORANLAR aktif sezonla
sınırlı tutulursa (sezon daha birkaç hafta ilerlediyse) n neredeyse hep 0 çıkar,
anlamsız olur; bu yüzden sadece gösterilecek satırı olan ligler için (performans
için) ek bir "tüm sezonlar" taraması yapılır. `LeaguePatternCount == 0` ise (yeni
eklenen bir ligde BİLGİ deseni hiç tutmamışsa) view "veri yok" gösterir, "%0" DEĞİL
— aksi halde "0 örnek" ile "gerçekten %0 oran" karışır. Yeni bir "BİLGİ N" deseni
eklenirse aynı digest deseni (üstte, tüm ligler, 14 günlük kesim)
uygulanmalı — kullanıcının varsayılan olarak sadece bir ligi görmesi riski her
zaman geçerli.

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

## Lig Fikstür — hafta-içi (round bazlı) desen (Controllers/LigFiksturController.cs)

BİLGİ 1/4'ten kategorik olarak farklı: onlar bir takımın kendi **ardışık haftalarına**
bakar, bu ise **aynı haftanın (round'un) FARKLI maçlarına** bakar — "sırayla değil
birbiri arasında" (kullanıcının tanımı). Kalıcı tablo yok, anlık hesaplanır. Varsayılan
**tüm ligler + tüm sezonlar birleşik** taranır; `leagueId`/`seasonId` query param'larıyla
(BİLGİ 1/4'teki gibi dropdown filtreler) daraltılabilir — lig seçilince sezon dropdown'ı
o lige göre daralır (BİLGİ 1/4 ile aynı desen). Filtre boşken bile lig bazlı kırılım
tablosu zaten "hangi ligde ne kadar" sorusunu cevaplıyor; dropdown'lar tek bir lig/sezonun
DETAY tablosuna (round-round dökümüne) odaklanmak içindir.

- **Tetikleyici**: bir round'da (LeagueId+SeasonId+Week) en az bir maç tam skoru
  **2-2** bitmiş, VE aynı round'da (farklı bir maçta) zaten en az bir **sürpriz**
  (`MatchSurprise.SurpriseType != "NONE"` — dönüş veya +6 gol, ikisi ayrımsız) var.
- **İsabet ("hit") — ÇEŞİT ÖNEMLİ (kullanıcı düzeltmesi)**: sadece "≥2 sürpriz" yeterli
  DEĞİL — iki sürpriz **FARKLI ÇEŞİTTEN** olmalı. +6 gol + 2-2 varsa isabet için ayrı bir
  maçta **DÖNÜŞ** olmalı; dönüş + 2-2 varsa isabet için ayrı bir maçta **+6 gol** olmalı.
  Aynı çeşitten iki sürpriz (örn. iki ayrı +6'lı maç, hiç dönüş yokken) İSABET SAYILMAZ —
  ilk sürümde bu ayrım yoktu, gerçek bir örnekle (Almanya Bundesliga 16. hafta: 2-2 + iki
  ayrı +6'lı maç, dönüş yok) yanlış İSABET veriyordu, düzeltildi. Kod: `goalIdx`/`turnIdx`
  (HIGH_GOAL/DOUBLE_SURPRISE → goal, TURNAROUND/DOUBLE_SURPRISE → turn), isHit = ikisi de
  dolu VE tek bir ÇİFTE SÜRPRİZ maçının kendi kendini kanıtlamadığından emin ol (`!(goalIdx.
  Count==1 && turnIdx.Count==1 && goalIdx[0]==turnIdx[0])`).
- **Taban oranı**: TÜM tamamlanmış round'ların (2-2 şartı OLMADAN) kaçında zaten hem
  +6-çeşidi hem dönüş-çeşidi sürpriz (farklı maçlarda) birlikte var — bununla kıyaslanır
  (aynı disiplin: BİLGİ 1/4'teki "taban oranla doğrula" kuralı burada da geçerli).
- **Round'un "tamamlanmış" sayılması**: `FixtureSchedule`'da o round için kayıtlı
  maç sayısı, `Matches`'teki oynanmış sayıdan fazlaysa round **devam ediyor**
  sayılır — istatistiklere (taban/oran) KARIŞMAZ, bunun yerine tetikleyici zaten
  oluşmuşsa "🔮 Şu An Tetiklenmiş" canlı bölümüne düşer (o round'un henüz
  oynanmamış maçları "aday" olarak listelenir). `FixtureSchedule` verisi olmayan
  (eski/senkronize edilmemiş) sezonlarda round her zaman tamamlanmış sayılır.
- **Bulgu (çeşit-düzeltmesi sonrası, tüm ligler 2020-2026)**: taban ~%17,5 (rastgele
  bir haftada hem +6 hem dönüş çeşidi birlikte bulunma ihtimali), tetikleyici
  oluştuğunda ~%25,9 — **fark +~8,4 puan, hâlâ pozitif bir sinyal ama düzeltme
  öncesi görünenden (+~13 puan) daha mütevazı** (çeşit ayrımı yapılmayan ilk
  sürüm oranı şişiriyordu). Lig bazlı kırılımda hangi liglerin daha güçlü sinyal
  verdiği sayfadaki tabloda görünür. Yine de: **bu bulgu kalıcı doğru kabul
  edilmemeli** — zaman içinde daha fazla veri birikince tekrar kontrol edilmeli
  (BİLGİ 1/4 de başta umut verici görünüp sonra taban oranın içinde eridi).

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

## Özet — ilk açılış sayfası (Controllers/OzetController.cs)

Uygulamanın varsayılan rotası (`Program.cs`, `{controller=Ozet}`) ve navbar'daki "⚡ Ana
Sayfa" linki artık buraya işaret ediyor — kullanıcı talebiyle, eski `HomeController`
("Sürpriz/Fikstür/Puan Durumu") **kaldırılmadı**, sadece navbar'da "🎯 Sürpriz / Fikstür"
adıyla ayrı bir sekmeye taşındı. Amaç: 5 analizin (BİLGİ 1, BİLGİ 4, DOGİ, GOL BEKLENTİSİ,
LİG FİKSTÜR) önümüzdeki ~14 gün içindeki bekleyen/takip edilmesi gereken maçlarını **tek,
göz yormayan bir listede** birleştirmek — aynı maç birden fazla analizden gelirse TEK
satırda, birden fazla renkli rozetle gösterilir (`(LeagueName, HomeTeam, AwayTeam)`
anahtarıyla birleştirilir, bkz. `OzetController.GetOrAdd`). **Gol Beklentisi bu birleşik
listeye dahil DEĞİL** — kullanıcı talebiyle ayrı tutulur, bkz. aşağı.

**Kalıcı tablo yok** — `OzetController.Index()` her istek geldiğinde anlık hesaplar.
Kod tekrarını önlemek için kendi mantığını yazmak yerine üç mevcut controller'ın TÜM
hesaplamasını çağırır:
- `Bilgi1Controller.BuildAsync(context, null, null)` → `CrossLeaguePending`
- `Bilgi4Controller.BuildAsync(context, null, null)` → `CrossLeaguePending`
- `LigFiksturController.BuildAsync(context, null, null)` → `LivePending`

Bu üçü yüzünden Bilgi1Controller/Bilgi4Controller/LigFiksturController'ın eski `Index()`
gövdeleri **`internal static async Task<TViewModel> BuildAsync(AppDbContext context,
int? leagueId, int? seasonId)`** metoduna taşındı; her controller'ın kendi `Index()`
action'ı artık sadece `View(await BuildAsync(_context, leagueId, seasonId))` çağırıyor.
`internal` görünürlük aynı assembly içindeki başka bir controller'ın DI/servis kaydı
gerekmeden doğrudan çağırabilmesi için yeterli — **yeni bir sayfa başka bir controller'ın
tam hesaplamasını yeniden kullanmak isterse bu deseni örnek al** (yeni bir servis
katmanına taşımaya gerek yok, `internal static BuildAsync` yeterli).

**DOGİ için özel, salt-okunur bir çapraz-referans var**: `DogiPattern.AlertMatchId`,
`AlertResult=="PENDING"` iken hep `null` kalır çünkü `DogiService.UpdatePendingPatternsAsync`
sadece oynanmış `Matches`'e bakar, `FixtureSchedule`'a hiç bakmaz (bkz. yukarı "DogiPattern"
bölümü) — yani Dogi'nin kendi sayfası pending bir pattern için "3. maç ne zaman/kime karşı"
bilgisini gösteremez. Bu SADECE Özet sayfası için `OzetController.Index()` içinde,
`FixtureSchedule`'da o takımın (`TeamId`, tetikleyici 2. maçtan sonraki) bir sonraki
maçını arayarak çözülüyor — **`DogiService`/`DogiPattern`'ın kendi semantiği
değiştirilmedi**, bu tamamen ek/read-only bir eşleştirme.

**Gol Beklentisi için — kullanıcı talebiyle DİĞER 4 analizden AYRI tutulur**: BİLGİ 1/4,
DOGİ ve LİG FİKSTÜR aynı `byKey` sözlüğünde birleşip TEK bir listede (`vm.Matches`)
rozetlenirken, Gol Beklentisi'nin bulguları bu birleşik listeye hiç KARIŞTIRILMAZ —
kendi bağımsız `byKey`'inde toplanıp ayrı bir listeye yazılır (`vm.GolBeklentisiMatches`,
`OzetController.BuildGoalExpectationMatchesAsync`) ve view'da ayrı, görsel olarak
ayrılmış (kesikli çizgiyle bölünmüş) bir bölümde gösterilir. Sebep: Gol Beklentisi
diğerlerinden kategorik olarak farklı bir sinyal (dönüş/desen değil, gol sayısı tahmini)
— aynı listeye karışınca "hangi analiz" ayrımı bulanıklaşıyordu. `GolBeklentisiController`'ın
tam pencere/kalibrasyon hesaplaması burada TEKRARLANMAZ — sadece "combined skor üst 2
kovaya mı düşüyor" sorusu için hafif bir career/recent/h2h hesaplaması yapılır
(`OzetController.AddGoalExpectationTagsAsync`), ortak formül
`Services/GoalExpectationCalculator.cs`'den (`Bucket`/`Combine`/`H2hWeight`) gelir — bu
dosya, `GolBeklentisiController`'ın kendi private implementasyonundan bu sayfa için
ayıklandı (ikisi arasında formül sapması olmaması için `GolBeklentisiController` artık
kendi `Bucket`/`Combine` metotlarını buraya delege eden ince wrapper'lar). Görünümde her
kart için tam açıklama (`OzetTagDto.Detail`) tooltip'te, kısa gösterim
(`OzetTagDto.Short`, örn. `"7,00 · "≥ 6,2""`) kartın üzerinde — tam cümleyi karta
basmak "göz yormayacak" hedefini bozuyordu, düzeltildi.

**Seçicilik (kullanıcı düzeltmesi — "çok fazla maç çıkıyor")**: ilk sürüm üst 2 kovayı
("5,8 – 6,2" + "≥ 6,2") işaretliyordu; gerçek kalibrasyon verisiyle (`/GolBeklentisi`
sayfasının Kalibrasyon tablosu) kontrol edilince bunun **tüm maçların ~%37,5'i**
olduğu görüldü — pratikte elemiyor. Tarihsel oranlar: "5,8–6,2" kovası 6+ gol'de
sadece **%8** (taban ortalama ~%6,6'ya çok yakın, gürültü sınırında), "≥ 6,2" kovası
ise **%9,2** (en yüksek ve en tutarlı). Düzeltme: `AddGoalExpectationTagsAsync` artık
**SADECE `"≥ 6,2"` kovasını** işaretliyor (`bucket != "≥ 6,2"` ise atla) — bu tek
başına oranı ~%17'ye indiriyor. Ayrıca bir tutarsızlık bulundu ve düzeltildi:
`GolBeklentisiController`'ın kendi kalibrasyon eğrisi takım başına **en az 8 maç**
şartıyla hesaplanmış (`eligible = cH.n >= 8 && cA.n >= 8`, satır ~170), ama
`OzetController`'daki `FinalBlended`'ın "az veri" eşiği 6'ydı — yani kalibrasyonun
hiç test etmediği (6-7 maçlık) takımlar da "yüksek kova" sayılıp işaretleniyordu.
Artık `FinalBlended`'daki `low = c.n < 8` ile kalibrasyonla TUTARLI. **Gol Beklentisi
sayfasının kendi mantığı (n>=6 eşiği, üst 2 kova gösterimi vb.) DEĞİŞTİRİLMEDİ** — bu
sıkılaştırma sadece Özet'in kendi filtresi, ayrı bir "sürpriz avı" seçiciliği. Yeni bir
eşik/kova değişikliği düşünülürse önce gerçek kalibrasyon tablosundan (`/GolBeklentisi`)
sayıları doğrula — varsayımla ("muhtemelen ayırt edici" gibi) karar verme.

**Senkronizasyon kararı**: sayfa açılışında OTOMATİK senkronizasyon tetiklenmiyor (DB'yi
her sayfa yüklemesinde 15-30 saniyelik bir API taramasına zorlamamak için) — bunun yerine
sayfanın en üstünde diğer ekranlardaki gibi manuel **"🔄 Senkronize Et"** butonu var
(`POST /Sync/RefreshAllActive`, aynı endpoint). Kullanıcı "orası sana kalmış" demişti;
bu proje zaten arka planda `ActiveSeasonSyncService` ile açılıştan ~8 sn sonra bir kez
otomatik senkron yapıyor (bkz. yukarı), o yüzden Özet sayfasının kendi başına ayrıca
zorunlu bir senkron tetiklemesi gereksiz maliyet olurdu.

Görünüm (`Views/Ozet/Index.cshtml`) günlere göre gruplu, kompakt kart listesi — her
kartta saat/lig rozeti/maç adı + sağda renkli, `title` (tooltip) ile detaylı kaynak
rozetleri (`OzetTagDto.Color`: BİLGİ 1 `#7dd3fc`, BİLGİ 4 `#c4b5fd`, DOGİ `#fbbf24`,
GOL BEKLENTİSİ `#38bdf8`, LİG FİKSTÜR `#f472b6`). Yeni bir analiz eklenirse aynı deseni
izle: `OzetController.Index()`'e yeni bir `--- ANALİZ ADI ---` bloğu + `OzetTagDto` için
yeni bir renk seç + `OzetViewModel`'e yeni bir sayaç alanı ekle.

**BİLGİ 1 / BİLGİ 4 rozetlerinde "Bunların dönüş oranı"** — kullanıcı talebiyle rozetin
üzerinde (`OzetTagDto.Short`, örn. `"BİLGİ 1 · %7,9 (n=126)"`) ve tooltip'te
(`OzetTagDto.Detail`) gösterilir. Bu oran BURADA YENİDEN HESAPLANMAZ —
`Bilgi1Controller`/`Bilgi4Controller`'ın `BuildAsync`'i zaten `CrossLeaguePending`
satırlarına `LeaguePatternTurnaroundRate`/`LeaguePatternCount` olarak dolduruyor
(bkz. yukarı "BİLGİ 1 / BİLGİ 4" bölümü), `OzetController` sadece bu hazır değeri
rozete taşır. `LeaguePatternCount == 0` ise "veri yok" yazılır (Bilgi1/4 sayfasındaki
"%0 ile n=0 karışmasın" kuralı burada da geçerli). **Kasıtlı olarak DOGİ/LİG FİKSTÜR
rozetlerine ayrı bir "tutma/isabet oranı" eklenmedi** — kullanıcı sadece BİLGİ 1/4 için
zaten var olan "bunların dönüş oranı" metriğinin yeterli olduğunu belirtti; her kaynak
için ayrı bir oran hesabı icat etmeye gerek yok.

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
- **Kısa ad / uzun ad tekrarı (api-sports kaynaklı)**: aynı kulüp için api-sports
  bazen kısa ("Tottenham") bazen uzun ("Tottenham Hotspur FC") ad döndürüyor —
  `ToBaseKey`'in yakaladığı FC/SC sonek farkından farklı bir durum (kelime sayısı
  farklı). Tespit edilen örnekler: Tottenham/Tottenham Hotspur FC, Real Betis/Real
  Betis Balompié, Heracles/Heracles Almelo, AZ/AZ Alkmaar, NEC/NEC Nijmegen, Casa
  Pia/Casa Pia AC, Rayo Vallecano/Rayo Vallecano De Madrid, Real Sociedad/Real
  Sociedad De Fútbol. Somut belirti: yeni sync'lenen maç `FixtureSchedule`'daki
  (eski takım ID'li) NS kaydıyla eşleşemiyor → oynanmış maç, ilgili ekranda (örn.
  Gol Beklentisi penceresi) hâlâ "oynanmamış tahmin" olarak görünmeye devam ediyor.
  Artık `TeamNameNormalizer.LooksLikeSameClub` bu deseni tanıyıp `Admin/MergeTeams`
  otomatik listesine ekliyor (rezerv takımları — "II"/"B"/"U21" vb. — gerçekten
  farklı takım olduğu için hariç tutulur, örn. "Villarreal" ≠ "Villarreal II").
  Yeni bir lig ilk kez API'den senkronize edildiğinde bu tür çiftler tekrar
  oluşabilir — `Admin/MergeTeams` sayfası kontrol edilmeli.
- **Tarih/saat dilimi hatası (düzeltildi)**: `FootballApiService.SyncLeagueSeasonAsync`
  api-sports'un döndürdüğü offset'li tarihi (`"+01:00"` gibi) `.ToUniversalTime()`
  çağırmadan doğrudan `DateTime.SpecifyKind(...,Utc)` ile etiketliyordu — System.Text.Json
  offset sıfır değilse tarihi **sunucunun yerel saatine** çevirip `Kind=Local` veriyor;
  bu değeri sonradan "Utc" diye etiketlemek `Match.MatchDate`'i sunucunun UTC farkı kadar
  (bu sunucuda +3 saat) ileri kaydırıyordu — bazı akşam maçları ertesi güne kayıyordu.
  `FixtureScheduleService.SyncScheduleAsync` zaten doğru yapıyordu (`.ToUniversalTime()`
  ekli). Düzeltme: `FootballApiService`'e de aynı `.ToUniversalTime()` eklendi. **Not:**
  bu düzeltme sadece BUNDAN SONRAKİ sync'lerde doğru tarih üretir; düzeltme öncesi
  API'den çekilmiş eski `Match.MatchDate` değerleri hâlâ kaymış olabilir (haftayı
  değiştirmez, sadece o günün/saatinin görünümünü etkiler) — geriye dönük toplu
  düzeltme henüz yapılmadı.

## Ek veri kaynakları — Standings / TeamSeasonStats / FixturePrediction

Analiz çeşitliliğini artırmak için api-sports'un 3 ek ucu eklendi. Üçü de mevcut
"kalibre edilmiş" modelleri (BİLGİ 1/4, Gol Beklentisi) **DEĞİŞTİRMEZ** — sadece ek
bilgi/karşılaştırma katmanıdır.

### `Team.ApiTeamId` (yeni alan) — önce bunu anla
`teams/statistics` ve `predictions` uçları api-sports'un KENDİ takım id'sini ister
(bizim `Team.Id`'miz değil). Bu yüzden `FootballApiService` ve `FixtureScheduleService`
artık her maç/fikstür senkronunda `teams.home/away.id`'yi de yakalayıp `Team.ApiTeamId`'ye
yazıyor (sadece `null` ise — üzerine yazmaz). **Sonuç:** bir lig+sezon için önce en az bir
maç veya fikstür senkronu (Senkronize Et / Fikstür Takvimi) çalışmış olmalı, yoksa o ligin
takımlarının `ApiTeamId`'si boş kalır ve aşağıdaki iki özellik o takımları atlar.

### `Standing` (Services/StandingsService.cs) — lig tablosu
api-sports `standings` ucundan (1 çağrı/lig) rank/puan/form/ev-deplasman ayrı istatistik
çeker, `(LeagueId,SeasonId,TeamId)` başına TEK güncel satır tutar (geçmiş saklanmaz).
Ucuz olduğu için **`ActiveSeasonRefresher`'a dahil** — "Senkronize Et" her tıklandığında
otomatik tazelenir. `HomeController`'da seçili takımın "Sezon Özeti" paneline **Lig Sırası**
(rank + puan + renkli form dizisi) olarak yansır — `Standing` kaydı yoksa panel sessizce
gizlenir.

### `TeamSeasonStat` (Services/TeamStatisticsService.cs) — takım özet istatistikleri
api-sports `teams/statistics` ucundan (**takım başına 1 çağrı** — ligin tamamını tek
seferde vermez, standings gibi ucuz değil) ev/deplasman AYRI gol ortalaması, temiz sayfa,
penaltı, sarı/kırmızı kart toplamı çeker. Maliyeti yüzünden **Senkronize Et'e dahil değil**
— `/Sync` ekranında lig+sezon başına ayrı "📊 Takım İstatistikleri" butonu (elle tetiklenir).
`GolBeklentisiController`'da "Takım gol ortalamaları" tablosuna **API Ev / API Dep** sütunu
olarak eklenir — bizim `Blended` hesabımız (kariyer+son10, ev/deplasman ayrımı YAPMAZ) ile
karşılaştırma için; aralarında büyük fark varsa o takım için kendi ortalamamız yanıltıcı
olabilir demektir (ör. evinde çok golcü, deplasmanda kısır bir takım).

### `FixturePrediction` (Services/PredictionService.cs) — api-sports'un kendi tahmini
api-sports `predictions` ucundan (**maç başına 1 çağrı**) kazanan/beraberlik/kaybeden
yüzdesi + serbest metin tavsiye ("Double chance: draw or Team" vb.) çeker. Maliyeti
yüzünden sadece **yakın vadeli** (varsayılan ~2 hafta, `PredictionService.DefaultWeeksAhead`)
OYNANMAMIŞ maçlar için, `/Sync` ekranındaki "🔮 Tahminler" butonuyla elle çekilir; zaten
24 saatten yeni bir kaydı olan maçı tekrar çekmez (gereksiz API çağrısı önlenir).
`GolBeklentisiController`'da pencere/takım tablosuna **"API Tahmini"** sütunu olarak
eklenir (`(HomeTeamId,AwayTeamId)` ile eşleştirilir) — kendi "Gol gücü" hesabımızla
KARIŞTIRILMAMALI, sadece yan yana benchmark içindir.

### Dikkat: EF Core'un çeviremediği LINQ kalıpları (bu üçünü yazarken 2 kez karşılaşıldı)
- `query.SelectMany(x => new[] { x.A, x.B })` — array-literal projeksiyonu SQL'e çevrilemiyor
  ("could not be translated" hatası). Çözüm: `A` ve `B`'yi AYRI `Select()` sorgularıyla çekip
  `Concat()` ile client tarafında birleştir (zaten `HomeController`'da kullanılan desen).
- `_context.X.FirstOrDefaultAsync(x => bellekteki_dizi.Where(...).Select(...).Contains(x.Y))`
  — in-memory bir diziyi sorgu içinde filtreleyip `Contains` ile karşılaştırmak da çevrilemiyor.
  Çözüm: önce bellek tarafında (LINQ-to-Objects) tek değeri bul, SONRA basit bir EF sorgusu yaz.
  (`TeamStatisticsService.SyncLeagueSeasonAsync` ve `SyncController.Predictions`'ta düzeltildi.)


