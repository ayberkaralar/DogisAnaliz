namespace DogisAnaliz.Models;

public class Match
{
    public int Id { get; set; }

    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public int LeagueId { get; set; }
    public League League { get; set; } = null!;

    public int Week { get; set; }
    public DateTime MatchDate { get; set; }

    public int HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;

    public int AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;

    // Skorlar
    public int HtHomeScore { get; set; }
    public int HtAwayScore { get; set; }
    public int FtHomeScore { get; set; }
    public int FtAwayScore { get; set; }

    // 1:1 İlişkiler (Sürpriz ve Detaylar)
    public MatchSurprise? Surprise { get; set; }
    public MatchDetail? Detail { get; set; }
}