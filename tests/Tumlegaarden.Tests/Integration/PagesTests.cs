using System.Net;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Tests.Integration;

/// <summary>Alle sider kan vises, kræver login og respekterer roller.</summary>
public sealed class PagesTests(TestApp app) : IClassFixture<TestApp>
{
    public static TheoryData<string> MemberPages =>
    [
        "/", "/Calendar", "/Events/Edit", "/Albums", "/Chat", "/Users", "/Cookies",
        "/Ordjagt", "/Ordjagt/Oev", "/AfterDark", "/AfterDark/Oev",
        "/Spil/Gaade", "/Spil/Kode", "/Spil/Kort", "/Spil/Anagram",
        "/Quiz", "/Quiz/Edit"
    ];

    [Theory]
    [MemberData(nameof(MemberPages))]
    public async Task MemberPages_RenderForBorler(string url)
    {
        await EnsureUserAsync("sider-borler", Roles.Borler);
        var html = await app.ClientFor("sider-borler").PageAsync(url);
        Assert.Contains("<nav class=\"topnav\"", html);
        Assert.Contains("Log ud", html);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Spil/Gaade")]
    [InlineData("/Quiz")]
    [InlineData("/Ordjagt")]
    public async Task MemberPages_RequireLogin(string url)
    {
        var response = await app.ClientFor(null).GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task PublicPages_WorkWithoutLogin()
    {
        var anonymous = app.ClientFor(null);
        Assert.Contains("Cookies og privatliv", await anonymous.PageAsync("/Cookies"));
        Assert.Contains("Log ind", await anonymous.PageAsync("/Account/Login"));
    }

    [Fact]
    public async Task Settings_OnlyForDiktatorAndAdmin()
    {
        await EnsureUserAsync("sider-borler", Roles.Borler);
        await EnsureUserAsync("sider-diktator", Roles.Diktator);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.ClientFor("sider-borler").GetAsync("/Settings")).StatusCode);
        Assert.Contains("Indstillinger", await app.ClientFor("sider-diktator").PageAsync("/Settings"));
    }

    [Fact]
    public async Task GameMenu_ListsAllGames()
    {
        await EnsureUserAsync("sider-borler", Roles.Borler);
        var html = await app.ClientFor("sider-borler").PageAsync("/");
        foreach (var path in new[] { "/Ordjagt", "/AfterDark", "/Spil/Gaade", "/Spil/Kode", "/Spil/Kort", "/Spil/Anagram", "/Quiz" })
            Assert.Contains($"href=\"{path}\"", html);
    }

    [Fact]
    public async Task SecurityHeaders_ArePresent()
    {
        await EnsureUserAsync("sider-borler", Roles.Borler);
        var response = await app.ClientFor("sider-borler").GetAsync("/Spil/Kode");
        Assert.Contains("script-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    private async Task EnsureUserAsync(string name, string role)
    {
        if (app.Db(db => db.Users.Any(u => u.UserName == $"{name}@tumlegaarden.dk"))) return;
        try { await app.CreateUserAsync(name, role); }
        catch (InvalidOperationException) { }   // en anden test nåede det samtidig
    }
}
