using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Tests;

/// <summary>Ordjagt After Dark: egne ord, egen nøgle, egen tabel og egen øvespil-cookie.</summary>
public class AfterDarkTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "GameData");
    private static readonly GameModes Games = GameModes.Load(Dir);

    private static IEnumerable<string> ListWords() =>
        File.ReadLines(Path.Combine(Dir, "ord-afterdark.txt")).Select(l => l.Trim()).Where(l => l.Length > 0 && l[0] != '#');

    [Fact]
    public void WordList_HasEnoughWords_AllFiveLetters()
    {
        var words = ListWords().ToList();
        Assert.True(words.Count >= 50, $"kun {words.Count} ord");
        Assert.Equal(words.Count, words.Distinct().Count());
        Assert.All(words, w => Assert.Equal(WordGame.WordLength, w.Length));
        Assert.Equal(words.Count, Games.AfterDark.Words.SolutionCount);
    }

    [Theory]
    [InlineData("neger")]
    [InlineData("jøder")]
    [InlineData("bøsse")]
    [InlineData("lebbe")]
    public void WordList_ExcludesSlursAgainstGroups(string slur) =>
        Assert.DoesNotContain(slur, ListWords());

    [Fact]
    public void AllWords_AreValidGuesses_AndOrdinaryWordsStillWork()
    {
        Assert.All(ListWords(), w => Assert.True(Games.AfterDark.Words.IsValidGuess(w), w));
        Assert.True(Games.AfterDark.Words.IsValidGuess("huset"));
        Assert.True(Games.AfterDark.Words.IsValidGuess("skole"));
        Assert.False(Games.AfterDark.Words.IsValidGuess("xyzqw"));
    }

    [Fact]
    public void DailyWord_ComesFromAfterDarkList()
    {
        var words = ListWords().ToHashSet();
        var secret = new byte[32];
        for (var d = 0; d < 30; d++)
            Assert.Contains(Games.AfterDark.Words.WordFor(new DateOnly(2026, 11, 1).AddDays(d), secret), words);
    }

    [Fact]
    public void Modes_DoNotShareSecretCookieOrProtector()
    {
        Assert.NotEqual(Games.Ordjagt.SecretKey, Games.AfterDark.SecretKey);
        Assert.NotEqual(Games.Ordjagt.PracticeCookie, Games.AfterDark.PracticeCookie);
        Assert.NotEqual(Games.Ordjagt.ProtectorPurpose, Games.AfterDark.ProtectorPurpose);
        Assert.StartsWith("__Host-", Games.AfterDark.PracticeCookie);
        Assert.NotEqual(Games.Ordjagt.Words.SolutionCount, Games.AfterDark.Words.SolutionCount);
    }

    [Fact]
    public async Task Rounds_AreStoredInSeparateTables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        Assert.Equal("GameRounds", db.Model.FindEntityType(typeof(GameRound))!.GetTableName());
        Assert.Equal("AfterDarkRounds", db.Model.FindEntityType(typeof(AfterDarkRound))!.GetTableName());
        Assert.Contains(db.Model.FindEntityType(typeof(AfterDarkRound))!.GetIndexes(), i => i.IsUnique && i.Properties.Count == 2);
        Assert.Null(db.Model.FindEntityType(typeof(GameRoundBase)));   // ingen fælles tabel
    }
}
