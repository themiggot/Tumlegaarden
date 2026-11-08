using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tumlegaarden.Web.Pages;

// Ingen attribut: fallback-politikken kræver login + to-faktor.
public class IndexModel : PageModel
{
    public void OnGet() { }
}
