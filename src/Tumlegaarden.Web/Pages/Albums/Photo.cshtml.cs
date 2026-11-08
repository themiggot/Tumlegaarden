using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Albums;

/// <summary>Et billede i stor størrelse. Kan slettes af den der uploadede det, albummets ejer, diktatorer og administratorer.</summary>
public class PhotoModel(AppDbContext db, UserManager<IdentityUser> userManager, PhotoStore store, ILogger<PhotoModel> logger) : PageModel
{
    public Photo Photo { get; private set; } = null!;
    public int? PreviousId { get; private set; }
    public int? NextId { get; private set; }
    public int Position { get; private set; }
    public int Total { get; private set; }
    public bool CanDelete { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        var ids = await db.Photos.Where(p => p.AlbumId == Photo.AlbumId).OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();
        var index = ids.IndexOf(id);
        Position = index + 1;
        Total = ids.Count;
        PreviousId = index > 0 ? ids[index - 1] : null;
        NextId = index < ids.Count - 1 ? ids[index + 1] : null;
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanDelete) return Forbid();

        var photo = await db.Photos.FindAsync(id);
        db.Photos.Remove(photo!);
        await db.SaveChangesAsync();
        store.Delete(photo!.AlbumId, photo.StorageName);
        logger.LogInformation("{User} slettede billede {PhotoId} fra album {AlbumId}.", User.Identity?.Name, id, photo.AlbumId);
        TempData["Status"] = "Billedet er slettet.";
        return RedirectToPage("/Albums/Album", new { id = photo.AlbumId });
    }

    private async Task<bool> LoadAsync(int id)
    {
        var photo = await db.Photos.AsNoTracking().Include(p => p.Album).FirstOrDefaultAsync(p => p.Id == id);
        if (photo is null) return false;
        Photo = photo;
        var userId = userManager.GetUserId(User);
        CanDelete = Roles.IsModerator(User) || photo.UploadedById == userId || photo.Album!.CreatedById == userId;
        return true;
    }
}
