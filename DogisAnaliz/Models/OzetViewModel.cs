namespace DogisAnaliz.Models;

/// <summary>
/// ANA SAYFA ÖZETİ — uygulamanın artık ilk açılış sayfası (kullanıcı talebiyle, eski Ana Sayfa
/// "Sürpriz/Fikstür/Puan Durumu" ekranı yerine geçti — o ekran kayboldu değil, navbar'da ayrı bir
/// linke taşındı, bkz. CLAUDE.md). BEŞ analizin ("BİLGİ 1", "BİLGİ 4", "DOGİ", "GOL BEKLENTİSİ",
/// "LİG FİKSTÜR") önümüzdeki ~14 gün içindeki bekleyen/takip edilmesi gereken maçlarını TEK,
/// göz yormayan bir listede birleştirir — aynı maç birden fazla analizde çıkarsa TEK satırda,
/// birden fazla rozetle gösterilir (bkz. <see cref="OzetMatchDto.Tags"/>).
/// Kalıcı tablo yok, anlık hesaplanır; hesaplama <see cref="Controllers.OzetController"/>'da,
/// mümkün olduğunca <c>Bilgi1Controller.BuildAsync</c>/<c>Bilgi4Controller.BuildAsync</c>/
/// <c>LigFiksturController.BuildAsync</c>'i ÇAĞIRARAK (tekrar yazmadan) yapılır.
/// </summary>
public class OzetViewModel
{
    public DateTime WindowFrom { get; set; }
    public DateTime WindowTo { get; set; }

    // BİLGİ 1 / BİLGİ 4 / DOGİ / LİG FİKSTÜR burada birleşik gösterilir (aynı maç birden
    // fazla analizden gelirse tek satırda, birden fazla rozetle). GOL BEKLENTİSİ kasıtlı
    // olarak buraya KARIŞTIRILMAZ — kullanıcı talebiyle ayrı bir alanda tutulur, bkz.
    // <see cref="GolBeklentisiMatches"/> (Controllers/OzetController.cs).
    public List<OzetMatchDto> Matches { get; set; } = new();

    // GOL BEKLENTİSİ kendi ayrı listesi — yukarıdaki birleşik özete karışmaz, sayfada
    // ayrı bir bölümde gösterilir.
    public List<OzetMatchDto> GolBeklentisiMatches { get; set; } = new();

    // Küçük bir "kaynak dağılımı" özeti — kaç maç hangi analiz(ler)den geldi (KPI şeridi için)
    public int Bilgi1Count { get; set; }
    public int Bilgi4Count { get; set; }
    public int DogiCount { get; set; }
    public int GolBeklentisiCount { get; set; }
    public int LigFiksturCount { get; set; }
}

public class OzetMatchDto
{
    public string LeagueName { get; set; } = string.Empty;
    public int Week { get; set; }
    public DateTime KickoffUtc { get; set; }
    public DateTime KickoffTr => KickoffUtc.AddHours(3);
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public List<OzetTagDto> Tags { get; set; } = new();
}

public class OzetTagDto
{
    /// <summary>"BİLGİ 1" | "BİLGİ 4" | "DOGİ" | "GOL BEKLENTİSİ" | "LİG FİKSTÜR"</summary>
    public string Source { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Color { get; set; } = "#94a3b8";

    /// <summary>Kısa gösterim metni (göz yormasın diye) — sadece Gol Beklentisi ayrı bölümünde
    /// kullanılır (örn. "7,00 · ≥ 6,2"); boşsa view <see cref="Source"/>'a düşer.
    /// Tam açıklama her zaman <see cref="Detail"/>'de (tooltip).</summary>
    public string Short { get; set; } = string.Empty;
}
