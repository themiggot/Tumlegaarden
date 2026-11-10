using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Game;

/// <summary>
/// Ordjagt: dagens ord på 5 bogstaver, 6 forsøg, én gang om dagen. Al logik på serveren;
/// ordet sendes aldrig til browseren, før spillet er slut.
/// </summary>
public class IndexModel(AppDbContext db, UserManager<IdentityUser> userManager, WordGame game, SiteSettings settings, TimeProvider time, ILogger<IndexModel> logger) : PageModel
{
    public DateOnly Today { get; private set; }
    public GameRound? Round { get; private set; }
    public string? Answer { get; private set; }
    public List<(string Word, LetterState[] States)> Rows { get; private set; } = [];
    public Dictionary<char, LetterState> Keys { get; private set; } = [];
    public List<LeaderRow> Leaderboard { get; private set; } = [];
    public LeaderRow? Me { get; private set; }
    public int[] Distribution { get; private set; } = new int[WordGame.MaxAttempts + 1];
    public bool CanGuess => Round is not { Finished: true };
    public string? ErrorMessage { get; private set; }
    public BoardView Board => new(Rows, Keys, CanGuess, ErrorMessage);
    public string? MyId { get; private set; }

    public sealed record LeaderRow(string UserId, string Name, int Points, int Won, int Lost, int? Best, double? AverageAttempts, string TodayText)
    {
        public int Played => Won + Lost;
        public int WinPercent => Played == 0 ? 0 : (int)Math.Round(100.0 * Won / Played);
    }

    public static int PointsFor(int attempts) => WordGame.Points(attempts, solved: true);

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostGuessAsync(string? guess)
    {
        var word = WordGame.Normalize(guess);
        if (word.Length != WordGame.WordLength) return Error($"Ordet skal have {WordGame.WordLength} bogstaver.");
        if (!game.IsValidGuess(word)) return Error($"\"{word.ToUpperInvariant()}\" står ikke i ordlisten.");

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Forbid();
        var today = DanishTime.Today(time);
        var date = today.ToDateTime(TimeOnly.MinValue);

        var round = await db.GameRounds.FirstOrDefaultAsync(r => r.UserId == user.Id && r.Date == date);
        if (round is { Finished: true }) return RedirectToPage();
        if (round?.GuessList.Contains(word) == true) return Error($"Du har allerede gættet \"{word.ToUpperInvariant()}\".");

        if (round is null)
        {
            round = new GameRound { UserId = user.Id, UserEmail = user.Email ?? "", Date = date };
            db.GameRounds.Add(round);
        }

        var answer = round.Answer ??= game.WordFor(today, await settings.GetOrCreateGameSecretAsync());
        round.Guesses = round.Guesses.Length == 0 ? word : $"{round.Guesses};{word}";
        round.Attempts++;
        round.Solved = word == answer;
        round.Finished = round.Solved || round.Attempts >= WordGame.MaxAttempts;
        round.Score = WordGame.Points(round.Attempts, round.Solved);
        round.UpdatedUtc = time.GetUtcNow().UtcDateTime;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Samme bruger gættede samtidig fra to faner – indlæs igen.
            return RedirectToPage();
        }

        if (round.Finished)
        {
            logger.LogInformation("{User} {Result} Ordjagt {Date} i {Attempts} forsøg.", User.Identity?.Name, round.Solved ? "vandt" : "tabte", today, round.Attempts);
            TempData["Status"] = round.Solved
                ? $"Flot! Fundet i {round.Attempts} forsøg: {round.Score} point."
                : $"Ikke i dag. Ordet var \"{answer.ToUpperInvariant()}\".";
        }
        return RedirectToPage();
    }

    private RedirectToPageResult Error(string message)
    {
        TempData["GameError"] = message;
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        ErrorMessage = TempData["GameError"] as string;
        Today = DanishTime.Today(time);
        var date = Today.ToDateTime(TimeOnly.MinValue);
        MyId = userManager.GetUserId(User);
        var answer = game.WordFor(Today, await settings.GetOrCreateGameSecretAsync());

        var rounds = await db.GameRounds.AsNoTracking().ToListAsync();
        Round = rounds.FirstOrDefault(r => r.UserId == MyId && r.Date == date);
        if (Round?.Answer is { Length: > 0 } stored) answer = stored;
        if (Round is not null)
        {
            Rows = Round.GuessList.Select(g => (g, WordGame.Evaluate(g, answer))).ToList();
            Keys = WordGame.KeyStates(Round.GuessList, answer);
            if (Round.Finished) Answer = answer;
        }

        // Et påbegyndt spil fra en tidligere dag, der aldrig blev afsluttet, tæller som tabt.
        bool IsLost(GameRound r) => !r.Solved && (r.Finished || r.Date < date);

        Leaderboard = rounds.GroupBy(r => r.UserId).Select(g =>
        {
            var won = g.Where(r => r.Solved).ToList();
            var todayRound = g.FirstOrDefault(r => r.Date == date);
            var todayText = todayRound switch
            {
                null => "–",
                { Solved: true } => $"{todayRound.Attempts}/{WordGame.MaxAttempts}",
                { Finished: true } => $"X/{WordGame.MaxAttempts}",
                _ => "i gang"
            };
            return new LeaderRow(g.Key, g.OrderByDescending(r => r.Date).First().UserEmail.Split('@')[0],
                g.Sum(r => r.Score), won.Count, g.Count(IsLost),
                won.Count > 0 ? won.Min(r => r.Attempts) : null,
                won.Count > 0 ? won.Average(r => r.Attempts) : null,
                todayText);
        })
        .OrderByDescending(r => r.Points).ThenByDescending(r => r.Won).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

        Me = Leaderboard.FirstOrDefault(r => r.UserId == MyId);
        foreach (var r in rounds.Where(r => r.UserId == MyId && r.Solved)) Distribution[r.Attempts]++;
    }
}
