namespace Tumlegaarden.Web.Services;

/// <summary>
/// En variant af Ordjagt. Hver variant har egne ord, egen hemmelig nøgle (eget dagens ord),
/// egen tabel (egen highscore) og egen øvespil-cookie.
/// </summary>
public sealed record GameMode(
    string Name,
    string DailyPage,
    string PracticePage,
    string SecretKey,
    string PracticeCookie,
    string ProtectorPurpose,
    string? Theme,
    WordGame Words);

/// <summary>Alle spil under "Spil" i menuen.</summary>
public sealed class GameModes(WordGame ordjagt, WordGame afterDark)
{
    public GameMode Ordjagt { get; } = new("Ordjagt", "/Game/Index", "/Game/Practice",
        SiteSettings.GameSecretKey, "__Host-Tumle-Ordjagt-Oev", "Tumlegaarden.Ordjagt.Practice.v1", null, ordjagt);

    public GameMode AfterDark { get; } = new("Ordjagt After Dark", "/Game/AfterDark", "/Game/AfterDarkPractice",
        SiteSettings.AfterDarkSecretKey, "__Host-Tumle-AfterDark-Oev", "Tumlegaarden.AfterDark.Practice.v1", "theme-afterdark", afterDark);

    public static GameModes Load(string directory) => new(WordGame.Load(directory), WordGame.LoadAfterDark(directory));
}
