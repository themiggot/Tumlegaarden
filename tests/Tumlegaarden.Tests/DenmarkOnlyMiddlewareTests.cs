using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Tests;

public class DenmarkOnlyMiddlewareTests
{
    private static readonly CountryIpFilter Denmark =
        CountryIpFilter.Load(Path.Combine(AppContext.BaseDirectory, "GeoData", "dk-ip-ranges.csv"));

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<(int Status, bool Passed)> Request(string ip, SecurityOptions? options = null, CountryIpFilter? filter = null)
    {
        var passed = false;
        var middleware = new DenmarkOnlyMiddleware(
            _ => { passed = true; return Task.CompletedTask; },
            filter ?? Denmark,
            Options.Create(options ?? new SecurityOptions()),
            new Env(),
            NullLogger<DenmarkOnlyMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);
        return (context.Response.StatusCode, passed);
    }

    [Fact]
    public async Task Danish_Ip_PassesThrough()
    {
        var (status, passed) = await Request("2.104.10.20");
        Assert.True(passed);
        Assert.Equal(200, status);
    }

    [Fact]
    public async Task Foreign_Ip_Gets403()
    {
        var (status, passed) = await Request("8.8.8.8");
        Assert.False(passed);
        Assert.Equal(403, status);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("192.168.1.10")]
    public async Task Local_Ip_PassesThrough(string ip) => Assert.True((await Request(ip)).Passed);

    [Fact]
    public async Task AllowList_LetsForeignIpThrough()
    {
        var options = new SecurityOptions { AllowedIpRanges = ["8.8.8.0/24", "1.1.1.1"] };
        Assert.True((await Request("8.8.8.8", options)).Passed);
        Assert.True((await Request("1.1.1.1", options)).Passed);
        Assert.False((await Request("1.1.1.2", options)).Passed);
    }

    [Fact]
    public async Task MissingData_BlocksEverythingExternal() =>
        Assert.Equal(403, (await Request("2.104.10.20", filter: CountryIpFilter.Empty)).Status);

    [Fact]
    public async Task Disabled_LetsEveryoneThrough() =>
        Assert.True((await Request("8.8.8.8", new SecurityOptions { DenmarkOnly = false })).Passed);
}
