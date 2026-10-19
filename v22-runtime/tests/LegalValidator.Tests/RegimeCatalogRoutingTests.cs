using Nd30.LegalValidator.Catalog;
using Xunit;

namespace Nd30.LegalValidator.Tests;

public sealed class RegimeCatalogRoutingTests
{
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                "release", "admin-nd30-verified-rc-v20.yaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Verified administrative release root not found.");
    }

    [Fact]
    public void Explicit_administrative_route_matches_original_435_rule_release()
    {
        var root = FindRoot();
        var original = RuleCatalog.LoadVerifiedRelease(root);
        var routed = VerifiedRegimeCatalogRouter.LoadForRegime(root, "administrative");
        Assert.Equal(435, routed.Rules.Count);
        Assert.Equal(original.ReleaseRuleIds.OrderBy(id => id, StringComparer.Ordinal),
            routed.ReleaseRuleIds.OrderBy(id => id, StringComparer.Ordinal));
        Assert.All(routed.Rules, rule =>
        {
            Assert.Equal("administrative", rule.Regime);
            Assert.Equal("verified", rule.Maturity);
            Assert.StartsWith("ND30.", rule.Id, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Party_source_registry_and_selector_cannot_be_used_as_a_verified_release()
    {
        var root = FindRoot();
        var selector = Path.GetFullPath(Path.Combine(root,
            "..", "rule-packs", "party-document-format-2026.yaml"));
        Assert.True(File.Exists(selector), "Party selector is expected to exist but is NOT a verified release.");
        var error = Assert.Throws<NotSupportedException>(
            () => VerifiedRegimeCatalogRouter.LoadForRegime(root, "party"));
        Assert.Contains("PARTY_VERIFIED_RELEASE_UNAVAILABLE", error.Message);
    }

    [Fact]
    public void Unreviewed_party_release_candidate_cannot_enable_a_new_runtime_path()
    {
        var root = Path.Combine(Path.GetTempPath(), $"p0a-deny-{Guid.NewGuid():N}");
        try
        {
            var release = Path.Combine(root, "release");
            Directory.CreateDirectory(release);
            File.WriteAllText(Path.Combine(release, "party-hd05-verified.yaml"),
                "{\"regime\":\"party\",\"rules\":[\"PARTY.DRAFT.001\"]}");
            var ex = Assert.Throws<NotSupportedException>(
                () => VerifiedRegimeCatalogRouter.LoadForRegime(root, "party"));
            Assert.Contains("PARTY_VERIFIED_RELEASE_UNAVAILABLE", ex.Message);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("administrative ")]
    [InlineData("ADMINISTRATIVE")]
    [InlineData("party ")]
    [InlineData("../release/admin-nd30-verified-rc-v20.yaml")]
    public void Unknown_or_noncanonical_regimes_never_fall_back_to_nd30(string regime)
    {
        var ex = Assert.Throws<NotSupportedException>(
            () => VerifiedRegimeCatalogRouter.LoadForRegime(FindRoot(), regime));
        Assert.Contains("UNSUPPORTED_DOCUMENT_REGIME", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Missing_regime_is_rejected_before_any_release_is_read(string? regime)
    {
        Assert.Throws<ArgumentException>(
            () => VerifiedRegimeCatalogRouter.LoadForRegime(FindRoot(), regime!));
    }
}
