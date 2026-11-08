using System.Text;
using SkiaSharp;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Tests;

/// <summary>Billedbehandlingen ved upload: orientering, metadata, formater og afvisning af ikke-billeder.</summary>
public sealed class PhotoStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tumle-test-" + Guid.NewGuid().ToString("N"));
    private readonly PhotoStore _store;

    public PhotoStoreTests() => _store = new PhotoStore(new AppPaths(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static Stream Fixture(string name) => File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static bool IsRed(SKColor c) => c.Red > 170 && c.Green < 90 && c.Blue < 90;
    private static bool IsBlue(SKColor c) => c.Blue > 170 && c.Red < 90 && c.Green < 110;

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public async Task ExifOrientation_IsApplied(int orientation)
    {
        await using var input = Fixture($"orientation-{orientation}.jpg");
        var saved = await _store.SaveAsync(1, input);

        using var result = SKBitmap.Decode(_store.FilePath(1, saved.StorageName, "large"));
        Assert.Equal((120, 80), (result.Width, result.Height));
        Assert.Equal((120, 80), (saved.Width, saved.Height));
        Assert.True(IsRed(result.GetPixel(5, 5)), $"Rød markør skal være øverst til venstre, fandt {result.GetPixel(5, 5)}");
        Assert.True(IsBlue(result.GetPixel(114, 74)), $"Blå markør skal være nederst til højre, fandt {result.GetPixel(114, 74)}");
    }

    [Fact]
    public async Task Metadata_IsRemoved()
    {
        await using var input = Fixture("orientation-6.jpg");
        var saved = await _store.SaveAsync(1, input);

        foreach (var size in new[] { "large", "thumb" })
        {
            var bytes = await File.ReadAllBytesAsync(_store.FilePath(1, saved.StorageName, size));
            var text = Encoding.Latin1.GetString(bytes);
            Assert.DoesNotContain("Exif", text);
            Assert.DoesNotContain("TestKamera", text);
        }
    }

    [Fact]
    public async Task LargePhoto_IsScaledToExactly2048()
    {
        await using var input = Fixture("stort-4000x3000.jpg");
        var saved = await _store.SaveAsync(1, input);
        using var result = SKBitmap.Decode(_store.FilePath(1, saved.StorageName, "large"));
        Assert.Equal((2048, 1536), (result.Width, result.Height));
    }

    [Fact]
    public async Task Thumbnail_IsSquare()
    {
        await using var input = Fixture("orientation-1.jpg");
        var saved = await _store.SaveAsync(1, input);
        using var thumb = SKBitmap.Decode(_store.FilePath(1, saved.StorageName, "thumb"));
        Assert.Equal((400, 400), (thumb.Width, thumb.Height));
    }

    [Fact]
    public async Task Transparency_BecomesWhite()
    {
        await using var input = Fixture("transparent.png");
        var saved = await _store.SaveAsync(1, input);
        using var result = SKBitmap.Decode(_store.FilePath(1, saved.StorageName, "large"));
        var c = result.GetPixel(5, 20);
        Assert.True(c.Red > 240 && c.Green > 240 && c.Blue > 240, $"Gennemsigtigt område skal være hvidt, fandt {c}");
    }

    [Theory]
    [InlineData("animation.gif")]
    [InlineData("billede.webp")]
    [InlineData("transparent.png")]
    public async Task SupportedFormats_AreAccepted(string file)
    {
        await using var input = Fixture(file);
        var saved = await _store.SaveAsync(1, input);
        Assert.True(File.Exists(_store.FilePath(1, saved.StorageName, "large")));
        Assert.True(saved.Bytes > 0);
    }

    [Theory]
    [InlineData("ikke-et-billede.jpg")]
    [InlineData("format.bmp")]
    public async Task UnsupportedFiles_AreRejected(string file)
    {
        await using var input = Fixture(file);
        await Assert.ThrowsAsync<PhotoStore.RejectedException>(() => _store.SaveAsync(1, input));
        Assert.False(Directory.Exists(Path.Combine(_root, "albums", "1")) && Directory.EnumerateFiles(Path.Combine(_root, "albums", "1")).Any());
    }

    [Fact]
    public async Task Delete_RemovesBothFiles()
    {
        await using var input = Fixture("orientation-1.jpg");
        var saved = await _store.SaveAsync(1, input);
        _store.Delete(1, saved.StorageName);
        Assert.False(File.Exists(_store.FilePath(1, saved.StorageName, "large")));
        Assert.False(File.Exists(_store.FilePath(1, saved.StorageName, "thumb")));
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("0123456789abcdef0123456789abcdeg")]
    public void FilePath_RejectsUnsafeNames(string name) =>
        Assert.ThrowsAny<ArgumentException>(() => _store.FilePath(1, name, "large"));
}
