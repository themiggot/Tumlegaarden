using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;

namespace Tumlegaarden.Web.Pages.Albums;

/// <summary>Alle brugere kan se alle albums og oprette nye.</summary>
public class IndexModel(AppDbContext db, UserManager<IdentityUser> userManager, TimeProvider time) : PageModel
{
    [BindProperty] public AlbumInput Input { get; set; } = new();
    public List<AlbumCard> Albums { get; private set; } = [];
    public long UsedBytes { get; private set; }

    public sealed record AlbumCard(int Id, string Title, string Description, int Count, int? CoverId, string CreatedByEmail);

    public sealed class AlbumInput
    {
        [Required(ErrorMessage = "Giv albummet et navn.")]
        [StringLength(Album.TitleMaxLength, ErrorMessage = "Navnet må højst være {1} tegn.")]
        public string Title { get; set; } = "";

        [StringLength(Album.DescriptionMaxLength, ErrorMessage = "Beskrivelsen må højst være {1} tegn.")]
        public string? Description { get; set; }
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
        if (user is null) return Forbid();
        var album = new Album
        {
            Title = Input.Title.Trim(),
            Description = (Input.Description ?? "").Replace("\r\n", "\n").Trim(),
            CreatedById = user.Id,
            CreatedByEmail = user.Email ?? "",
            CreatedUtc = time.GetUtcNow().UtcDateTime
        };
        db.Albums.Add(album);
        await db.SaveChangesAsync();
        TempData["Status"] = $"Albummet \"{album.Title}\" er oprettet. Upload billeder nedenfor.";
        return RedirectToPage("/Albums/Album", new { id = album.Id });
    }

    private async Task LoadAsync()
    {
        Albums = await db.Albums.AsNoTracking()
            .OrderByDescending(a => a.CreatedUtc)
            .Select(a => new AlbumCard(a.Id, a.Title, a.Description, a.Photos.Count,
                a.Photos.OrderBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefault(), a.CreatedByEmail))
            .ToListAsync();
        UsedBytes = await db.Photos.SumAsync(p => (long?)p.Bytes) ?? 0;
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.0} MB",
        _ => $"{bytes / 1024.0 / 1024 / 1024:0.00} GB"
    };
}
