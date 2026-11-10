using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Game;

/// <summary>Det der skal vises i gitter, gætteformular og tastatur – fælles for dagens spil og øvespil.</summary>
public sealed record BoardView(
    IReadOnlyList<(string Word, LetterState[] States)> Rows,
    IReadOnlyDictionary<char, LetterState> Keys,
    bool CanGuess,
    string? Error)
{
    public static string StateClass(LetterState s) => s switch
    {
        LetterState.Correct => "t-correct",
        LetterState.Present => "t-present",
        _ => "t-absent"
    };

    public static string StateText(LetterState s) => s switch
    {
        LetterState.Correct => "rigtig plads",
        LetterState.Present => "findes et andet sted",
        _ => "findes ikke"
    };
}
