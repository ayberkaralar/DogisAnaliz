namespace DogisAnaliz.Models;

/// <summary>
/// GOL BEKLENTİSİ — iki takımın maçlarının tarihsel gol üretimine dayalı, çok gollü maç (+6 / toplam≥5)
/// tahmini. Skor = ev takımının + deplasman takımının "harmanlanmış gol ortalaması" (kariyer + son 10 maç)
/// + varsa H2H (birbirleriyle geçmiş maçları) harmanı. Yürüyen (lookahead'siz) kalibrasyon: her lig için
/// bu birleşik skorun kovalara göre tarihsel +6 / +5 oranı.
/// <see cref="WindowFixtures"/>: seçilen tarihin haftası + bir sonraki hafta — oynanmışsa gerçek skorla,
/// oynanmamışsa tahminle listelenir (tutarlılığı gözle takip edebilmek için).
/// </summary>
public class GolBeklentisiViewModel
{
    public List<GbLeagueDto> Leagues { get; set; } = new();
    public List<Season> Seasons { get; set; } = new();
    public int? SelectedLeagueId { get; set; }
    public int? SelectedSeasonId { get; set; }
    public string SelectedLeagueName { get; set; } = string.Empty;
    public string SelectedSeasonName { get; set; } = string.Empty;

    /// <summary>Kullanıcının girdiği tarih (yoksa bugün varsayılır).</summary>
    public DateTime? SelectedDate { get; set; }
    public int WindowWeekStart { get; set; }
    public int WindowWeekEnd { get; set; }
    public DateTime? WindowFrom { get; set; }
    public DateTime? WindowTo { get; set; }

    public double BasePlus6 { get; set; }   // ligin geneli +6 oranı %
    public double BasePlus5 { get; set; }   // ligin geneli toplam≥5 oranı %
    public double BaseAvgGoals { get; set; }

    public List<GbBucketDto> Buckets { get; set; } = new();

    /// <summary>Seçili tarih penceresindeki (o hafta + sonraki hafta) maçlar — oynanmış + oynanmamış birlikte.</summary>
    public List<GbFixtureDto> WindowFixtures { get; set; } = new();

    public List<GbTeamDto> Teams { get; set; } = new();
}

public class GbLeagueDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int MatchCount { get; set; }
}

public class GbBucketDto
{
    public string Label { get; set; } = string.Empty;   // "≥ 6,2" vb.
    public int N { get; set; }
    public double Plus6Rate { get; set; }
    public double Plus5Rate { get; set; }
    public double AvgGoals { get; set; }
}

public class GbTeamDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CareerAvg { get; set; }   // tüm maçlarındaki maç başı toplam gol
    public double RecentAvg { get; set; }   // son 10 maç
    public double Blended { get; set; }     // 0.5*kariyer + 0.5*son10
    public int Played { get; set; }
    public bool LowData { get; set; }
}

public class GbFixtureDto
{
    public int Week { get; set; }
    public DateTime KickoffUtc { get; set; }
    public DateTime KickoffTr => KickoffUtc.AddHours(3);
    public string Home { get; set; } = string.Empty;
    public string Away { get; set; } = string.Empty;

    /// <summary>Maç oynandı mı? Oynandıysa aşağıdaki tahmin alanları "o anki" (maç öncesi) veriyle hesaplanmıştır.</summary>
    public bool IsPlayed { get; set; }
    public int? FtHome { get; set; }
    public int? FtAway { get; set; }
    public int? ActualTotal => (FtHome.HasValue && FtAway.HasValue) ? FtHome + FtAway : null;
    public string ScoreText => IsPlayed && FtHome.HasValue && FtAway.HasValue ? $"{FtHome}-{FtAway}" : "–";
    public bool? HitPlus6 => IsPlayed ? ActualTotal >= 6 : null;
    public bool? HitPlus5 => IsPlayed ? ActualTotal >= 5 : null;

    public double HomeRate { get; set; }
    public double AwayRate { get; set; }
    public double TeamPart { get; set; }    // ev + dep harman (H2H'siz)
    public int H2hCount { get; set; }       // bu iki takımın geçmiş karşılaşma sayısı (DB'de)
    public double H2hAvg { get; set; }      // o karşılaşmalardaki maç başı toplam gol
    public double Combined { get; set; }    // H2H harmanı uygulanmış nihai skor (gol gücü)
    public string BucketLabel { get; set; } = string.Empty;
    public double Plus6Rate { get; set; }
    public double Plus5Rate { get; set; }
    public bool LowData { get; set; }
}
