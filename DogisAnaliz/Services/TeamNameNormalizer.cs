namespace DogisAnaliz.Services;

/// <summary>
/// Tüm servisler bu helper'ı kullanır — takım adlarını normalleştirir
/// ve case-insensitive duplicate takım oluşumunu önler.
/// </summary>
public static class TeamNameNormalizer
{
    // Karşılaştırma için çıkarılacak yaygın ekler (sadece ToBaseKey için)
    private static readonly string[] _strippableSuffixes =
    {
        " FC", " AFC", " F.C.", " A.F.C.", " SC", " CF"
    };

    /// <summary>
    /// Saklama için normalize edilmiş ad:
    /// trim + her kelimenin baş harfi büyük (Title Case)
    /// Örnek: "west ham" → "West Ham", "CHELSEA FC" → "Chelsea Fc" değil → "Chelsea FC"
    /// </summary>
    public static string Normalize(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return rawName?.Trim() ?? "";

        // Trim + birden fazla boşluğu teke indir
        var name = System.Text.RegularExpressions.Regex.Replace(rawName.Trim(), @"\s+", " ");

        // Her kelimeyi capitalize et
        var words = name.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length == 0) continue;
            // Tamamı büyük harfli kısaltmaları koru (FC, AFC, SC, vb.)
            if (words[i] == words[i].ToUpperInvariant() && words[i].Length <= 4)
                continue;
            words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant();
        }
        return string.Join(' ', words);
    }

    /// <summary>
    /// Arama anahtarı için normalize (lowercase):
    /// "West Ham" ve "west ham" aynı anahtarı üretir.
    /// </summary>
    public static string ToKey(string rawName) => Normalize(rawName).ToLowerInvariant();

    /// <summary>
    /// "Arsenal" ile "Arsenal FC" eşleşebilsin diye ek çıkarılmış anahtar.
    /// Sadece duplicate tespit/merge için kullanılır.
    /// </summary>
    public static string ToBaseKey(string rawName)
    {
        var key = ToKey(rawName);
        foreach (var suffix in _strippableSuffixes)
        {
            var s = suffix.ToLowerInvariant();
            if (key.EndsWith(s))
                return key[..^s.Length].TrimEnd();
        }
        return key;
    }
}

