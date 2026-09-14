namespace DogisAnaliz.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>api-sports'un kendi takım kimliği (fixtures cevabındaki teams.home/away.id).
    /// teams/statistics, injuries, coachs gibi "takım id'si isteyen" uçları çağırabilmek için
    /// gerekli — null ise bu takım henüz hiçbir fixture senkronundan geçmemiş demektir.</summary>
    public int? ApiTeamId { get; set; }
}