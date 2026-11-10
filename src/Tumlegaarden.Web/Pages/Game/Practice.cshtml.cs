using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Game;

/// <summary>
/// Øvespil i Ordjagt: ubegrænset antal spil med tilfældige ord, tæller ikke med i highscoren.
/// Spillets tilstand ligger i en krypteret cookie (ingen database), så ordet hverken kan læses eller ændres.
/// Dagens ord bruges aldrig som øveord.
/// </summary>
public class PracticeModel(WordGame game, SiteSettings settings, IDataProtectionProvider dataProtection, UserManager<IdentityUser> userManager, TimeProvider time) : PageModel
{
    public const string CookieName = "__Host-Tumle-Ordjagt-Oev";

    private sealed class State
    {
        public string U { get; set; } = "";          // bruger (cookien virker kun for den, der fik den)
        public string W { get; set; } = "";          // ordet
        public List<string> G { get; set; } = [];    // gæt
        public bool S { get; set; }                  // løst
        public bool F { get; set; }                  // færdig
        public int Played { get; set; }
        public int Won { get; set; }
    }

    private IDataProtector Protector => dataProtection.CreateProtector("Tumlegaarden.Ordjagt.Practice.v1");

    public List<(string Word, LetterState[] States)> Rows { get; private set; } = [];
    public Dictionary<char, LetterState> Keys { get; private set; } = [];
    public bool Finished { get; private set; }
    public bool Solved { get; private set; }
    public string? Answer { get; private set; }
    public int Attempts { get; private set; }
    public int Played { get; private set; }
    public int Won { get; private set; }
    public string? Error { get; private set; }
    public BoardView Board => new(Rows, Keys, !Finished, Error);

    public async Task OnGetAsync()
    {
        Error = TempData["GameError"] as string;
        var state = Load() ?? await NewGameAsync(null);
        Show(state);
    }

    public async Task<IActionResult> OnPostGuessAsync(string? guess)
    {
        var state = Load() ?? await NewGameAsync(null);
        var word = WordGame.Normalize(guess);
        if (state.F) return RedirectToPage();
        if (word.Length != WordGame.WordLength) return Error_($"Ordet skal have {WordGame.WordLength} bogstaver.");
        if (!game.IsValidGuess(word)) return Error_($"\"{word.ToUpperInvariant()}\" står ikke i ordlisten.");
        if (state.G.Contains(word)) return Error_($"Du har allerede gættet \"{word.ToUpperInvariant()}\".");

        state.G.Add(word);
        state.S = word == state.W;
        state.F = state.S || state.G.Count >= WordGame.MaxAttempts;
        if (state.F)
        {
            state.Played++;
            if (state.S) state.Won++;
            TempData["Status"] = state.S
                ? $"Flot! Fundet i {state.G.Count} forsøg. (Øvespil giver ingen point.)"
                : $"Ikke denne gang. Ordet var \"{state.W.ToUpperInvariant()}\".";
        }
        Save(state);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostNewAsync()
    {
        await NewGameAsync(Load());
        return RedirectToPage();
    }

    private RedirectToPageResult Error_(string message)
    {
        TempData["GameError"] = message;
        return RedirectToPage();
    }

    private async Task<State> NewGameAsync(State? previous)
    {
        var today = DanishTime.Today(time);
        var daily = game.WordFor(today, await settings.GetOrCreateGameSecretAsync());
        var state = new State
        {
            U = userManager.GetUserId(User) ?? "",
            W = game.RandomWord(daily, previous?.W),
            Played = previous?.Played ?? 0,
            Won = previous?.Won ?? 0
        };
        Save(state);
        return state;
    }

    private State? Load()
    {
        if (!Request.Cookies.TryGetValue(CookieName, out var raw)) return null;
        try
        {
            var state = JsonSerializer.Deserialize<State>(Protector.Unprotect(raw));
            return state is not null && state.U == userManager.GetUserId(User) && game.IsValidGuess(state.W) ? state : null;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }

    private void Save(State state) =>
        Response.Cookies.Append(CookieName, Protector.Protect(JsonSerializer.Serialize(state)), new CookieOptions
        {
            Path = "/",
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true
        });

    private void Show(State state)
    {
        Rows = state.G.Select(g => (g, WordGame.Evaluate(g, state.W))).ToList();
        Keys = WordGame.KeyStates(state.G, state.W);
        Finished = state.F;
        Solved = state.S;
        Attempts = state.G.Count;
        Answer = state.F ? state.W : null;
        Played = state.Played;
        Won = state.Won;
    }
}
