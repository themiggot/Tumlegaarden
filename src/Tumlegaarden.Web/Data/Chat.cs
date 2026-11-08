namespace Tumlegaarden.Web.Data;

/// <summary>Et chatrum. Det globale rum ("Alle") er for alle brugere; andre rum kun for medlemmer.</summary>
public sealed class ChatRoom
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsGlobal { get; set; }
    public string CreatedByEmail { get; set; } = "";
    public DateTime CreatedUtc { get; set; }

    public List<ChatRoomMember> Members { get; set; } = [];

    public const int NameMaxLength = 60;
}

public sealed class ChatRoomMember
{
    public int RoomId { get; set; }
    public required string UserId { get; set; }
    public ChatRoom? Room { get; set; }
}

public sealed class ChatMessage
{
    public long Id { get; set; }
    public int RoomId { get; set; }
    public required string UserId { get; set; }
    public required string UserEmail { get; set; }
    public required string Text { get; set; }
    public DateTime SentUtc { get; set; }

    public const int TextMaxLength = 1000;
}
