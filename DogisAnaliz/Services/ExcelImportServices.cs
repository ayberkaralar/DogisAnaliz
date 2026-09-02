using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using DogisAnaliz.Data;
using DogisAnaliz.Models;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Services;

public class ExcelImportService
{
    private readonly AppDbContext _context;

    public ExcelImportService(AppDbContext context)
    {
        _context = context;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<(int Imported, bool AlreadyExists)> ImportExcelAsync(
        string filePath, string leagueName, string country, string seasonName, int startYear, int endYear)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Excel dosyası bulunamadı: {filePath}");

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var result = reader.AsDataSet(new ExcelDataSetConfiguration()
        {
            ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
        });

        var table = result.Tables[0];
        int importedCount = 0;

        // 1. Sezon Kaydı
        var season = await _context.Seasons.FirstOrDefaultAsync(s => s.SeasonName == seasonName);
        if (season == null)
        {
            season = new Season { SeasonName = seasonName, StartYear = startYear, EndYear = endYear };
            _context.Seasons.Add(season);
            await _context.SaveChangesAsync();
        }

        // 2. Lig Kaydı
        var league = await _context.Leagues.FirstOrDefaultAsync(l => l.Name == leagueName);
        if (league == null)
        {
            league = new League { Name = leagueName, Country = country };
            _context.Leagues.Add(league);
            await _context.SaveChangesAsync();
        }

        // 3. ÇELİŞKİ KONTROLÜ — Bu lig+sezon için zaten veri var mı?
        bool alreadyExists = await _context.Matches
            .AnyAsync(m => m.LeagueId == league.Id && m.SeasonId == season.Id);
        if (alreadyExists)
            return (0, true);

        // 4. Satırları İçe Aktar
        foreach (DataRow row in table.Rows)
        {
            try
            {
                string macMetni = row["MAÇ"]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(macMetni)) continue;

                // Takımları Ayır (- veya –)
                var teams = Regex.Split(macMetni, @"\s*[-–—]\s*");
                if (teams.Length < 2) continue;

                string homeName = teams[0].Trim();
                string awayName = teams[1].Trim();

                // Hafta Sayısını Al
                int week = ExtractWeekNumber(row["HAFTA"]?.ToString() ?? "");

                // Tarihi Oku
                DateTime matchDate = ParseDate(row["TARİH"]?.ToString());

                // Skorları Oku
                var (htHome, htAway) = ParseScore(row["İY"]?.ToString());
                var (ftHome, ftAway) = ParseScore(row["MS"]?.ToString());

                // Takımları Getir / Oluştur
                var homeTeam = await GetOrCreateTeamAsync(homeName);
                var awayTeam = await GetOrCreateTeamAsync(awayName);

                // Maç Nesnesi
                var match = new DogisAnaliz.Models.Match
                {
                    SeasonId = season.Id,
                    LeagueId = league.Id,
                    Week = week,
                    MatchDate = matchDate,
                    HomeTeamId = homeTeam.Id,
                    AwayTeamId = awayTeam.Id,
                    HtHomeScore = htHome,
                    HtAwayScore = htAway,
                    FtHomeScore = ftHome,
                    FtAwayScore = ftAway
                };

                // Sürpriz Hesapla
                var surprise = new MatchSurprise();
                surprise.CalculateSurprise(htHome, htAway, ftHome, ftAway);
                match.Surprise = surprise;

                _context.Matches.Add(match);
                importedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Satır Hatası]: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return (importedCount, false);
    }

    private int ExtractWeekNumber(string weekText)
    {
        var match = Regex.Match(weekText, @"\d+");
        return match.Success ? int.Parse(match.Value) : 0;
    }

    private DateTime ParseDate(string? dateText)
    {
        if (DateTime.TryParse(dateText, out DateTime parsedDate))
            return DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);

        return DateTime.UtcNow;
    }

    private (int Home, int Away) ParseScore(string? scoreText)
    {
        if (string.IsNullOrWhiteSpace(scoreText)) return (0, 0);
        var parts = Regex.Split(scoreText.Trim(), @"\s*[-–]\s*");
        if (parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int a))
        {
            return (h, a);
        }
        return (0, 0);
    }

    private async Task<Team> GetOrCreateTeamAsync(string teamName)
    {
        var team = await _context.Teams.FirstOrDefaultAsync(t => t.Name == teamName);
        if (team == null)
        {
            team = new Team { Name = teamName };
            _context.Teams.Add(team);
            await _context.SaveChangesAsync();
        }
        return team;
    }
}