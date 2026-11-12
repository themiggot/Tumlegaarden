using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Spil;

public static class MiniGameKeys
{
    public const string Riddle = "gaade";
    public const string Code = "kode";
    public const string Cards = "kort";
    public const string Anagram = "anagram";
}

public sealed record MiniLeaderRow(string UserId, string Name, int Points, int Played, int Won, string Best, string Today);

/// <summary>Highscore-tabellen under hvert lille spil.</summary>
public sealed record MiniLeaderboard(IReadOnlyList<MiniLeaderRow> Rows, string? MyId, string WonHeader, string BestHeader, string Note);

/// <summary>
/// Fælles for de små daglige spil: dagens række i GameResults, den hemmelige nøgle, fejlbeskeder og highscore.
/// Al logik kører på serveren; svaret sendes aldrig til browseren, før spillet er slut.
/// </summary>
public abstract class MiniGameModel(AppDbContext db, UserManager<IdentityUser> userManager, MiniGames games, SiteSettings settings, TimeProvider time)
    : PageModel
{
    protected abstract string Game { get; }

    protected AppDbContext Db => db;
    protected MiniGames Games => games;
    protected TimeProvider Time => time;

    public DateOnly Today { get; private set; }
    protected DateTime TodayDate => Today.ToDateTime(TimeOnly.MinValue);
    protected byte[] Secret { get; private set; } = [];
    public string? MyId { get; private set; }
    /// <summary>Brugerens spil i dag, eller null før første træk.</summary>
    public GameResult? Round { get; private set; }
    public string? ErrorMessage { get; private set; }
    public MiniLeaderboard? Leaderboard { get; private set; }

    public string TodayText => char.ToUpper(WordGame.DisplayDate(Today)[0]) + WordGame.DisplayDate(Today)[1..];

    /// <summary>Gør klar: dato, nøgle, bruger og dagens spil (til visning eller opdatering).</summary>
    protected async Task PrepareAsync(bool tracking)
    {
        Today = DanishTime.Today(time);
        Secret = await settings.GetOrCreateGameSecretAsync(MiniGames.SecretKey);
        MyId = userManager.GetUserId(User);
        var query = tracking ? db.GameResults : db.GameResults.AsNoTracking();
        var date = TodayDate;
        Round = await query.FirstOrDefaultAsync(r => r.Game == Game && r.UserId == MyId && r.Date == date);
    }

    /// <summary>Dagens række for brugeren; oprettes ved første træk.</summary>
    protected async Task<GameResult?> GetOrCreateRoundAsync()
    {
        if (Round is not null) return Round;
        var user = await userManager.GetUserAsync(User);
        if (user is null) return null;
        Round = new GameResult { Game = Game, UserId = user.Id, UserEmail = user.Email ?? "", Date = TodayDate };
        db.GameResults.Add(Round);
        return Round;
    }

    /// <summary>Gemmer. Ved samtidige træk fra to faner vinder det første; det andet indlæses bare igen.</summary>
    protected async Task<IActionResult> SaveAndReloadAsync()
    {
        Round!.UpdatedUtc = time.GetUtcNow().UtcDateTime;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
        }
        return RedirectToPage();
    }

    protected RedirectToPageResult Error(string message)
    {
        TempData["GameError"] = message;
        return RedirectToPage();
    }

    protected void ReadError() => ErrorMessage = TempData["GameError"] as string;

    /// <summary>Bygger highscoren for spillet.</summary>
    /// <param name="best">Teksten i "bedste"-kolonnen ud fra brugerens spil.</param>
    /// <param name="today">Teksten i "i dag"-kolonnen for dagens spil.</param>
    protected async Task LoadLeaderboardAsync(string wonHeader, string bestHeader, string note,
        Func<IReadOnlyList<GameResult>, string> best, Func<GameResult, string> today)
    {
        var date = TodayDate;
        var rounds = await db.GameResults.AsNoTracking().Where(r => r.Game == Game).ToListAsync();
        var rows = rounds.GroupBy(r => r.UserId).Select(g =>
        {
            var list = g.ToList();
            var todayRound = list.FirstOrDefault(r => r.Date == date);
            return new MiniLeaderRow(g.Key, list.OrderByDescending(r => r.Date).First().UserEmail.Split('@')[0],
                list.Sum(r => r.Score), list.Count, list.Count(r => r.Solved), best(list), todayRound is null ? "–" : today(todayRound));
        })
        .OrderByDescending(r => r.Points).ThenByDescending(r => r.Won).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();
        Leaderboard = new MiniLeaderboard(rows, MyId, wonHeader, bestHeader, note);
    }
}
