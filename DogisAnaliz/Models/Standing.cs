namespace DogisAnaliz.Models;

/// <summary>
/// Lig tablosu — bir lig+sezon+takım için o anki sıralama, puan, form ve ev/deplasman ayrı
/// istatistikler. api-sports "standings" ucundan çekilir (<see cref="Services.StandingsService"/>),
/// tüm lig tek çağrıda döner. (LeagueId, SeasonId, TeamId) anahtarıyla upsert edilir — puan durumu
/// her hafta değiştiği için sürekli güncellenir, geçmişi tutmaz (an itibarıyla tek satır).
/// </summary>
public class Standing
{
    public int Id { get; set; }

    public int LeagueId { get; set; }
    public League League { get; set; } = null!;

    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public int Rank { get; set; }
    public int Points { get; set; }
    public int GoalsDiff { get; set; }

    /// <summary>Son maçlar, en yeniden en eskiye — örn. "WWDLL".</summary>
    public string Form { get; set; } = string.Empty;

    /// <summary>api-sports'un o sıra için yorumu, örn. "Promotion - Champions League (League phase)".</summary>
    public string? Description { get; set; }

    public int Played { get; set; }
    public int Win { get; set; }
    public int Draw { get; set; }
    public int Lose { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }

    public int HomePlayed { get; set; }
    public int HomeWin { get; set; }
    public int HomeDraw { get; set; }
    public int HomeLose { get; set; }
    public int HomeGoalsFor { get; set; }
    public int HomeGoalsAgainst { get; set; }

    public int AwayPlayed { get; set; }
    public int AwayWin { get; set; }
    public int AwayDraw { get; set; }
    public int AwayLose { get; set; }
    public int AwayGoalsFor { get; set; }
    public int AwayGoalsAgainst { get; set; }

    public DateTime UpdatedAt { get; set; }
}
