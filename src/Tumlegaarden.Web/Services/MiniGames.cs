using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Tumlegaarden.Web.Services;

/// <summary>
/// De små daglige spil: Dagens gåde, Kodeknækker, Højere eller lavere og Anagram.
/// Ren logik uden database. Dagens opgave udledes af en hemmelig nøgle (i databasen) og datoen,
/// så den er ens for alle, skifter ved midnat og ikke kan regnes ud fra det offentlige repo.
/// </summary>
public sealed class MiniGames
{
    public const string SecretKey = "Game.Mini.Secret";
    private static readonly DateOnly Epoch = new(2026, 1, 1);

    public IReadOnlyList<Riddle> Riddles { get; }
    private readonly HashSet<string> _anagramWords;
    private readonly string[] _anagramKeys;
    private readonly ConcurrentDictionary<string, string[]> _orders = new();
    private readonly ConcurrentDictionary<string, string[]> _possible = new();

    public MiniGames(IEnumerable<Riddle> riddles, IEnumerable<string> anagramWords, IEnumerable<string> anagramKeys)
    {
        Riddles = riddles.ToArray();
        _anagramWords = anagramWords.Select(WordGame.Normalize).Where(w => w.Length is >= Anagram.MinLength and <= Anagram.Letters && w.All(IsLetter)).ToHashSet(StringComparer.Ordinal);
        _anagramKeys = anagramKeys.Select(WordGame.Normalize).Where(w => w.Length == Anagram.Letters && w.All(IsLetter)).Distinct().Order(StringComparer.Ordinal).ToArray();
        _anagramWords.UnionWith(_anagramKeys);
        if (Riddles.Count == 0 || _anagramKeys.Length == 0) throw new InvalidOperationException("Spillene mangler data.");
    }

    public static MiniGames Load(string directory) => new(
        ReadLines(Path.Combine(directory, "gaader.txt")).Select(Riddle.Parse),
        ReadLines(Path.Combine(directory, "anagram-ord.txt")).Concat(ReadLines(Path.Combine(directory, "ord-gyldige.txt"))),
        ReadLines(Path.Combine(directory, "anagram-noegler.txt")));

    private static IEnumerable<string> ReadLines(string path) =>
        File.ReadLines(path, Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0 && l[0] != '#');

    private static bool IsLetter(char c) => c is >= 'a' and <= 'z' or 'æ' or 'ø' or 'å';

    public int AnagramKeyCount => _anagramKeys.Length;
    public int AnagramWordCount => _anagramWords.Count;

    /// <summary>Tilfældige, men faste bytes for et spil på en dato.</summary>
    public static byte[] Seed(byte[] secret, string game, DateOnly date) =>
        HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes($"{game}:{date:yyyy-MM-dd}"));

    /// <summary>Vælger dagens element fra en liste, så intet gentages, før hele listen er brugt.</summary>
    private string Pick(string game, IReadOnlyList<string> items, DateOnly date, byte[] secret)
    {
        var order = _orders.GetOrAdd(game + ":" + Convert.ToHexString(SHA256.HashData(secret)), _ =>
            items.Select(i => (Key: Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes($"{game}:{i}"))), Item: i))
                 .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Item).ToArray());
        var day = date.DayNumber - Epoch.DayNumber;
        return order[((day % order.Length) + order.Length) % order.Length];
    }

    // ---------- Dagens gåde ----------

    public Riddle RiddleFor(DateOnly date, byte[] secret)
    {
        var text = Pick("gaade", Riddles.Select(r => r.Text).ToArray(), date, secret);
        return Riddles.First(r => r.Text == text);
    }

    // ---------- Kodeknækker ----------

    public static int[] CodeFor(DateOnly date, byte[] secret) =>
        Seed(secret, "kode", date).Take(CodeBreaker.Length).Select(b => b % CodeBreaker.Colors.Length).ToArray();

    // ---------- Højere eller lavere ----------

    /// <summary>Dagens blandede kortspil (52 kort), ens for alle.</summary>
    public static Card[] DeckFor(DateOnly date, byte[] secret)
    {
        var seed = Seed(secret, "kort", date);
        return Enumerable.Range(0, 52)
            .Select(i => (Card: new Card(i % 13 + 2, i / 13), Key: Convert.ToHexString(HMACSHA256.HashData(seed, new[] { (byte)i }))))
            .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Card).ToArray();
    }

    // ---------- Anagram ----------

    public string AnagramKeyFor(DateOnly date, byte[] secret) => Pick("anagram", _anagramKeys, date, secret);

    /// <summary>Bogstaverne i blandet (men fast) rækkefølge, så nøgleordet ikke står direkte.</summary>
    public static char[] AnagramLetters(string key, DateOnly date, byte[] secret)
    {
        var seed = Seed(secret, "anagram-bland", date);
        return key.Select((c, i) => (c, k: seed[i])).OrderBy(x => x.k).ThenBy(x => x.c).Select(x => x.c).ToArray();
    }

    public bool IsAnagramWord(string word) => _anagramWords.Contains(word);

    /// <summary>Alle gyldige ord, der kan laves af nøgleordets bogstaver.</summary>
    public IReadOnlyList<string> AnagramWords(string key) =>
        _possible.GetOrAdd(key, k => _anagramWords.Where(w => Anagram.CanMake(w, k)).OrderBy(w => w.Length).ThenBy(w => w, StringComparer.Ordinal).ToArray());
}

/// <summary>Et ordsprog eller en talemåde med ét manglende ord (___).</summary>
public sealed record Riddle(string Text, string Answer)
{
    public const string Blank = "___";
    public const int MaxAttempts = 3;
    private static readonly int[] PointsByAttempt = [0, 100, 60, 30];

