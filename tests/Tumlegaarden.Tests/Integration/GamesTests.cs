using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Tumlegaarden.Web.Pages.Spil;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Tests.Integration;

/// <summary>Spillene gennem rigtige HTTP-forespørgsler: gæt gemmes, point gives, og snyd afvises.</summary>
public sealed class GamesTests(TestApp app) : IClassFixture<TestApp>
{
    private static DateOnly Today => DanishTime.Today(TimeProvider.System);

    private Task<byte[]> SecretAsync(string key) =>
        app.WithServicesAsync(sp => sp.GetRequiredService<SiteSettings>().GetOrCreateGameSecretAsync(key));

    private MiniGames Mini => app.Services.GetRequiredService<MiniGames>();
    private GameModes Modes => app.Services.GetRequiredService<GameModes>();

    private Tumlegaarden.Web.Data.GameResult? Result(string game, string user) =>
        app.Db(db => db.GameResults.SingleOrDefault(r => r.Game == game && r.UserEmail == $"{user}@tumlegaarden.dk"));

    [Fact]
    public async Task Ordjagt_DailyWord_GivesPoints_AndAfterDarkIsSeparate()
    {
        await app.CreateUserAsync("ordjagt");
        var client = app.ClientFor("ordjagt");
        var answer = Modes.Ordjagt.Words.WordFor(Today, await SecretAsync(SiteSettings.GameSecretKey));
        var html = await client.PageAsync("/Ordjagt");
        Assert.DoesNotContain($">{answer.ToUpperInvariant()[0]}<", html.Split("class=\"board\"")[1].Split("</div>")[0]);   // brættet er tomt

        await client.PostFormAsync("/Ordjagt?handler=Guess", ("guess", "xyzqw"));
        Assert.Equal(0, app.Db(db => db.GameRounds.Count(r => r.UserEmail == "ordjagt@tumlegaarden.dk")));   // ugyldigt ord tæller ikke

        await client.PostFormAsync("/Ordjagt?handler=Guess", ("guess", answer.ToUpperInvariant()));
        var round = app.Db(db => db.GameRounds.Single(r => r.UserEmail == "ordjagt@tumlegaarden.dk"));
        Assert.True(round.Solved && round.Finished);
        Assert.Equal(100, round.Score);
        Assert.Equal(answer, round.Answer);

        var dark = Modes.AfterDark.Words.WordFor(Today, await SecretAsync(SiteSettings.AfterDarkSecretKey));
        await client.PostFormAsync("/AfterDark?handler=Guess", ("guess", dark));
        Assert.Equal(100, app.Db(db => db.AfterDarkRounds.Single(r => r.UserEmail == "ordjagt@tumlegaarden.dk").Score));
        Assert.Equal(1, app.Db(db => db.GameRounds.Count(r => r.UserEmail == "ordjagt@tumlegaarden.dk")));
    }

