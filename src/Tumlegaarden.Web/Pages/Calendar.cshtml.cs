using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages;

// Ingen attribut: fallback-politikken kræver login + to-faktor + en rolle.
public class CalendarModel(AppDbContext db, UserManager<IdentityUser> userManager, TimeProvider time) : PageModel
{
    public DateOnly Month { get; private set; }
    public DateOnly Today { get; private set; }
    public List<CalendarEvent> Events { get; private set; } = [];
    public int? OpenId { get; private set; }
    /// <summary>Alle brugere kan oprette begivenheder.</summary>
    public bool CanCreate => true;
    public string? UserId => _userId ??= userManager.GetUserId(User);
    private string? _userId;

    public CultureInfo Culture => DanishTime.Culture;
    public string MonthKey(DateOnly d) => d.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public string MonthTitle => Culture.TextInfo.ToTitleCase(Month.ToString("MMMM yyyy", Culture));

    public async Task OnGetAsync(string? m, int? e)
    {
        Today = DanishTime.Today(time);
        Month = DateOnly.TryParseExact(m, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                && parsed.Year is >= 2000 and <= 2100
            ? parsed
            : new DateOnly(Today.Year, Today.Month, 1);
        OpenId = e;

        var from = Month.ToDateTime(TimeOnly.MinValue);
        var to = from.AddMonths(1);
        // Med i måneden hvis den starter før månedens slutning og slutter (eller starter) efter månedens start.
        Events = await db.Events.AsNoTracking()
            .Include(x => x.Responses)
            .Where(x => x.StartsAt < to && (x.EndsAt ?? x.StartsAt) >= from)
            .OrderBy(x => x.StartsAt).ThenBy(x => x.Title)
            .ToListAsync();
    }

    /// <summary>Alle kan rette egne begivenheder. Diktatorer og administratorer kan rette alle.</summary>
    public bool CanEdit(CalendarEvent ev) => Roles.IsModerator(User) || ev.CreatedById == UserId;

    public bool? MyAnswer(CalendarEvent ev) => ev.Responses.FirstOrDefault(r => r.UserId == UserId)?.Attending;

    /// <summary>Deltager / deltager ikke. Samme knap igen fjerner svaret.</summary>
    public async Task<IActionResult> OnPostRespondAsync(int id, bool attending, string? m)
    {
        var ev = await db.Events.FindAsync(id);
        if (ev is null) return NotFound();
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        var response = await db.EventResponses.FindAsync(id, user.Id);
        string status;
        if (response is not null && response.Attending == attending)
        {
            db.EventResponses.Remove(response);
            status = $"Dit svar til \"{ev.Title}\" er fjernet.";
        }
        else
        {
            if (response is null)
            {
                response = new EventResponse { EventId = id, UserId = user.Id, UserEmail = user.Email ?? "" };
                db.EventResponses.Add(response);
            }
            response.Attending = attending;
            response.UpdatedUtc = time.GetUtcNow().UtcDateTime;
            status = attending ? $"Du deltager i \"{ev.Title}\"." : $"Du deltager ikke i \"{ev.Title}\".";
        }
        await db.SaveChangesAsync();

        TempData["Status"] = status;
        return Redirect($"{Url.Page("/Calendar", new { m = ev.StartsAt.ToString("yyyy-MM", CultureInfo.InvariantCulture), e = id })}#e-{id}");
    }

    /// <summary>Uger (mandag–søndag) der dækker måneden, med ISO-ugenummer.</summary>
    public IEnumerable<(int Week, DateOnly[] Days)> Weeks()
    {
        var first = Month;
        var start = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        var last = first.AddMonths(1).AddDays(-1);
        for (var day = start; day <= last; day = day.AddDays(7))
        {
            yield return (ISOWeek.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue)),
                Enumerable.Range(0, 7).Select(i => day.AddDays(i)).ToArray());
        }
    }

    public IEnumerable<CalendarEvent> On(DateOnly day) => Events.Where(x => x.Covers(day));

    private static string Clock(DateTime d) => d.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>"lør. 14. nov." eller "lør. 14. nov. – man. 16. nov."</summary>
    public static string DateText(CalendarEvent ev)
    {
        var start = ev.StartsAt.ToString("ddd d. MMM", DanishTime.Culture);
        return ev.IsMultiDay ? $"{start} – {ev.EndsAt!.Value.ToString("ddd d. MMM", DanishTime.Culture)}" : start;
    }

    /// <summary>"18:00", "18:00–22:00", "fra 18:00", "til 12:00" eller "Hele dagen".</summary>
    public static string TimeText(CalendarEvent ev)
    {
        var endTime = ev.EndHasTime == true && ev.EndsAt is { } end ? Clock(end) : null;
        return (ev.HasTime, endTime) switch
        {
            (true, not null) => $"{Clock(ev.StartsAt)}–{endTime}",
            (true, null) => ev.IsMultiDay ? $"fra {Clock(ev.StartsAt)}" : Clock(ev.StartsAt),
            (false, not null) => $"til {endTime}",
            _ => ev.IsMultiDay ? "Hele dagene" : "Hele dagen"
        };
    }

    /// <summary>Teksten i kalendergitteret: tid på første dag, pil på de følgende.</summary>
    public static (string? Time, bool Continues) ChipFor(CalendarEvent ev, DateOnly day) =>
        day == ev.StartDate ? (ev.HasTime ? Clock(ev.StartsAt) : null, false) : (null, true);
}
