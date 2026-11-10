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
        var authority = Regex.Replace(matrix, @"\s+", " ");
        foreach (var token in new[]
        {
            "artTiers.force_binding", "standardArts.force_binding", "not an alias for `binding`",
            "integer 0..5", "`spiritual_resilience` / `spiritual_healing`", "unknown art keys remain invalid",
            "baseCost 5 / minCost 2", "effectiveCost 2", "player AP 6→3→1", "guardian AP 6→4→2",
            "does not add an upgrade, acquisition rule or UI entry"
        })
            Assert.Contains(token, authority, StringComparison.Ordinal);
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
