using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Albums;

/// <summary>
/// Et album. Alle kan se det og uploade billeder til det.
/// Ejeren, diktatorer og administratorer kan omdøbe og slette albummet.
/// </summary>
[RequestSizeLimit(PhotoStore.MaxRequestBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = PhotoStore.MaxRequestBytes)]
public class AlbumModel(AppDbContext db, UserManager<IdentityUser> userManager, PhotoStore store, TimeProvider time, ILogger<AlbumModel> logger) : PageModel
{
    public Album Album { get; private set; } = null!;
    [BindProperty] public EditInput Edit { get; set; } = new();
    public bool CanManage { get; private set; }
    public List<string> UploadErrors { get; private set; } = [];

    public sealed class EditInput
    {
        [Required(ErrorMessage = "Giv albummet et navn.")]
        [StringLength(Data.Album.TitleMaxLength, ErrorMessage = "Navnet må højst være {1} tegn.")]
        public string Title { get; set; } = "";

        [StringLength(Data.Album.DescriptionMaxLength, ErrorMessage = "Beskrivelsen må højst være {1} tegn.")]
        public string? Description { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        Edit = new EditInput { Title = Album.Title, Description = Album.Description };
        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(int id, List<IFormFile> files, CancellationToken ct)
    {
        if (!await LoadAsync(id)) return NotFound();
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        if (files.Count == 0)
        {
            TempData["Status"] = "Vælg et eller flere billeder først.";
            return RedirectToPage(new { id });
        }

        var errors = new List<string>();
        var saved = 0;
        foreach (var file in files.Take(PhotoStore.MaxFilesPerUpload))
        {
            var name = Path.GetFileName(file.FileName);
            if (file.Length == 0) { errors.Add($"{name}: tom fil"); continue; }
            if (file.Length > PhotoStore.MaxFileBytes) { errors.Add($"{name}: større end 25 MB"); continue; }

            try
            {
                await using var stream = file.OpenReadStream();
                var result = await store.SaveAsync(id, stream, ct);
                db.Photos.Add(new Photo
                {
                    AlbumId = id,
                    StorageName = result.StorageName,
                    OriginalName = name.Length > 255 ? name[..255] : name,
                    Width = result.Width,
                    Height = result.Height,
                    Bytes = result.Bytes,
                    UploadedById = user.Id,
                    UploadedByEmail = user.Email ?? "",
                    UploadedUtc = time.GetUtcNow().UtcDateTime
                });
                await db.SaveChangesAsync(ct);
                saved++;
            }
            catch (PhotoStore.RejectedException ex)
            {
                errors.Add($"{name}: {ex.Message}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Upload af {File} til album {AlbumId} fejlede.", name, id);
                errors.Add($"{name}: kunne ikke behandles");
            }
        }
        if (files.Count > PhotoStore.MaxFilesPerUpload)
            errors.Add($"Kun de første {PhotoStore.MaxFilesPerUpload} billeder blev behandlet. Upload resten bagefter.");

        logger.LogInformation("{User} uploadede {Count} billeder til album {AlbumId}.", User.Identity?.Name, saved, id);
        TempData["Status"] = saved switch { 0 => "Ingen billeder blev uploadet.", 1 => "1 billede er uploadet.", _ => $"{saved} billeder er uploadet." };
        if (errors.Count > 0) TempData["UploadErrors"] = string.Join("\n", errors);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEditAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanManage) return Forbid();
        if (!ModelState.IsValid) return Page();

        var album = await db.Albums.FindAsync(id);
        album!.Title = Edit.Title.Trim();
        album.Description = (Edit.Description ?? "").Replace("\r\n", "\n").Trim();
        await db.SaveChangesAsync();
        TempData["Status"] = "Albummet er gemt.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, bool confirm)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanManage) return Forbid();
        if (!confirm)
        {
            TempData["Status"] = "Sæt flueben i Bekræft for at slette albummet.";
            return RedirectToPage(new { id });
        }

        var album = await db.Albums.FindAsync(id);
        db.Albums.Remove(album!);
        await db.SaveChangesAsync();
        store.DeleteAlbum(id);
        logger.LogInformation("{User} slettede album {AlbumId} med {Count} billeder.", User.Identity?.Name, id, Album.Photos.Count);
        TempData["Status"] = $"Albummet \"{Album.Title}\" og dets {Album.Photos.Count} billeder er slettet.";
        return RedirectToPage("/Albums/Index");
    }

    private async Task<bool> LoadAsync(int id)
    {
        var album = await db.Albums.AsNoTracking().Include(a => a.Photos.OrderBy(p => p.Id)).FirstOrDefaultAsync(a => a.Id == id);
        if (album is null) return false;
        Album = album;
        CanManage = Roles.IsModerator(User) || album.CreatedById == userManager.GetUserId(User);
        if (TempData.Peek("UploadErrors") is string e) UploadErrors = e.Split('\n').ToList();
        return true;
    }
}
