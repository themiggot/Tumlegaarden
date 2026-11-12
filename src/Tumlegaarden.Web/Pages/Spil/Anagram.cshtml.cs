using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Spil;

/// <summary>Anagram: find så mange ord som muligt af dagens 7 bogstaver. Spillet er åbent hele dagen.</summary>
public class AnagramModel(AppDbContext db, UserManager<IdentityUser> userManager, MiniGames games, SiteSettings settings, TimeProvider time)
    : MiniGameModel(db, userManager, games, settings, time)
{
    protected override string Game => MiniGameKeys.Anagram;

    public char[] Letters { get; private set; } = [];
    public List<string> Found { get; private set; } = [];
    public int Possible { get; private set; }
    public int MaxPoints { get; private set; }
    public int Points => Round?.Score ?? 0;
    public bool AllFound => Round?.Finished == true;

    public async Task OnGetAsync()
    {
        ReadError();
        await PrepareAsync(tracking: false);
        var key = Games.AnagramKeyFor(Today, Secret);
        Letters = MiniGames.AnagramLetters(key, Today, Secret);
        var words = Games.AnagramWords(key);
        Possible = words.Count;
        MaxPoints = words.Sum(Anagram.Points);
        Found = (Round?.StateList ?? []).OrderBy(w => w.Length).ThenBy(w => w, StringComparer.Ordinal).ToList();
        await LoadLeaderboardAsync("Med 7 bogstaver", "Bedste dag",
            "Point: 3 bogstaver 1, 4 bogstaver 2, 5 bogstaver 3, 6 bogstaver 5 og et ord med alle 7 bogstaver 15. Bedste dag = flest point på én dag.",
            list => list.Max(r => r.Score).ToString(DanishTime.Culture),
            r => $"{r.Score} ({r.Attempts} ord)");
    }

    public async Task<IActionResult> OnPostAsync(string? word)
    {
        var w = WordGame.Normalize(word);
        if (w.Length < Anagram.MinLength) return Error($"Ord skal have mindst {Anagram.MinLength} bogstaver.");
        await PrepareAsync(tracking: true);
        var key = Games.AnagramKeyFor(Today, Secret);
        var upper = w.ToUpperInvariant();
        if (!Anagram.CanMake(w, key)) return Error($"\"{upper}\" kan ikke laves af dagens bogstaver.");
        if (!Games.IsAnagramWord(w)) return Error($"\"{upper}\" står ikke i ordlisten.");
        if (Round?.StateList.Contains(w) == true) return Error($"Du har allerede fundet \"{upper}\".");

        var round = await GetOrCreateRoundAsync();
        if (round is null) return Forbid();
        var points = Anagram.Points(w);
        round.State = round.State.Length == 0 ? w : $"{round.State};{w}";
        round.Attempts++;
        round.Score += points;
        if (w.Length == Anagram.Letters) round.Solved = true;
        round.Finished = round.Attempts >= Games.AnagramWords(key).Count;
        TempData["Status"] = w.Length == Anagram.Letters
            ? $"Alle {Anagram.Letters} bogstaver! \"{upper}\" giver {points} point."
            : $"\"{upper}\" giver {points} point.";
        return await SaveAndReloadAsync();
    }
}
