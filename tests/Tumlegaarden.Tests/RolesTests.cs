using System.Security.Claims;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Tests;

public class RolesTests
{
    private static ClaimsPrincipal As(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));

    [Theory]
    [InlineData(Roles.Administrator, Roles.Administrator, true)]   // admin kan redigere andre admins
    [InlineData(Roles.Administrator, Roles.Diktator, true)]
    [InlineData(Roles.Administrator, Roles.Borler, true)]
    [InlineData(Roles.Diktator, Roles.Administrator, false)]
    [InlineData(Roles.Diktator, Roles.Diktator, false)]           // kun lavere niveau
    [InlineData(Roles.Diktator, Roles.Borler, true)]
    [InlineData(Roles.Borler, Roles.Borler, false)]
    public void CanManage_FollowsRank(string actor, string target, bool expected) =>
        Assert.Equal(expected, Roles.CanManage(As(actor), target));

    [Fact]
    public void Assignable_DiktatorCannotGrantAdministrator() =>
        Assert.Equal([Roles.Diktator, Roles.Borler], Roles.Assignable(As(Roles.Diktator)));

    [Fact]
    public void Assignable_AdministratorCanGrantAll() =>
        Assert.Equal(Roles.All, Roles.Assignable(As(Roles.Administrator)));

    [Fact]
    public void Assignable_BorlerCannotGrantAnything() =>
        Assert.Empty(Roles.Assignable(As(Roles.Borler)));

    [Theory]
    [InlineData(Roles.Administrator, true)]
    [InlineData(Roles.Diktator, true)]
    [InlineData(Roles.Borler, false)]
    public void IsModerator(string role, bool expected) => Assert.Equal(expected, Roles.IsModerator(As(role)));
}
