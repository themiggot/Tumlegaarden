namespace Tumlegaarden.Web.Data;

/// <summary>Fælles felter for en brugers spil på én dag. Hver spiltype har sin egen tabel.</summary>
public abstract class GameRoundBase
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string UserEmail { get; set; }
    /// <summary>Dansk dato (kl. 00:00) for dagens ord.</summary>
    public DateTime Date { get; set; }
    /// <summary>Gættene adskilt af semikolon, fx "huset;skole".</summary>
    public string Guesses { get; set; } = "";
    /// <summary>Ordet spillet blev spillet med. Gemmes, så spillet vises rigtigt, selv hvis ordlisten ændres senere.
    /// Null for spil fra før kolonnen fandtes.</summary>
    public string? Answer { get; set; }
    public int Attempts { get; set; }
    public bool Solved { get; set; }
    public bool Finished { get; set; }
    public int Score { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public IReadOnlyList<string> GuessList => Guesses.Length == 0 ? [] : Guesses.Split(';');
}

/// <summary>En brugers spil i Ordjagt for én dag. Oprettes ved første gæt.</summary>
public sealed class GameRound : GameRoundBase;

/// <summary>En brugers spil i Ordjagt After Dark for én dag. Egen tabel, så highscoren er adskilt.</summary>
public sealed class AfterDarkRound : GameRoundBase;
