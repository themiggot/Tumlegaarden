using System.Globalization;
using Microsoft.AspNetCore.Identity;
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
    public bool CanCreate { get; private set; }
    private string? _userId;
    private bool _isAdmin;

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
            .Where(x => x.StartsAt >= from && x.StartsAt < to)
            .OrderBy(x => x.StartsAt).ThenBy(x => x.Title)
            .ToListAsync();

        _userId = userManager.GetUserId(User);
        _isAdmin = User.IsInRole(Roles.Administrator);
        CanCreate = _isAdmin || User.IsInRole(Roles.Diktator);
    }

    /// <summary>Diktatorer kan rette egne begivenheder, administratorer alle.</summary>
    public bool CanEdit(CalendarEvent ev) => _isAdmin || (CanCreate && ev.CreatedById == _userId);

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
