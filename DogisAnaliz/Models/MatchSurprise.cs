namespace DogisAnaliz.Models;

public class MatchSurprise
{
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;

    public string IyMsCode { get; set; } = string.Empty; // "1/2", "2/1", "X/1" vb.
    public int TotalGoals { get; set; }

    // Sürpriz Flagleri
    public bool IsTurnaround { get; set; }     // Sürpriz 1: 1/2 veya 2/1 mi?
    public bool IsHighGoal { get; set; }       // Sürpriz 2: +6 Gol mü? (TotalGoals >= 6)
    public bool IsDoubleSurprise { get; set; } // Çifte Sürpriz: Hem Dönüş HE DE +6 Gol
    public string SurpriseType { get; set; } = "NONE"; // "NONE", "TURNAROUND", "HIGH_GOAL", "DOUBLE_SURPRISE"

    // Sürpriz Durumunu Otomatik Hesaplayan Metot
    public void CalculateSurprise(int htHome, int htAway, int ftHome, int ftAway)
    {
        char iy = htHome > htAway ? '1' : (htHome < htAway ? '2' : 'X');
        char ms = ftHome > ftAway ? '1' : (ftHome < ftAway ? '2' : 'X');

        IyMsCode = $"{iy}/{ms}";
        TotalGoals = ftHome + ftAway;

        IsTurnaround = (IyMsCode == "1/2" || IyMsCode == "2/1");
        IsHighGoal = (TotalGoals >= 6);
        IsDoubleSurprise = (IsTurnaround && IsHighGoal);

        if (IsDoubleSurprise) SurpriseType = "DOUBLE_SURPRISE";
        else if (IsTurnaround) SurpriseType = "TURNAROUND";
        else if (IsHighGoal) SurpriseType = "HIGH_GOAL";
        else SurpriseType = "NONE";
    }
}