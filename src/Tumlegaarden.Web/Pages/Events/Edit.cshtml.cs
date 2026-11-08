using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Web.Pages.Events;

/// <summary>Opret, rediger og slet begivenheder. Alle opretter og retter egne; diktatorer og administratorer retter alle.</summary>
public class EditModel(AppDbContext db, UserManager<IdentityUser> userManager, TimeProvider time, ILogger<EditModel> logger) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public int? Id { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Giv begivenheden et navn.")]
        [StringLength(CalendarEvent.TitleMaxLength, ErrorMessage = "Navnet må højst være {1} tegn.")]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Vælg en startdato.")]
        public DateOnly? Date { get; set; }

        /// <summary>Tom = hele dagen.</summary>
        public TimeOnly? Time { get; set; }

        /// <summary>Tom = samme dag som start.</summary>
        public DateOnly? EndDate { get; set; }

        /// <summary>Tom = intet sluttidspunkt.</summary>
        public TimeOnly? EndTime { get; set; }

        [StringLength(CalendarEvent.BodyMaxLength, ErrorMessage = "Teksten må højst være {1} tegn.")]
        public string? Body { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? id, string? date)
    {
        if (id is null)
        {
            Input.Date = DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
            return Page();
        }

        var ev = await db.Events.FindAsync(id);
        if (ev is null) return NotFound();
        if (!CanEdit(ev)) return Forbid();

        Id = ev.Id;
        Input = new InputModel
        {
            Title = ev.Title,
            Date = DateOnly.FromDateTime(ev.StartsAt),
            Time = ev.HasTime ? TimeOnly.FromDateTime(ev.StartsAt) : null,
            EndDate = ev.IsMultiDay ? ev.EndDate : null,
            EndTime = ev.EndHasTime == true && ev.EndsAt is { } end ? TimeOnly.FromDateTime(end) : null,
            Body = ev.Body
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Id = id;
        if (Input.Date is { Year: < 2000 or > 2100 })
            ModelState.AddModelError("Input.Date", "Datoen skal ligge mellem år 2000 og 2100.");

        // Slut: sluttid uden slutdato betyder samme dag.
        var endDate = Input.EndDate ?? (Input.EndTime is not null ? Input.Date : null);
        if (Input.Date is { } startDate && endDate is { } ed)
        {
            if (ed < startDate)
                ModelState.AddModelError("Input.EndDate", "Slutdatoen skal være samme dag som starten eller senere.");
            else if (ed.DayNumber - startDate.DayNumber > CalendarEvent.MaxDays)
                ModelState.AddModelError("Input.EndDate", $"En begivenhed kan højst vare {CalendarEvent.MaxDays} dage.");
            else if (ed == startDate && Input.Time is { } st && Input.EndTime is { } et && et <= st)
                ModelState.AddModelError("Input.EndTime", "Sluttidspunktet skal være efter starttidspunktet.");
        }
        if (!ModelState.IsValid) return Page();

        CalendarEvent ev;
        if (id is null)
        {
            var user = await userManager.GetUserAsync(User);
            if (user is null) return Forbid();
            ev = new CalendarEvent
            {
                Title = "",
                CreatedById = user.Id,
                CreatedByEmail = user.Email ?? "",
                CreatedUtc = time.GetUtcNow().UtcDateTime
            };
            db.Events.Add(ev);
        }
        else
        {
            var existing = await db.Events.FindAsync(id);
            if (existing is null) return NotFound();
            if (!CanEdit(existing)) return Forbid();
            ev = existing;
            ev.UpdatedUtc = time.GetUtcNow().UtcDateTime;
        }

        ev.Title = Input.Title.Trim();
        ev.Body = (Input.Body ?? "").Replace("\r\n", "\n").Trim();
        ev.HasTime = Input.Time is not null;
        ev.StartsAt = Input.Date!.Value.ToDateTime(Input.Time ?? TimeOnly.MinValue);
        // Samme dag uden sluttid = ingen slut. Ellers gemmes slutdato (og evt. sluttid).
        if (endDate is null || (endDate == Input.Date && Input.EndTime is null))
        {
            ev.EndsAt = null;
            ev.EndHasTime = null;
        }
        else
        {
            ev.EndsAt = endDate.Value.ToDateTime(Input.EndTime ?? TimeOnly.MinValue);
            ev.EndHasTime = Input.EndTime is not null;
        }
        await db.SaveChangesAsync();

        logger.LogInformation("{User} {Action} begivenhed {Id}.", User.Identity?.Name, id is null ? "oprettede" : "redigerede", ev.Id);
        TempData["Status"] = id is null ? $"\"{ev.Title}\" er tilføjet." : $"\"{ev.Title}\" er gemt.";
        return BackToCalendar(ev);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, bool confirm)
    {
        var ev = await db.Events.FindAsync(id);
        if (ev is null) return NotFound();
        if (!CanEdit(ev)) return Forbid();
        if (!confirm)
        {
            TempData["Status"] = "Sæt flueben i Bekræft for at slette.";
            return RedirectToPage(new { id });
        }

        db.Events.Remove(ev);
        await db.SaveChangesAsync();
        logger.LogInformation("{User} slettede begivenhed {Id}.", User.Identity?.Name, id);
        TempData["Status"] = $"\"{ev.Title}\" er slettet.";
        return RedirectToPage("/Calendar", new { m = ev.StartsAt.ToString("yyyy-MM", CultureInfo.InvariantCulture) });
    }

    private bool CanEdit(CalendarEvent ev) => Roles.IsModerator(User) || ev.CreatedById == userManager.GetUserId(User);

    private RedirectResult BackToCalendar(CalendarEvent ev) =>
        Redirect($"{Url.Page("/Calendar", new { m = ev.StartsAt.ToString("yyyy-MM", CultureInfo.InvariantCulture), e = ev.Id })}#e-{ev.Id}");
}
