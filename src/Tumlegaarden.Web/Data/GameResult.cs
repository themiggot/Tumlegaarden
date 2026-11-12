namespace Tumlegaarden.Web.Data;

/// <summary>
/// En brugers spil i et af de små daglige spil (gåde, kodeknækker, kort, anagram) på én dag.
/// Én fælles tabel; <see cref="Game"/> siger hvilket spil. Spillets forløb ligger i <see cref="State"/>.
/// </summary>
public sealed class GameResult
{
    public int Id { get; set; }
    /// <summary>Spillets nøgle, fx "gaade" (se <see cref="Tumlegaarden.Web.Pages.Spil.MiniGameKeys"/>).</summary>
    public required string Game { get; set; }
    public required string UserId { get; set; }
    public required string UserEmail { get; set; }
    /// <summary>Dansk dato (kl. 00:00).</summary>
    public DateTime Date { get; set; }
    /// <summary>Gæt eller fundne ord adskilt af semikolon.</summary>
    public string State { get; set; } = "";
    /// <summary>Antal gæt (gåde, kode), rigtige kort i træk (kort) eller fundne ord (anagram).</summary>
    public int Attempts { get; set; }
    public bool Solved { get; set; }
    public bool Finished { get; set; }
    public int Score { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public List<string> StateList => State.Length == 0 ? [] : [.. State.Split(';')];
}
