using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using QuizEntity = Tumlegaarden.Web.Data.Quiz;

namespace Tumlegaarden.Web.Pages.Quiz;

/// <summary>
/// Spillebrættet. Værten vender et kort (spørgsmålet "bagved" vises), viser svaret og giver point:
/// rigtigt giver kortets point og lukker kortet; forkert trækker point fra, så et andet hold kan prøve.
/// Andre kan se brættet og stillingen, men ikke spørgsmål og svar. Virker uden JavaScript.
/// </summary>
public class PlayModel(AppDbContext db, UserManager<IdentityUser> userManager, TimeProvider time) : PageModel
{
    public QuizGame Game { get; private set; } = null!;
    public QuizEntity Quiz { get; private set; } = null!;
    public bool IsHost { get; private set; }
    public int[] Points { get; private set; } = [];
    public HashSet<string> Used { get; private set; } = [];
    /// <summary>Det åbne kort (kun for værten).</summary>
    public QuizQuestion? Open { get; private set; }
    public bool ShowAnswer { get; private set; }
    public List<(string Team, int Score)> Standings { get; private set; } = [];

    public string CategoryName(int c) => Quiz.Categories.FirstOrDefault(x => x.Column == c)?.Name is { Length: > 0 } n ? n : $"Kategori {c + 1}";
    public QuizQuestion? Cell(int c, int r) => Quiz.Questions.FirstOrDefault(q => q.Column == c && q.Row == r && q.Question.Length > 0);

    public async Task<IActionResult> OnGetAsync(int id, int? c, int? r, bool svar = false)
    {
        if (!await LoadAsync(id, tracking: false)) return NotFound();
        if (IsHost && !Game.Finished && c is int col && r is int row && Cell(col, row) is { } q && !Used.Contains(QuizGame.Cell(col, row)))
        {
            Open = q;
            ShowAnswer = svar;
        }
        return Page();
    }

    /// <summary>Holdet svarede rigtigt (+point, kortet lukkes) eller forkert (−point, kortet forbliver åbent).</summary>
    public async Task<IActionResult> OnPostAwardAsync(int id, int c, int r, int team, bool correct)
    {
        if (!await LoadAsync(id, tracking: true)) return NotFound();
        if (!IsHost) return Forbid();
        var cell = QuizGame.Cell(c, r);
        if (Game.Finished || Cell(c, r) is null || Used.Contains(cell) || team < 0 || team >= Game.TeamList.Count) return RedirectToPage(new { id });

        var scores = Game.ScoreList;
        while (scores.Count < Game.TeamList.Count) scores.Add(0);
        scores[team] += correct ? Points[r] : -Points[r];
        Game.Scores = string.Join(',', scores);
        if (correct)
        {
            Close(cell);
            TempData["Status"] = $"{Game.TeamList[team]} får {Points[r]} point.";
            await SaveAsync();
            return RedirectToPage(new { id });
        }
        TempData["Status"] = $"{Game.TeamList[team]} mister {Points[r]} point. Et andet hold kan prøve.";
        await SaveAsync();
        return RedirectToPage(new { id, c, r });
    }

    /// <summary>Ingen svarede rigtigt: luk kortet uden point.</summary>
    public async Task<IActionResult> OnPostSkipAsync(int id, int c, int r)
    {
        if (!await LoadAsync(id, tracking: true)) return NotFound();
        if (!IsHost) return Forbid();
        if (!Game.Finished && Cell(c, r) is not null)
        {
            Close(QuizGame.Cell(c, r));
            await SaveAsync();
        }
        return RedirectToPage(new { id });
    }

    /// <summary>Ret et holds point manuelt (fx efter en fejl), i trin af laveste række.</summary>
    public async Task<IActionResult> OnPostAdjustAsync(int id, int team, bool up)
    {
        if (!await LoadAsync(id, tracking: true)) return NotFound();
        if (!IsHost) return Forbid();
        if (team < 0 || team >= Game.TeamList.Count) return RedirectToPage(new { id });
        var scores = Game.ScoreList;
        while (scores.Count < Game.TeamList.Count) scores.Add(0);
        scores[team] += up ? Points.Min() : -Points.Min();
        Game.Scores = string.Join(',', scores);
        await SaveAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostFinishAsync(int id)
    {
        if (!await LoadAsync(id, tracking: true)) return NotFound();
        if (!IsHost) return Forbid();
        Game.Finished = true;
        await SaveAsync();
        return RedirectToPage(new { id });
    }

    private void Close(string cell)
    {
        Used.Add(cell);
        Game.Used = string.Join(';', Used);
        var cards = Quiz.Questions.Count(q => q.Column < Quiz.Columns && q.Row < Quiz.Rows && q.Question.Length > 0);
        if (Used.Count >= cards) Game.Finished = true;
    }

    private async Task SaveAsync()
    {
        Game.UpdatedUtc = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
    }

    private async Task<bool> LoadAsync(int id, bool tracking)
    {
        var query = db.QuizGames.Include(g => g.Quiz!).ThenInclude(q => q.Categories).Include(g => g.Quiz!).ThenInclude(q => q.Questions).Where(g => g.Id == id);
        var game = await (tracking ? query : query.AsNoTracking()).FirstOrDefaultAsync();
        if (game?.Quiz is null) return false;
        Game = game;
        Quiz = game.Quiz;
        IsHost = game.HostId == userManager.GetUserId(User);
        Points = Quiz.Points;
        Used = game.UsedSet;
        var scores = game.ScoreList;
        Standings = game.TeamList.Select((t, i) => (t, i < scores.Count ? scores[i] : 0)).ToList();
        return true;
    }
}
