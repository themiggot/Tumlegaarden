using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Tumlegaarden.Web.Services;

/// <summary>
/// Gemmer uploadede billeder sikkert: tjekker at det er et rigtigt billede (JPEG, PNG, WebP, GIF),
/// afviser kæmpebilleder, retter orientering, fjerner metadata (GPS, kamera m.m.) og gemmer
/// en ny JPEG i to størrelser. Den originale fil gemmes aldrig.
/// </summary>
public sealed class PhotoStore(AppPaths paths)
{
    public const long MaxFileBytes = 25 * 1024 * 1024;
    public const int MaxFilesPerUpload = 30;
    public const long MaxRequestBytes = 200 * 1024 * 1024;
    private const long MaxPixels = 50_000_000;
    private const int LargeEdge = 2048;
    private const int ThumbEdge = 400;

    public sealed record Saved(string StorageName, int Width, int Height, long Bytes);

    public sealed class RejectedException(string message) : Exception(message);

    public async Task<Saved> SaveAsync(int albumId, Stream input, CancellationToken ct = default)
    {
        ImageInfo info;
        try
        {
            info = await Image.IdentifyAsync(input, ct);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new RejectedException("ikke et understøttet billede (brug JPEG, PNG, WebP eller GIF)");
        }

        if (info.Metadata.DecodedImageFormat is not (JpegFormat or PngFormat or WebpFormat or GifFormat))
            throw new RejectedException("ikke et understøttet billede (brug JPEG, PNG, WebP eller GIF)");
        if ((long)info.Width * info.Height > MaxPixels)
            throw new RejectedException("billedet er for stort (højst 50 megapixel)");

        input.Position = 0;
        var options = new DecoderOptions { TargetSize = new Size(LargeEdge, LargeEdge), MaxFrames = 1 };
        using var image = await Image.LoadAsync<Rgb24>(options, input, ct);

        image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(LargeEdge, LargeEdge) }));
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        var name = Guid.NewGuid().ToString("N");
        var dir = AlbumDirectory(albumId);
        Directory.CreateDirectory(dir);

        var large = FilePath(albumId, name, "large");
        var thumb = FilePath(albumId, name, "thumb");
        try
        {
            await image.SaveAsJpegAsync(large, new JpegEncoder { Quality = 85 }, ct);
            using (var small = image.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Crop, Size = new Size(ThumbEdge, ThumbEdge) })))
            {
                await small.SaveAsJpegAsync(thumb, new JpegEncoder { Quality = 78 }, ct);
            }
        }
        catch
        {
            TryDelete(large);
            TryDelete(thumb);
            throw;
        }

        return new Saved(name, image.Width, image.Height, new FileInfo(large).Length + new FileInfo(thumb).Length);
    }

    public string FilePath(int albumId, string storageName, string size)
    {
        if (size is not ("large" or "thumb")) throw new ArgumentOutOfRangeException(nameof(size));
        if (storageName.Length != 32 || !storageName.All(Uri.IsHexDigit)) throw new ArgumentException("Ugyldigt filnavn.", nameof(storageName));
        return Path.Combine(AlbumDirectory(albumId), $"{storageName}_{size}.jpg");
    }

    public void Delete(int albumId, string storageName)
    {
        TryDelete(FilePath(albumId, storageName, "large"));
        TryDelete(FilePath(albumId, storageName, "thumb"));
    }

    public void DeleteAlbum(int albumId)
    {
        var dir = AlbumDirectory(albumId);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private string AlbumDirectory(int albumId) => Path.Combine(paths.Albums, albumId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
