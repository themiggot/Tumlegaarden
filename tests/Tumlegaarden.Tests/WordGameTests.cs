using Tumlegaarden.Web.Services;
using static Tumlegaarden.Web.Services.LetterState;

namespace Tumlegaarden.Tests;

public class WordGameTests
{
    private static readonly WordGame Game = WordGame.Load(Path.Combine(AppContext.BaseDirectory, "GameData"));

    [Fact]
    public void Lists_AreLoaded()
    {
        Assert.True(Game.SolutionCount >= 1000, $"Kun {Game.SolutionCount} mulige dagens ord");
        Assert.True(Game.ValidCount >= 9000, $"Kun {Game.ValidCount} gyldige gæt");
    }

    [Theory]
    [InlineData("huset", true)]
    [InlineData("HUSET", true)]
    [InlineData(" æbler ", true)]
    [InlineData("løber", true)]
    [InlineData("xyzqw", false)]
    [InlineData("hus", false)]
    [InlineData("husets", false)]
    public void IsValidGuess(string word, bool expected) => Assert.Equal(expected, Game.IsValidGuess(word));

    [Fact]
    public void Evaluate_AllCorrect() => Assert.Equal([Correct, Correct, Correct, Correct, Correct], WordGame.Evaluate("spise", "spise"));

    [Fact]
    public void Evaluate_PresentAndAbsent() =>
        Assert.Equal([Present, Present, Absent, Absent, Absent], WordGame.Evaluate("nanna", "anker"));

    [Fact]
    public void Evaluate_DuplicateLetters_CorrectBeatsPresent() =>
        // "hoppe" har to p'er på plads 3 og 4; de tre andre p'er i gættet findes ikke.
        Assert.Equal([Absent, Absent, Correct, Correct, Absent], WordGame.Evaluate("ppppp", "hoppe"));

    [Fact]
    public void Evaluate_DuplicateInGuess_OnlyAsManyAsInAnswer() =>
        // "katte" har ét k: første k er rigtigt, det andet k i gættet findes ikke igen.
        Assert.Equal([Correct, Absent, Correct, Correct, Correct], WordGame.Evaluate("kktte", "katte"));

    [Fact]
    public void Evaluate_DanishLettersAreOwnLetters() =>
        Assert.Equal([Absent, Correct, Correct, Correct, Correct], WordGame.Evaluate("abler", "æbler"));

    [Theory]
    [InlineData(1, true, 100)]
    [InlineData(2, true, 80)]
    [InlineData(3, true, 60)]
    [InlineData(4, true, 40)]
    [InlineData(5, true, 25)]
    [InlineData(6, true, 10)]
    [InlineData(6, false, 0)]
    public void Points_DependOnAttempts(int attempts, bool solved, int expected) => Assert.Equal(expected, WordGame.Points(attempts, solved));

    [Fact]
    public void DailyWord_IsStable_AndNeverRepeatsWithinACycle()
    {
        var secret = new byte[32];
        new Random(1).NextBytes(secret);
        var start = new DateOnly(2026, 11, 10);
        Assert.Equal(Game.WordFor(start, secret), Game.WordFor(start, secret));

        var words = Enumerable.Range(0, Game.SolutionCount).Select(d => Game.WordFor(start.AddDays(d), secret)).ToList();
        Assert.Equal(Game.SolutionCount, words.Distinct().Count());
        Assert.All(words, w => Assert.True(Game.IsValidGuess(w)));
    }

    [Fact]
    public void DailyWord_DependsOnSecret()
    {
        var a = new byte[32]; var b = new byte[32]; b[0] = 1;
        var day = new DateOnly(2026, 11, 10);
        var sameAll = Enumerable.Range(0, 10).All(d => Game.WordFor(day.AddDays(d), a) == Game.WordFor(day.AddDays(d), b));
        Assert.False(sameAll);
    }

    [Fact]
    public void RandomWord_NeverReturnsExcludedWords()
    {
        var excluded = Game.WordFor(new DateOnly(2026, 11, 10), new byte[32]);
        for (var i = 0; i < 2000; i++)
        {
            var w = Game.RandomWord(excluded);
            Assert.NotEqual(excluded, w);
            Assert.True(Game.IsValidGuess(w));
        }
    }

        [Fact]
    public void KeyStates_KeepBestState()
    {
        var keys = WordGame.KeyStates(["skole", "hoppe"], "hoppe");
        Assert.Equal(Correct, keys['o']);   // 'o' var "findes" i skole, men rigtig i hoppe
        Assert.Equal(Absent, keys['k']);
        Assert.Equal(Correct, keys['e']);
    }
}
