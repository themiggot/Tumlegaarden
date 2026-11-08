namespace Tumlegaarden.Web.Data;

public sealed class Album
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string Description { get; set; } = "";
    public required string CreatedById { get; set; }
    public required string CreatedByEmail { get; set; }
    public DateTime CreatedUtc { get; set; }

    public List<Photo> Photos { get; set; } = [];

    public const int TitleMaxLength = 100;
    public const int DescriptionMaxLength = 1000;
}

/// <summary>Et billede i et album. Filerne ligger i datamappen under albums/{AlbumId}/{StorageName}_{størrelse}.jpg.</summary>
public sealed class Photo
{
    public int Id { get; set; }
    public int AlbumId { get; set; }
    public Album? Album { get; set; }
    /// <summary>Tilfældigt filnavn (GUID). Brugerens filnavn bruges aldrig på disken.</summary>
    public required string StorageName { get; set; }
    public string OriginalName { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    /// <summary>Samlet størrelse af stor version + miniature på disken.</summary>
    public long Bytes { get; set; }
    public required string UploadedById { get; set; }
    public required string UploadedByEmail { get; set; }
    public DateTime UploadedUtc { get; set; }
}
