using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Spil;

/// <summary>
/// Højere eller lavere: dagens kortspil er blandet ens for alle. Gæt om næste kort er højere eller lavere;
/// ét forkert gæt, og spillet er slut. 10 point pr. rigtigt gæt. Næste kort sendes aldrig til browseren.
/// </summary>
public class KortModel(AppDbContext db, UserManager<IdentityUser> userManager, MiniGames games, SiteSettings settings, TimeProvider time)
    : MiniGameModel(db, userManager, games, settings, time)
{
    protected override string Game => MiniGameKeys.Cards;

    /// <summary>Vendte kort: første kort + ét for hvert gæt.</summary>
    public List<Card> Shown { get; private set; } = [];
    public List<bool> Results { get; private set; } = [];
    public bool Finished => Round?.Finished == true;
    public int Correct => Round?.Attempts ?? 0;
    public Card Current => Shown[^1];

    public async Task OnGetAsync()
    {
        ReadError();
        await PrepareAsync(tracking: false);
        var deck = MiniGames.DeckFor(Today, Secret);
        var guesses = Round?.StateList ?? [];
        Shown = deck.Take(guesses.Count + 1).ToList();
        for (var i = 0; i < guesses.Count; i++) Results.Add(HigherLower.IsCorrect(deck[i], deck[i + 1], guesses[i] == "h"));
        await LoadLeaderboardAsync("10+ i træk", "Længste",
            $"{HigherLower.PointsPerCard} point pr. rigtigt gæt. Samme værdi tæller som rigtigt. Es er højest. Længste = flest rigtige i træk.",
            list => list.Max(r => r.Attempts).ToString(DanishTime.Culture),
            r => r.Finished ? r.Attempts.ToString(DanishTime.Culture) : $"{r.Attempts} (i gang)");
    }

    public async Task<IActionResult> OnPostAsync(string? guess)
    {
        if (guess is not ("h" or "l")) return Error("Vælg højere eller lavere.");
        await PrepareAsync(tracking: true);
        if (Round is { Finished: true }) return RedirectToPage();
        var round = await GetOrCreateRoundAsync();
        if (round is null) return Forbid();

        var deck = MiniGames.DeckFor(Today, Secret);
        var position = round.StateList.Count;
        var right = HigherLower.IsCorrect(deck[position], deck[position + 1], guess == "h");
        round.State = round.State.Length == 0 ? guess : $"{round.State};{guess}";
        if (right) round.Attempts++;
        round.Finished = !right || round.Attempts >= HigherLower.MaxCorrect;
        round.Solved = round.Attempts >= 10;
        round.Score = round.Attempts * HigherLower.PointsPerCard;
        if (round.Finished)
            TempData["Status"] = right
                ? $"Hele bunken! {round.Attempts} rigtige: {round.Score} point."
                : $"Forkert – det var {deck[position + 1].Name}. {round.Attempts} rigtige i træk: {round.Score} point.";
        return await SaveAndReloadAsync();
    }
}
