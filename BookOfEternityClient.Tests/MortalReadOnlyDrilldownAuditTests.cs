using BookOfEternityClient.CommandProtocol;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalReadOnlyDrilldownAuditTests
{
    [Fact]
    public void MortalReadOnlyDrilldownAudit_CoversEveryMortalWorldReadOnlyDescriptor()
    {
        var auditPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "docs",
            "audits",
            "mortal-readonly-drilldown-audit.md");
        Assert.True(File.Exists(auditPath), $"Missing #948 audit artifact at {auditPath}.");

        var audit = File.ReadAllText(auditPath);
        var descriptors = ExplorerCommandCatalog.Descriptors
            .Where(static descriptor =>
                descriptor.Group == ExplorerCommandGroup.MortalWorld &&
                descriptor.MutationMode == ExplorerCommandMutationMode.ReadOnly &&
                descriptor.BrowserHandlerKind == ExplorerCommandBrowserHandlerKind.MortalWorld)
            .OrderBy(static descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missing = descriptors
            .Where(descriptor =>
                !audit.Contains($"`{descriptor.Id}`", StringComparison.OrdinalIgnoreCase) ||
                !audit.Contains($"`{descriptor.PrimaryAlias}`", StringComparison.OrdinalIgnoreCase))
            .Select(static descriptor => $"{descriptor.Id} ({descriptor.PrimaryAlias})")
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "The #948 mortal read-only drill-down audit must list every mortal read-only command id and primary alias. Missing: " +
            string.Join(", ", missing));
    }
}