    [Fact]
    public async Task Practice_DoesNotTouchTheDatabase()
    {
        await app.CreateUserAsync("oevelse");
        var client = app.ClientFor("oevelse");
        await client.PageAsync("/Ordjagt/Oev");
        var response = await client.PostFormAsync("/Ordjagt/Oev?handler=Guess", ("guess", "huset"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("HUSET", await client.PageAsync("/Ordjagt/Oev"));
        Assert.Equal(0, app.Db(db => db.GameRounds.Count(r => r.UserEmail == "oevelse@tumlegaarden.dk")));
    }

    [Fact]
    public async Task Riddle_WrongThenRight_Gives60Points_AndHintAppears()
    {
        await app.CreateUserAsync("gaade");
        var client = app.ClientFor("gaade");
        var riddle = Mini.RiddleFor(Today, await SecretAsync(MiniGames.SecretKey));

        await client.PostFormAsync("/Spil/Gaade", ("guess", "forkertord"));
        Assert.Contains(riddle.Hint(1), await client.PageAsync("/Spil/Gaade"));
        await client.PostFormAsync("/Spil/Gaade", ("guess", "forkertord"));
        Assert.Equal(1, Result(MiniGameKeys.Riddle, "gaade")!.Attempts);                  // samme gæt tæller ikke igen

        await client.PostFormAsync("/Spil/Gaade", ("guess", $" {riddle.Answer.ToUpperInvariant()}! "));
        var result = Result(MiniGameKeys.Riddle, "gaade")!;
        Assert.True(result.Solved && result.Finished);
        Assert.Equal(60, result.Score);
        await client.PostFormAsync("/Spil/Gaade", ("guess", "ekstra"));
        Assert.Equal(2, Result(MiniGameKeys.Riddle, "gaade")!.Attempts);                  // slut er slut
        Assert.Contains("gaade", await client.PageAsync("/Spil/Gaade"));                   // står på highscoren
    }

    [Fact]
    public async Task Code_InvalidGuessesAreIgnored_RightCodeGives100()
    {
        await app.CreateUserAsync("kode");
        var client = app.ClientFor("kode");
        var code = MiniGames.CodeFor(Today, await SecretAsync(MiniGames.SecretKey));

        await client.PostFormAsync("/Spil/Kode", ("g0", "9"), ("g1", "0"), ("g2", "0"), ("g3", "0"));
        await client.PostFormAsync("/Spil/Kode", ("g0", "0"), ("g1", "0"), ("g2", "0"));
        Assert.Null(Result(MiniGameKeys.Code, "kode"));

        await client.PostFormAsync("/Spil/Kode", code.Select((c, i) => ($"g{i}", c.ToString())).ToArray());
        var result = Result(MiniGameKeys.Code, "kode")!;
        Assert.True(result.Solved);
        Assert.Equal(100, result.Score);
        Assert.Equal(CodeBreaker.Encode(code), result.State);
    }

    [Fact]
    public async Task Cards_CorrectGuessesScore_WrongGuessEndsGame()
    {
        await app.CreateUserAsync("kort");
        var client = app.ClientFor("kort");
        var deck = MiniGames.DeckFor(Today, await SecretAsync(MiniGames.SecretKey));

        var i = 0;
        for (; i < 3; i++)
            await client.PostFormAsync("/Spil/Kort", ("guess", deck[i + 1].Rank >= deck[i].Rank ? "h" : "l"));
        Assert.Equal(30, Result(MiniGameKeys.Cards, "kort")!.Score);

        while (deck[i + 1].Rank == deck[i].Rank) { await client.PostFormAsync("/Spil/Kort", ("guess", "h")); i++; }
        await client.PostFormAsync("/Spil/Kort", ("guess", deck[i + 1].Rank > deck[i].Rank ? "l" : "h"));
        var result = Result(MiniGameKeys.Cards, "kort")!;
        Assert.True(result.Finished);
        Assert.Equal(i * HigherLower.PointsPerCard, result.Score);
        Assert.DoesNotContain("▲ Højere", await client.PageAsync("/Spil/Kort"));
    }

    [Fact]
    public async Task Anagram_KeyWordGives15_InvalidAndDuplicateRejected()
    {
        await app.CreateUserAsync("anagram");
        var client = app.ClientFor("anagram");
        var key = Mini.AnagramKeyFor(Today, await SecretAsync(MiniGames.SecretKey));

        await client.PostFormAsync("/Spil/Anagram", ("word", "qqq"));
        await client.PostFormAsync("/Spil/Anagram", ("word", "xy"));
        Assert.Null(Result(MiniGameKeys.Anagram, "anagram"));

        await client.PostFormAsync("/Spil/Anagram", ("word", key));
        await client.PostFormAsync("/Spil/Anagram", ("word", key));
        var shorter = Mini.AnagramWords(key).First(w => w.Length == 3);
        await client.PostFormAsync("/Spil/Anagram", ("word", shorter));
        var result = Result(MiniGameKeys.Anagram, "anagram")!;
        Assert.Equal(2, result.Attempts);
        Assert.Equal(16, result.Score);
        Assert.True(result.Solved);
    }
}
