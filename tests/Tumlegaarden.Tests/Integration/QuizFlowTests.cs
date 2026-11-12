using System.Net;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Tests.Integration;

/// <summary>Quiz: lav, ret, rettigheder, spil som vært og sletning.</summary>
public sealed class QuizFlowTests(TestApp app) : IClassFixture<TestApp>
{
    private static (string, string)[] QuizForm(string title) =>
    [
        ("Input.Title", title), ("Input.Description", "Til test"), ("Input.Columns", "2"), ("Input.Rows", "2"), ("Input.PointValues", "100, 300"),
        ("Input.Categories[0]", "Dyr"), ("Input.Categories[1]", "Maskiner"),
        // Indeks = kolonne * 6 + række (ikke fortløbende – skal virke alligevel)
        ("Input.Questions[0]", "Hvad siger koen?"), ("Input.Answers[0]", "Muh"),
        ("Input.Questions[1]", "Hvad siger grisen?"), ("Input.Answers[1]", "Øf"),
        ("Input.Questions[6]", "Hvad kører på marken?"), ("Input.Answers[6]", "Traktor"),
        ("Input.Questions[11]", "Uden for brættet"), ("Input.Answers[11]", "Gemmes ikke"),
    ];

    private int QuizId(string title) => app.Db(db => db.Quizzes.Single(q => q.Title == title).Id);

