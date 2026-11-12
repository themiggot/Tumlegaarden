using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Tests;

/// <summary>De små spil: gåde, kodeknækker, højere/lavere og anagram.</summary>
public class MiniGamesTests
{
    private static readonly MiniGames Games = MiniGames.Load(Path.Combine(AppContext.BaseDirectory, "GameData"));
    private static readonly byte[] Secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private static readonly DateOnly Day = new(2026, 11, 12);

    // ---------- Data ----------

    [Fact]
    public void Data_IsLoaded()
    {
        Assert.True(Games.Riddles.Count >= 100, $"{Games.Riddles.Count} gåder");
        Assert.True(Games.AnagramKeyCount >= 1000, $"{Games.AnagramKeyCount} nøgleord");
        Assert.True(Games.AnagramWordCount >= 20000, $"{Games.AnagramWordCount} ord");
        Assert.All(Games.Riddles, r => Assert.Matches("^[a-zæøå]+$", r.Answer));
    }

    [Fact]
    public void DailyPicks_AreStableAndChangeDaily_AndRiddlesDoNotRepeat()
    {
        Assert.Equal(Games.RiddleFor(Day, Secret), Games.RiddleFor(Day, Secret));
        Assert.Equal(Games.AnagramKeyFor(Day, Secret), Games.AnagramKeyFor(Day, Secret));
        Assert.Equal(MiniGames.CodeFor(Day, Secret), MiniGames.CodeFor(Day, Secret));
        var riddles = Enumerable.Range(0, Games.Riddles.Count).Select(d => Games.RiddleFor(Day.AddDays(d), Secret).Text).ToList();
        Assert.Equal(riddles.Count, riddles.Distinct().Count());                       // ingen gentagelser før listen er brugt
        Assert.NotEqual(MiniGames.DeckFor(Day, Secret), MiniGames.DeckFor(Day.AddDays(1), Secret));
        Assert.NotEqual(Games.AnagramKeyFor(Day, Secret), Games.AnagramKeyFor(Day, new byte[32]));   // nøglen betyder noget
    }

    // ---------- Gåde ----------

    [Fact]
    public void Riddle_HintsAndAnswers()
    {
        var r = Riddle.Parse("Morgenstund har guld i ___|mund");
        Assert.Equal("Morgenstund har guld i ", r.Before);
        Assert.Equal("", r.After);
        Assert.True(r.IsCorrect("  MUND. "));
        Assert.False(r.IsCorrect("hånd"));
        Assert.Equal("_ _ _ _", r.Hint(0));
        Assert.Equal("M _ _ _", r.Hint(1));
        Assert.Equal("M _ _ D", r.Hint(2));
        Assert.Equal([100, 60, 30, 0], new[] { Riddle.Points(1, true), Riddle.Points(2, true), Riddle.Points(3, true), Riddle.Points(3, false) });
        Assert.Throws<FormatException>(() => Riddle.Parse("uden hul|ord"));
    }

    // ---------- Kodeknækker ----------

    [Theory]
    [InlineData("0123", "0123", 4, 0)]
    [InlineData("3210", "0123", 0, 4)]
    [InlineData("0011", "0101", 2, 2)]
    [InlineData("0000", "0123", 1, 0)]    // gentaget farve tæller kun så mange gange, som den er i koden
    [InlineData("1100", "0012", 0, 3)]
    [InlineData("4455", "0123", 0, 0)]
    public void Code_Score(string guess, string code, int exact, int near) =>
        Assert.Equal((exact, near), CodeBreaker.Score(CodeBreaker.Decode(guess), CodeBreaker.Decode(code)));

    [Fact]
    public void Code_IsValidAndInRange()
    {
        var code = MiniGames.CodeFor(Day, Secret);
        Assert.True(CodeBreaker.IsValid(code));
        Assert.False(CodeBreaker.IsValid([0, 1, 2]));
        Assert.False(CodeBreaker.IsValid([0, 1, 2, 6]));
        Assert.Equal("5201", CodeBreaker.Encode(CodeBreaker.Decode("5201")));
        Assert.Equal(100, CodeBreaker.Points(1, true));
        Assert.Equal(10, CodeBreaker.Points(8, true));
        Assert.Equal(0, CodeBreaker.Points(8, false));
    }

    // ---------- Højere eller lavere ----------

    [Fact]
    public void Deck_HasAll52CardsOnce()
    {
        var deck = MiniGames.DeckFor(Day, Secret);
        Assert.Equal(52, deck.Length);
        Assert.Equal(52, deck.Distinct().Count());
        Assert.All(deck, c => Assert.InRange(c.Rank, 2, 14));
    }

    [Fact]
    public void HigherLower_Rules()
    {
        var five = new Card(5, 0);
        Assert.True(HigherLower.IsCorrect(five, new Card(9, 1), guessHigher: true));
        Assert.False(HigherLower.IsCorrect(five, new Card(9, 1), guessHigher: false));
        Assert.True(HigherLower.IsCorrect(five, new Card(2, 1), guessHigher: false));
        Assert.True(HigherLower.IsCorrect(five, new Card(5, 3), guessHigher: false));   // samme værdi = rigtigt
        Assert.Equal("E", new Card(14, 0).RankText);
        Assert.Equal("hjerter dame", new Card(12, 1).Name);
        Assert.True(new Card(3, 2).IsRed);
    }

    // ---------- Anagram ----------

    [Theory]
    [InlineData("ost", "stormen", true)]
    [InlineData("monster", "stormen", true)]
    [InlineData("rommen", "stormen", false)]    // kun ét m
    [InlineData("tø", "stormen", false)]        // for kort
    [InlineData("bøn", "bønnerå", true)]
    [InlineData("åbne", "bønnerå", true)]
    [InlineData("åå", "bønnerå", false)]
    public void Anagram_CanMake(string word, string key, bool expected) => Assert.Equal(expected, Anagram.CanMake(word, key));

    [Fact]
    public void Anagram_DailyKey_HasEnoughWords_AndLettersAreAPermutation()
    {
        for (var d = 0; d < 30; d++)
        {
            var key = Games.AnagramKeyFor(Day.AddDays(d), Secret);
            var words = Games.AnagramWords(key);
            Assert.Contains(key, words);
            Assert.True(words.Count >= 12, $"{key}: {words.Count}");
            Assert.All(words, w => Assert.True(Anagram.CanMake(w, key) && Games.IsAnagramWord(w)));
            Assert.Equal(key.Order(), MiniGames.AnagramLetters(key, Day.AddDays(d), Secret).Order());
        }
        Assert.Equal([1, 2, 3, 5, 15], new[] { "ost", "hest", "huset", "huset!"[..5] + "e", "monster" }.Select(Anagram.Points));
    }
}
