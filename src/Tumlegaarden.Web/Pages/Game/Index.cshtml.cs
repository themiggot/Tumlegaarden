using Microsoft.AspNetCore.Identity;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Game;

/// <summary>Ordjagt: dagens ord. Logikken ligger i <see cref="DailyGameModel{TRound}"/>.</summary>
public class IndexModel(AppDbContext db, UserManager<IdentityUser> userManager, GameModes games, SiteSettings settings, TimeProvider time, ILogger<IndexModel> logger)
    : DailyGameModel<GameRound>(db, userManager, settings, time, logger)
{
    public override GameMode Mode => games.Ordjagt;
    protected override GameRound NewRound(string userId, string email, DateTime date) => new() { UserId = userId, UserEmail = email, Date = date };
}
