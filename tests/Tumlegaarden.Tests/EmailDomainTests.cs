using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Tests;

public class EmailDomainTests
{
    [Theory]
    [InlineData("anton@tumlegaarden.dk", true)]
    [InlineData("Anton@TUMLEGAARDEN.DK", true)]
    [InlineData("anton@gmail.com", false)]
    [InlineData("anton@mail.tumlegaarden.dk", false)]
    [InlineData("anton@tumlegaarden.dk.evil.com", false)]
    [InlineData("a@b@tumlegaarden.dk", false)]
    [InlineData(" anton@tumlegaarden.dk", false)]
    [InlineData("Anton <anton@tumlegaarden.dk>", false)]
    [InlineData("", false)]
    public void OnlyExactDomain(string email, bool allowed) => Assert.Equal(allowed, EmailDomain.IsAllowed(email, "tumlegaarden.dk"));
}