    [Fact]
    public async Task Create_Edit_Permissions_Play_Delete()
    {
        await app.CreateUserAsync("quiz-ejer");
        await app.CreateUserAsync("quiz-anden");
        await app.CreateUserAsync("quiz-diktator", Roles.Diktator);
        var owner = app.ClientFor("quiz-ejer");
        var other = app.ClientFor("quiz-anden");

        // Lav quizzen
        var saved = await owner.PostFormAsync("/Quiz/Edit?handler=Save", QuizForm("Testquiz").Select(f => new KeyValuePair<string, string>(f.Item1, f.Item2)), "/Quiz/Edit");
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var id = QuizId("Testquiz");
        var quiz = app.Db(db => db.Quizzes.Include(q => q.Categories).Include(q => q.Questions).Single(q => q.Id == id));
        Assert.Equal((2, 2, "100,300"), (quiz.Columns, quiz.Rows, quiz.PointValues));
        Assert.Equal(["Dyr", "Maskiner"], quiz.Categories.OrderBy(c => c.Column).Select(c => c.Name));
        Assert.Equal(3, quiz.Questions.Count);                                              // cellen uden for 2×2 gemmes ikke
        Assert.Contains(quiz.Questions, q => q.Column == 1 && q.Row == 0 && q.Answer == "Traktor");

        // Rettigheder: andre kan ikke rette eller slette; diktator kan rette
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/Quiz/Edit/{id}")).StatusCode);
        var steal = await other.PostFormAsync($"/Quiz/Edit/{id}?handler=Save", QuizForm("Stjålet").Select(f => new KeyValuePair<string, string>(f.Item1, f.Item2)), "/Quiz/Edit");
        Assert.Equal(HttpStatusCode.Forbidden, steal.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostFormAsync($"/Quiz?handler=Delete&id={id}", ("confirm", "true"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.ClientFor("quiz-diktator").GetAsync($"/Quiz/Edit/{id}")).StatusCode);

        // Ret: gem igen erstatter spørgsmålene uden dubletter
        var edited = QuizForm("Testquiz").Select(f => f.Item1 == "Input.Answers[0]" ? (f.Item1, "Muuuh") : f).Select(f => new KeyValuePair<string, string>(f.Item1, f.Item2));
        await owner.PostFormAsync($"/Quiz/Edit/{id}?handler=Save", edited, $"/Quiz/Edit/{id}");
        Assert.Equal(3, app.Db(db => db.QuizQuestions.Count(q => q.QuizId == id)));
        Assert.Equal("Muuuh", app.Db(db => db.QuizQuestions.Single(q => q.QuizId == id && q.Column == 0 && q.Row == 0).Answer));

        // Spil: ejeren er vært for to hold
        var start = await owner.PostFormAsync($"/Quiz?handler=Start&id={id}", ("teams", "Røde\r\n\r\nBlå\nRøde"));
        var gameUrl = start.Headers.Location!.OriginalString;
        Assert.Matches(@"^/Quiz/Spil/\d+$", gameUrl);
        var game = app.Db(db => db.QuizGames.Single(g => g.QuizId == id));
        Assert.Equal(["Røde", "Blå"], game.TeamList);                                       // tomme linjer og dubletter fjernes

        var board = await owner.PageAsync(gameUrl);
        Assert.DoesNotContain("Hvad siger koen?", board);                                  // spørgsmål vises først, når kortet vendes
        var card = await owner.PageAsync(gameUrl + "?c=0&r=0");
        Assert.Contains("Hvad siger koen?", card);
        Assert.DoesNotContain("Muuuh", card);                                               // svaret først efter "Vis svar"
        Assert.Contains("Muuuh", await owner.PageAsync(gameUrl + "?c=0&r=0&svar=true"));

        // Tilskuer: ser brættet, men hverken spørgsmål eller svar og kan ikke give point
        var viewer = await other.PageAsync(gameUrl + "?c=0&r=0&svar=true");
        Assert.DoesNotContain("Hvad siger koen?", viewer);
        Assert.DoesNotContain("Muuuh", viewer);
        var award = await other.PostFormAsync(gameUrl + "?handler=Award", [new("c", "0"), new("r", "0"), new("team", "0"), new("correct", "true")], "/Quiz");
        Assert.Equal(HttpStatusCode.Forbidden, award.StatusCode);

        // Forkert (−100, kortet åbent), rigtigt (+100, kortet lukkes), ingen svar, manuel justering
        async Task Post(string handler, params (string, string)[] f) =>
            await owner.PostFormAsync($"{gameUrl}?handler={handler}", f.Select(x => new KeyValuePair<string, string>(x.Item1, x.Item2)), gameUrl);
        await Post("Award", ("c", "0"), ("r", "0"), ("team", "0"), ("correct", "false"));
        await Post("Award", ("c", "0"), ("r", "0"), ("team", "1"), ("correct", "true"));
        await Post("Award", ("c", "0"), ("r", "0"), ("team", "1"), ("correct", "true"));   // allerede lukket – ingen ekstra point
        await Post("Award", ("c", "5"), ("r", "5"), ("team", "0"), ("correct", "true"));   // findes ikke
        await Post("Skip", ("c", "0"), ("r", "1"));
        await Post("Adjust", ("team", "0"), ("up", "true"));
        game = app.Db(db => db.QuizGames.Single(g => g.QuizId == id));
        Assert.Equal([0, 100], game.ScoreList);
        Assert.False(game.Finished);

        await Post("Award", ("c", "1"), ("r", "0"), ("team", "0"), ("correct", "true"));
        game = app.Db(db => db.QuizGames.Single(g => g.QuizId == id));
        Assert.Equal([100, 100], game.ScoreList);
        Assert.True(game.Finished);                                                         // alle 3 kort er brugt
        Assert.Contains("Uafgjort mellem Røde og Blå med 100 point", WebUtility.HtmlDecode(await owner.PageAsync(gameUrl)));

        // Slet: quiz, kategorier, spørgsmål og spil forsvinder
        await owner.PostFormAsync($"/Quiz?handler=Delete&id={id}", ("confirm", "true"));
        Assert.Equal(0, app.Db(db => db.Quizzes.Count(q => q.Id == id) + db.QuizQuestions.Count(q => q.QuizId == id)
            + db.QuizCategories.Count(q => q.QuizId == id) + db.QuizGames.Count(g => g.QuizId == id)));
    }

    [Fact]
    public async Task Resize_KeepsTypedText_WithoutSaving()
    {
        await app.CreateUserAsync("quiz-stoerrelse");
        var client = app.ClientFor("quiz-stoerrelse");
        var form = QuizForm("Ikke gemt").Select(f => f.Item1 == "Input.Columns" ? (f.Item1, "4") : f).Select(f => new KeyValuePair<string, string>(f.Item1, f.Item2));
        var response = await client.PostFormAsync("/Quiz/Edit?handler=Resize", form, "/Quiz/Edit");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Kategori 4", html);
        Assert.Contains("Hvad siger koen?", html);
        Assert.Equal(0, app.Db(db => db.Quizzes.Count(q => q.Title == "Ikke gemt")));
    }

    [Fact]
    public async Task Save_WithoutTitle_ShowsError()
    {
        await app.CreateUserAsync("quiz-titel");
        var form = QuizForm("").Select(f => new KeyValuePair<string, string>(f.Item1, f.Item2));
        var response = await app.ClientFor("quiz-titel").PostFormAsync("/Quiz/Edit?handler=Save", form, "/Quiz/Edit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Giv quizzen en titel.", await response.Content.ReadAsStringAsync());
    }
}
