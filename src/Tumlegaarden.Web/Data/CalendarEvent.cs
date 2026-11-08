namespace Tumlegaarden.Web.Data;

/// <summary>En begivenhed i kalenderen. Tidspunktet er dansk lokal tid (vægur), ikke UTC.</summary>
public sealed class CalendarEvent
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public DateTime StartsAt { get; set; }
    /// <summary>False = heldagsbegivenhed (kun dato).</summary>
    public bool HasTime { get; set; }
    public string Body { get; set; } = "";
    public required string CreatedById { get; set; }
    public required string CreatedByEmail { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    public List<EventResponse> Responses { get; set; } = [];

    public const int TitleMaxLength = 100;
    public const int BodyMaxLength = 4000;
}
