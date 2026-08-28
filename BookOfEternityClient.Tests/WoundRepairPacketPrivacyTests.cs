using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundRepairPacketPrivacyTests
{
    private static readonly JsonSerializerOptions ReadableJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [Theory]
    [InlineData("ownerId", "owner_secret_17")]
    [InlineData("providerId", "provider_secret_23")]
    [InlineData("routeSeal", "route_secret_31")]
    [InlineData("resourceSeal", "resource_secret_47")]
    [InlineData("privateNpcData", "npc_secret_59")]
    public void Build_RecursivelyRemovesHiddenAuthorityFromEveryPacketSurface(
        string forbiddenKey,
        string secret)
    {
        var candidate = CreateCandidate(
            "woundDecisions[0].proposal.owner",
            "wound_response_unknown_field",
            secret);
        candidate.SafeContext["internalDetails"] = new JsonObject
        {
            [forbiddenKey] = secret,
            ["nested"] = new JsonArray(new JsonObject { [forbiddenKey] = secret })
        };
        var proposal = candidate.RejectedDecision["proposal"]!.AsObject();
        proposal["privateEnvelope"] = new JsonObject
        {
            [forbiddenKey] = secret,
            ["nested"] = new JsonArray(new JsonObject { [forbiddenKey] = secret })
        };

        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateRequest(candidate)));
        var serialized = packet.ToJsonObject().ToJsonString(ReadableJson);

        Assert.DoesNotContain(forbiddenKey, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secret, serialized, StringComparison.Ordinal);
        Assert.Equal(
            new[] { "event", "target", "realm" },
            packet.SafeContext.Select(pair => pair.Key));
        Assert.Equal("осколок после обвала", packet.SafeContext["event"]!.GetValue<string>());
        Assert.Equal("игрок", packet.SafeContext["target"]!.GetValue<string>());
        Assert.Equal("Смертный мир", packet.SafeContext["realm"]!.GetValue<string>());
        Assert.Equal(
            "redacted unsafe authority evidence",
            Assert.Single(packet.Issues).Actual);
    }

    [Fact]
    public void Build_StripsPermanentIdentitiesAndSealsAtArbitraryNestedDepth()
    {
        var candidate = CreateCandidate(
            "woundDecisions[0].proposal.severity",
            "wound_severity_above_opportunity",
            "III");
        var proposal = candidate.RejectedDecision["proposal"]!.AsObject();
        proposal["woundId"] = "wound_permanent_secret";
        proposal["owner"] = new JsonObject
        {
            ["ownerId"] = "npc_permanent_secret",
            ["carrierPath"] = "game_state/npcs/npc_wounds.json#/entries/0"
        };
        proposal["consequenceDefinitions"] = new JsonArray(new JsonObject
        {
            ["definitionRef"] = "local_definition_safe_001",
            ["definition"] = new JsonObject
            {
                ["definitionKey"] = "wound_grip_penalty",
                ["authorityFingerprint"] = Fingerprint('f'),
                ["links"] = new JsonArray(new JsonObject
                {
                    ["woundId"] = "wound_permanent_secret",
                    ["effectId"] = "effect_permanent_secret",
                    ["resourceId"] = "resource_permanent_secret",
                    ["providerSeal"] = "provider_seal_secret"
                }),
                ["components"] = new JsonArray(new JsonObject
                {
                    ["componentId"] = "grip_penalty",
                    ["profile"] = "characteristic_modifier",
                    ["payload"] = new JsonObject
                    {
                        ["characteristic"] = "strength",
                        ["operation"] = "add",
                        ["value"] = -1,
                        ["sourceSeal"] = "source_seal_secret"
                    }
                })
            },
            ["root"] = new JsonObject
            {
                ["ownership"] = new JsonObject { ["kind"] = "base_wound" },
                ["slots"] = new JsonArray(new JsonObject
                {
                    ["profileKey"] = "characteristic_modifier",
                    ["readableSummary"] = "Боль мешает удерживать тяжёлые предметы."
                })
            }
        });

        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateRequest(candidate)));
        var serialized = packet.ToJsonObject().ToJsonString(ReadableJson);

        Assert.DoesNotContain("wound_permanent_secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("npc_permanent_secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("effect_permanent_secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("resource_permanent_secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("provider_seal_secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("source_seal_secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(Fingerprint('f'), serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("carrierPath", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("authorityFingerprint", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceSeal", serialized, StringComparison.Ordinal);
        Assert.Contains("local_definition_safe_001", serialized, StringComparison.Ordinal);
        Assert.Contains("wound_grip_penalty", serialized, StringComparison.Ordinal);
        Assert.Contains("Боль мешает удерживать тяжёлые предметы.", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ExcludesUnrelatedResponseAndPrivateGmNotes()
    {
        var candidate = CreateCandidate(
            "woundDecisions[0].proposal.severity",
            "wound_severity_above_opportunity",
            "III");
        candidate.RejectedDecision["unrelatedQuestChange"] = new JsonObject
        {
            ["questId"] = "quest_private_secret",
            ["resolution"] = "unrelated response secret"
        };
        candidate.RejectedDecision["gmPrivateNotes"] = new JsonArray(
            "истинное имя стража",
            "скрытая мотивация");
        candidate.RejectedDecision["proposal"]!["gmPrivateNotes"] =
            "не показывать игроку";

        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateRequest(candidate)));
        var serialized = packet.ToJsonObject().ToJsonString(ReadableJson);

        Assert.DoesNotContain("unrelatedQuestChange", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("quest_private_secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("gmPrivateNotes", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("истинное имя стража", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("скрытая мотивация", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("не показывать игроку", serialized, StringComparison.Ordinal);
        Assert.Contains("Рваная рана предплечья", serialized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("game_state/player/wounds.json.activeWounds[0].owner.ownerId")]
    [InlineData("game_state/npcs/npc_wounds.json.entries[0].providerBinding.providerId")]
    [InlineData("game_state/wounds/wound_history.json.transitions[0].routeSeal")]
    [InlineData("game_state/resources/resource_state.json.entries[0].resourceSeal")]
    [InlineData("game_state/npcs/npc_wounds.json.entries[0].privateNpcData")]
    public void Build_RejectsCanonicalOrPrivateAuthorityPathsInsteadOfEchoingThem(
        string protectedPath)
    {
        var request = CreateRequest(CreateCandidate(
            protectedPath,
            "wound_materialization_invalid_field",
            "protected internal evidence"));

        Assert.Empty(WoundRepairPacketBuilder.Build(request));
        Assert.True(WoundRepairPacketBuilder.RequiresFailClosedRollback(request));
    }

    [Fact]
    public void Build_DetachesTheSanitizedPacketFromMutableInputGraphs()
    {
        var candidate = CreateCandidate(
            "woundDecisions[0].proposal.severity",
            "wound_severity_above_opportunity",
            "III");
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateRequest(candidate)));

        candidate.SafeContext["ownerId"] = "late_owner_secret";
        candidate.RejectedDecision["proposal"]!["woundId"] = "late_wound_secret";
        candidate.RejectedDecision["proposal"]!["display"]!["name"] = "Подменённое имя";

        var first = packet.ToJsonObject();
        first["safeContext"]!["providerId"] = "late_provider_secret";
        first["preservedProposal"]!["effectId"] = "late_effect_secret";
        var secondSerialized = packet.ToJsonObject().ToJsonString(ReadableJson);

        Assert.DoesNotContain("late_owner_secret", secondSerialized, StringComparison.Ordinal);
        Assert.DoesNotContain("late_wound_secret", secondSerialized, StringComparison.Ordinal);
        Assert.DoesNotContain("late_provider_secret", secondSerialized, StringComparison.Ordinal);
        Assert.DoesNotContain("late_effect_secret", secondSerialized, StringComparison.Ordinal);
        Assert.DoesNotContain("Подменённое имя", secondSerialized, StringComparison.Ordinal);
        Assert.Contains("Рваная рана предплечья", secondSerialized, StringComparison.Ordinal);
    }

    private static WoundRepairBuildRequest CreateRequest(
        params WoundRepairCandidateInput[] candidates) => new(
        "session_wound_privacy",
        "request_wound_privacy",
        "snapshot_wound_privacy",
        candidates);

    private static WoundRepairCandidateInput CreateCandidate(
        string path,
        string code,
        string actual)
    {
        var issue = new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Rejected wound proposal contains unsafe authority.",
            code: code,
            section: "wound_materialization",
            expected: "hidden owner/provider/route/resource authority",
            actual: actual,
            repairHint: "owner_secret provider_secret route_secret resource_secret npc_secret");
        return new WoundRepairCandidateInput(
            "repair_wound",
            "candidate_privacy_001",
            Fingerprint('a'),
            "opportunity_privacy_001",
            new JsonObject
            {
                ["event"] = "осколок после обвала",
                ["target"] = "игрок",
                ["realm"] = "Смертный мир"
            },
            new[] { "none", "materialize" },
            "I",
            "II",
            new JsonObject
            {
                ["opportunityRef"] = "opportunity_privacy_001",
                ["decision"] = "materialize",
                ["woundRef"] = "local_wound_ref_privacy_001",
                ["proposal"] = CreateProposal()
            },
            new[] { issue });
    }

    private static JsonObject CreateProposal() => new()
    {
        ["classification"] = new JsonObject
        {
            ["woundType"] = "laceration",
            ["locationProfile"] = new JsonObject
            {
                ["kind"] = "body_part",
                ["readableLocus"] = "левое предплечье"
            }
        },
        ["display"] = new JsonObject
        {
            ["name"] = "Рваная рана предплечья",
            ["description"] = "Края раны расходятся при движении кисти.",
            ["visibleSymptoms"] = new JsonArray("кровотечение", "боль при хвате"),
            ["prognosis"] = "Без очистки возможно воспаление.",
            ["visibility"] = "known_to_player",
            ["acquisitionNarration"] =
                "Крюк срывается с цепи и вспарывает вам предплечье."
        },
        ["severity"] = "II",
        ["complications"] = new JsonArray(),
        ["consequenceDefinitions"] = new JsonArray(),
        ["treatment"] = new JsonObject
        {
            ["diagnosisPaths"] = new JsonArray(),
            ["routes"] = new JsonArray(),
            ["failurePolicy"] = "progress_stalls"
        },
        ["recovery"] = new JsonObject
        {
            ["mode"] = "mortal_clock",
            ["currentStepProgress"] = 0
        }
    };

    private static string Fingerprint(char value) =>
        "sha256:" + new string(value, 64);
}
