using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExampleDocumentationValidationTests
{
    [Fact]
    public void AlternativeTreatmentRepairWorkedExamples_ExecuteRealProjectionAndStrictTransport()
    {
        var rejected = Assert.Single(ParseNamedJsonFences("E_CLI_Wound_Materialization.txt",
            "wound_mortal_alternative_repair_rejected_author_v1"));
        var original = Assert.Single(rejected["woundTreatmentAuthorings"]!.AsArray())!.AsObject();
        var parsed = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
            JsonSerializer.SerializeToElement(original), "woundTreatmentAuthorings[0]");
        Assert.False(parsed.IsValid);
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(new WoundRepairBuildRequest(
            "session_wound_diagnosis", "request_wound_diagnosis", "snapshot_wound_diagnosis", new[]
            {
                new WoundRepairCandidateInput("author_alternative_treatment", "candidate_alternative_treatment_001",
                    "sha256:" + new string('b', 64), "authoring_request_public_001",
                    new JsonObject { ["event"] = "В найденных записях описан другой способ лечения",
                        ["target"] = "игрок", ["realm"] = "Смертный мир" },
                    new[] { "decline", "author" }, "I", "IV", original, parsed.Issues)
            })));
        var workedPacket = Assert.Single(ParseNamedJsonFences("E_CLI_Wound_Materialization.txt",
            "wound_mortal_alternative_repair_safe_packet_v1"));
        Assert.Equal(packet.ToJsonObject().ToJsonString(), workedPacket.ToJsonString());
        var receipts = JsonSerializer.SerializeToElement(new[] { packet.CreateReceipt() }, new JsonSerializerOptions
            { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.True(WoundRepairPacketBuilder.IsValidPersistedRepairWave(
            JsonSerializer.SerializeToElement(new JsonArray(workedPacket)), receipts,
            packet.SessionId, packet.RequestId, packet.SnapshotToken));
        var response = Assert.Single(ParseNamedJsonFences("E_CLI_Wound_Materialization.txt",
            "wound_mortal_alternative_repair_corrected_author_v1"));
        var corrected = Assert.Single(response["woundTreatmentAuthorings"]!.AsArray())!.AsObject();
        Assert.True(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        var unrelated = corrected.DeepClone().AsObject();
        unrelated["route"]!["displayName"] = "unrelated rewrite";
        Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(unrelated));
        corrected["route"]!["operationKey"] = "private copied authority";
        Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
    }

    private static readonly string[] RegisteredSpiritualWoundProfiles =
        SpiritualWoundEffectProfileCatalog.RegisteredProfiles
            .OrderBy(static profile => profile, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void AlternativeTreatmentResponseWorkedExamples_ParseRoundTripAndRejectCopiedAuthority()
    {
        var markers = new[]
        {
            "wound_mortal_alternative_response_visible_author_v1",
            "wound_mortal_alternative_response_hidden_author_v1",
            "wound_mortal_alternative_response_decline_v1"
        };

        foreach (var marker in markers)
        {
            var root = Assert.Single(ParseNamedJsonFences(
                "E_CLI_Wound_Materialization.txt", marker));
            var response = JsonSerializer.Deserialize<GameResponse>(root.ToJsonString());
            var rawEntries = Assert.IsType<JsonElement[]>(response!.WoundTreatmentAuthorings);
            var rawEntry = Assert.Single(rawEntries);

            var parsedBatch = WoundResponseInputComposer.ParseAlternativeTreatmentAuthorings(
                JsonSerializer.SerializeToElement(rawEntries), "woundTreatmentAuthorings");
            Assert.True(parsedBatch.IsValid, string.Join(" | ", parsedBatch.Issues.Select(issue =>
                $"{issue.Code}@{issue.FilePath}:{issue.Expected}:{issue.Actual}")));
            Assert.Single(parsedBatch.Drafts);

            var parsed = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
                rawEntry, "woundTreatmentAuthorings[0]");
            Assert.True(parsed.IsValid, string.Join(" | ", parsed.Issues.Select(issue =>
                $"{issue.Code}@{issue.FilePath}:{issue.Expected}:{issue.Actual}")));
            var draft = Assert.Single(parsed.Drafts);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
                WoundResponseInputComposer.WriteAlternativeTreatmentAuthoringCanonical(writer, draft);
            var written = Assert.IsAssignableFrom<JsonNode>(JsonNode.Parse(stream.ToArray()));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(rawEntry.GetRawText()), written), marker);

            var copiedAuthority = written.DeepClone().AsObject();
            copiedAuthority["authorityFingerprint"] = "sha256:copied";
            var rejected = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
                JsonSerializer.SerializeToElement(copiedAuthority),
                "woundTreatmentAuthorings[0]");
            Assert.False(rejected.IsValid);
            Assert.Contains(rejected.Issues, issue =>
                issue.Code == "wound_response_unknown_field" &&
                issue.FilePath == "woundTreatmentAuthorings[0].authorityFingerprint");
        }
    }

    [Fact]
    public void AlternativeTreatmentResponseWorkedExamples_RejectCanonicalRemovalSelector()
    {
        var root = Assert.Single(ParseNamedJsonFences(
            "E_CLI_Wound_Materialization.txt",
            "wound_mortal_alternative_response_visible_author_v1"));
        var entry = Assert.Single(root["woundTreatmentAuthorings"]!.AsArray())!.AsObject();
        var operation = Assert.Single(
            entry["route"]!["outcomes"]![0]!["result"]!.AsArray())!.AsObject();
        Assert.Equal("remove_complication", operation["kind"]!.GetValue<string>());
        var offeredRef = operation["complicationRef"]!.GetValue<string>();
        operation.Remove("complicationRef");
        operation["complicationId"] = offeredRef;

        var rejected = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
            JsonSerializer.SerializeToElement(entry),
            "woundTreatmentAuthorings[0]");

        Assert.False(rejected.IsValid);
        Assert.Empty(rejected.Drafts);
        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "wound_materialization_unknown_field" &&
            issue.FilePath ==
                "woundTreatmentAuthorings[0].route.outcomes[0].result[0].complicationId");
    }

    [Theory]
    [InlineData("wound_mortal_roll_scope_all_v1", "all")]
    [InlineData("wound_mortal_roll_scope_skill_v1", "skill")]
    public void MortalWoundRollScopeWorkedExamples_ComposeAndBindExactSkill(
        string marker,
        string scopeKind)
    {
        var fences = ParseNamedJsonFences("E_CLI_Wound_Materialization.txt", marker);
        Assert.Equal(2, fences.Count);
        var skillAuthority = CreateRollScopeExampleAuthority();
        Assert.True(JsonNode.DeepEquals(
            skillAuthority.CreateGmCatalog(),
            fences[0]["effectSkillScopeCatalog"]));

        var response = fences[1];
        var decision = Assert.IsType<JsonObject>(Assert.Single(
            response["woundDecisions"]!.AsArray()));
        var proposal = Assert.IsType<JsonObject>(decision["proposal"]);
        Assert.Equal(scopeKind == "skill" ? 2 : 1, proposal["consequenceDefinitions"]!.AsArray().Count);
        var originalRef = scopeKind == "skill" ? "ashglass_lockpicking_hindrance_local" : "bell_contusion_hindrance_local";
        var originalComponent = scopeKind == "skill" ? "ashglass_lockpicking_roll" : "bell_contusion_roll";
        var wrapper = Assert.IsType<JsonObject>(Assert.Single(
            proposal["consequenceDefinitions"]!.AsArray(), row => row!["definitionRef"]!.GetValue<string>() == originalRef));
        var definition = Assert.IsType<JsonObject>(wrapper["definition"]);
        var components = Assert.IsType<JsonArray>(definition["components"]);
        var component = Assert.IsType<JsonObject>(Assert.Single(components));
        Assert.Equal(originalComponent, component["componentId"]!.GetValue<string>());
        Assert.Equal("roll_modifier", component["profile"]!.GetValue<string>());
        Assert.Equal(scopeKind, component["payload"]!["scope"]!["kind"]!.GetValue<string>());
        Assert.Equal("skill_check", Assert.Single(
            component["payload"]!["operations"]!.AsArray())!.GetValue<string>());
        if (scopeKind == "skill")
            Assert.Equal("skill_lockpicking", component["payload"]!["scope"]!["skillId"]!.GetValue<string>());
        else
            Assert.False(component["payload"]!["scope"]!.AsObject().ContainsKey("skillId"));
        Assert.Empty(definition["links"]!.AsArray());
        var slot = Assert.IsType<JsonObject>(Assert.Single(wrapper["root"]!["slots"]!.AsArray()));
        Assert.Equal("roll_modifier", slot["profileKey"]!.GetValue<string>());

        var (binding, opportunity) = CreateRollScopeExampleOpportunity(
            decision["opportunityRef"]!.GetValue<string>());
        var scene = response["response"]!.GetValue<string>();
        var rawDecision = JsonSerializer.SerializeToElement(decision);
        var composed = WoundResponseInputComposer.Compose(
            binding, new[] { opportunity }, new[] { rawDecision }, scene,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, DescribeRollScopeExampleIssues(composed.Issues));
        Assert.Equal(2, Assert.Single(composed.Transitions).ProposedAfter.Severity.Rank);
        var scopeIssues = WoundResponseInputComposer.ValidateSkillScopes(
            binding, new[] { new WoundResponseCommandDraft(opportunity, rawDecision, scene) },
            composed.Transitions, skillAuthority, out var locations);
        Assert.Empty(scopeIssues);
        Assert.NotNull(locations);

        if (scopeKind == "skill")
        {
            Assert.DoesNotContain("\"complicationId\"", response.ToJsonString(), StringComparison.Ordinal);
            var transition = Assert.Single(composed.Transitions);
            var proposedComplication = Assert.Single(transition.ProposedAfter.Complications);
            Assert.Single(transition.RootApplications, root => root.OwnershipDomain == WoundRootOwnershipDomain.BaseWound);
            Assert.Equal(proposedComplication.ComplicationId, Assert.Single(transition.RootApplications,
                root => root.OwnershipDomain.Kind == "complication").OwnershipDomain.ComplicationId);
            var input = WoundAcceptedTurnTestFixture.CreateDefaultInput() with
            {
                Binding = binding, Opportunities = new[] { opportunity }, Transitions = composed.Transitions
            };
            var preparation = WoundAcceptedTurnPlanner.Prepare(input);
            Assert.True(preparation.Success, DescribeRollScopeExampleIssues(preparation.Issues));
            var prepared = Assert.IsType<WoundPreparedAcceptedTurnPlan>(preparation.Plan);
            var effects = WoundEffectBatchPlanner.Build(prepared,
                WoundAcceptedTurnTestFixture.CreateEffectInput(prepared) with
                {
                    SkillScopeAuthority = skillAuthority,
                    WoundApplicationLocations = locations!.BindPreparedSources(prepared)
                },
                new EffectIdentityFactory());
            Assert.True(effects.Success, string.Join(Environment.NewLine,
                effects.Issues.Select(issue => $"{issue.Code}: {issue.FilePath}: {issue.Expected}: {issue.Actual}")));
            var finalized = WoundAcceptedTurnPlanner.Finalize(prepared, effects);
            Assert.True(finalized.Success, DescribeRollScopeExampleIssues(finalized.Issues));
            var wound = Assert.IsType<WoundMaterializationEnvelope>(Assert.Single(
                Assert.Single(finalized.Plan!.CarrierContributions).Mutations).AfterWound);
            var complication = Assert.Single(wound.Complications);
            var treatment = MortalWoundTreatmentContract.ParseProjection(wound.Treatment,
                "example.treatment", wound.Owner.Realm, "player", wound.Severity.Rank,
                wound.Complications, wound.Recovery.DeteriorationPolicy);
            Assert.True(treatment.IsValid, DescribeRollScopeExampleIssues(treatment.Issues));
            var route = Assert.IsType<MortalWoundProcedureRouteDefinition>(Assert.Single(treatment.Treatment!.Routes));
            var success = Assert.Single(route.Bands, band => band.Category == "success");
            Assert.Equal(complication.ComplicationId,
                Assert.IsType<MortalWoundRemoveComplicationOperation>(success.DeclaredResult[0]).ComplicationId);
            Assert.IsType<MortalWoundStabilizeOperation>(success.DeclaredResult[1]);
            Assert.Equal(2, wound.Consequences.OwnedEffectSources.RootBindings.Count);
            var complicationRoot = Assert.Single(complication.OwnedEffectIds);
            var baseRoot = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings,
                root => root.EffectId != complicationRoot);
            Assert.Equal("ashglass-lockpicking-hindrance", baseRoot.DefinitionKey);
            Assert.Equal("ashglass-fragment-grip-limit", Assert.Single(
                wound.Consequences.OwnedEffectSources.RootBindings, root => root.EffectId == complicationRoot).DefinitionKey);
            var simulated = MortalWoundTreatmentWorkingWoundSimulator.Simulate(wound,
                new[] { success.DeclaredResult });
            Assert.True(simulated.IsApplicable);
            Assert.True(simulated.Improved);
            var after = Assert.IsType<WoundMaterializationEnvelope>(simulated.WorkingWound);
            Assert.Empty(after.Complications);
            Assert.Equal(baseRoot, Assert.Single(after.Consequences.OwnedEffectSources.RootBindings));
            Assert.Equal(1, Assert.Single(after.Consequences.Entries).Slot);
            Assert.Equal("stabilized", after.Care.State);
            Assert.DoesNotContain("not_stabilized", after.Recovery.Blockers);
        }

        Assert.Contains(proposal["display"]!["acquisitionNarration"]!.GetValue<string>(),
            scene, StringComparison.Ordinal);
        foreach (var playerText in new[]
                 {
                     scene, proposal["display"]!.ToJsonString(),
                     definition["display"]!.ToJsonString(), slot["readableSummary"]!.GetValue<string>()
                 })
            AssertRollScopeExamplePlayerText(playerText);
        foreach (var forbidden in new[] { "woundId", "effectId", "transitionId", "applicationRef", "effectChanges" })
            Assert.DoesNotContain("\"" + forbidden + "\"", response.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void EffectRollScopeWorkedExample_UsesCommonExactSelector()
    {
        var fences = ParseNamedJsonFences(
            "E_CLI_Effect_Materialization.txt", "effect_mortal_roll_scope_skill_v1");
        Assert.Equal(3, fences.Count);
        var skillAuthority = CreateRollScopeExampleAuthority(includeSourceSkill: true);
        Assert.True(JsonNode.DeepEquals(
            skillAuthority.CreateGmCatalog(), fences[0]["effectSkillScopeCatalog"]));
        var source = fences[1];
        var definition = Assert.IsType<JsonObject>(Assert.Single(
            source["activeEffectDefinitions"]!.AsArray()));
        using var document = JsonDocument.Parse(source["activeEffectDefinitions"]!.ToJsonString());
        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement, "effect_mortal_roll_scope_skill_v1", "mortal_world"));
        var components = Assert.IsType<JsonArray>(definition["components"]);
        var component = Assert.IsType<JsonObject>(Assert.Single(components));
        Assert.Equal("roll_modifier", component["profile"]!.GetValue<string>());
        Assert.Equal("skill", component["payload"]!["scope"]!["kind"]!.GetValue<string>());
        Assert.Equal("skill_lockpicking", component["payload"]!["scope"]!["skillId"]!.GetValue<string>());
        var response = fences[2];
        var command = Assert.IsType<JsonObject>(Assert.Single(response["effectChanges"]!.AsArray()));
        Assert.Equal("apply", command["operation"]!.GetValue<string>());
        Assert.Equal("skill", command["source"]!["kind"]!.GetValue<string>());
        Assert.Equal(source["skillId"]!.GetValue<string>(), command["source"]!["sourceId"]!.GetValue<string>());
        Assert.Equal("skill_precision_focus", command["source"]!["sourceId"]!.GetValue<string>());
        Assert.Equal(definition["definitionKey"]!.GetValue<string>(),
            command["source"]!["definitionKey"]!.GetValue<string>());
        Assert.Equal("player", command["target"]!["kind"]!.GetValue<string>());
        Assert.Equal("player_current", command["target"]!["targetId"]!.GetValue<string>());
        Assert.Equal("accepted_turn", command["eventRef"]!["kind"]!.GetValue<string>());
        Assert.Equal("turn_42", command["eventRef"]!["authorityId"]!.GetValue<string>());
        Assert.Empty(command["parameters"]!.AsObject());
        Assert.Empty(skillAuthority.ValidateNewComponents(
            new EffectTargetKey("mortal_world", "player", "player_current"), components, "example.components"));
        AssertRollScopeExamplePlayerText(response["response"]!.GetValue<string>());
        AssertRollScopeExamplePlayerText(command["reason"]!.GetValue<string>());
        AssertRollScopeExamplePlayerText(definition["display"]!.ToJsonString());
    }

    [Fact]
    public void AfterlifeEffectRollScopeWorkedExample_RemainsBroad()
    {
        var source = ParseNamedJsonFences("E_CLI_Afterlife_Turns.txt", "afterlife_effect_profile_v1")[0];
        var definitions = Assert.IsType<JsonArray>(source["activeEffectDefinitions"]);
        using var document = JsonDocument.Parse(definitions.ToJsonString());
        foreach (var realm in new[] { "chaos_sea", "shining_abode" })
            Assert.Empty(EffectSourceDefinitionContract.ValidateArray(document.RootElement, "afterlifeRollScope", realm));
        var component = Assert.IsType<JsonObject>(Assert.Single(
            Assert.Single(definitions)!["components"]!.AsArray()));
        Assert.Equal("roll_modifier", component["profile"]!.GetValue<string>());
        var payload = component["payload"]!;
        Assert.Equal("defense_roll", Assert.Single(payload["operations"]!.AsArray())!.GetValue<string>());
        Assert.Equal("all", payload["scope"]!["kind"]!.GetValue<string>());
        Assert.Single(payload["scope"]!.AsObject());
    }

    private static EffectRollSkillScopeAuthority CreateRollScopeExampleAuthority(
        bool includeSourceSkill = false)
    {
        var skills = new JsonArray(new JsonObject
        {
            ["skillId"] = "skill_lockpicking", ["skillName"] = "Взлом",
            ["active"] = true, ["lifecycle"] = "active"
        });
        if (includeSourceSkill)
            skills.Add(new JsonObject
            {
                ["skillId"] = "skill_precision_focus", ["skillName"] = "Точное сосредоточение",
                ["active"] = true, ["lifecycle"] = "active"
            });
        // Independent canonical fixture; never derive authority from the advisory request.
        var roots = new Dictionary<string, JsonNode?>
        {
            ["game_state/player/skills_active.json"] = new JsonObject { ["activeSkillChanges"] = skills }
        };
        return EffectRollSkillScopeAuthority.Build(new(roots, roots));
    }

    private static (WoundAcceptedTurnBinding Binding, WoundOpportunityAuthority Opportunity)
        CreateRollScopeExampleOpportunity(string publicRef)
    {
        var evidence = new WoundOpportunityEventEvidence(
            "narrative", "narrative_injury", "event_scope_example", "harmful", 2,
            "Принятый удар может оставить самостоятельную рану.");
        var acceptedEvent = new WoundAcceptedEventAuthority(
            "event_scope_example", "narrative_injury", "event_scope_example",
            WoundOpportunityEventEvidenceFingerprint.Compute(evidence));
        var events = new[] { acceptedEvent };
        var binding = new WoundAcceptedTurnBinding(
            "session_scope_example", "request_scope_example", "snapshot_scope_example",
            "mortal_world", 42, events, WoundAcceptedEventSetFingerprint.Compute(events));
        var result = WoundOpportunityAuthority.Compose(new WoundOpportunityBuildRequest(
            binding, "opportunity_scope_example", publicRef, acceptedEvent.EventRef,
            new WoundOwnerCoordinate("mortal_world", "player", "player_current", WoundCarrierCatalog.PlayerPath),
            "physical", "mortal_narrative_injury_v1", "combat_action", "action_scope_example", "active",
            evidence, 2, null, new WoundOpportunitySafeContext(
                "игрок", "последствия принятого удара", new[] { "anatomical", "systemic", "other" })));
        Assert.True(result.Success, DescribeRollScopeExampleIssues(result.Issues));
        return (binding, Assert.IsType<WoundOpportunityAuthority>(result.Opportunity));
    }

    private static string DescribeRollScopeExampleIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(static issue => issue.ToString()));

    private static void AssertRollScopeExamplePlayerText(string text)
    {
        foreach (var forbidden in new[]
                 {
                     "skill_lockpicking", "skill_precision_focus", "effectId", "woundId", "game_state/"
                 })
            Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
    }

    [Fact]
    public void WoundMaterializationManifest_CoversProfileMatrixAndWorkedSourceGraph()
    {
        var manifest = ExampleValidationManifest.Load();
        var required = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["wound_spiritual_profiles_v1"] = "E_CLI_Effect_Materialization.txt",
            ["wound_spiritual_source_worked_v1"] = "E_CLI_Effect_Materialization.txt"
        };

        foreach (var (contractId, expectedFile) in required)
        {
            var entry = Assert.Single(
                manifest.EffectMaterializationCoverage,
                candidate => string.Equals(
                    candidate.ContractId,
                    contractId,
                    StringComparison.Ordinal));
            Assert.Equal(expectedFile, entry.File);
            Assert.Contains("Chaos Sea", entry.Realms, StringComparer.Ordinal);
            Assert.Contains("Shining Abode", entry.Realms, StringComparer.Ordinal);
            AssertTruthfulValidationMetadata(entry);
            Assert.NotEmpty(entry.RequiredText);

            var example = File.ReadAllText(Path.Combine(
                TestRepoPaths.RepoRoot,
                "Examples",
                expectedFile));
            Assert.All(entry.RequiredText, token =>
                Assert.Contains(token, example, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void SpiritualWoundProfileExample_UsesExactClosedRuntimeCatalog()
    {
        Assert.Equal(8, RegisteredSpiritualWoundProfiles.Length);
        var root = Assert.Single(ParseNamedJsonFences(
            "E_CLI_Effect_Materialization.txt",
            "wound_spiritual_profiles_v1"));
        var fragments = Assert.IsType<JsonArray>(
                root["registeredSpiritualWoundProfileFragments"])
            .OfType<JsonObject>()
            .ToArray();

        Assert.Equal(
            RegisteredSpiritualWoundProfiles,
            fragments
                .Select(fragment => fragment["profile"]!.GetValue<string>())
                .OrderBy(static profile => profile, StringComparer.Ordinal));

        foreach (var fragment in fragments)
        {
            var profile = fragment["profile"]!.GetValue<string>();
            var descriptor = SpiritualWoundEffectProfileCatalog.Profiles[profile];
            var payload = Assert.IsType<JsonObject>(fragment["payload"]);
            Assert.Equal(descriptor.Axis, payload["axis"]!.GetValue<string>());
            Assert.Equal(
                new[] { "axis", "magnitude", "operation" },
                payload.Select(static property => property.Key)
                    .OrderBy(static property => property, StringComparer.Ordinal));

            foreach (var realm in new[] { "chaos_sea", "shining_abode" })
            {
                var definition = EffectMaterializationTestFixture
                    .CreateSpiritualWoundDefinition(
                        profile,
                        realm: realm,
                        definitionKey: "definition_documented_" + profile);
                definition["components"] = new JsonArray(fragment.DeepClone());
                definition["triggers"]![0]!["componentIds"] = new JsonArray(
                    fragment["componentId"]!.GetValue<string>());
                using var document = JsonDocument.Parse(
                    new JsonArray(definition).ToJsonString());
                Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
                    document.RootElement,
                    $"woundSpiritualProfiles.{profile}",
                    realm));
            }
        }
    }

    [Fact]
    public void SpiritualWoundWorkedExample_IsCompleteGmSourceGraphWithoutClientIds()
    {
        var root = Assert.Single(ParseNamedJsonFences(
            "E_CLI_Effect_Materialization.txt",
            "wound_spiritual_source_worked_v1"));
        var decision = Assert.Single(
            Assert.IsType<JsonArray>(root["woundDecisions"])
                .OfType<JsonObject>());
        Assert.Equal("materialize", decision["decision"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(
            decision["woundRef"]!.GetValue<string>()));
        var proposal = Assert.IsType<JsonObject>(decision["proposal"]);
        var definitions = Assert.IsType<JsonArray>(
                proposal["consequenceDefinitions"])
            .OfType<JsonObject>()
            .ToArray();
        Assert.NotEmpty(definitions);

        var documentedProfiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var wrapper in definitions)
        {
            Assert.False(string.IsNullOrWhiteSpace(
                wrapper["definitionRef"]!.GetValue<string>()));
            var definition = Assert.IsType<JsonObject>(wrapper["definition"]);
            Assert.Empty(Assert.IsType<JsonArray>(definition["links"]));
            var rootDescriptor = Assert.IsType<JsonObject>(wrapper["root"]);
            Assert.NotEmpty(Assert.IsType<JsonArray>(rootDescriptor["slots"]));
            foreach (var component in Assert.IsType<JsonArray>(
                         definition["components"]).OfType<JsonObject>())
            {
                documentedProfiles.Add(component["profile"]!.GetValue<string>());
            }

            var clientBound = definition.DeepClone().AsObject();
            clientBound["links"] = new JsonArray(new JsonObject
            {
                ["kind"] = "wound",
                ["targetId"] = "wound_documentation_validation",
                ["role"] = "source"
            });
            foreach (var realm in new[] { "chaos_sea", "shining_abode" })
            {
                clientBound["allowedRealms"] = new JsonArray(realm);
                using var document = JsonDocument.Parse(
                    new JsonArray(clientBound.DeepClone()).ToJsonString());
                Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
                    document.RootElement,
                    "woundSpiritualWorkedSource",
                    realm));
            }
        }

        Assert.All(documentedProfiles, profile =>
            Assert.Contains(profile, RegisteredSpiritualWoundProfiles));
        var serialized = root.ToJsonString();
        foreach (var forbidden in new[]
                 {
                     "woundId",
                     "effectId",
                     "complicationId",
                     "transitionId",
                     "applicationRef",
                     "effectChanges"
                 })
        {
            Assert.DoesNotContain(forbidden, serialized, StringComparison.Ordinal);
        }
    }
}
