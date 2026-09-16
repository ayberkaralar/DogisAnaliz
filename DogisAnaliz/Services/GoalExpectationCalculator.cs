namespace DogisAnaliz.Services;

/// <summary>
/// Gol Beklentisi'nin ÇEKİRDEK formülü — <see cref="Controllers.GolBeklentisiController"/> ve
/// <see cref="Controllers.OzetController"/> (Ana Sayfa özeti, "+6 gol olabilir" sinyali) AYNI
/// formülü kullanır (buraya taşınmadan önce ikisi ayrı ayrı yazılırsa zamanla sapabilirdi).
/// Formülün açıklaması için bkz. CLAUDE.md "Gol Beklentisi — tek gerçek öngörü sinyali".
/// </summary>
public static class GoalExpectationCalculator
{
    public static readonly string[] BucketOrder = { "< 5,0", "5,0 – 5,4", "5,4 – 5,8", "5,8 – 6,2", "≥ 6,2" };

    /// <summary>Birleşik gol beklentisi (Combine sonucu) → kova etiketi.</summary>
    public static string Bucket(double combined) => combined switch
    {
        < 5.0 => "< 5,0",
        < 5.4 => "5,0 – 5,4",
        < 5.8 => "5,4 – 5,8",
        < 6.2 => "5,8 – 6,2",
        _     => "≥ 6,2",
    };

    /// <summary>H2H (ikili geçmiş) karşılaşma sayısı arttıkça ağırlığı artar, maks %50.</summary>
    public static double H2hWeight(int n) => Math.Min(0.5, 0.09 * n);

    /// <summary>İki takımın harmanlanmış gol ortalamaları toplamı (teamPart) ile varsa H2H
    /// ortalamasını (h2hAvg, h2hN karşılaşma sayısı) birleştirir. h2hN &lt; 3 ise H2H'ye hiç
    /// güvenilmez, sadece teamPart döner.</summary>
    public static double Combine(double teamPart, double h2hAvg, int h2hN)
    {
        if (h2hN < 3) return teamPart;
        double w = H2hWeight(h2hN);
        return w * (2.0 * h2hAvg) + (1 - w) * teamPart;
    }
}
