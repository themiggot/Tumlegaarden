using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Web.Pages.Quiz;

/// <summary>Alle quizzer. Alle kan lave quizzer og starte et spil som vært; kun ejeren og diktatorer/admin kan rette og slette.</summary>
public class IndexModel(AppDbContext db, UserManager<IdentityUser> userManager, TimeProvider time) : PageModel
{
    public const int GameRetentionDays = 30;

    public sealed record QuizCard(int Id, string Title, string Description, int Columns, int Rows, int Filled, string CreatedById, string CreatedByEmail);
    public sealed record GameCard(int Id, int QuizId, string QuizTitle, string HostEmail, string HostId, DateTime StartedUtc, bool Finished, string Teams);

    public List<QuizCard> Quizzes { get; private set; } = [];
    public List<GameCard> Games { get; private set; } = [];
    public string? MyId { get; private set; }
    public bool IsModerator => Roles.IsModerator(User);
    public bool CanEdit(string createdById) => IsModerator || createdById == MyId;

    public async Task OnGetAsync()
    {
        MyId = userManager.GetUserId(User);
        Quizzes = await db.Quizzes.AsNoTracking().OrderByDescending(q => q.UpdatedUtc ?? q.CreatedUtc)
            .Select(q => new QuizCard(q.Id, q.Title, q.Description, q.Columns, q.Rows,
                q.Questions.Count(x => x.Column < q.Columns && x.Row < q.Rows && x.Question != "" && x.Answer != ""), q.CreatedById, q.CreatedByEmail))
            .ToListAsync();
        var now = time.GetUtcNow().UtcDateTime;
        // Gamle spil (holdnavne, point og vært) gemmes ikke for evigt.
        await db.QuizGames.Where(g => g.UpdatedUtc < now.AddDays(-GameRetentionDays)).ExecuteDeleteAsync();
        var since = now.AddDays(-14);
        Games = await db.QuizGames.AsNoTracking().Where(g => g.UpdatedUtc > since).OrderByDescending(g => g.UpdatedUtc)
            .Select(g => new GameCard(g.Id, g.QuizId, g.Quiz!.Title, g.HostEmail, g.HostId, g.StartedUtc, g.Finished, g.Teams))
            .ToListAsync();
    }

    /// <summary>Starter et nyt spil med værten som den aktuelle bruger. Holdnavne: ét pr. linje.</summary>
    public async Task<IActionResult> OnPostStartAsync(int id, string? teams)
    {
        var quiz = await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id);
        var user = await userManager.GetUserAsync(User);
        if (quiz is null || user is null) return NotFound();

        var names = (teams ?? "").Replace("\r", "").Split('\n')
            .Select(t => t.Trim()).Where(t => t.Length > 0)
            .Select(t => t.Length > QuizGame.TeamNameMaxLength ? t[..QuizGame.TeamNameMaxLength] : t)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(QuizGame.MaxTeams).ToList();
        if (names.Count == 0) names = ["Hold 1", "Hold 2"];

        var now = time.GetUtcNow().UtcDateTime;
        var game = new QuizGame
        {
            QuizId = quiz.Id, HostId = user.Id, HostEmail = user.Email ?? "",
            Teams = string.Join('\n', names), Scores = string.Join(',', names.Select(_ => 0)),
            StartedUtc = now, UpdatedUtc = now
        };
        db.QuizGames.Add(game);
        await db.SaveChangesAsync();
        return RedirectToPage("/Quiz/Play", new { id = game.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, bool confirm)
    {
        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.Id == id);
        if (quiz is null) return NotFound();
        MyId = userManager.GetUserId(User);
        if (!CanEdit(quiz.CreatedById)) return Forbid();
        if (!confirm)
        {
            TempData["Status"] = "Sæt flueben i Bekræft for at slette.";
            return RedirectToPage();
        }
        db.Quizzes.Remove(quiz);
        await db.SaveChangesAsync();
        TempData["Status"] = $"Quizzen \"{quiz.Title}\" er slettet.";
        return RedirectToPage();
    }
}
