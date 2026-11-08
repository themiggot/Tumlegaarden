using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tumlegaarden.Web.Pages;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public int Code { get; private set; }

    public void OnGet(int? code) => Code = code ?? 500;
    public void OnPost(int? code) => Code = code ?? 500;
}
