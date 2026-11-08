using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tumlegaarden.Web.Pages;

/// <summary>Startsiden efter login. Fallback-politikken kræver login + to-faktor + en rolle.</summary>
public class IndexModel : PageModel
{
    public void OnGet() { }
}
