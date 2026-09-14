using DogisAnaliz.Models;

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

    // Rezerv/altyapı takımı işaretleri — bunlar GERÇEKTEN farklı takımlardır (örn. "Villarreal II"
    // "Villarreal"ın kendisi değil, B takımıdır), kısa-ad eşleşmesinde yanlışlıkla birleştirilmesin.
    private static readonly HashSet<string> _reserveMarkers = new()
    {
        "ii", "iii", "b", "u21", "u20", "u19", "u23", "youth", "reserves", "reserve",
        "jong" // Hollanda'da rezerv/altyapı takımı öneki (örn. "Jong Ajax" ≠ "Ajax")
    };

    private static string[] SignificantWords(string rawName) => rawName
        .ToLowerInvariant().Replace(".", "").Replace("'", "")
        .Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// api-sports bazen aynı kulüp için farklı zamanlarda/uçlarda kısa ("Tottenham") ve uzun
    /// ("Tottenham Hotspur FC") ad döndürüyor; bu ikisi <see cref="ToBaseKey"/>'in yakaladığı
    /// basit "FC/SC" sonek farkından farklı — kelime sayısı farklı. Fark sadece BAŞTA değil
    /// SONDA/ORTADA da olabilir (örn. "1899 Hoffenheim" / "Hoffenheim" — kuruluş yılı öneki).
    /// Bu yüzden kısa olanın kelime dizisinin uzun olanın İÇİNDE, aynı sırada, ARDIŞIK bir yerde
    /// (baştan/ortadan/sondan fark etmez) geçip geçmediğine bakılır. Rezerv takımı (II/B/U21 vb.)
    /// işaretleri varsa GERÇEK farklı takım kabul edilip eşleşme reddedilir (örn. "Villarreal" ≠
    /// "Villarreal II", "Jong Ajax" ≠ "Ajax"). Sadece duplicate tespiti (Admin/MergeTeams otomatik
    /// liste) için kullanılır — otomatik birleştirme yapmaz, kullanıcı yine elle onaylar. UYARI:
    /// nadiren yanlış pozitif verebilir (aynı şehrin PAYLAŞTIĞI tek kelime iki farklı kulübü
    /// eşleştirebilir — örn. "Lausanne" (FC Lausanne-Sport) ile "Stade Lausanne-Ouchy" GERÇEKTEN
    /// farklı iki İsviçre kulübü ama kelime alt-dizisi eşleşmesi bunları da öneriye sokar). Bu
    /// yüzden liste kullanıcıya sadece ÖNERİ olarak sunulur, körü körüne toplu birleştirme yapılmaz.
    /// </summary>
    public static bool LooksLikeSameClub(string nameA, string nameB)
    {
        var wa = SignificantWords(nameA);
        var wb = SignificantWords(nameB);
        if (wa.Length == 0 || wb.Length == 0 || wa.Length == wb.Length) return false;

        var shorter = wa.Length < wb.Length ? wa : wb;
        var longer = wa.Length < wb.Length ? wb : wa;
        if (shorter.Any(_reserveMarkers.Contains) || longer.Any(_reserveMarkers.Contains)) return false;

        for (int start = 0; start <= longer.Length - shorter.Length; start++)
        {
            bool match = true;
            for (int i = 0; i < shorter.Length; i++)
                if (longer[start + i] != shorter[i]) { match = false; break; }
            if (match) return true;
        }
        return false;
    }

    /// <summary>
    /// Senkronizasyon sırasında (FootballApiService/FixtureScheduleService/OpenFootballService)
    /// tam ad eşleşmesi bulunamadığında, YENİ bir <see cref="Team"/> satırı oluşturmadan ÖNCE
    /// çağrılır: var olan takımlar arasında güvenli bir eşleşme var mı diye bakar. Örn. "Ajax"
    /// senkronize edilirken DB'de zaten "AFC Ajax" varsa, ikinci bir "Ajax" satırı oluşturmak
    /// yerine mevcut "AFC Ajax" kullanılır — aksi halde her senkronizasyonda aynı duplicate takım
    /// GERİ GELİR (kullanıcı Admin/MergeTeams'ten elle birleştirse bile).
    ///
    /// <see cref="LooksLikeSameClub"/>'dan (Admin/MergeTeams'teki İNSAN ONAYLI öneri listesi)
    /// bilerek daha KATI: sadece BAŞTA veya SONDA ek/çıkarma farkını kabul eder, ORTADAKİ
    /// eşleşmeleri reddeder (örn. "Dender"↔"FCV Dender EH" burada YAKALANMAZ, sadece admin
    /// listesinde önerilir) — çünkü bu metod insan onayı OLMADAN otomatik çalışır; yanlış bir
    /// eşleşme (örn. "Lausanne"↔"Stade Lausanne-Ouchy", GERÇEKTEN 2 farklı kulüp) maçları sessizce
    /// yanlış takıma yazardı. Birden fazla eşleşme varsa (belirsiz) null döner — yeni kayıt
    /// oluşturulur, en kötü ihtimalle Admin/MergeTeams'te görünüp elle birleştirilir.
    /// </summary>
    public static Team? FindSafeFuzzyMatch(string candidateName, IEnumerable<Team> existingTeams)
    {
        var wc = SignificantWords(candidateName);
        if (wc.Length == 0 || wc.Any(_reserveMarkers.Contains)) return null;

        Team? found = null;
        foreach (var t in existingTeams)
        {
            var wt = SignificantWords(t.Name);
            if (wt.Length == 0 || wt.Length == wc.Length) continue;
            if (wt.Any(_reserveMarkers.Contains)) continue;

            var shorter = wc.Length < wt.Length ? wc : wt;
            var longer  = wc.Length < wt.Length ? wt : wc;

            bool prefixMatch = true, suffixMatch = true;
            for (int i = 0; i < shorter.Length; i++)
            {
                if (longer[i] != shorter[i]) prefixMatch = false;
                if (longer[longer.Length - shorter.Length + i] != shorter[i]) suffixMatch = false;
            }

            if (prefixMatch || suffixMatch)
            {
                if (found != null && found.Id != t.Id) return null; // belirsiz (2. eşleşme) → güvenli tarafta kal
                found = t;
            }
        }
        return found;
    }
}

