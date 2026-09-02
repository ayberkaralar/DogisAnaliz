using DogisAnaliz.Data;
using DogisAnaliz.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Controllers;

public class ImportController : Controller
{
    private readonly ExcelImportService _importService;
    private readonly AppDbContext _context;

    public ImportController(ExcelImportService importService, AppDbContext context)
    {
        _importService = importService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.Leagues = await _context.Leagues.AsNoTracking().OrderBy(l => l.Name).ToListAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Upload(
        IFormFile excelFile,
        string leagueName,
        string country,
        string seasonName,
        int startYear,
        int endYear,
        bool forceOverwrite = false)
    {
        ViewBag.Leagues = await _context.Leagues.AsNoTracking().OrderBy(l => l.Name).ToListAsync();

        if (excelFile == null || excelFile.Length == 0)
        {
            ViewBag.Error = "Lütfen bir Excel dosyası seçin.";
            return View("Index");
        }

        if (string.IsNullOrWhiteSpace(leagueName) || string.IsNullOrWhiteSpace(seasonName))
        {
            ViewBag.Error = "Lig adı ve sezon adı zorunludur.";
            return View("Index");
        }

        // Geçici dosyaya kaydet
        var tempPath = Path.Combine(Path.GetTempPath(), $"import_{Guid.NewGuid()}.xlsx");
        try
        {
            await using (var stream = System.IO.File.Create(tempPath))
                await excelFile.CopyToAsync(stream);

            var (imported, alreadyExists) = await _importService.ImportExcelAsync(
                tempPath, leagueName, country, seasonName, startYear, endYear);

            if (alreadyExists && !forceOverwrite)
            {
                // Veri zaten var — kullanıcıya sor
                ViewBag.Warning = $"'{leagueName} — {seasonName}' için veritabanında zaten veri mevcut!";
                ViewBag.FormData = new
                {
                    leagueName, country, seasonName, startYear, endYear,
                    fileName = excelFile.FileName
                };
                // Dosyayı session'da saklamak yerine yeniden yükleme yapacağız
                TempData["TempFilePath"] = tempPath;
                TempData["LeagueName"]   = leagueName;
                TempData["Country"]      = country;
                TempData["SeasonName"]   = seasonName;
                TempData["StartYear"]    = startYear;
                TempData["EndYear"]      = endYear;
                return View("Index");
            }

            if (alreadyExists && forceOverwrite)
            {
                // Mevcut maçları sil ve yeniden import et
                var leagueEntity = await _context.Leagues.FirstOrDefaultAsync(l => l.Name == leagueName);
                var seasonEntity  = await _context.Seasons.FirstOrDefaultAsync(s => s.SeasonName == seasonName);
                if (leagueEntity != null && seasonEntity != null)
                {
                    var existing = _context.Matches
                        .Where(m => m.LeagueId == leagueEntity.Id && m.SeasonId == seasonEntity.Id);
                    _context.Matches.RemoveRange(existing);
                    await _context.SaveChangesAsync();
                }
                // Yeniden import
                (imported, _) = await _importService.ImportExcelAsync(
                    tempPath, leagueName, country, seasonName, startYear, endYear);
            }

            ViewBag.Success = $"{imported} maç başarıyla '{leagueName} — {seasonName}' için aktarıldı.";
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"İçe aktarma hatası: {ex.Message}";
        }
        finally
        {
            if (System.IO.File.Exists(tempPath) && TempData["TempFilePath"] == null)
                System.IO.File.Delete(tempPath);
        }

        return View("Index");
    }

    [HttpPost]
    public async Task<IActionResult> ForceUpload()
    {
        ViewBag.Leagues = await _context.Leagues.AsNoTracking().OrderBy(l => l.Name).ToListAsync();

        var tempPath    = TempData["TempFilePath"]?.ToString();
        var leagueName  = TempData["LeagueName"]?.ToString() ?? "";
        var country     = TempData["Country"]?.ToString() ?? "";
        var seasonName  = TempData["SeasonName"]?.ToString() ?? "";
        int startYear   = TempData["StartYear"] is int sy ? sy : 2025;
        int endYear     = TempData["EndYear"]   is int ey ? ey : 2026;

        if (string.IsNullOrEmpty(tempPath) || !System.IO.File.Exists(tempPath))
        {
            ViewBag.Error = "Oturum sona erdi. Lütfen dosyayı tekrar yükleyin.";
            return View("Index");
        }

        try
        {
            // Mevcut maçları sil
            var leagueEntity = await _context.Leagues.FirstOrDefaultAsync(l => l.Name == leagueName);
            var seasonEntity  = await _context.Seasons.FirstOrDefaultAsync(s => s.SeasonName == seasonName);
            if (leagueEntity != null && seasonEntity != null)
            {
                var existing = _context.Matches
                    .Where(m => m.LeagueId == leagueEntity.Id && m.SeasonId == seasonEntity.Id);
                _context.Matches.RemoveRange(existing);
                await _context.SaveChangesAsync();
            }

            var (imported, _) = await _importService.ImportExcelAsync(
                tempPath, leagueName, country, seasonName, startYear, endYear);

            ViewBag.Success = $"{imported} maç '{leagueName} — {seasonName}' üzerine yazılarak aktarıldı.";
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Üzerine yazma hatası: {ex.Message}";
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }

        return View("Index");
    }
}
