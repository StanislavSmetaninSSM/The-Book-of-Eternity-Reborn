using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmDocumentationDeliveryTests
{
    [Theory]
    [InlineData("missing_package_source")]
    [InlineData("missing_session_copy")]
    public async Task Block5PhysicalFaultsReachTheRealWriterAtDistinctDeliveryBoundaries(string fault)
    {
        const string target = "Rules/Block_5_Wounds.txt";
        // Run only after actual module authoring and fresh package SOURCE acceptance.
        // A missing module before the producer is fixture failure, never this negative's proof.
        var actualSource = File.ReadAllBytes(Path.Combine(TestRepoPaths.RepoRoot, target));
        Assert.NotEmpty(actualSource);
        var expectedHash = GmDocumentationDeliveryScenario.Hash(actualSource);
        var result = await GmDocumentationDeliveryScenario.RunAsync(TestRepoPaths.RepoRoot, publish: true,
            physicalFault: fault, physicalFaultPath: target);
        _output.WriteLine("GM1557_BLOCK5_PHYSICAL_NEGATIVE " + result.Evidence.ToJsonString());
        Assert.Null(result.Failure);
        Assert.True(result.Ast!["runtimeExecuted"]!.GetValue<bool>());
        Assert.True(result.Evidence["exitConfirmed"]!.GetValue<bool>());
        Assert.True(result.Evidence["rootsRemoved"]!.GetValue<bool>());
        var physical = result.Evidence["physicalFault"]!;
        Assert.Equal(fault, physical["kind"]!.GetValue<string>());
        Assert.True(physical["presentBefore"]!.GetValue<bool>());
        Assert.False(physical["presentAfter"]!.GetValue<bool>());
        Assert.Equal(expectedHash, physical["beforeSha256"]!.GetValue<string>());
        Assert.Contains("delivery.copy_missing:" + target, result.Issues);
        if (fault == "missing_package_source")
            Assert.Contains("delivery.manifest_missing_or_duplicate:" + target, result.Issues);
        else
        {
            Assert.DoesNotContain("delivery.manifest_inventory", result.Issues);
            Assert.DoesNotContain("delivery.manifest_missing_or_duplicate:" + target, result.Issues);
        }
        Assert.DoesNotContain("delivery.cleanup_failed", result.Issues);
        Assert.False(Directory.Exists(result.Evidence["root"]!.GetValue<string>()));
        Assert.False(Directory.Exists(result.Evidence["package"]!.GetValue<string>()));
    }
}
