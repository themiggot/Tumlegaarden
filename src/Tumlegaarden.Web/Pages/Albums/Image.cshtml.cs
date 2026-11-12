using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Albums;

/// <summary>
/// Udleverer billedfiler fra datamappen – kun til brugere der er logget ind (fallback-politikken).
/// Filerne ligger uden for web-mappen og kan ikke hentes direkte.
/// </summary>
public class ImageModel(AppDbContext db, PhotoStore store) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id, string size)
    {
        // Rutens regex er versalufølsom ("THUMB" slipper igennem), så normalisér og afvis alt andet her.
        size = size.ToLowerInvariant();
        if (size is not ("thumb" or "large")) return NotFound();

        var photo = await db.Photos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (photo is null) return NotFound();

        var path = store.FilePath(photo.AlbumId, photo.StorageName, size);
        if (!System.IO.File.Exists(path)) return NotFound();

        // Indholdet for et id ændrer sig aldrig. "private": kun browserens egen cache, aldrig delte proxyer.
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        return PhysicalFile(path, "image/jpeg");
    }
}
