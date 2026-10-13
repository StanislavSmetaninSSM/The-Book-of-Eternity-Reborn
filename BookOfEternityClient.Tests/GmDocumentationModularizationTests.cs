using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmDocumentationModularizationTests
{
    private static GmDocumentationPreservationFixtures Fixture() => new(TestRepoPaths.RepoRoot);
    private static string Read(string path) => File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));

    [Fact]
    public void FrozenMapAndExplicitInverseRoundTripEverySourceByte()
    {
        var fixture = Fixture();
        Assert.Empty(fixture.VerifyMap());
        // This is a pure frozen-source fixture, not actual relocation or delivery evidence.
        Assert.Empty(fixture.VerifyMoved(fixture.CreateCompleteFixture()));
        Assert.Equal(188, fixture.Map["snippetInventory"]!.AsArray().Count);
        Assert.Equal(80, fixture.Map["manifestSource"]!["rows"]!.AsArray().Count);
        Assert.Equal(36, fixture.Map["expectedDelivery"]!.AsArray().Count);
        Assert.Equal(2, fixture.Map["deferredBSourcePins"]!.AsArray().Count);
    }

    [Fact]
    public void ActualDestinationBodiesReassembleExactFrozenSourcesWithoutDuplicateAuthority()
    {
        var fixture = Fixture();
        // Intended positive RED before the content move. A source/map existing does not prove a destination.
        Assert.Empty(fixture.VerifyMoved(fixture.ReadActualDestinations()));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unlisted_edit")]
    [InlineData("reordered_fences")]
    [InlineData("invalid_context")]
    [InlineData("lost_companion")]
    [InlineData("orphan")]
    [InlineData("context")]
    [InlineData("duplicate_owner")]
    public void IndependentMutationFixturesRefuseTheIntendedPreservationFailure(string mutation)
    {
        var fixture = Fixture();
        var candidate = fixture.CreateCompleteFixture();
        Assert.Empty(fixture.VerifyMoved(candidate));
        var block = fixture.Blocks.First(value => fixture.Map["snippetInventory"]!.AsArray()
            .Count(row => GmDocumentationPreservationFixtures.S(row!, "block") == GmDocumentationPreservationFixtures.S(value, "id")) >= 2);
        if (mutation == "invalid_context") block = fixture.Blocks.First(value => fixture.Map["snippetInventory"]!.AsArray()
            .Any(row => GmDocumentationPreservationFixtures.S(row!, "block") == GmDocumentationPreservationFixtures.S(value, "id") &&
                GmDocumentationPreservationFixtures.S(row!, "expected") == "Invalid"));
        if (mutation == "context") block = fixture.Blocks.First(value => GmDocumentationPreservationFixtures.S(value, "contextWrapper").Length > 0);
        var destination = GmDocumentationPreservationFixtures.S(block, "destination");
        var document = candidate[destination];
        var start = GmDocumentationPreservationFixtures.Bytes(GmDocumentationPreservationFixtures.S(block, "startMarker"));
        var end = GmDocumentationPreservationFixtures.Bytes(GmDocumentationPreservationFixtures.S(block, "endMarker"));
        var first = document.AsSpan().IndexOf(start);
        var last = first + start.Length + document.AsSpan(first + start.Length).IndexOf(end);
        var payload = document[(first + start.Length)..last];
        string expected;
        switch (mutation)
        {
            case "duplicate_owner":
                fixture.Map["blocks"]!.AsArray().Add(fixture.Map["blocks"]![0]!.DeepClone()); expected = "map.duplicate_owner"; break;
            case "missing": candidate[destination] = document[..first].Concat(document[(last + end.Length)..]).ToArray(); expected = "payload.missing:"; break;
            case "duplicate": candidate[destination] = document[..first].Concat(document[first..(last + end.Length)]).Concat(document[first..]).ToArray(); expected = "payload.duplicate:"; break;
            case "unlisted_edit": candidate[destination] = GmDocumentationPreservationFixtures.ReplaceOnce(document, payload, payload.Concat(new byte[] { 32 }).ToArray()); expected = "payload.unlisted_edit:"; break;
            case "reordered_fences":
            {
                var snippets = GmDocumentationPreservationFixtures.ExtractSnippets(payload);
                Assert.NotEqual(snippets[0].Hash, snippets[1].Hash);
                var a = snippets[0]; var b = snippets[1];
                var changed = payload[..a.Start].Concat(payload[b.Start..b.End]).Concat(payload[a.End..b.Start])
                    .Concat(payload[a.Start..a.End]).Concat(payload[b.End..]).ToArray();
                candidate[destination] = GmDocumentationPreservationFixtures.ReplaceOnce(document, payload, changed);
                expected = "snippet.order:"; break;
            }
            case "invalid_context":
            {
                var changed = GmDocumentationPreservationFixtures.Bytes(Regex.Replace(GmDocumentationPreservationFixtures.Text(payload),
                    "INVALID|INCORRECT|VIOLATION|НЕПРАВ", "VALID", RegexOptions.IgnoreCase));
                candidate[destination] = GmDocumentationPreservationFixtures.ReplaceOnce(document, payload, changed);
                expected = "snippet.classification:"; break;
            }
            case "lost_companion":
            {
                var companion = GmDocumentationPreservationFixtures.ExtractSnippets(payload)[0];
                candidate[destination] = GmDocumentationPreservationFixtures.ReplaceOnce(document, payload,
                    payload[..companion.Start].Concat(payload[companion.End..]).ToArray());
                expected = "snippet.count:"; break;
            }
            case "orphan": candidate.Add("Examples/E_CLI_Afterlife_Shadow.txt", candidate[destination].ToArray()); expected = "destination.orphan:"; break;
            case "context":
                candidate[destination] = GmDocumentationPreservationFixtures.ReplaceOnce(document,
                    GmDocumentationPreservationFixtures.Bytes(GmDocumentationPreservationFixtures.S(block, "contextWrapper")), []);
                expected = "context.missing:"; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        Assert.Contains(fixture.VerifyMoved(candidate), issue => issue.StartsWith(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void ActualManifestRetainsCompleteSemanticsAndNamesTheMovedOwners()
    {
        var fixture = Fixture();
        Assert.Empty(fixture.VerifyManifest(fixture.ReadJson("Examples/example_validation_manifest.json")));
        // No new runner, input, expected write, exemption or coverage-limit qualification is implied by remapped paths.
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("semantics")]
    [InlineData("companion")]
    [InlineData("duplicate")]
    public void ManifestMutationFixturesRefuseLostCoverageAndCompanionBehavior(string mutation)
    {
        var fixture = Fixture(); var candidate = fixture.CreateCompleteManifestFixture();
        Assert.Empty(fixture.VerifyManifest(candidate));
        var row = fixture.Map["manifestSource"]!["rows"]!.AsArray().First(value =>
            GmDocumentationPreservationFixtures.S(value!, "family") == "runtimeScenarios");
        var index = GmDocumentationPreservationFixtures.I(row!, "index");
        var actual = candidate["runtimeScenarios"]![index]!.AsObject();
        if (mutation == "missing") candidate["runtimeScenarios"]![index] = null;
        else if (mutation == "duplicate") candidate["runtimeScenarios"]!.AsArray().Add(actual.DeepClone());
        else if (mutation == "semantics") actual["runner"] = "syntaxOnly";
        else
        {
            var files = actual["expectedModifiedFiles"]!.AsArray();
            Assert.NotEmpty(files); files.RemoveAt(0); // A real companion after-image obligation, not an invented empty field.
        }
        Assert.Contains(fixture.VerifyManifest(candidate), issue => issue.StartsWith(mutation == "missing" ? "manifest.missing:" : mutation == "duplicate" ? "manifest.duplicate:" : "manifest.semantic:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("bootstrap", "", false)]
    [InlineData("ordinary", "L", true)]
    [InlineData("scheduler", "L,S,G", true)]
    [InlineData("multi_pending", "L,G,A", true)]
    [InlineData("receipt_only", "", false)]
    [InlineData("wound_frontier", "W,C,P,E", true)]
    [InlineData("repair", "G", true)]
    public void ActualRouterNamesOnlyTheCurrentFamilyDependencyUnion(string family, string required, bool core)
    {
        var topics = required.Length == 0 ? Array.Empty<string>() : required.Split(',');
        Assert.Empty(GmDocumentationPreservationFixtures.VerifyRoute(Read("CLI_Rules_Index.md"), family, topics, core));
        if (family is "bootstrap" or "receipt_only") Assert.Empty(topics);
        if (family == "wound_frontier") Assert.Equal(new[] { "C", "E", "P", "W" }, topics.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("L", "L", "")]
    [InlineData("S", "L,S,G", "L")]
    [InlineData("G", "L,G", "L")]
    [InlineData("A", "L,A", "L")]
    [InlineData("H", "L,H", "L")]
    [InlineData("C", "L,C,P,E", "L")]
    [InlineData("E", "L,E", "L")]
    [InlineData("P", "L,P", "L")]
    [InlineData("M", "L,M", "L")]
    [InlineData("W", "W,C,P,E", "")]
    public void ActualRouterPreservesEachTopicActivationAndItsInactiveControl(string topic, string active, string inactive)
    {
        var router = Read("CLI_Rules_Index.md");
        Assert.Empty(GmDocumentationPreservationFixtures.VerifyRoute(router, "topic_" + topic + "_active", active.Split(','), true));
        Assert.Empty(GmDocumentationPreservationFixtures.VerifyRoute(router, "topic_" + topic + "_inactive",
            inactive.Length == 0 ? [] : inactive.Split(','), true));
        var fixture = Fixture();
        Assert.Contains(fixture.Map["topicLegend"]![topic]!["rule"]!.GetValue<string>(), router, StringComparison.Ordinal);
        Assert.Contains(fixture.Map["topicLegend"]![topic]!["example"]!.GetValue<string>(), router, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("universal_read")]
    [InlineData("second_pending")]
    [InlineData("bootstrap_core")]
    [InlineData("receipt_ordinary")]
    [InlineData("frontier_ordinary")]
    public void RoutingMutationFixturesRefuseScopeExpansionOrLostUnion(string mutation)
    {
        var fixture = Fixture();
        var router = GmDocumentationPreservationFixtures.Text(fixture.CreateCompleteFixture()["CLI_Rules_Index.md"]);
        var family = mutation is "bootstrap_core" ? "bootstrap" : mutation is "receipt_ordinary" ? "receipt_only" : mutation == "frontier_ordinary" ? "wound_frontier" : "multi_pending";
        var required = family == "multi_pending" ? new[] { "L", "G", "A" } : family == "wound_frontier" ? new[] { "W", "C", "P", "E" } : Array.Empty<string>();
        var core = family is "multi_pending" or "wound_frontier";
        Assert.Empty(GmDocumentationPreservationFixtures.VerifyRoute(router, family, required, core));
        var row = router.Split('\n').Single(line => line.StartsWith("| " + family + " |", StringComparison.Ordinal));
        if (mutation == "frontier_ordinary") router += "\nFrontier must read ordinary turn_request.\n";
        else if (mutation == "universal_read") router += "\nRead all modules every turn.\n";
        else if (mutation == "second_pending") router = router.Replace(row, row.Replace("A,G,L", "G,L", StringComparison.Ordinal), StringComparison.Ordinal);
        else if (mutation == "bootstrap_core") router = router.Replace(row, row.Replace("false", "true", StringComparison.Ordinal), StringComparison.Ordinal);
        else router = router.Replace(row, row.Replace(" | - | ", " | L | ", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains(GmDocumentationPreservationFixtures.VerifyRoute(router, family, required, core), issue => issue.StartsWith("route.", StringComparison.Ordinal));
    }

    [Fact]
    public void ActiveD01BodiesContainTheApprovedBanAndSafeRepairAlternatives()
    {
        var fixture = Fixture();
        foreach (var edit in fixture.Edits.Where(edit => GmDocumentationPreservationFixtures.S(edit, "kind").StartsWith("D01", StringComparison.Ordinal)))
        {
            var actual = File.ReadAllBytes(Path.Combine(TestRepoPaths.RepoRoot, GmDocumentationPreservationFixtures.S(edit, "source")));
            var after = Convert.FromBase64String(GmDocumentationPreservationFixtures.S(edit, "afterBase64"));
            var before = Convert.FromBase64String(GmDocumentationPreservationFixtures.S(edit, "beforeBase64"));
            Assert.True(actual.AsSpan().IndexOf(after) >= 0, "D01 approved active after-image missing: " + GmDocumentationPreservationFixtures.S(edit, "source"));
            Assert.True(actual.AsSpan().IndexOf(before) < 0, "D01 source-reading grant remains active.");
        }
        var daemon = Read("BookOfEternityClient/game_master_daemon.ps1");
        Assert.Contains("Do not read implementation code such as BookOfEternityClient/**/*.cs during normal play or validation repair", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:GmSourceFallbackDirective", daemon, StringComparison.Ordinal);
        // Final AST/dispatch dataflow and real packaged delivery are separate, still-unwritten adapter guards.
    }
}
