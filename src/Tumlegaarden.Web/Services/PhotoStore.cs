using SkiaSharp;

namespace Tumlegaarden.Web.Services;

/// <summary>
/// Gemmer uploadede billeder sikkert med SkiaSharp (MIT): tjekker at det er et rigtigt billede
/// (JPEG, PNG, WebP, GIF), afviser kæmpebilleder, retter orientering, og gemmer en ny JPEG i to
/// størrelser. Den nye JPEG indeholder ingen metadata (GPS, kamera m.m.), og originalen gemmes aldrig.
/// </summary>
public sealed class PhotoStore(AppPaths paths)
{
    /// <summary>Højeste værdi en administrator kan vælge for maks. billedstørrelse.</summary>
    public const int MaxAllowedMegabytes = 25;
    public const int MaxFilesPerUpload = 30;
    public const long MaxRequestBytes = 200 * 1024 * 1024;
    private const long MaxPixels = 50_000_000;
    private const int LargeEdge = 2048;
    private const int ThumbEdge = 400;
    private const int LargeQuality = 85;
    private const int ThumbQuality = 78;

    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    public sealed record Saved(string StorageName, int Width, int Height, long Bytes);

    public sealed class RejectedException(string message) : Exception(message);

    private const string NotAnImage = "ikke et understøttet billede (brug JPEG, PNG, WebP eller GIF)";

    public async Task<Saved> SaveAsync(int albumId, Stream input, CancellationToken ct = default)
    {
        // Læs filen ind (højst den tilladte størrelse) – Skia skal kunne søge i data.
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        using var data = SKData.CreateCopy(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        using var codec = SKCodec.Create(data) ?? throw new RejectedException(NotAnImage);

        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp or SKEncodedImageFormat.Gif))
            throw new RejectedException(NotAnImage);
        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
            throw new RejectedException("billedet er for stort (højst 50 megapixel)");

        using var decoded = Decode(codec);
        using var oriented = Orient(decoded, codec.EncodedOrigin);
        using var large = FitWithin(oriented, LargeEdge);
        using var thumb = CropSquare(large, ThumbEdge);

        var name = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(AlbumDirectory(albumId));
        var largePath = FilePath(albumId, name, "large");
        var thumbPath = FilePath(albumId, name, "thumb");
        try
        {
            await WriteJpegAsync(large, largePath, LargeQuality, ct);
            await WriteJpegAsync(thumb, thumbPath, ThumbQuality, ct);
        }
        catch
        {
            TryDelete(largePath);
            TryDelete(thumbPath);
            throw;
        }

        return new Saved(name, large.Width, large.Height, new FileInfo(largePath).Length + new FileInfo(thumbPath).Length);
    }

    /// <summary>Afkoder (kun første billede i en GIF). JPEG afkodes direkte i mindre størrelse for at spare hukommelse.</summary>
    private static SKBitmap Decode(SKCodec codec)
    {
        // JPEG kan afkodes i 1/8, 2/8 … 8/8 størrelse. Vælg den mindste, der stadig er mindst LargeEdge,
        // så billedet ikke bliver mindre end 2048 px – den præcise nedskalering sker bagefter.
        var size = codec.Info.Size;
        if (Math.Max(size.Width, size.Height) > LargeEdge)
        {
            for (var eighths = 1; eighths < 8; eighths++)
            {
                var candidate = codec.GetScaledDimensions(eighths / 8f);
                if (Math.Max(candidate.Width, candidate.Height) >= LargeEdge)
                {
                    size = candidate;
                    break;
                }
            }
        }
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw new RejectedException(NotAnImage);
        }
        return bitmap;
    }

    /// <summary>Drejer/spejler efter EXIF-orientering og lægger billedet på hvid baggrund (JPEG har ingen gennemsigtighed).</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width, h = source.Height;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        // x' = ScaleX·x + SkewX·y + TransX,  y' = SkewY·x + ScaleY·y + TransY
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),     // spejlet vandret
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), // 180°
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),   // spejlet lodret
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),       // transponeret
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),     // 90° med uret
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), // transverseret
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),   // 90° mod uret
            _ => SKMatrix.Identity
        };

        var result = new SKBitmap(new SKImageInfo(swap ? source.Height : source.Width, swap ? source.Width : source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.White);
        canvas.SetMatrix(matrix);
        using var image = SKImage.FromBitmap(source);
        // Kun drejning/spejling i hele pixels – ingen udglatning nødvendig.
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return result;
    }

    private static SKBitmap FitWithin(SKBitmap source, int edge)
    {
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= edge) return source.Copy();
        var scale = (double)edge / longest;
        var info = new SKImageInfo(Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)), SKColorType.Rgba8888, SKAlphaType.Premul);
        return source.Resize(info, Sampling) ?? throw new RejectedException("billedet kunne ikke skaleres");
    }

    /// <summary>Kvadratisk miniature fra midten af billedet.</summary>
    private static SKBitmap CropSquare(SKBitmap source, int edge)
    {
        var side = Math.Min(source.Width, source.Height);
        var src = SKRect.Create((source.Width - side) / 2f, (source.Height - side) / 2f, side, side);
        var result = new SKBitmap(new SKImageInfo(edge, edge, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        using var image = SKImage.FromBitmap(source);
        canvas.Clear(SKColors.White);
        canvas.DrawImage(image, src, SKRect.Create(edge, edge), Sampling);
        return result;
    }

    private static async Task WriteJpegAsync(SKBitmap bitmap, string path, int quality, CancellationToken ct)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality) ?? throw new InvalidOperationException("JPEG-kodning fejlede.");
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.WriteAsync(encoded.AsSpan().ToArray(), ct);
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
