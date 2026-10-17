using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmDocumentationModularizationTests
{
    private static GmDocumentationPreservationFixtures Fixture() => new(TestRepoPaths.RepoRoot);
    private static string Read(string path) => File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));

    // Independent contract expectations: approved delivery-routing disposition §2,
    // not values read back from content-map.json or the router under test.
    private static string ApprovedPredicate(string id) => id switch
    {
        "bootstrap" => "Initial context pack only; no per-turn/repair/terminal task",
        "ordinary" => "currentRealm=Chaos Sea; no active special contracts or due flags",
        "scheduler" => "mustEvaluateGuardianProjectProgression=true; no other mutation",
        "multi_pending" => "Current Guardian social talk AND resident talk; no resource/profile mutation",
        "receipt_only" => "requestKind=qte_deferred_effect_resolution AND current safePacket; restricted before overlays",
        "wound_frontier" => "Current issued spiritualWoundContinuation phase=decision or dependent_draft; conflict source",
        "repair" => "Current guardian trade repair packet; permitted-read scope only; no spiritual continuation",
        "topic_L_active" => "Resolved ordinary afterlife realm; no handoff",
        "topic_L_inactive" => "Missing/null/empty realm remains unresolved fault; no ordinary topics",
        "topic_S_active" => "mustEvaluateGuardianProjectProgression=true",
        "topic_S_inactive" => "All due flags false and no catchup",
        "topic_G_active" => "Current nonempty pending_guardian_trade_request",
        "topic_G_inactive" => "Stale/empty Guardian trade bundle without contract",
        "topic_A_active" => "Current resident roster/transfer request",
        "topic_A_inactive" => "Resident mentioned without request/state change",
        "topic_H_active" => "Active Shining core action; prepared package absent",
        "topic_H_inactive" => "Valid prepared package handoff blocks ordinary Shining work",
        "topic_C_active" => "Current explicit active spiritual conflict action",
        "topic_C_inactive" => "Old conflict mentioned in chronicle, no current action",
        "topic_E_active" => "Current ordinary bounded resource/effect safe packet",
        "topic_E_inactive" => "No resource/effect work; not dedicated receipt-only",
        "topic_P_active" => "Current new durable afterlife actor/profile change",
        "topic_P_inactive" => "Untouched client Fresh Guardian seed",
        "topic_M_active" => "Current afterlife threat/story/Saref change",
        "topic_M_inactive" => "No current story/state delta",
        "topic_W_active" => "Current issued spiritual frontier with conflict source",
        "topic_W_inactive" => "Private checkpoint or stale Ready with no issued envelope",
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

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
    [InlineData(true)]
    [InlineData(false)]
    public void ContentProfileReadAvailabilityIncludesActualBodiesWithoutWriteAuthority(bool guardian)
    {
        var profile = guardian
            ? BookOfEternityClient.Services.GmWorkers.GmWorkerBridgeProfileTemplates.CreateGuardianAbodeContentCodexTemplate()
            : BookOfEternityClient.Services.GmWorkers.GmWorkerBridgeProfileTemplates.CreateSoulContentCodexTemplate();
        var bodies = Fixture().Destinations;
        Assert.Equal(24, bodies.Length);
        foreach (var body in bodies)
        {
            Assert.Contains(body, profile.Permissions.ReadPaths);
            Assert.True(File.Exists(Path.Combine(TestRepoPaths.RepoRoot, body)), body);
        }
        Assert.Empty(profile.Permissions.ProposalWritePaths);
        Assert.True(profile.Permissions.ProposalOnly);
        Assert.False(profile.Enabled);
    }

    [Fact]
    public void ActualFlatDiscoveryRetainsEveryMovedExampleAndClassification()
    {
        var fixture = Fixture();
        var expected = fixture.Map["snippetInventory"]!.AsArray()
            .Where(row => GmDocumentationPreservationFixtures.S(row!, "source") == "Examples/E_CLI_Afterlife_Turns.txt")
            .Select(row => GmDocumentationPreservationFixtures.S(row!, "bodySha256") + ":" +
                GmDocumentationPreservationFixtures.S(row!, "expected"))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var actual = ExampleSnippetExtractor.ExtractAll()
            .Where(snippet => GmDocumentationBodyFiles.IsThematicExample(snippet.File))
            .Select(snippet => GmDocumentationPreservationFixtures.Hash(
                GmDocumentationPreservationFixtures.Bytes(snippet.RawText)) + ":" + snippet.Expected)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Assert.Equal(187, expected.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ActualEntrypointEditsReverseToTheExactCurrentBaseSources()
    {
        var fixture = Fixture();
        var baselines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CLI_Agent_Daemon_Specification.md"] = "50bc7e73ae74801398a20d32b1effd2503dd773efad871ada7192764182ade89",
            ["TaskGuides/CLI_Step_Main.txt"] = "aa5f32bbd2acf0e0f63ca3969e97d03dd15a8756c9f447bce10f6435b569603b",
            ["BookOfEternityClient/game_master_daemon.ps1"] = "8b0c9329cc2e3a8eef259f50cf5c66d6c9519e92e6cacc4bd8b0a0eaacae2b05",
            ["BookOfEternityClient/Launcher/CLI_Launch_Script.md"] = "e6c2896edf4b227db6742d7d91e26eda3ec1c47fdb3a145b1faa9a8b9d448d39",
            ["BookOfEternityClient/Launcher/Generate_CLI_Launch_Script.ps1"] = "592e6c607df18e5f0935bf4574ba3a861edb39e70c612f7eaf12653d5ddb555d",
            ["CLI_API_Specification.md"] = "250a2ea151a3c932b4a460343e6d61ef2bc5ea70e2979d473189f446e6e29b22",
        };
        var groups = fixture.Edits.Where(edit => baselines.ContainsKey(
            GmDocumentationPreservationFixtures.S(edit, "source")))
            .GroupBy(edit => GmDocumentationPreservationFixtures.S(edit, "source")).ToArray();
        Assert.Equal(baselines.Count, groups.Length);
        foreach (var group in groups)
        {
            var bytes = File.ReadAllBytes(Path.Combine(TestRepoPaths.RepoRoot, group.Key));
            foreach (var edit in group.OrderBy(edit => GmDocumentationPreservationFixtures.I(edit, "start")))
            {
                var after = Convert.FromBase64String(GmDocumentationPreservationFixtures.S(edit, "afterBase64"));
                var offset = bytes.AsSpan().IndexOf(after);
                Assert.True(offset >= 0, group.Key);
                Assert.True(bytes.AsSpan(offset + after.Length).IndexOf(after) < 0, group.Key);
                bytes = GmDocumentationPreservationFixtures.ReplaceOnce(bytes, after,
                    Convert.FromBase64String(GmDocumentationPreservationFixtures.S(edit, "beforeBase64")));
            }
            Assert.Equal(baselines[group.Key], GmDocumentationPreservationFixtures.Hash(bytes));
        }
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
        Assert.Empty(GmDocumentationPreservationFixtures.VerifyRoute(Read("CLI_Rules_Index.md"), family, ApprovedPredicate(family), topics, core));
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
        Assert.Empty(GmDocumentationPreservationFixtures.VerifyRoute(router, "topic_" + topic + "_active",
            ApprovedPredicate("topic_" + topic + "_active"), active.Split(','), true));
        Assert.Empty(GmDocumentationPreservationFixtures.VerifyRoute(router, "topic_" + topic + "_inactive",
            ApprovedPredicate("topic_" + topic + "_inactive"), inactive.Length == 0 ? [] : inactive.Split(','), true));
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
    [InlineData("due_flag_inverse")]
    [InlineData("frontier_stale_private")]
    [InlineData("active_inactive_swapped")]
    public void RoutingMutationFixturesRefuseScopeExpansionOrLostUnion(string mutation)
    {
        var fixture = Fixture();
        var router = GmDocumentationPreservationFixtures.Text(fixture.CreateCompleteFixture()["CLI_Rules_Index.md"]);
        var family = mutation switch
        {
            "bootstrap_core" => "bootstrap",
            "receipt_ordinary" => "receipt_only",
            "frontier_ordinary" or "frontier_stale_private" => "wound_frontier",
            "due_flag_inverse" => "scheduler",
            "active_inactive_swapped" => "topic_S_active",
            _ => "multi_pending"
        };
        var required = family switch
        {
            "multi_pending" => new[] { "L", "G", "A" },
            "wound_frontier" => new[] { "W", "C", "P", "E" },
            "scheduler" or "topic_S_active" => new[] { "L", "S", "G" },
            _ => Array.Empty<string>()
        };
        var core = family is not ("bootstrap" or "receipt_only");
        var predicate = ApprovedPredicate(family);
        Assert.Empty(GmDocumentationPreservationFixtures.VerifyRoute(router, family, predicate, required, core));
        var row = router.Split('\n').Single(line => line.StartsWith("| " + family + " |", StringComparison.Ordinal));
        var conditionOnly = mutation is "due_flag_inverse" or "frontier_stale_private" or "active_inactive_swapped";
        if (conditionOnly)
        {
            var changedPredicate = mutation switch
            {
                "due_flag_inverse" => predicate.Replace("=true", "=false", StringComparison.Ordinal),
                "frontier_stale_private" => ApprovedPredicate("topic_W_inactive"),
                _ => ApprovedPredicate("topic_S_inactive")
            };
            Assert.NotEqual(predicate, changedPredicate);
            var changedRow = row.Replace("| " + predicate + " |", "| " + changedPredicate + " |", StringComparison.Ordinal);
            Assert.Equal(row.Split('|')[3..], changedRow.Split('|')[3..]); // Every topics/exclusions/core cell stays exact.
            router = router.Replace(row, changedRow, StringComparison.Ordinal);
            if (mutation == "active_inactive_swapped")
            {
                var inactive = router.Split('\n').Single(line => line.StartsWith("| topic_S_inactive |", StringComparison.Ordinal));
                router = router.Replace(inactive, inactive.Replace("| " + ApprovedPredicate("topic_S_inactive") + " |",
                    "| " + predicate + " |", StringComparison.Ordinal), StringComparison.Ordinal);
                Assert.Contains(GmDocumentationPreservationFixtures.VerifyRoute(router, "topic_S_inactive",
                    ApprovedPredicate("topic_S_inactive"), ["L"], true), issue => issue == "route.predicate:topic_S_inactive");
            }
        }
        else if (mutation == "frontier_ordinary") router += "\nFrontier must read ordinary turn_request.\n";
        else if (mutation == "universal_read") router += "\nRead all modules every turn.\n";
        else if (mutation == "second_pending") router = router.Replace(row, row.Replace("A,G,L", "G,L", StringComparison.Ordinal), StringComparison.Ordinal);
        else if (mutation == "bootstrap_core") router = router.Replace(row, row.Replace("false", "true", StringComparison.Ordinal), StringComparison.Ordinal);
        else router = router.Replace(row, row.Replace(" | - | ", " | L | ", StringComparison.Ordinal), StringComparison.Ordinal);
        var issues = GmDocumentationPreservationFixtures.VerifyRoute(router, family, predicate, required, core);
        Assert.Contains(issues, issue => conditionOnly ? issue == "route.predicate:" + family : issue.StartsWith("route.", StringComparison.Ordinal));
        if (conditionOnly) Assert.DoesNotContain(issues, issue => issue.StartsWith("route.required:", StringComparison.Ordinal) ||
            issue.StartsWith("route.excluded:", StringComparison.Ordinal) || issue.StartsWith("route.core:", StringComparison.Ordinal));
    }

    [Fact]
    public void ActiveD01BodiesContainTheApprovedBanAndSafeRepairAlternatives()
    {
        var fixture = Fixture();
        Assert.Empty(fixture.VerifyD01(fixture.ReadActualD01Scopes()));
        var daemon = Read("BookOfEternityClient/game_master_daemon.ps1");
        Assert.Contains("Do not read implementation code such as BookOfEternityClient/**/*.cs during normal play or validation repair", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:GmSourceFallbackDirective", daemon, StringComparison.Ordinal);
        // Final AST/dispatch dataflow and real packaged delivery are separate, still-unwritten adapter guards.
    }

    [Theory]
    [InlineData("owner_loss")]
    [InlineData("restored_grant")]
    public void D01MutationFixturesRefuseMissingActiveOwnerAndRestoredGrant(string mutation)
    {
        var fixture = Fixture(); var candidate = fixture.CreateCompleteD01Fixture();
        Assert.Empty(fixture.VerifyD01(candidate));
        var moved = fixture.D01Edits.Single(edit => GmDocumentationPreservationFixtures.S(edit, "id") == "N002");
        var owner = fixture.ActiveEditOwner(moved);
        Assert.Equal("OtherGuides/GM_Common_Core.md", owner);
        Assert.Equal("CLI_Agent_Daemon_Specification.md", fixture.ActiveEditOwner(fixture.D01Edits.Single(edit =>
            GmDocumentationPreservationFixtures.S(edit, "id") == "N001")));
        Assert.Equal("TaskGuides/CLI_Step_Main.txt", fixture.ActiveEditOwner(fixture.D01Edits.Single(edit =>
            GmDocumentationPreservationFixtures.S(edit, "id") == "N003")));
        var stub = GmDocumentationPreservationFixtures.S(moved, "source");
        var after = Convert.FromBase64String(GmDocumentationPreservationFixtures.S(moved, "afterBase64"));
        Assert.True(candidate[stub].AsSpan().IndexOf(after) < 0); // Compact stub does not duplicate the active paragraph.
        if (mutation == "owner_loss") candidate.Remove(owner);
        else candidate[stub] = candidate[stub].Concat(Convert.FromBase64String(
            GmDocumentationPreservationFixtures.S(moved, "beforeBase64"))).ToArray();
        Assert.Contains(fixture.VerifyD01(candidate), issue => issue.StartsWith(
            mutation == "owner_loss" ? "d01.owner_missing:N002" : "d01.restored_grant:N002:", StringComparison.Ordinal));
    }
}
