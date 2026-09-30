using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PromptDocumentationCoverageTests
{
    [Fact]
    public void InternalWoundLegacyVocabulary_IsClientOwnedAcrossSharedDocs()
    {
        var common = ReadRepoFile("OtherGuides", "Effect_Materialization_Contract.md");
        var example = ReadRepoFile("Examples", "E_CLI_Effect_Materialization.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");
        Assert.Contains("Client-owned `wound_legacy`", common, StringComparison.Ordinal);
        Assert.Contains("not an additional GM `effectChanges[].source` selector", common, StringComparison.Ordinal);
        Assert.Contains("lifetime.linkKind", common, StringComparison.Ordinal);
        Assert.Contains("Worked continuation after healing", example, StringComparison.Ordinal);
        Assert.Contains("Do not apply, resend, or reconstruct the legacy effect.", example, StringComparison.Ordinal);
        Assert.Contains("wound_legacy", manifest, StringComparison.Ordinal);
    }
}
