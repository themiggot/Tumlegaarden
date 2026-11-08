using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Web.Pages.Chat;

/// <summary>Opret chatrum og vælg medlemmer. Kun diktatorer og administratorer.</summary>
[Authorize(Policy = Policies.DiktatorOrHigher)]
public class RoomsModel(AppDbContext db, UserManager<IdentityUser> userManager, TimeProvider time, ILogger<RoomsModel> logger) : PageModel
{
    [BindProperty] public CreateInput Input { get; set; } = new();
    public List<ChatRoom> Rooms { get; private set; } = [];
    public List<(string Id, string Email)> Users { get; private set; } = [];

    public sealed class CreateInput
    {
        [Required(ErrorMessage = "Giv rummet et navn.")]
        [StringLength(ChatRoom.NameMaxLength, ErrorMessage = "Navnet må højst være {1} tegn.")]
        public string Name { get; set; } = "";
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }
        var user = await userManager.GetUserAsync(User);
        var room = new ChatRoom { Name = Input.Name.Trim(), CreatedByEmail = user?.Email ?? "", CreatedUtc = time.GetUtcNow().UtcDateTime };
        if (user is not null) room.Members.Add(new ChatRoomMember { UserId = user.Id });
        db.ChatRooms.Add(room);
        await db.SaveChangesAsync();
        logger.LogInformation("{User} oprettede chatrum {Room}.", User.Identity?.Name, room.Id);
        TempData["Status"] = $"Rummet \"{room.Name}\" er oprettet. Vælg hvem der har adgang.";
        return Redirect($"{Url.Page("/Chat/Rooms")}#room-{room.Id}");
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
        TempData["Status"] = $"Adgang til \"{room.Name}\" er gemt ({valid.Count} {(valid.Count == 1 ? "medlem" : "medlemmer")}).";
        return Redirect($"{Url.Page("/Chat/Rooms")}#room-{id}");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, bool confirm)
    {
        var room = await db.ChatRooms.FirstOrDefaultAsync(r => r.Id == id && !r.IsGlobal);
        if (room is null) return NotFound();
        if (!confirm)
        {
            TempData["Status"] = "Sæt flueben i Bekræft for at slette rummet.";
            return Redirect($"{Url.Page("/Chat/Rooms")}#room-{id}");
        }
        db.ChatRooms.Remove(room);
        await db.SaveChangesAsync();
        logger.LogInformation("{User} slettede chatrum {Room}.", User.Identity?.Name, id);
        TempData["Status"] = $"Rummet \"{room.Name}\" og alle dets beskeder er slettet.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Rooms = await db.ChatRooms.AsNoTracking().Include(r => r.Members).Where(r => !r.IsGlobal).OrderBy(r => r.Name).ToListAsync();
        Users = (await db.Users.AsNoTracking().OrderBy(u => u.Email).Select(u => new { u.Id, u.Email }).ToListAsync())
            .Select(u => (u.Id, u.Email ?? "")).ToList();
    }
}
