using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Tests;

/// <summary>To-faktor-hemmeligheder: krypteret nøgle, hashede gendannelseskoder og konvertering af gammel klartekst.</summary>
public sealed class ProtectedUserStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider _services = null!;
    private IServiceScope _scope = null!;
    private UserManager<IdentityUser> _users = null!;
    private AppDbContext _db = null!;
    private IdentityUser _user = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddUserStore<ProtectedUserStore>()
            .AddDefaultTokenProviders();
        _services = services.BuildServiceProvider();
        _scope = _services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await _db.Database.EnsureCreatedAsync();
        _users = _scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        _user = new IdentityUser { UserName = "anton@tumlegaarden.dk", Email = "anton@tumlegaarden.dk", EmailConfirmed = true };
        Assert.True((await _users.CreateAsync(_user)).Succeeded);
    }

    public async Task DisposeAsync()
    {
        _scope.Dispose();
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<string?> RawTokenAsync(string name) =>
        await _db.Set<IdentityUserToken<string>>().AsNoTracking()
            .Where(t => t.UserId == _user.Id && t.Name == name).Select(t => t.Value).SingleOrDefaultAsync();

    [Fact]
    public async Task AuthenticatorKey_IsEncryptedInDatabase_ButReadableThroughIdentity()
    {
        await _users.ResetAuthenticatorKeyAsync(_user);
        var key = await _users.GetAuthenticatorKeyAsync(_user);
        var raw = await RawTokenAsync(TwoFactorSecrets.AuthenticatorKeyName);

        Assert.NotNull(key);
        Assert.Equal(32, key!.Length);                // base32-nøgle som Identity laver den
        Assert.StartsWith("dp1:", raw);
        Assert.DoesNotContain(key, raw);
    }

    [Fact]
    public async Task RecoveryCodes_AreHashed_AndWorkOnlyOnce()
    {
        var codes = (await _users.GenerateNewTwoFactorRecoveryCodesAsync(_user, 10))!.ToList();
        var raw = await RawTokenAsync(TwoFactorSecrets.RecoveryCodesName);

        Assert.StartsWith("h1:", raw);
        Assert.All(codes, c => Assert.DoesNotContain(c, raw));
        Assert.Equal(10, await _users.CountRecoveryCodesAsync(_user));

        Assert.True((await _users.RedeemTwoFactorRecoveryCodeAsync(_user, codes[3])).Succeeded);
        Assert.False((await _users.RedeemTwoFactorRecoveryCodeAsync(_user, codes[3])).Succeeded);   // kun én gang
        Assert.Equal(9, await _users.CountRecoveryCodesAsync(_user));
        Assert.False((await _users.RedeemTwoFactorRecoveryCodeAsync(_user, "AAAAA-BBBBB")).Succeeded);
    }

    [Fact]
    public async Task RecoveryCode_IgnoresCaseSpacesAndDash()
    {
        var code = (await _users.GenerateNewTwoFactorRecoveryCodesAsync(_user, 3))!.First();
        var sloppy = " " + code.Replace("-", " ").ToLowerInvariant() + " ";
        Assert.True((await _users.RedeemTwoFactorRecoveryCodeAsync(_user, sloppy)).Succeeded);
    }

    [Fact]
    public async Task NewCodes_ReplaceOldCodes()
    {
        var old = (await _users.GenerateNewTwoFactorRecoveryCodesAsync(_user, 10))!.First();
        await _users.GenerateNewTwoFactorRecoveryCodesAsync(_user, 10);
        Assert.False((await _users.RedeemTwoFactorRecoveryCodeAsync(_user, old)).Succeeded);
    }

    [Fact]
    public async Task Migration_ConvertsPlaintextValues_AndTheyStillWork()
    {
        // Sådan lå værdierne i databasen før kryptering.
        const string plainKey = "JT4W65KD5TUP2ARAJCY5GOFL2YTNCMWD";
        _db.Set<IdentityUserToken<string>>().AddRange(
            new IdentityUserToken<string> { UserId = _user.Id, LoginProvider = TwoFactorSecrets.LoginProvider, Name = TwoFactorSecrets.AuthenticatorKeyName, Value = plainKey },
            new IdentityUserToken<string> { UserId = _user.Id, LoginProvider = TwoFactorSecrets.LoginProvider, Name = TwoFactorSecrets.RecoveryCodesName, Value = "BG9CD-4W2GY;Y3VBC-F8RNJ;6VXHT-52VF3" });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var dp = _scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        Assert.Equal(2, await TwoFactorSecretMigrator.MigrateAsync(_db, dp, NullLogger.Instance));
        Assert.Equal(0, await TwoFactorSecretMigrator.MigrateAsync(_db, dp, NullLogger.Instance));   // kan gentages uden skade
        _db.ChangeTracker.Clear();

        Assert.StartsWith("dp1:", await RawTokenAsync(TwoFactorSecrets.AuthenticatorKeyName));
        Assert.DoesNotContain("Y3VBC", await RawTokenAsync(TwoFactorSecrets.RecoveryCodesName));

        var user = (await _users.FindByIdAsync(_user.Id))!;
        Assert.Equal(plainKey, await _users.GetAuthenticatorKeyAsync(user));
        Assert.Equal(3, await _users.CountRecoveryCodesAsync(user));
        Assert.True((await _users.RedeemTwoFactorRecoveryCodeAsync(user, "Y3VBC-F8RNJ")).Succeeded);
        Assert.Equal(2, await _users.CountRecoveryCodesAsync(user));
    }

    [Fact]
    public async Task UnmigratedPlaintextCodes_StillWork_AndAreHashedOnUse()
    {
        _db.Set<IdentityUserToken<string>>().Add(new IdentityUserToken<string>
            { UserId = _user.Id, LoginProvider = TwoFactorSecrets.LoginProvider, Name = TwoFactorSecrets.RecoveryCodesName, Value = "AAAAA-11111;BBBBB-22222" });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var user = (await _users.FindByIdAsync(_user.Id))!;
        Assert.True((await _users.RedeemTwoFactorRecoveryCodeAsync(user, "AAAAA-11111")).Succeeded);
        _db.ChangeTracker.Clear();
        var raw = await RawTokenAsync(TwoFactorSecrets.RecoveryCodesName);
        Assert.StartsWith("h1:", raw);
        Assert.DoesNotContain("BBBBB", raw);
    }
}
