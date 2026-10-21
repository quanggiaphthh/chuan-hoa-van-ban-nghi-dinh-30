namespace Nd30.LegalValidator.Catalog;

/// <summary>
/// Additive selection gate around the existing generated/verified rule catalog.
/// Does not modify the V24 source-payload or ND30 release manifest.
/// Party source registration and selector packs do not authorize runtime rules.
/// </summary>
public static class VerifiedRegimeCatalogRouter
{
    public static RuleCatalog LoadForRegime(string root, string regime)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (string.IsNullOrWhiteSpace(regime))
            throw new ArgumentException("A document regime must be explicitly selected.", nameof(regime));

        if (string.Equals(regime, "administrative", StringComparison.Ordinal))
            return RuleCatalog.LoadVerifiedRelease(root);

        if (string.Equals(regime, "party", StringComparison.Ordinal))
            throw new NotSupportedException(
                "PARTY_VERIFIED_RELEASE_UNAVAILABLE: No approved, verified Party canonical rule release is available. " +
                "Registration of HD05/QD399 sources and a Party selector does not authorize runtime validation.");

        throw new NotSupportedException(
            "UNSUPPORTED_DOCUMENT_REGIME: Explicitly select a supported verified-rule regime.");
    }
}
