namespace Tumlegaarden.Web.Data;

/// <summary>En brugers svar på en begivenhed: deltager eller deltager ikke. Ét svar pr. bruger pr. begivenhed.</summary>
public sealed class EventResponse
{
    public int EventId { get; set; }
    public required string UserId { get; set; }
    public required string UserEmail { get; set; }
    public bool Attending { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public CalendarEvent? Event { get; set; }

    /// <summary>Navnet før @ – kortere at vise i lister.</summary>
    public string DisplayName => UserEmail.Split('@')[0];
}
