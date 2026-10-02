namespace Nd30.DocumentEngine.Safety;

/// <summary>Conservative resource limits for an untrusted personal DOCX workflow.</summary>
public static class PackageSafetyLimits
{
    public const long MaximumCompressedInputBytes = 20L * 1024 * 1024;
    public const long MaximumTotalUncompressedBytes = 128L * 1024 * 1024;
    public const long MaximumSingleEntryUncompressedBytes = 16L * 1024 * 1024;
    public const int MaximumEntryCount = 512;
    public const double MaximumEntryCompressionRatio = 100;
    public const long MaximumXmlCharactersPerPart = 4L * 1024 * 1024;
    public const long MaximumXmlCharactersFromEntities = 1024;
    public const long MaximumTotalXmlBytes = 32L * 1024 * 1024;
    public const int MaximumXmlDepth = 128;
    public const long MaximumXmlNodesPerPart = 500_000;
}

internal sealed record PackageSafetyBudget(
    long MaximumCompressedInputBytes,
    long MaximumTotalUncompressedBytes,
    long MaximumSingleEntryUncompressedBytes,
    int MaximumEntryCount,
    double MaximumEntryCompressionRatio,
    long MaximumXmlCharactersPerPart,
    int MaximumXmlDepth,
    long MaximumXmlNodesPerPart,
    long MaximumTotalXmlBytes)
{
    internal static PackageSafetyBudget Default { get; } = new(
        PackageSafetyLimits.MaximumCompressedInputBytes,
        PackageSafetyLimits.MaximumTotalUncompressedBytes,
        PackageSafetyLimits.MaximumSingleEntryUncompressedBytes,
        PackageSafetyLimits.MaximumEntryCount,
        PackageSafetyLimits.MaximumEntryCompressionRatio,
        PackageSafetyLimits.MaximumXmlCharactersPerPart,
        PackageSafetyLimits.MaximumXmlDepth,
        PackageSafetyLimits.MaximumXmlNodesPerPart,
        PackageSafetyLimits.MaximumTotalXmlBytes);
}
