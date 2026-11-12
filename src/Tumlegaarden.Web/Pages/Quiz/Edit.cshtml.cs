using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;
using QuizEntity = Tumlegaarden.Web.Data.Quiz;

namespace Tumlegaarden.Web.Pages.Quiz;

/// <summary>Lav eller ret en quiz: titel, størrelse, point pr. række, kategorinavne og spørgsmål/svar for hver celle.</summary>
public class EditModel(AppDbContext db, UserManager<IdentityUser> userManager, TimeProvider time) : PageModel
{
    /// <summary>Formularens felter. Cellerne ligger fladt med indeks kolonne * MaxRows + række.</summary>
    public sealed class QuizInput
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public int Columns { get; set; } = 5;
        public int Rows { get; set; } = 5;
        public string? PointValues { get; set; } = "100,200,300,400,500";
        public Dictionary<int, string?> Categories { get; set; } = [];
        public Dictionary<int, string?> Questions { get; set; } = [];
        public Dictionary<int, string?> Answers { get; set; } = [];
    }

    public static int Index(int column, int row) => column * QuizEntity.MaxRows + row;

    [BindProperty] public QuizInput Input { get; set; } = new();
    public int? Id { get; private set; }
    public int[] Points => QuizPoints.Parse(Input.PointValues, Input.Rows);
    public List<string> Errors { get; } = [];

    public string Category(int c) => Input.Categories.GetValueOrDefault(c) ?? "";
    public string Question(int c, int r) => Input.Questions.GetValueOrDefault(Index(c, r)) ?? "";
    public string Answer(int c, int r) => Input.Answers.GetValueOrDefault(Index(c, r)) ?? "";

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null) return Page();
        var quiz = await LoadAsync(id.Value);
        if (quiz is null) return NotFound();
        if (!CanEdit(quiz)) return Forbid();
        Id = quiz.Id;
        Input = new QuizInput
        {
            Title = quiz.Title, Description = quiz.Description, Columns = quiz.Columns, Rows = quiz.Rows, PointValues = string.Join(",", quiz.Points),
            Categories = quiz.Categories.ToDictionary(x => x.Column, x => (string?)x.Name)
        };
        foreach (var q in quiz.Questions.Where(q => q.Column < QuizEntity.MaxColumns && q.Row < QuizEntity.MaxRows))
        {
            Input.Questions[Index(q.Column, q.Row)] = q.Question;
            Input.Answers[Index(q.Column, q.Row)] = q.Answer;
        }
        return Page();
    }

    /// <summary>Ændrer antal kategorier/rækker uden at gemme; det indtastede bevares.</summary>
    public async Task<IActionResult> OnPostResizeAsync(int? id)
    {
        if (id is not null && await LoadAsync(id.Value) is { } quiz && !CanEdit(quiz)) return Forbid();
        Id = id;
        Clamp();
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(int? id)
    {
        Clamp();
        ModelState.Clear();
        Id = id;
        var title = (Input.Title ?? "").Trim();
        var description = (Input.Description ?? "").Replace("\r\n", "\n").Trim();
        if (title.Length == 0) Errors.Add("Giv quizzen en titel.");
        if (title.Length > QuizEntity.TitleMaxLength) Errors.Add($"Titlen må højst være {QuizEntity.TitleMaxLength} tegn.");
        if (description.Length > QuizEntity.DescriptionMaxLength) Errors.Add($"Beskrivelsen må højst være {QuizEntity.DescriptionMaxLength} tegn.");
        for (var c = 0; c < Input.Columns; c++)
        {
            if (Category(c).Trim().Length > QuizCategory.NameMaxLength) Errors.Add($"Kategori {c + 1}: navnet må højst være {QuizCategory.NameMaxLength} tegn.");
            for (var r = 0; r < Input.Rows; r++)
            {
                if (Question(c, r).Trim().Length > QuizQuestion.QuestionMaxLength) Errors.Add($"Kategori {c + 1}, række {r + 1}: spørgsmålet er for langt.");
                if (Answer(c, r).Trim().Length > QuizQuestion.AnswerMaxLength) Errors.Add($"Kategori {c + 1}, række {r + 1}: svaret er for langt.");
            }
        }
        if (Errors.Count > 0) return Page();

        QuizEntity? quiz;
        var now = time.GetUtcNow().UtcDateTime;
        if (id is null)
        {
            var user = await userManager.GetUserAsync(User);
            if (user is null) return Forbid();
            quiz = new QuizEntity { Title = title, CreatedById = user.Id, CreatedByEmail = user.Email ?? "", CreatedUtc = now };
            db.Quizzes.Add(quiz);
        }
        else
        {
            quiz = await LoadAsync(id.Value, tracking: true);
            if (quiz is null) return NotFound();
            if (!CanEdit(quiz)) return Forbid();
            quiz.UpdatedUtc = now;
            db.QuizCategories.RemoveRange(quiz.Categories);
            db.QuizQuestions.RemoveRange(quiz.Questions);
            await db.SaveChangesAsync();   // unikke indeks: slet de gamle, før de nye indsættes
            quiz.Categories.Clear();
            quiz.Questions.Clear();
        }

        quiz.Title = title;
        quiz.Description = description;
        quiz.Columns = Input.Columns;
        quiz.Rows = Input.Rows;
        quiz.PointValues = string.Join(",", Points);
        for (var c = 0; c < Input.Columns; c++)
        {
            quiz.Categories.Add(new QuizCategory { Column = c, Name = Category(c).Trim() });
            for (var r = 0; r < Input.Rows; r++)
            {
                var question = Question(c, r).Replace("\r\n", "\n").Trim();
                var answer = Answer(c, r).Trim();
                if (question.Length > 0 || answer.Length > 0)
                    quiz.Questions.Add(new QuizQuestion { Column = c, Row = r, Question = question, Answer = answer });
            }
        }
        await db.SaveChangesAsync();
        TempData["Status"] = $"Quizzen \"{quiz.Title}\" er gemt.";
        return RedirectToPage(new { id = quiz.Id });
    }

    private void Clamp()
    {
        Input.Columns = Math.Clamp(Input.Columns, QuizEntity.MinColumns, QuizEntity.MaxColumns);
        Input.Rows = Math.Clamp(Input.Rows, QuizEntity.MinRows, QuizEntity.MaxRows);
    }

    private Task<QuizEntity?> LoadAsync(int id, bool tracking = false)
    {
        var q = db.Quizzes.Include(x => x.Categories).Include(x => x.Questions).Where(x => x.Id == id);
        return (tracking ? q : q.AsNoTracking()).FirstOrDefaultAsync();
    }

    private bool CanEdit(QuizEntity quiz) => Roles.IsModerator(User) || quiz.CreatedById == userManager.GetUserId(User);
}
