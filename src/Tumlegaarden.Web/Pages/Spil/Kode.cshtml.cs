using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Spil;

/// <summary>Kodeknækker: gæt dagens kode på 4 farver i højst 8 forsøg.</summary>
public class KodeModel(AppDbContext db, UserManager<IdentityUser> userManager, MiniGames games, SiteSettings settings, TimeProvider time)
    : MiniGameModel(db, userManager, games, settings, time)
{
    protected override string Game => MiniGameKeys.Code;

    public List<(int[] Guess, int Exact, int Near)> Rows { get; private set; } = [];
    public int[]? Answer { get; private set; }
    public bool Finished => Round?.Finished == true;
    /// <summary>Sidste gæt, så formularen starter med det (nemt at ændre én farve).</summary>
    public int[] Preset { get; private set; } = [0, 0, 0, 0];

    public static int PointsFor(int attempts) => CodeBreaker.Points(attempts, solved: true);

    public async Task OnGetAsync()
    {
        ReadError();
        await PrepareAsync(tracking: false);
        var code = MiniGames.CodeFor(Today, Secret);
        foreach (var s in Round?.StateList ?? [])
        {
            var g = CodeBreaker.Decode(s);
            var (exact, near) = CodeBreaker.Score(g, code);
            Rows.Add((g, exact, near));
        }
        if (Rows.Count > 0) Preset = Rows[^1].Guess;
        if (Finished) Answer = code;
        await LoadLeaderboardAsync("Knækket", "Bedste",
            "Point: 100 for 1 forsøg, 90, 80, 70, 55, 40, 25 og 10 for 8 forsøg. Bedste = færrest forsøg.",
            list => list.Where(r => r.Solved).Select(r => r.Attempts).DefaultIfEmpty().Min() is int b and > 0 ? $"{b} forsøg" : "–",
            r => r.Solved ? $"{r.Attempts}/{CodeBreaker.MaxAttempts}" : r.Finished ? $"X/{CodeBreaker.MaxAttempts}" : "i gang");
    }

    public async Task<IActionResult> OnPostAsync(int? g0, int? g1, int? g2, int? g3)
    {
        int?[] raw = [g0, g1, g2, g3];
        if (raw.Any(x => x is null)) return Error("Vælg en farve på alle 4 pladser.");
        var guess = raw.Select(x => x!.Value).ToArray();
        if (!CodeBreaker.IsValid(guess)) return Error("Ugyldig farve.");

        await PrepareAsync(tracking: true);
        if (Round is { Finished: true }) return RedirectToPage();
        var round = await GetOrCreateRoundAsync();
        if (round is null) return Forbid();

        var code = MiniGames.CodeFor(Today, Secret);
        var encoded = CodeBreaker.Encode(guess);
        round.State = round.State.Length == 0 ? encoded : $"{round.State};{encoded}";
        round.Attempts++;
        round.Solved = CodeBreaker.Score(guess, code).Exact == CodeBreaker.Length;
        round.Finished = round.Solved || round.Attempts >= CodeBreaker.MaxAttempts;
        round.Score = CodeBreaker.Points(round.Attempts, round.Solved);
        if (round.Finished)
            TempData["Status"] = round.Solved ? $"Koden er knækket i {round.Attempts} forsøg: {round.Score} point." : "Koden holdt. Prøv igen i morgen.";
        return await SaveAndReloadAsync();
    }
}
