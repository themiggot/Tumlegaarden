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
public class IndexModel(AppDbContext db, UserManager<IdentityUser> userManager, TimeProvider time) : PageModel
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
        Events = await db.Events.AsNoTracking()
            .Include(x => x.Responses)
            .Where(x => x.StartsAt >= from && x.StartsAt < to)
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
        return Redirect($"{Url.Page("/Index", new { m = ev.StartsAt.ToString("yyyy-MM", CultureInfo.InvariantCulture), e = id })}#e-{id}");
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

    public IEnumerable<CalendarEvent> On(DateOnly day) => Events.Where(x => DateOnly.FromDateTime(x.StartsAt) == day);

    public static string TimeText(CalendarEvent ev) => ev.HasTime ? ev.StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture) : "Hele dagen";
}
