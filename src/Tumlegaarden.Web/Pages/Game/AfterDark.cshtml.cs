using Microsoft.AspNetCore.Identity;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Game;

/// <summary>Ordjagt After Dark: bandeord og frække ord, eget dagens ord og egen highscore.</summary>
public class AfterDarkModel(AppDbContext db, UserManager<IdentityUser> userManager, GameModes games, SiteSettings settings, TimeProvider time, ILogger<AfterDarkModel> logger)
    : DailyGameModel<AfterDarkRound>(db, userManager, settings, time, logger)
{
    public override GameMode Mode => games.AfterDark;
    protected override AfterDarkRound NewRound(string userId, string email, DateTime date) => new() { UserId = userId, UserEmail = email, Date = date };
}
