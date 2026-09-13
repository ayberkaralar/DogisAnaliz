namespace DogisAnaliz.Models;

/// <summary>
/// Bir lig+sezonun TÜM maç takvimi — oynanmamış (NS) maçlar dahil.
/// "Kim kiminle, hangi hafta, ne zaman" bilgisini tutar. Skor/sürpriz hesabı burada YOK;
/// oynanmış maçın asıl kaynağı <see cref="Match"/> tablosudur. Bu tablo ileriye dönük
/// fikstür (tahminleme girdisi) içindir. api-sports fixtures ucundan çekilir ve
/// <see cref="ApiFixtureId"/> anahtarıyla upsert edilir (postpone/erteleme güncellenir).
/// </summary>
public class FixtureSchedule
{
    public int Id { get; set; }

    /// <summary>api-sports fixture.id — upsert (yeniden çekimde eşleştirme) anahtarı.</summary>
    public int ApiFixtureId { get; set; }

    public int LeagueId { get; set; }
    public League League { get; set; } = null!;

    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public int Week { get; set; }

    /// <summary>Ham tur metni, örn. "Regular Season - 4".</summary>
    public string Round { get; set; } = string.Empty;

    /// <summary>Başlama saati (UTC).</summary>
    public DateTime KickoffUtc { get; set; }

    public int HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;

    public int AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;

    /// <summary>api-sports status.short: NS, FT, PST, CANC, 1H, HT, 2H, LIVE vb.</summary>
    public string Status { get; set; } = "NS";

    /// <summary>Maç oynandıysa MS skoru; oynanmadıysa null. Kolaylık alanı — asıl kaynak <see cref="Match"/>.</summary>
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }

    /// <summary>Bu kaydın en son ne zaman senkronlandığı (UTC).</summary>
    public DateTime UpdatedAt { get; set; }
}
