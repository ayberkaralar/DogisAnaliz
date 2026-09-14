namespace DogisAnaliz.Models;

/// <summary>
/// api-sports'un KENDİ tahmin modeli (win/draw/loss yüzdeleri, tavsiye metni) — bir maç için
/// çekilir (<see cref="Services.PredictionService"/>). Bizim modellerimizi (BİLGİ 1/4, Gol
/// Beklentisi) DEĞİŞTİRMEZ — sadece yan yana koyup "bizim tahminimiz piyasa/başka bir modelle
/// ne kadar örtüşüyor" diye karşılaştırma/benchmark amaçlıdır. <see cref="ApiFixtureId"/>
/// (aynı <see cref="FixtureSchedule.ApiFixtureId"/>) anahtarıyla upsert edilir.
/// Maliyeti fixture başına 1 çağrı olduğundan sadece YAKIN vadeli (örn. gelecek 2 hafta)
/// oynanmamış maçlar için çekilir — sezonun tamamı için çekmek API limitini zorlar.
/// </summary>
public class FixturePrediction
{
    public int Id { get; set; }

    public int ApiFixtureId { get; set; }

    public int LeagueId { get; set; }
    public League League { get; set; } = null!;

    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public int HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;

    public int AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;

    public int? WinnerTeamId { get; set; }
    public string? WinnerComment { get; set; }
    public bool WinOrDraw { get; set; }

    /// <summary>api-sports ham metni, örn. "-3.5" (alt/üst tavsiyesi) — sayısal değil, olduğu gibi saklanır.</summary>
    public string? UnderOver { get; set; }
    public string? GoalsHome { get; set; }
    public string? GoalsAway { get; set; }

    /// <summary>api-sports'un serbest metin tavsiyesi, örn. "Double chance : draw or Team".</summary>
    public string? Advice { get; set; }

    public double PercentHome { get; set; }
    public double PercentDraw { get; set; }
    public double PercentAway { get; set; }

    public DateTime UpdatedAt { get; set; }
}
