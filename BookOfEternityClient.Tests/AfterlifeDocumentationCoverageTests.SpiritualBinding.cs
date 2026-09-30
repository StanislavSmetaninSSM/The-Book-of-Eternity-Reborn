using System.Text.RegularExpressions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeDocumentationCoverageTests
{
    /// <summary>
    /// Checks that GM-facing entrypoints preserve the two-response binding correction boundary.
    /// </summary>
    [Fact]
    public void SpiritualBindingResultClosureDocumentation_PinsTwoResponseBoundary()
    {
        var guide = Regex.Replace(
            ReadRepoFile("OtherGuides", "Wound_Materialization_Contract.md"), @"\s+", " ");
        foreach (var token in new[]
        {
            "spiritual_wound_binding_result_closure_v1", "binding", "force_binding",
            "before.controlState", "after.controlState", "no_effect", "blocked",
            "independently proved action cost", "effective raw final-control carrier",
            "detached", "ordinary validation", "saved wound choice",
            "continuationId", "woundDecisions: []", "Ready", "zero"
        })
            Assert.Contains(token, guide, StringComparison.Ordinal);

        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var worker = ReadRepoFile("OtherGuides", "GM_Worker_Bridges.md");
        var api = ReadRepoFile("CLI_API_Specification.md");
        var daemon = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        foreach (var text in new[] { matrix, worker, api, daemon })
        {
            var normalized = Regex.Replace(text, @"\s+", " ");
            Assert.Contains("spiritual_wound_binding_result_closure_v1", normalized, StringComparison.Ordinal);
            Assert.Contains("continuationId", normalized, StringComparison.Ordinal);
            Assert.Contains("saved wound choice", normalized, StringComparison.Ordinal);
        }
    }
}
