namespace Tumlegaarden.Web.Data;

/// <summary>En brugers spil i Ordjagt for én dag. Oprettes ved første gæt.</summary>
public sealed class GameRound
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string UserEmail { get; set; }
    /// <summary>Dansk dato (kl. 00:00) for dagens ord.</summary>
    public DateTime Date { get; set; }
    /// <summary>Gættene adskilt af semikolon, fx "huset;skole".</summary>
    public string Guesses { get; set; } = "";
    public int Attempts { get; set; }
    public bool Solved { get; set; }
    public bool Finished { get; set; }
    public int Score { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public IReadOnlyList<string> GuessList => Guesses.Length == 0 ? [] : Guesses.Split(';');
}
