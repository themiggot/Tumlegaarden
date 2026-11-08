using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tumlegaarden.Web.Security;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages;

/// <summary>Indstillinger for sitet. Kun administratorer.</summary>
[Authorize(Policy = Policies.Administrator)]
public class SettingsModel(SiteSettings settings, ILogger<SettingsModel> logger) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Angiv en størrelse.")]
        [Range(1, PhotoStore.MaxAllowedMegabytes, ErrorMessage = "Vælg mellem {1} og {2} MB.")]
        public int? MaxImageMegabytes { get; set; }
    }

    public async Task OnGetAsync() => Input.MaxImageMegabytes = await settings.GetMaxImageMegabytesAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        await settings.SetMaxImageMegabytesAsync(Input.MaxImageMegabytes!.Value, User.Identity?.Name);
        logger.LogInformation("{Admin} satte maks. billedstørrelse til {Mb} MB.", User.Identity?.Name, Input.MaxImageMegabytes);
        TempData["Status"] = $"Gemt. Billeder må nu højst være {Input.MaxImageMegabytes} MB.";
        return RedirectToPage();
    }
}
