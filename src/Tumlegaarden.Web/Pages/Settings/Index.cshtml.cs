using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Settings;

/// <summary>
/// Alle indstillinger for sitet samlet ét sted.
/// Albums: kun administratorer. Chatrum: diktatorer og administratorer.
/// </summary>
[Authorize(Policy = Policies.DiktatorOrHigher)]
public class IndexModel(
    SiteSettings settings,
    AppDbContext db,
    UserManager<IdentityUser> userManager,
    TimeProvider time,
    ILogger<IndexModel> logger) : PageModel
{
    public bool IsAdmin => User.IsInRole(Roles.Administrator);

    // --- Albums ---
    [BindProperty] public AlbumsInput Albums { get; set; } = new();

    public sealed class AlbumsInput
    {
        [Required(ErrorMessage = "Angiv en størrelse.")]
        [Range(1, PhotoStore.MaxAllowedMegabytes, ErrorMessage = "Vælg mellem {1} og {2} MB.")]
        public int? MaxImageMegabytes { get; set; }
    }

    // --- Chatrum ---
    [BindProperty] public RoomInput NewRoom { get; set; } = new();
    public List<ChatRoom> Rooms { get; private set; } = [];
    public List<(string Id, string Email)> Users { get; private set; } = [];

    public sealed class RoomInput
    {
        [Required(ErrorMessage = "Giv rummet et navn.")]
        [StringLength(ChatRoom.NameMaxLength, ErrorMessage = "Navnet må højst være {1} tegn.")]
        public string Name { get; set; } = "";
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAlbumsAsync()
    {
        if (!IsAdmin) return Forbid();
        ValidateOnly(Albums, nameof(Albums));
        if (!ModelState.IsValid)
        {
            await LoadAsync(keepAlbumsInput: true);
            return Page();
        }
        await settings.SetMaxImageMegabytesAsync(Albums.MaxImageMegabytes!.Value, User.Identity?.Name);
        logger.LogInformation("{Admin} satte maks. billedstørrelse til {Mb} MB.", User.Identity?.Name, Albums.MaxImageMegabytes);
        return Done($"Gemt. Billeder må nu højst være {Albums.MaxImageMegabytes} MB.", "albums");
    }

    public async Task<IActionResult> OnPostCreateRoomAsync()
    {
        ValidateOnly(NewRoom, nameof(NewRoom));
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }
        var user = await userManager.GetUserAsync(User);
        var room = new ChatRoom { Name = NewRoom.Name.Trim(), CreatedByEmail = user?.Email ?? "", CreatedUtc = time.GetUtcNow().UtcDateTime };
        if (user is not null) room.Members.Add(new ChatRoomMember { UserId = user.Id });
        db.ChatRooms.Add(room);
        await db.SaveChangesAsync();
        logger.LogInformation("{User} oprettede chatrum {Room}.", User.Identity?.Name, room.Id);
        return Done($"Rummet \"{room.Name}\" er oprettet. Vælg hvem der har adgang.", $"room-{room.Id}");
    }

    public async Task<IActionResult> OnPostMembersAsync(int id, string[] members)
    {
        var room = await db.ChatRooms.Include(r => r.Members).FirstOrDefaultAsync(r => r.Id == id && !r.IsGlobal);
        if (room is null) return NotFound();

        var valid = await db.Users.Where(u => members.Contains(u.Id)).Select(u => u.Id).ToListAsync();
        room.Members.RemoveAll(m => !valid.Contains(m.UserId));
        foreach (var userId in valid.Where(u => room.Members.All(m => m.UserId != u)))
            room.Members.Add(new ChatRoomMember { UserId = userId });
        await db.SaveChangesAsync();
        logger.LogInformation("{User} satte {Count} medlemmer i chatrum {Room}.", User.Identity?.Name, valid.Count, id);
        return Done($"Adgang til \"{room.Name}\" er gemt ({valid.Count} {(valid.Count == 1 ? "medlem" : "medlemmer")}).", $"room-{id}");
    }

    public async Task<IActionResult> OnPostDeleteRoomAsync(int id, bool confirm)
    {
        var room = await db.ChatRooms.FirstOrDefaultAsync(r => r.Id == id && !r.IsGlobal);
        if (room is null) return NotFound();
        if (!confirm) return Done("Sæt flueben i Bekræft for at slette rummet.", $"room-{id}");
        db.ChatRooms.Remove(room);
        await db.SaveChangesAsync();
        logger.LogInformation("{User} slettede chatrum {Room}.", User.Identity?.Name, id);
        return Done($"Rummet \"{room.Name}\" og alle dets beskeder er slettet.", "chatrum");
    }

    /// <summary>
    /// Siden har flere formularer. Valider kun den, der blev sendt – ellers giver de andre
    /// (tomme) formularer valideringsfejl, nogle gange endda uden præfiks i nøglen.
    /// </summary>
    private void ValidateOnly(object model, string prefix)
    {
        ModelState.Clear();
        TryValidateModel(model, prefix);
    }

    private RedirectResult Done(string message, string anchor)
    {
        TempData["Status"] = message;
        return Redirect($"{Url.Page("/Settings/Index")}#{anchor}");
    }

    private async Task LoadAsync(bool keepAlbumsInput = false)
    {
        if (IsAdmin && !keepAlbumsInput) Albums.MaxImageMegabytes = await settings.GetMaxImageMegabytesAsync();
        Rooms = await db.ChatRooms.AsNoTracking().Include(r => r.Members).Where(r => !r.IsGlobal).OrderBy(r => r.Name).ToListAsync();
        Users = (await db.Users.AsNoTracking().OrderBy(u => u.Email).Select(u => new { u.Id, u.Email }).ToListAsync())
            .Select(u => (u.Id, u.Email ?? "")).ToList();
    }
}
