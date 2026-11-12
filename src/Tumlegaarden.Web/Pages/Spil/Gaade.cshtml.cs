using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Spil;

/// <summary>Dagens gåde: find det manglende ord i et ordsprog. 3 forsøg, hjælp efter hvert forkert gæt.</summary>
public class GaadeModel(AppDbContext db, UserManager<IdentityUser> userManager, MiniGames games, SiteSettings settings, TimeProvider time)
    : MiniGameModel(db, userManager, games, settings, time)
{
    protected override string Game => MiniGameKeys.Riddle;

    public Riddle Riddle { get; private set; } = null!;
    public List<string> Guesses { get; private set; } = [];
    public int Wrong => Guesses.Count - (Round?.Solved == true ? 1 : 0);
    public bool Finished => Round?.Finished == true;

    public async Task OnGetAsync()
    {
        ReadError();
        await PrepareAsync(tracking: false);
        Riddle = Games.RiddleFor(Today, Secret);
        Guesses = Round?.StateList ?? [];
        await LoadLeaderboardAsync("Løst", "Bedste",
            "Point: 100 for 1 forsøg, 60 for 2, 30 for 3. Bedste = færrest forsøg i en løst gåde.",
            list => list.Where(r => r.Solved).Select(r => r.Attempts).DefaultIfEmpty().Min() is int b and > 0 ? $"{b} forsøg" : "–",
            r => r.Solved ? $"{r.Attempts}/{Riddle.MaxAttempts}" : r.Finished ? $"X/{Riddle.MaxAttempts}" : "i gang");
    }

    public async Task<IActionResult> OnPostAsync(string? guess)
    {
        var word = Riddle.Normalize(guess);
        if (word.Length is 0 or > 30) return Error("Skriv ét ord.");
        await PrepareAsync(tracking: true);
        if (Round is { Finished: true }) return RedirectToPage();
        if (Round?.StateList.Contains(word) == true) return Error($"Du har allerede gættet \"{word}\".");

        var riddle = Games.RiddleFor(Today, Secret);
        var round = await GetOrCreateRoundAsync();
        if (round is null) return Forbid();
        round.State = round.State.Length == 0 ? word : $"{round.State};{word}";
        round.Attempts++;
        round.Solved = riddle.IsCorrect(word);
        round.Finished = round.Solved || round.Attempts >= Riddle.MaxAttempts;
        round.Score = Riddle.Points(round.Attempts, round.Solved);
        if (round.Finished)
            TempData["Status"] = round.Solved ? $"Rigtigt! {round.Score} point." : $"Desværre. Ordet var \"{riddle.Answer}\".";
        return await SaveAndReloadAsync();
    }
}
