using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Chat;

/// <summary>
/// Chat som almindelig side (virker uden JavaScript) og JSON-endepunkter til chatboksen nederst til højre.
/// </summary>
[EnableRateLimiting("chat")]
public class IndexModel(ChatService chat, PresenceTracker presence, ILogger<IndexModel> logger) : PageModel
{
    public List<ChatRoom> Rooms { get; private set; } = [];
    public ChatRoom? Room { get; private set; }
    public List<ChatService.MessageDto> Messages { get; private set; } = [];
    public bool CanManage => Roles.IsModerator(User);

    public async Task<IActionResult> OnGetAsync(int? room)
    {
        Rooms = await chat.RoomsForAsync(User);
        Room = room is null ? Rooms.FirstOrDefault(r => r.IsGlobal) ?? Rooms.FirstOrDefault() : Rooms.FirstOrDefault(r => r.Id == room);
        if (Room is null) return room is null ? Page() : Forbid();
        Messages = await chat.MessagesAsync(User, Room.Id, 0, 100);
        return Page();
    }

    /// <summary>Rum brugeren har adgang til, med id på seneste besked (til ulæst-markering).</summary>
    public async Task<IActionResult> OnGetRoomsAsync() =>
        new JsonResult(new
        {
            rooms = await chat.RoomSummariesAsync(User),
            canManage = CanManage,
            online = presence.Online().Select(o => o.DisplayName)
        });

    public async Task<IActionResult> OnGetMessagesAsync(int room, long after = 0)
    {
        if (await chat.RoomForAsync(User, room) is null) return Forbid();
        return new JsonResult(new { messages = await chat.MessagesAsync(User, room, after) });
    }

    public async Task<IActionResult> OnPostSendAsync(int room, string? text)
    {
        var wantsJson = Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
        if (await chat.RoomForAsync(User, room) is null) return Forbid();

        var clean = ChatService.Clean(text);
        if (clean is null)
        {
            const string error = "Beskeden skal være mellem 1 og 1000 tegn.";
            if (wantsJson) return BadRequest(new { error });
            TempData["Status"] = error;
            return RedirectToPage(new { room });
        }

        var message = await chat.SendAsync(User, room, clean);
        logger.LogDebug("{User} skrev i rum {Room}.", User.Identity?.Name, room);
        return wantsJson ? new JsonResult(new { message }) : Redirect($"{Url.Page("/Chat/Index", new { room })}#chat-end");
    }
}
