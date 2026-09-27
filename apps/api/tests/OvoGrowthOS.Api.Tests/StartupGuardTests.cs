using Microsoft.Extensions.Configuration;
using OvoGrowthOS.Api;

namespace OvoGrowthOS.Api.Tests;

public sealed class StartupGuardTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Development_and_testing_keep_repo_defaults()
    {
        StartupGuard.Ensure(Config([]), "Development");
        StartupGuard.Ensure(Config([]), "Testing");
    }

    [Fact]
    public void Production_rejects_missing_or_blank_key()
    {
        Assert.Contains("JWT_KEY", Assert.Throws<InvalidOperationException>(() => StartupGuard.Ensure(Config([]), "Production")).Message);
        Assert.Contains("JWT_KEY", Assert.Throws<InvalidOperationException>(() => StartupGuard.Ensure(Config(new() { ["Jwt:Key"] = "   " }), "Production")).Message);
    }

    [Fact]
    public void Production_rejects_repo_default_or_short_key()
    {
        var defaultMessage = Assert.Throws<InvalidOperationException>(() => StartupGuard.Ensure(Config(new() { ["Jwt:Key"] = StartupGuard.DefaultJwtKey }), "Production")).Message;
        Assert.Contains("JWT_KEY", defaultMessage);
        Assert.Contains("en az 32 karakter", Assert.Throws<InvalidOperationException>(() => StartupGuard.Ensure(Config(new() { ["Jwt:Key"] = "too-short-for-jwt" }), "Production")).Message);
    }

    [Fact]
    public void Production_rejects_default_admin_hash()
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "rotated-key-with-more-than-thirty-two-chars",
            ["DefaultAdmin:PasswordHash"] = StartupGuard.DefaultAdminPasswordHash
        };
        Assert.Contains("DEFAULT_ADMIN_PASSWORD_HASH", Assert.Throws<InvalidOperationException>(() => StartupGuard.Ensure(Config(values), "Production")).Message);
    }

    [Fact]
    public void Production_accepts_rotated_key_and_custom_admin_hash()
    {
        StartupGuard.Ensure(Config(new()
        {
            ["Jwt:Key"] = "rotated-key-with-more-than-thirty-two-chars",
            ["DefaultAdmin:PasswordHash"] = "b3ZvLWdyb3d0aC1vcw==.rotated"
        }), "Production");
    }

    [Fact]
    public void UsesDefaultAdminHash_matches_configuration_value() =>
        Assert.True(StartupGuard.UsesDefaultAdminHash(Config(new() { ["DefaultAdmin:PasswordHash"] = StartupGuard.DefaultAdminPasswordHash })) &&
            !StartupGuard.UsesDefaultAdminHash(Config(new() { ["DefaultAdmin:PasswordHash"] = "b3ZvLWdyb3d0aC1vcw==.rotated" })));
}