    public static Riddle Parse(string line)
    {
        var parts = line.Split('|');
        if (parts.Length != 2 || !parts[0].Contains(Blank)) throw new FormatException($"Ugyldig gåde: {line}");
        return new Riddle(parts[0].Trim(), WordGame.Normalize(parts[1]));
    }

    public string Before => Text[..Text.IndexOf(Blank, StringComparison.Ordinal)];
    public string After => Text[(Text.IndexOf(Blank, StringComparison.Ordinal) + Blank.Length)..];

    public bool IsCorrect(string guess) => Normalize(guess) == Answer;

    /// <summary>Små bogstaver, uden mellemrum i enderne og uden tegnsætning.</summary>
    public static string Normalize(string? guess) =>
        new string(WordGame.Normalize(guess).Where(c => char.IsLetter(c)).ToArray());

    /// <summary>Hjælp efter forkerte gæt: antal bogstaver altid, første bogstav efter 1 forkert, også sidste efter 2.</summary>
    public string Hint(int wrong)
    {
        var chars = Answer.Select((c, i) =>
            (wrong >= 1 && i == 0) || (wrong >= 2 && i == Answer.Length - 1) ? char.ToUpperInvariant(c) : '_');
        return string.Join(' ', chars);
    }

    public static int Points(int attempts, bool solved) => solved && attempts is >= 1 and <= MaxAttempts ? PointsByAttempt[attempts] : 0;
}

/// <summary>Kodeknækker: gæt en kode på 4 farver (gentagelser tilladt) i højst 8 forsøg.</summary>
public static class CodeBreaker
{
    public const int Length = 4;
    public const int MaxAttempts = 8;
    public static readonly string[] Colors = ["rød", "blå", "grøn", "gul", "lilla", "orange"];
    private static readonly int[] PointsByAttempt = [0, 100, 90, 80, 70, 55, 40, 25, 10];

    /// <summary>Rigtig farve på rigtig plads (Exact) og rigtig farve på forkert plads (Near).</summary>
    public static (int Exact, int Near) Score(IReadOnlyList<int> guess, IReadOnlyList<int> code)
    {
        var exact = 0;
        var restGuess = new int[Colors.Length];
        var restCode = new int[Colors.Length];
        for (var i = 0; i < Length; i++)
        {
            if (guess[i] == code[i]) exact++;
            else
            {
                restGuess[guess[i]]++;
                restCode[code[i]]++;
            }
        }
        var near = 0;
        for (var c = 0; c < Colors.Length; c++) near += Math.Min(restGuess[c], restCode[c]);
        return (exact, near);
    }

    public static int Points(int attempts, bool solved) => solved && attempts is >= 1 and <= MaxAttempts ? PointsByAttempt[attempts] : 0;

    /// <summary>"0123" ↔ [0,1,2,3].</summary>
    public static string Encode(IEnumerable<int> code) => string.Concat(code.Select(c => (char)('0' + c)));
    public static int[] Decode(string s) => s.Select(c => c - '0').ToArray();

    public static bool IsValid(IReadOnlyList<int>? guess) =>
        guess is { Count: Length } && guess.All(c => c >= 0 && c < Colors.Length);
}

/// <summary>Et spillekort. Rang 2–14 (es er højest), kulør 0–3.</summary>
public readonly record struct Card(int Rank, int Suit)
{
    private static readonly string[] Suits = ["♠", "♥", "♦", "♣"];
    private static readonly string[] SuitNames = ["spar", "hjerter", "ruder", "klør"];

    public string RankText => Rank switch { 11 => "J", 12 => "D", 13 => "K", 14 => "E", _ => Rank.ToString(System.Globalization.CultureInfo.InvariantCulture) };
    public string SuitSymbol => Suits[Suit];
    public bool IsRed => Suit is 1 or 2;
    public string Name => $"{SuitNames[Suit]} {Rank switch { 11 => "knægt", 12 => "dame", 13 => "konge", 14 => "es", _ => RankText }}";
}

/// <summary>Højere eller lavere: gæt om næste kort er højere eller lavere. Samme rang tæller som rigtigt.</summary>
public static class HigherLower
{
    public const int PointsPerCard = 10;
    public const int MaxCorrect = 51;

    public static bool IsCorrect(Card current, Card next, bool guessHigher) =>
        next.Rank == current.Rank || (guessHigher ? next.Rank > current.Rank : next.Rank < current.Rank);
}

/// <summary>Anagram: find ord på mindst 3 bogstaver af dagens 7 bogstaver.</summary>
public static class Anagram
{
    public const int Letters = 7;
    public const int MinLength = 3;

    public static bool CanMake(string word, string key)
    {
        if (word.Length < MinLength || word.Length > key.Length) return false;
        Span<int> counts = stackalloc int[128];
        var extra = new Dictionary<char, int>();
        foreach (var c in key)
        {
            if (c < 128) counts[c]++;
            else extra[c] = extra.GetValueOrDefault(c) + 1;
        }
        foreach (var c in word)
        {
            if (c < 128)
            {
                if (--counts[c] < 0) return false;
            }
            else
            {
                if (extra.GetValueOrDefault(c) == 0) return false;
                extra[c]--;
            }
        }
        return true;
    }

    /// <summary>3 bogstaver: 1 point, 4: 2, 5: 3, 6: 5, alle 7: 15.</summary>
    public static int Points(string word) => word.Length switch { 3 => 1, 4 => 2, 5 => 3, 6 => 5, 7 => 15, _ => 0 };
}
