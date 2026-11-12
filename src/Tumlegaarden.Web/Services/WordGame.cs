using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Tumlegaarden.Web.Services;

public enum LetterState { Absent, Present, Correct }

/// <summary>
/// Ordjagt: gæt dagens ord på 5 bogstaver i højst 6 forsøg. Ren logik uden database:
/// ordlister, vurdering af gæt, point og hvilket ord der er dagens.
/// </summary>
public sealed class WordGame
{
    public const int WordLength = 5;
    public const int MaxAttempts = 6;

    /// <summary>Point efter antal forsøg (index 1–6). Tabt spil giver 0.</summary>
    private static readonly int[] PointsByAttempt = [0, 100, 80, 60, 40, 25, 10];

    /// <summary>Første dag i ordenes rækkefølge. Dagens ord = rækkefølge[(dato - Epoch) mod antal].</summary>
    private static readonly DateOnly Epoch = new(2026, 1, 1);

    private readonly HashSet<string> _valid;
    private readonly string[] _solutions;

    public WordGame(IEnumerable<string> solutions, IEnumerable<string> validGuesses)
    {
        _solutions = solutions.Select(Normalize).Where(IsWordShape).Distinct().Order(StringComparer.Ordinal).ToArray();
        _valid = validGuesses.Select(Normalize).Where(IsWordShape).ToHashSet(StringComparer.Ordinal);
        _valid.UnionWith(_solutions);
        if (_solutions.Length == 0) throw new InvalidOperationException("Ordjagt har ingen ord at vælge imellem.");
    }

    public static WordGame Load(string directory) => new(ReadList(Path.Combine(directory, "ord-dagens.txt")), ReadList(Path.Combine(directory, "ord-gyldige.txt")));

    /// <summary>After Dark: egne løsningsord; alle almindelige ord kan stadig bruges som gæt.</summary>
    public static WordGame LoadAfterDark(string directory) => new(ReadList(Path.Combine(directory, "ord-afterdark.txt")), ReadList(Path.Combine(directory, "ord-gyldige.txt")));

    private static IEnumerable<string> ReadList(string path) =>
        File.ReadLines(path, Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0 && l[0] != '#');

    public int SolutionCount => _solutions.Length;
    public int ValidCount => _valid.Count;

    public static string Normalize(string? word) => (word ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormC);

    private static bool IsWordShape(string w) => w.Length == WordLength && w.All(c => c is >= 'a' and <= 'z' or 'æ' or 'ø' or 'å');

    public bool IsValidGuess(string word) => _valid.Contains(Normalize(word));

    public static int Points(int attempts, bool solved) => solved && attempts is >= 1 and <= MaxAttempts ? PointsByAttempt[attempts] : 0;

    /// <summary>
    /// Vurderer et gæt. Rigtige bogstaver på rigtig plads markeres først; derefter "findes i ordet"
    /// højst så mange gange, som bogstavet forekommer i de resterende pladser (korrekt med dobbeltbogstaver).
    /// </summary>
    public static LetterState[] Evaluate(string guess, string answer)
    {
        guess = Normalize(guess);
        answer = Normalize(answer);
        var result = new LetterState[WordLength];
        var remaining = new Dictionary<char, int>();
        for (var i = 0; i < WordLength; i++)
        {
            if (guess[i] == answer[i]) result[i] = LetterState.Correct;
            else remaining[answer[i]] = remaining.GetValueOrDefault(answer[i]) + 1;
        }
        for (var i = 0; i < WordLength; i++)
        {
            if (result[i] == LetterState.Correct) continue;
            if (remaining.GetValueOrDefault(guess[i]) > 0)
            {
                result[i] = LetterState.Present;
                remaining[guess[i]]--;
            }
        }
        return result;
    }

    /// <summary>
    /// Dagens ord. Ordene blandes i en fast rækkefølge bestemt af en hemmelig nøgle (som ikke ligger i
    /// det offentlige repo), og der går en dag frem ad gangen – så ingen ord gentages, før alle er brugt.
    /// </summary>
    public string WordFor(DateOnly date, byte[] secret)
    {
        var order = _orders.GetOrAdd(Convert.ToBase64String(secret), _ =>
            _solutions.OrderBy(w => Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(w))), StringComparer.Ordinal).ToArray());
        var day = date.DayNumber - Epoch.DayNumber;
        return order[((day % order.Length) + order.Length) % order.Length];
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string[]> _orders = new();

    /// <summary>Tilfældigt ord til øvespil – aldrig et af de udelukkede (fx dagens ord).</summary>
    public string RandomWord(params string?[] exclude)
    {
        var excluded = exclude.Where(e => e is not null).Select(Normalize).ToHashSet();
        string word;
        do word = _solutions[Random.Shared.Next(_solutions.Length)];
        while (excluded.Contains(word) && excluded.Count < _solutions.Length);
        return word;
    }

        /// <summary>Tastaturets tilstand: den bedste tilstand hvert bogstav har opnået i gættene.</summary>
    public static Dictionary<char, LetterState> KeyStates(IEnumerable<string> guesses, string answer)
    {
        var keys = new Dictionary<char, LetterState>();
        foreach (var g in guesses)
        {
            var states = Evaluate(g, answer);
            for (var i = 0; i < WordLength; i++)
            {
                var c = Normalize(g)[i];
                if (!keys.TryGetValue(c, out var old) || states[i] > old) keys[c] = states[i];
            }
        }
        return keys;
    }

    public static string DisplayDate(DateOnly date) => date.ToString("dddd d. MMMM", DanishTime.Culture);
}
