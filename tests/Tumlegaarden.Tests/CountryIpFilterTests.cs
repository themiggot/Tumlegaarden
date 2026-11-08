using System.Net;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Tests;

public class CountryIpFilterTests
{
    private static readonly CountryIpFilter Denmark =
        CountryIpFilter.Load(Path.Combine(AppContext.BaseDirectory, "GeoData", "dk-ip-ranges.csv"));

    [Fact]
    public void DataFile_HasDanishRanges() => Assert.True(Denmark.RangeCount > 1000);

    [Theory]
    [InlineData("2.104.10.20")]             // TDC
    [InlineData("194.239.1.1")]             // TDC
    [InlineData("::ffff:2.104.10.20")]      // IPv4-mappet IPv6
    [InlineData("2001:668:1f:fcaa::1")]
    public void Danish_Addresses_AreAllowed(string ip) => Assert.True(Denmark.Contains(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("8.8.8.8")]                 // USA
    [InlineData("1.1.1.1")]                 // Australien
    [InlineData("81.2.69.142")]             // Storbritannien
    [InlineData("2001:4860:4860::8888")]    // USA, IPv6
    public void Foreign_Addresses_AreBlocked(string ip) => Assert.False(Denmark.Contains(IPAddress.Parse(ip)));

    [Fact]
    public void Ranges_AreInclusive_AndMerged()
    {
        var filter = CountryIpFilter.Parse(["# kommentar", "10.0.0.0,10.0.0.9", "10.0.0.10,10.0.0.20", "10.0.0.15,10.0.0.30", "bad,line"]);
        Assert.Equal(1, filter.RangeCount);
        Assert.True(filter.Contains(IPAddress.Parse("10.0.0.0")));
        Assert.True(filter.Contains(IPAddress.Parse("10.0.0.30")));
        Assert.False(filter.Contains(IPAddress.Parse("10.0.0.31")));
        Assert.False(filter.Contains(IPAddress.Parse("9.255.255.255")));
    }

    [Fact]
    public void Empty_ContainsNothing() => Assert.False(CountryIpFilter.Empty.Contains(IPAddress.Parse("2.104.10.20")));
}
