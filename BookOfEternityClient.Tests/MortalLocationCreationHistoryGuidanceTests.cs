using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLocationCreationHistoryGuidanceTests
{
    [Fact]
    public void CliLocationHistoryGuidance_DistinguishesEmptyCreationFromOwnedArchive()
    {
        var operations = ValidationAndRepairSource.Read(Path.Combine(
            TestRepoPaths.RepoRoot, "Rules", "Block_CLI_Operations.txt"));
        // Check the operative instruction, not an unrelated example elsewhere.
        var instruction = Assert.Single(operations.Split('\n'), line =>
            line.TrimStart().StartsWith("- eventDescriptions", StringComparison.Ordinal));

        Assert.Contains("eventDescriptions is a client-managed location archive", instruction, StringComparison.Ordinal);
        Assert.Contains("For complete new-location creation, include exactly eventDescriptions: []", instruction, StringComparison.Ordinal);
        Assert.Contains("Never author archive entries", instruction, StringComparison.Ordinal);
        Assert.Contains("For existing-location selection or updates, omit this field and write only lastEventsDescription", instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("do NOT emit it inside currentLocationData or worldMapUpdates", instruction, StringComparison.Ordinal);
    }
}
