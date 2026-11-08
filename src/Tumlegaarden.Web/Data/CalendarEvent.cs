namespace Tumlegaarden.Web.Data;

/// <summary>En begivenhed i kalenderen. Tidspunktet er dansk lokal tid (vægur), ikke UTC.</summary>
public sealed class CalendarEvent
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public DateTime StartsAt { get; set; }
    /// <summary>False = ingen starttid (kun dato).</summary>
    public bool HasTime { get; set; }
    /// <summary>Slut (dansk lokal tid). Null = begivenheden ligger på én dag.</summary>
    public DateTime? EndsAt { get; set; }
    /// <summary>True hvis EndsAt har et sluttidspunkt; ellers gælder kun slutdatoen. Nullable, så kolonnen kan tilføjes til en eksisterende tabel.</summary>
    public bool? EndHasTime { get; set; }
    public string Body { get; set; } = "";
    public required string CreatedById { get; set; }
    public required string CreatedByEmail { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    public List<EventResponse> Responses { get; set; } = [];

    public DateOnly StartDate => DateOnly.FromDateTime(StartsAt);
    public DateOnly EndDate => EndsAt is { } end ? DateOnly.FromDateTime(end) : StartDate;
    public bool IsMultiDay => EndDate > StartDate;
    public bool Covers(DateOnly day) => day >= StartDate && day <= EndDate;

    /// <summary>Længste tilladte varighed.</summary>
    public const int MaxDays = 366;
    public const int TitleMaxLength = 100;
    public const int BodyMaxLength = 4000;
}
