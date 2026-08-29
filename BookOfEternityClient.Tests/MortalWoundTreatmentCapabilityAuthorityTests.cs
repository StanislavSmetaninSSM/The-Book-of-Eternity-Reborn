using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// RED contract for the T066 canonical-skill capability exporter.  The fixture is
/// deliberately source-shaped only: canonical accepted state, coordinates, proof
/// sealing, and final-plan after-images must remain production-owned.
/// </summary>
public sealed class MortalWoundTreatmentCapabilityAuthorityTests
{
    private const string CapabilityRef = "field_medicine_guaranteed_care";
    private const string SkillId = "skill_field_medicine_01";

    public static IEnumerable<object[]> CurrentExportRows => new[]
    {
        "player_active_skill_exports_physical_capability",
        "player_passive_skill_exports_physical_capability",
        "npc_active_skill_exports_physical_capability",
        "npc_passive_skill_exports_physical_capability",
        "provider_owned_proof_may_target_combatant",
        "provider_owned_proof_may_target_combatant_member",
        "target_owned_combatant_requires_promotion",
        "cross_root_player_npc_exact_skill_id_is_owner_scoped",
        "cross_root_player_npc_confusable_skill_id_is_owner_scoped",
        "ordinary_capability_projection_comes_from_same_skill",
        "mastery_gate_remains_separate_skill_tier_requirement",
        "idless_extension_bearing_skill_rejects",
        "duplicate_skill_id_across_active_and_passive_rejects",
        "case_changed_skill_id_rejects",
        "unicode_confusable_skill_id_rejects",
        "duplicate_capability_ref_across_skill_kinds_rejects",
        "unicode_confusable_capability_ref_rejects",
        "inactive_skill_rejects",
        "retired_skill_rejects",
        "wrong_owner_rejects",
        "wrong_role_rejects",
        "wrong_realm_rejects",
        "spiritual_domain_rejects",
        "unknown_domain_rejects",
        "invalid_severity_envelope_rejects",
        "open_extension_field_rejects",
        "open_operation_limits_field_rejects",
        "aggregate_operation_limit_overflow_rejects",
        "all_zero_operation_limit_rejects",
        "source_semantic_change_changes_proof",
        "display_name_change_never_grants_authority",
        "proof_is_detached_and_immutable",
        "proof_contains_only_context_coordinates_owner_skill_capability_and_fingerprints"
    }.Select(static row => new object[] { DescribeScenario(row, publication: false) });

    public static IEnumerable<object[]> PublicationExportRows => new[]
    {
        "untouched_player_skill_reads_current_lease_bound_root",
        "untouched_npc_skill_reads_current_lease_bound_root",
        "touched_player_active_skill_reads_exact_final_after_image",
        "touched_player_passive_skill_reads_exact_final_after_image",
        "touched_npc_skill_reads_exact_final_after_image",
        "unchanged_final_source_reexports_equal_proof",
        "removed_final_skill_rejects",
        "retired_final_skill_rejects",
        "changed_final_skill_id_rejects",
        "changed_final_capability_ref_rejects",
        "changed_final_domain_rejects",
        "changed_final_operation_limits_reject",
        "duplicate_or_confusable_final_skill_row_rejects",
        "confusable_final_skill_sibling_rejects",
        "stale_final_source_rejects",
        "publication_export_accepts_no_detached_json_or_publication_intent"
    }.Select(static row => new object[] { DescribeScenario(row, publication: true) });

    [Fact]
    public void IndependentControl_ExistingActiveWoundFixtureStillParses()
    {
        var parsed = WoundMaterializationContract.Parse(
            WoundContractTestData.CreateActiveWound().ToJsonString(),
            "wound");

        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        Assert.NotNull(parsed.Wound);
    }

    [Theory]
    [InlineData("provider_owned_proof_may_target_combatant")]
    [InlineData("provider_owned_proof_may_target_combatant_member")]
    public void FixtureControl_CombatTargetHasOneAcceptedCarrierAndIdentity(string name)
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(name, publication: false));

        fixture.AssertAcceptedCombatWoundCarrier();
    }

    [Fact]
    public void FixtureControl_PristineEffectRootsProduceAcceptedEmptyMechanicsSnapshot()
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario("player_active_skill_exports_physical_capability", publication: false));

        fixture.AssertPristineEffectMechanicsSnapshot();
    }

    [Fact]
    public void FixtureControl_SelectedSkillRowsUseExistingProductionMaterializationShapes()
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario("player_active_skill_exports_physical_capability", publication: false));

        fixture.AssertProductionValidSkillShapes();
    }

    [Fact]
    public void FixtureControl_LiveTurnRequestMatchesAcceptedBinding()
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario("player_active_skill_exports_physical_capability", publication: false));

        fixture.AssertLiveTurnRequestMatchesBinding();
    }

    [Theory]
    [MemberData(nameof(CurrentExportRows))]
    public void ExportCurrent_UsesOnlyCanonicalPlayerOrNpcSkillCapabilitySource(
        CapabilityScenario scenario)
    {
        using var fixture = CapabilityAuthorityFixture.Create(scenario);

        if (scenario.ExpectedBoundary == CapabilityFailureBoundary.Context)
        {
            AssertContextRejected(fixture, scenario);
            return;
        }

        if (scenario.ExpectedBoundary == CapabilityFailureBoundary.AcceptedState)
        {
            AssertAcceptedStateRejected(fixture, scenario);
            return;
        }

        var acceptedState = BuildAcceptedState(fixture);
        var coordinates = CreateCoordinates(fixture, acceptedState);
        fixture.ApplyLiveExporterMutation();
        var result = InvokeCapabilityExporter(
            "ExportCurrent",
            acceptedState,
            coordinates,
            CapabilityRef,
            scenario.ActorRole);
        AssertCapabilityResult(result, scenario);
        AssertScenarioSpecificCurrentSemantics(fixture, scenario, result);
    }

    [Theory]
    [MemberData(nameof(PublicationExportRows))]
    public void ExportForPublication_RevalidatesTheExactCurrentOrFinalSkillSource(
        CapabilityScenario scenario)
    {
        using var fixture = CapabilityAuthorityFixture.Create(scenario);

        var result = InvokeExportForPublication(fixture, scenario);
        AssertCapabilityResult(result, scenario);
    }

    private static CapabilityProofView InvokeExportCurrent(
        CapabilityAuthorityFixture fixture,
        CapabilityScenario scenario)
    {
        var acceptedState = BuildAcceptedState(fixture);
        var coordinates = CreateCoordinates(fixture, acceptedState);
        return InvokeCapabilityExporter(
            "ExportCurrent",
            acceptedState,
            coordinates,
            CapabilityRef,
            scenario.ActorRole);
    }

    private static CapabilityProofView InvokeExportForPublication(
        CapabilityAuthorityFixture fixture,
        CapabilityScenario scenario)
    {
        var acceptedState = BuildAcceptedState(fixture);
        var coordinates = CreateCoordinates(fixture, acceptedState);
        var publicationPlan = BuildPublicationPlan(fixture, acceptedState, coordinates);
        var result = InvokeCapabilityExporter(
            "ExportForPublication",
            acceptedState,
            coordinates,
            CapabilityRef,
            scenario.ActorRole,
            publicationPlan);
        fixture.AssertPublicationExportReadsSelectedRoot(result);
        if (scenario.Name == "unchanged_final_source_reexports_equal_proof")
        {
            var current = InvokeExportCurrent(fixture, scenario);
            Assert.NotNull(current.Proof);
            Assert.NotNull(result.Proof);
            Assert.Equal(
                ReadRequiredProperty(current.Proof!, "SourceSemanticFingerprint"),
                ReadRequiredProperty(result.Proof!, "SourceSemanticFingerprint"));
            Assert.Equal(
                ReadRequiredProperty(current.Proof!, "ProofFingerprint"),
                ReadRequiredProperty(result.Proof!, "ProofFingerprint"));
        }
        if (scenario.Name.StartsWith("touched_", StringComparison.Ordinal))
        {
            var current = InvokeExportCurrent(fixture, scenario);
            Assert.NotNull(current.Proof);
            Assert.NotNull(result.Proof);
            Assert.NotEqual(
                ReadRequiredProperty(current.Proof!, "SourceSemanticFingerprint"),
                ReadRequiredProperty(result.Proof!, "SourceSemanticFingerprint"));
            Assert.NotEqual(
                ReadRequiredProperty(current.Proof!, "ProofFingerprint"),
                ReadRequiredProperty(result.Proof!, "ProofFingerprint"));
        }
        return result;
    }

    private static object BuildAcceptedState(CapabilityAuthorityFixture fixture)
    {
        var result = BuildAcceptedStateResult(fixture);
        AssertValidResultShell(result.IsValid, result.Issues, result.Authority, "accepted state");
        return result.Authority!;
    }

    private static AcceptedStateView BuildAcceptedStateResult(CapabilityAuthorityFixture fixture)
    {
        var treatmentAuthorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(treatmentAuthorityType);

        var parsedContext = ParseAuthorityInputResult(
            treatmentAuthorityType,
            "ParseContext",
            "Context",
            fixture.ContextRoot,
            "treatmentContext");
        AssertValidResultShell(parsedContext.IsValid, parsedContext.Issues, parsedContext.Value, "treatment context");
        var context = parsedContext.Value!;
        var method = Assert.Single(
            typeof(AcceptedTurnAuthorityRegistry).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate =>
                candidate.Name == "GetOrBuildMortalWoundTreatmentAcceptedState" &&
                candidate.GetParameters().Length == 5);
        var parameters = method.GetParameters();
        Assert.Equal(typeof(FileSystemManager), parameters[0].ParameterType);
        Assert.Equal(fixture.Lease.GetType(), parameters[1].ParameterType);
        Assert.Equal(typeof(WoundAcceptedTurnBinding), parameters[2].ParameterType);
        Assert.Equal(context.GetType(), parameters[3].ParameterType);
        Assert.Equal(typeof(string), parameters[4].ParameterType);

        var result = Invoke(method, new object[]
        {
            fixture.FileSystem,
            fixture.Lease,
            fixture.Binding,
            context,
            CapabilityAuthorityFixture.WoundId
        });
        var typedResult = ReadTypedResult(result, "Authority");
        return new AcceptedStateView(
            typedResult.IsValid,
            typedResult.Issues,
            typedResult.Value);
    }

    private static void AssertContextRejected(
        CapabilityAuthorityFixture fixture,
        CapabilityScenario scenario)
    {
        var treatmentAuthorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(treatmentAuthorityType);
        var result = ParseAuthorityInputResult(
            treatmentAuthorityType,
            "ParseContext",
            "Context",
            fixture.ContextRoot,
            "treatmentContext");
        AssertInvalidResultShell(result.IsValid, result.Issues, result.Value, scenario, "context");
    }

    private static void AssertAcceptedStateRejected(
        CapabilityAuthorityFixture fixture,
        CapabilityScenario scenario)
    {
        var result = BuildAcceptedStateResult(fixture);
        AssertInvalidResultShell(result.IsValid, result.Issues, result.Authority, scenario, "accepted state");
    }

    private static object CreateCoordinates(
        CapabilityAuthorityFixture fixture,
        object acceptedState)
    {
        var plannerType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentPlanner",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(plannerType);
        var method = Assert.Single(
            plannerType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate =>
                candidate.Name == "CreateAttemptCoordinates" &&
                candidate.GetParameters().Length == 5);
        var parameters = method.GetParameters();
        Assert.Equal(acceptedState.GetType(), parameters[0].ParameterType);
        Assert.Equal(typeof(WoundMaterializationEnvelope), parameters[1].ParameterType);
        Assert.Equal(typeof(string), parameters[2].ParameterType);
        Assert.Equal(typeof(string), parameters[3].ParameterType);
        Assert.Equal(typeof(string), parameters[4].ParameterType);

        var result = Invoke(method, new object[]
        {
            acceptedState,
            fixture.Before,
            CapabilityAuthorityFixture.OperationKey,
            CapabilityAuthorityFixture.RouteId,
            CapabilityAuthorityFixture.EventRef
        });
        return ReadValidTypedResult(result, "Coordinates", "attempt coordinates");
    }

    private static object BuildPublicationPlan(
        CapabilityAuthorityFixture fixture,
        object acceptedState,
        object coordinates)
    {
        // T066 must own publication composition: tests must never assemble an
        // AcceptedMechanicsInput, authority fingerprints, event export, before image,
        // or owner companion after-image.  This coordinator is responsible for taking
        // the exact same lease/binding accepted state and re-peeking the validated
        // common cache that already holds the final canonical skill root.
        var coordinatorType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentCapabilityPublicationCoordinator",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(coordinatorType);
        var method = Assert.Single(
            coordinatorType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.Name == "GetOrBuildValidatedPlan" &&
                                candidate.GetParameters().Length == 2);
        var parameters = method.GetParameters();
        Assert.Equal(acceptedState.GetType(), parameters[0].ParameterType);
        Assert.Equal(coordinates.GetType(), parameters[1].ParameterType);
        var result = Invoke(method, new[] { acceptedState, coordinates });
        var publicationPlan = ReadValidTypedResult(result, "Plan", "capability publication coordinator");
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var cachedBinding,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        Assert.Same(publicationPlan, cached.Plan);
        fixture.AssertCachedPublicationPlanBinding(cachedBinding);
        fixture.AssertPublicationPlanBindsOnlyTheSelectedRoot(publicationPlan);
        return publicationPlan;
    }

    private static CapabilityProofView InvokeCapabilityExporter(
        string methodName,
        object acceptedState,
        object coordinates,
        string capabilityRef,
        string actorRole,
        object? publicationPlan = null)
    {
        var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentCapabilityAuthority",
            throwOnError: false,
            ignoreCase: false);

        Assert.NotNull(authorityType);

        var method = Assert.Single(
            authorityType.GetMethods(BindingFlags.Public | BindingFlags.Static),
            candidate =>
                candidate.Name == methodName &&
                candidate.GetParameters().Length == (publicationPlan is null ? 4 : 5));

        var parameters = method.GetParameters();
        Assert.Equal(acceptedState.GetType(), parameters[0].ParameterType);
        Assert.Equal(coordinates.GetType(), parameters[1].ParameterType);
        Assert.Equal(typeof(string), parameters[2].ParameterType);
        Assert.Equal(typeof(string), parameters[3].ParameterType);
        if (publicationPlan is not null)
        {
            Assert.Equal(publicationPlan.GetType(), parameters[4].ParameterType);
        }

        var arguments = publicationPlan is null
            ? new[] { acceptedState, coordinates, (object)capabilityRef, actorRole }
            : new[] { acceptedState, coordinates, (object)capabilityRef, actorRole, publicationPlan };
        var result = Invoke(method, arguments);
        Assert.Equal("MortalWoundTreatmentCapabilityProofResult", result.GetType().Name);
        var isValid = Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid"));
        var issues = ReadEnumerableProperty(result, "Issues")
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        var proof = ReadPropertyAllowingNull(result, "Proof");
        return new CapabilityProofView(isValid, issues, proof);
    }

    private static JsonObject CreateCanonicalSkillFixture() => new()
    {
        ["realm"] = "mortal_world",
        ["player"] = CreateSkillOwner("player_current"),
        ["npc"] = CreateSkillOwner("npc_field_medic_01")
    };

    private static JsonObject CreateSkillOwner(string ownerId) => new()
    {
        ["ownerId"] = ownerId,
        ["activeSkills"] = new JsonArray(CreateSkill("active")),
        ["passiveSkills"] = new JsonArray(CreateSkill("passive"))
    };

    private static JsonObject CreateSkill(string kind)
    {
        // These are the smallest current production-valid materialization shapes from
        // ActorMaterializationContractTests.  The capability adapter must consume the
        // canonical skill rows, not a parallel test-only projection.
        var common = new JsonObject
        {
            ["skillId"] = kind == "active" ? SkillId : "skill_triage_passive_01",
            ["displayName"] = kind == "active" ? "Field Medicine" : "Triage Discipline",
            ["lifecycle"] = "active",
            ["active"] = true,
            ["tier"] = 3,
            ["skillName"] = kind == "active" ? "Field Medicine" : "Triage Discipline",
            ["skillDescription"] = kind == "active"
                ? "Provides precise field care under pressure."
                : "Retains a disciplined triage routine.",
            ["rarity"] = "Common"
        };
        if (kind == "active")
        {
            common["actionCost"] = "Main";
            common["combatEffect"] = new JsonObject
            {
                ["isActivatedEffect"] = true,
                ["actionName"] = "Field treatment",
                ["effects"] = new JsonArray(new JsonObject
                {
                    ["effectType"] = "Damage",
                    ["value"] = "10%",
                    ["targetType"] = "Enemy",
                    ["effectDescription"] = "A controlled intervention.",
                    ["poiseDamage"] = "5%"
                })
            };
        }
        else
        {
            common["type"] = "Utility";
            common["group"] = "Medicine";
            common["masteryLevel"] = 3;
            common["maxMasteryLevel"] = 5;
            common["structuredBonuses"] = null;
        }

        return common;
    }

    private static void ConfigureCapabilitySource(
        JsonObject fixture,
        CapabilityScenario scenario)
    {
        foreach (var owner in new[] { "player", "npc" })
        {
            foreach (var skillArray in new[] { "activeSkills", "passiveSkills" })
            {
                Skill(fixture, owner, skillArray).Remove("mortalWoundTreatmentCapabilities");
            }
        }

        var source = Skill(fixture, scenario.SourceOwner, scenario.SourceSkillArray);
        source["skillId"] = scenario.SkillId;
        AddCapability(source, CapabilityRef);
    }

    private static void AddCapability(JsonObject skill, string capabilityRef)
    {
        skill["mortalWoundTreatmentCapabilities"] = new JsonArray(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["capabilityRef"] = capabilityRef,
            ["woundDomain"] = "physical",
            ["minimumSeverityRank"] = 1,
            ["maximumSeverityRank"] = 4,
            ["operationLimits"] = new JsonObject
            {
                ["mayStabilize"] = true,
                ["maximumRecoveryPoints"] = 1,
                ["maximumSeverityReductionSteps"] = 1,
                ["removableComplicationKinds"] = new JsonArray("infection"),
                ["mayHealAtSeverityI"] = true,
                ["maximumCosmeticHealLegacies"] = 1,
                ["maximumMechanicalEffectHealLegacies"] = 1
            }
        });
    }

    private static JsonObject OtherSkill(JsonObject fixture, CapabilityScenario scenario) =>
        Skill(
            fixture,
            scenario.SourceOwner,
            scenario.SourceSkillArray == "activeSkills" ? "passiveSkills" : "activeSkills");

    private static void ApplyScenario(JsonObject fixture, CapabilityScenario scenario)
    {
        ConfigureCapabilitySource(fixture, scenario);
        var sourceSkill = Skill(fixture, scenario.SourceOwner, scenario.SourceSkillArray);
        var capability = Capability(sourceSkill);

        switch (scenario.Name)
        {
            case "idless_extension_bearing_skill_rejects":
                sourceSkill.Remove("skillId");
                break;
            case "duplicate_skill_id_across_active_and_passive_rejects":
                OtherSkill(fixture, scenario)["skillId"] = sourceSkill["skillId"]!.DeepClone();
                break;
            case "cross_root_player_npc_exact_skill_id_is_owner_scoped":
                Skill(fixture, "player", "passiveSkills")["skillId"] = sourceSkill["skillId"]!.DeepClone();
                break;
            case "cross_root_player_npc_confusable_skill_id_is_owner_scoped":
                Skill(fixture, "player", "passiveSkills")["skillId"] =
                    scenario.SkillId.Replace("i", "і", StringComparison.Ordinal);
                break;
            case "case_changed_skill_id_rejects":
                OtherSkill(fixture, scenario)["skillId"] = scenario.SkillId.ToUpperInvariant();
                break;
            case "unicode_confusable_skill_id_rejects":
                OtherSkill(fixture, scenario)["skillId"] =
                    scenario.SkillId.Replace("i", "і", StringComparison.Ordinal);
                break;
            case "duplicate_capability_ref_across_skill_kinds_rejects":
                AddCapability(OtherSkill(fixture, scenario), CapabilityRef);
                break;
            case "unicode_confusable_capability_ref_rejects":
                AddCapability(OtherSkill(fixture, scenario),
                    CapabilityRef.Replace("i", "і", StringComparison.Ordinal));
                break;
            case "inactive_skill_rejects":
                sourceSkill["active"] = false;
                break;
            case "retired_skill_rejects":
                sourceSkill["lifecycle"] = "retired";
                break;
            case "wrong_realm_rejects":
                fixture["realm"] = "chaos_sea";
                break;
            case "spiritual_domain_rejects":
                capability["woundDomain"] = "spiritual";
                break;
            case "unknown_domain_rejects":
                capability["woundDomain"] = "unknown";
                break;
            case "invalid_severity_envelope_rejects":
                capability["minimumSeverityRank"] = 4;
                capability["maximumSeverityRank"] = 1;
                break;
            case "open_extension_field_rejects":
                capability["callerMayOverride"] = true;
                break;
            case "open_operation_limits_field_rejects":
                Limits(capability)["callerMayOverride"] = true;
                break;
            case "aggregate_operation_limit_overflow_rejects":
                Limits(capability)["maximumRecoveryPoints"] = long.MaxValue;
                break;
            case "all_zero_operation_limit_rejects":
                Limits(capability)["mayStabilize"] = false;
                Limits(capability)["maximumRecoveryPoints"] = 0;
                Limits(capability)["maximumSeverityReductionSteps"] = 0;
                Limits(capability)["removableComplicationKinds"] = new JsonArray();
                Limits(capability)["mayHealAtSeverityI"] = false;
                Limits(capability)["maximumCosmeticHealLegacies"] = 0;
                Limits(capability)["maximumMechanicalEffectHealLegacies"] = 0;
                break;
            case "source_semantic_change_changes_proof":
            case "changed_final_operation_limits_reject":
                Limits(capability)["maximumRecoveryPoints"] = 2;
                break;
            case "touched_player_active_skill_reads_exact_final_after_image":
            case "touched_player_passive_skill_reads_exact_final_after_image":
            case "touched_npc_skill_reads_exact_final_after_image":
                // A touched final companion must be semantically different from its
                // sealed current root, otherwise a current-root reader could pass.
                Limits(capability)["maximumRecoveryPoints"] = 2;
                break;
            case "display_name_change_never_grants_authority":
                sourceSkill["displayName"] = "Unrelated wording only";
                break;
            case "removed_final_skill_rejects":
                fixture[scenario.SourceOwner]![scenario.SourceSkillArray] = new JsonArray();
                break;
            case "retired_final_skill_rejects":
                sourceSkill["lifecycle"] = "retired";
                break;
            case "changed_final_skill_id_rejects":
                sourceSkill["skillId"] = "skill_after_image_changed_01";
                break;
            case "changed_final_capability_ref_rejects":
                capability["capabilityRef"] = "after_image_changed_capability";
                break;
            case "changed_final_domain_rejects":
                capability["woundDomain"] = "spiritual";
                break;
            case "duplicate_or_confusable_final_skill_row_rejects":
                OtherSkill(fixture, scenario)["skillId"] = sourceSkill["skillId"]!.DeepClone();
                break;
            case "confusable_final_skill_sibling_rejects":
                OtherSkill(fixture, scenario)["skillId"] =
                    scenario.SkillId.Replace("i", "і", StringComparison.Ordinal);
                break;
            case "stale_final_source_rejects":
                sourceSkill["active"] = false;
                break;
        }
    }

    private static void AssertCanonicalFixtureShape(JsonObject fixture)
    {
        Assert.NotNull(fixture["realm"]);
        Assert.NotNull(fixture["player"]);
        Assert.NotNull(fixture["npc"]);
        Assert.NotNull(fixture["player"]!["activeSkills"]);
        Assert.NotNull(fixture["player"]!["passiveSkills"]);
        Assert.NotNull(fixture["npc"]!["activeSkills"]);
        Assert.NotNull(fixture["npc"]!["passiveSkills"]);
    }

    private static void AssertCapabilityResult(
        CapabilityProofView result,
        CapabilityScenario scenario)
    {
        Assert.Equal(scenario.ExpectedValid, result.IsValid);
        if (!scenario.ExpectedValid)
        {
            AssertInvalidResultShell(result.IsValid, result.Issues, result.Proof, scenario, "capability exporter");
            return;
        }

        Assert.Empty(result.Issues);
        Assert.NotNull(result.Proof);
        var proof = result.Proof!;
        AssertProofShape(proof, scenario.Name);
        Assert.Equal(scenario.SkillId, Assert.IsType<string>(ReadRequiredProperty(proof, "SkillId")));
        Assert.Equal(scenario.SourceOwner, Assert.IsType<string>(ReadRequiredProperty(proof, "OwnerKind")));
        Assert.Equal(
            scenario.SourceOwner == "player" ? "player_current" : "npc_field_medic_01",
            Assert.IsType<string>(ReadRequiredProperty(proof, "OwnerId")));
        Assert.Equal(
            scenario.SourceSkillArray == "activeSkills" ? "active" : "passive",
            Assert.IsType<string>(ReadRequiredProperty(proof, "SkillKind")));
    }

    private static void AssertProofShape(object proof, string scenario)
    {
        Assert.False(proof is JsonNode or JsonElement or JsonDocument);
        var properties = proof.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetIndexParameters().Length == 0)
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "AcceptedStateFingerprint",
                "CapabilityRef",
                "ContextFingerprint",
                "CoordinatesFingerprint",
                "MaximumSeverityRank",
                "MinimumSeverityRank",
                "OperationLimits",
                "OwnerId",
                "OwnerKind",
                "ProofFingerprint",
                "SkillId",
                "SkillKind",
                "SnapshotToken",
                "SourcePath",
                "SourceSemanticFingerprint",
                "WoundDomain"
            },
            properties);
        Assert.Equal(CapabilityRef, Assert.IsType<string>(ReadRequiredProperty(proof, "CapabilityRef")));
        Assert.Equal("physical", Assert.IsType<string>(ReadRequiredProperty(proof, "WoundDomain")));
        Assert.InRange(Assert.IsType<int>(ReadRequiredProperty(proof, "MinimumSeverityRank")), 1, 4);
        Assert.InRange(Assert.IsType<int>(ReadRequiredProperty(proof, "MaximumSeverityRank")), 1, 4);
        Assert.NotEqual(string.Empty, Assert.IsType<string>(ReadRequiredProperty(proof, "SourceSemanticFingerprint")));
        Assert.NotEqual(string.Empty, Assert.IsType<string>(ReadRequiredProperty(proof, "ProofFingerprint")));
        Assert.False(
            JsonSerializer.Serialize(proof, proof.GetType()).Contains("mortalWoundTreatmentCapabilities", StringComparison.Ordinal),
            $"{scenario} leaked source JSON through the detached proof.");
    }

    private static void AssertScenarioSpecificCurrentSemantics(
        CapabilityAuthorityFixture fixture,
        CapabilityScenario scenario,
        CapabilityProofView result)
    {
        if (!scenario.ExpectedValid)
            return;

        switch (scenario.Name)
        {
            case "source_semantic_change_changes_proof":
            case "display_name_change_never_grants_authority":
                using (var baselineFixture = CapabilityAuthorityFixture.Create(
                           scenario with { Name = "baseline_current_source" }))
                {
                    var baseline = InvokeExportCurrent(baselineFixture, baselineFixture.Scenario);
                    AssertCapabilityResult(baseline, baselineFixture.Scenario);
                    Assert.NotNull(baseline.Proof);
                    Assert.NotNull(result.Proof);
                    var baselineSource = Assert.IsType<string>(ReadRequiredProperty(
                        baseline.Proof!, "SourceSemanticFingerprint"));
                    var currentSource = Assert.IsType<string>(ReadRequiredProperty(
                        result.Proof!, "SourceSemanticFingerprint"));
                    var baselineProof = Assert.IsType<string>(ReadRequiredProperty(
                        baseline.Proof!, "ProofFingerprint"));
                    var currentProof = Assert.IsType<string>(ReadRequiredProperty(
                        result.Proof!, "ProofFingerprint"));
                    if (scenario.Name == "source_semantic_change_changes_proof")
                    {
                        Assert.NotEqual(baselineSource, currentSource);
                        Assert.NotEqual(baselineProof, currentProof);
                    }
                    else
                    {
                        Assert.Equal(baselineSource, currentSource);
                        Assert.Equal(baselineProof, currentProof);
                    }
                }
                break;
            case "proof_is_detached_and_immutable":
                Assert.NotNull(result.Proof);
                var beforeMutation = JsonSerializer.Serialize(result.Proof, result.Proof!.GetType());
                var operationLimits = ReadRequiredProperty(result.Proof, "OperationLimits");
                Assert.All(
                    operationLimits.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public),
                    static property => Assert.False(property.CanWrite));
                var complicationKinds = ReadRequiredProperty(
                    operationLimits,
                    "RemovableComplicationKinds");
                if (complicationKinds is IList mutableKinds)
                {
                    Assert.ThrowsAny<Exception>(() => mutableKinds.Add("forged"));
                }
                fixture.MutatePersistedSelectedSourceAfterExport();
                Assert.Equal(beforeMutation, JsonSerializer.Serialize(result.Proof, result.Proof.GetType()));
                break;
            case "ordinary_capability_projection_comes_from_same_skill":
            case "mastery_gate_remains_separate_skill_tier_requirement":
                AssertGuaranteedRequirementBundleDelegatesToUnchangedT060(fixture);
                break;
        }
    }

    private static void AssertGuaranteedRequirementBundleDelegatesToUnchangedT060(
        CapabilityAuthorityFixture fixture)
    {
        var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(authorityType);
        // The existing baseline exposes no public accepted ParseSnapshot that this
        // fixture can construct without becoming a second requirement authority.
        // Freeze the exact three-argument target that CreateForGuaranteed delegates
        // to, then assert that production factory's returned, typed T060 surface.
        var resolver = Assert.Single(
            authorityType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.Name == "ResolveRequirements" &&
                                candidate.GetParameters().Length == 3);
        Assert.Equal(typeof(WoundTreatmentRoute), resolver.GetParameters()[0].ParameterType);

        var acceptedState = BuildAcceptedState(fixture);
        var coordinates = CreateCoordinates(fixture, acceptedState);
        var bundleType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentRequirementAuthorityBundle",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(bundleType);
        var factory = Assert.Single(
            bundleType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.Name == "CreateForGuaranteed" &&
                                candidate.GetParameters().Length == 3);
        var parameters = factory.GetParameters();
        Assert.Equal(acceptedState.GetType(), parameters[0].ParameterType);
        Assert.Equal(coordinates.GetType(), parameters[1].ParameterType);
        Assert.Equal(typeof(WoundMaterializationEnvelope), parameters[2].ParameterType);
        var result = Invoke(factory, new[] { acceptedState, coordinates, (object)fixture.Before });
        var authority = ReadValidTypedResult(result, "Authority", "guaranteed requirement bundle");
        AssertClosedProperties(authority, new[]
        {
            "Mode", "ContextFingerprint", "AcceptedStateFingerprint", "RouteFingerprint", "CourseId",
            "CourseMilestoneOrdinal", "CourseCoordinateFingerprint", "CourseRequirementStatus",
            "InterruptionReason", "Scopes", "AuthorityFingerprint"
        });
        Assert.Equal("guaranteed", Assert.IsType<string>(ReadRequiredProperty(authority, "Mode")));
        Assert.Null(ReadPropertyAllowingNull(authority, "CourseId"));
        Assert.Null(ReadPropertyAllowingNull(authority, "CourseMilestoneOrdinal"));
        Assert.Null(ReadPropertyAllowingNull(authority, "CourseCoordinateFingerprint"));
        Assert.Null(ReadPropertyAllowingNull(authority, "CourseRequirementStatus"));
        Assert.Null(ReadPropertyAllowingNull(authority, "InterruptionReason"));
        AssertAuthorityFingerprint(ReadRequiredProperty(authority, "AuthorityFingerprint"));

        var scopes = ReadEnumerableProperty(authority, "Scopes").ToArray();
        var scope = Assert.Single(scopes);
        AssertClosedProperties(scope, new[]
        {
            "Scope", "CourseMilestoneOrdinal", "Status", "Bindings", "FailureWitnesses",
            "AuthorityFingerprint"
        });
        Assert.Equal("common", Assert.IsType<string>(ReadRequiredProperty(scope, "Scope")));
        Assert.Null(ReadPropertyAllowingNull(scope, "CourseMilestoneOrdinal"));
        Assert.Equal("Satisfied", Assert.IsType<string>(ReadRequiredProperty(scope, "Status")));
        Assert.Empty(ReadEnumerableProperty(scope, "FailureWitnesses"));
        AssertAuthorityFingerprint(ReadRequiredProperty(scope, "AuthorityFingerprint"));

        var bindings = ReadEnumerableProperty(scope, "Bindings").ToArray();
        Assert.Equal(2, bindings.Length);
        AssertGuaranteedBinding(bindings[0], fixture, requirementIndex: 0, "source_capability", CapabilityRef);
        AssertGuaranteedBinding(bindings[1], fixture, requirementIndex: 1, "skill_tier", fixture.Scenario.SkillId);
        AssertNoGuaranteeProofOrLimitSurface(authority);
    }

    private static void AssertGuaranteedBinding(
        object binding,
        CapabilityAuthorityFixture fixture,
        int requirementIndex,
        string kind,
        string authorityRef)
    {
        AssertClosedProperties(binding, new[]
        {
            "RequirementIndex", "ResolvedRequirement", "SuccessWitness", "BindingFingerprint"
        });
        Assert.Equal(requirementIndex, Assert.IsType<int>(ReadRequiredProperty(binding, "RequirementIndex")));
        AssertAuthorityFingerprint(ReadRequiredProperty(binding, "BindingFingerprint"));

        var row = ReadRequiredProperty(binding, "ResolvedRequirement");
        AssertClosedProperties(row, new[]
        {
            "AuthorityFingerprint", "AuthorityRef", "CurrentState", "CurrentTier", "Kind", "LocationId",
            "MinimumTier", "OwnerId", "OwnerKind", "ProviderId", "ProviderKind", "Realm",
            "RequestedQuantity", "RequirementIndex", "TargetId", "TargetKind"
        });
        Assert.Equal(requirementIndex, Assert.IsType<int>(ReadRequiredProperty(row, "RequirementIndex")));
        Assert.Equal(kind, Assert.IsType<string>(ReadRequiredProperty(row, "Kind")));
        Assert.Equal(authorityRef, Assert.IsType<string>(ReadRequiredProperty(row, "AuthorityRef")));
        Assert.Equal("mortal_world", Assert.IsType<string>(ReadRequiredProperty(row, "Realm")));
        Assert.Equal(fixture.Scenario.ProviderKind, ReadNullableStringProperty(row, "ProviderKind"));
        Assert.Equal(fixture.Scenario.ProviderId, ReadNullableStringProperty(row, "ProviderId"));
        Assert.Equal(fixture.Scenario.TargetKind, ReadNullableStringProperty(row, "TargetKind"));
        Assert.Equal(fixture.Scenario.TargetId, ReadNullableStringProperty(row, "TargetId"));
        Assert.Equal("loc_field_clinic_001", ReadNullableStringProperty(row, "LocationId"));
        AssertAuthorityFingerprint(ReadRequiredProperty(row, "AuthorityFingerprint"));

        var witness = ReadRequiredProperty(binding, "SuccessWitness");
        Assert.False(witness is JsonNode or JsonDocument or JsonElement);
        foreach (var name in new[]
                 {
                     "Scope", "RequirementIndex", "Kind", "AuthorityRef", "SnapshotToken", "Realm",
                     "WitnessFingerprint"
                 })
            Assert.NotNull(witness.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public));
        Assert.Equal("common", Assert.IsType<string>(ReadRequiredProperty(witness, "Scope")));
        Assert.Equal(requirementIndex, Assert.IsType<int>(ReadRequiredProperty(witness, "RequirementIndex")));
        Assert.Equal(kind, Assert.IsType<string>(ReadRequiredProperty(witness, "Kind")));
        Assert.Equal(authorityRef, Assert.IsType<string>(ReadRequiredProperty(witness, "AuthorityRef")));
        Assert.Equal("mortal_world", Assert.IsType<string>(ReadRequiredProperty(witness, "Realm")));
        AssertAuthorityFingerprint(ReadRequiredProperty(witness, "WitnessFingerprint"));
        AssertNoGuaranteeProofOrLimitSurface(witness);
    }

    private static void AssertNoGuaranteeProofOrLimitSurface(object value)
    {
        Assert.DoesNotContain(
            value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name),
            static name => name.Contains("Proof", StringComparison.Ordinal) ||
                           name.Contains("Limit", StringComparison.Ordinal) ||
                           name.Contains("mortalWoundTreatmentCapabilities", StringComparison.Ordinal));
    }

    private static void AssertAuthorityFingerprint(object fingerprint) =>
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            Assert.IsType<string>(fingerprint)));

    private static void AssertClosedProperties(object value, IEnumerable<string> expected) =>
        Assert.Equal(
            expected.OrderBy(static name => name, StringComparer.Ordinal),
            value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));

    private static string? ReadNullableStringProperty(object instance, string name)
    {
        var value = ReadPropertyAllowingNull(instance, name);
        return value is null ? null : Assert.IsType<string>(value);
    }

    private static TypedResultView ParseAuthorityInputResult(
        Type authorityType,
        string methodName,
        string valueProperty,
        JsonObject root,
        string path)
    {
        var method = Assert.Single(
            authorityType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate =>
                candidate.Name == methodName &&
                candidate.GetParameters().Length == 2 &&
                candidate.GetParameters().All(static parameter => parameter.ParameterType == typeof(string)));
        var parsed = Invoke(method, new object[] { root.ToJsonString(), path });
        var result = ReadTypedResult(parsed, valueProperty);
        if (result.Value is not null)
            Assert.False(result.Value is JsonNode or JsonElement or JsonDocument or string);
        return result;
    }

    private static object ReadValidTypedResult(object result, string valueProperty, string boundary)
    {
        var parsed = ReadTypedResult(result, valueProperty);
        AssertValidResultShell(parsed.IsValid, parsed.Issues, parsed.Value, boundary);
        return parsed.Value!;
    }

    private static TypedResultView ReadTypedResult(object result, string valueProperty)
    {
        var properties = result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetIndexParameters().Length == 0)
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[] { "IsValid", "Issues", valueProperty }.OrderBy(static name => name, StringComparer.Ordinal),
            properties);
        var isValid = Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid"));
        var issues = ReadEnumerableProperty(result, "Issues")
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        var value = ReadPropertyAllowingNull(result, valueProperty);
        return new TypedResultView(isValid, issues, value);
    }

    private static void AssertValidResultShell(
        bool isValid,
        IReadOnlyList<ValidationIssue> issues,
        object? value,
        string boundary)
    {
        Assert.True(isValid, $"{boundary} failed: {DescribeIssues(issues)}");
        Assert.Empty(issues);
        Assert.NotNull(value);
    }

    private static void AssertInvalidResultShell(
        bool isValid,
        IReadOnlyList<ValidationIssue> issues,
        object? value,
        CapabilityScenario scenario,
        string actualBoundary)
    {
        Assert.NotEqual(CapabilityFailureBoundary.None, scenario.ExpectedBoundary);
        Assert.Equal(scenario.ExpectedBoundary.ToString(), actualBoundary switch
        {
            "context" => CapabilityFailureBoundary.Context.ToString(),
            "accepted state" => CapabilityFailureBoundary.AcceptedState.ToString(),
            "capability exporter" => CapabilityFailureBoundary.Exporter.ToString(),
            _ => actualBoundary
        });
        Assert.False(isValid);
        Assert.Null(value);
        Assert.NotEmpty(issues);
        Assert.All(issues, static issue =>
        {
            Assert.False(string.IsNullOrWhiteSpace(issue.Code));
            Assert.False(string.IsNullOrWhiteSpace(issue.FilePath));
        });
        if (scenario.ExpectedCode is not null)
        {
            Assert.NotNull(scenario.ExpectedPath);
            Assert.Contains(
                issues,
                issue =>
                    string.Equals(issue.Code, scenario.ExpectedCode, StringComparison.Ordinal) &&
                    string.Equals(issue.FilePath, scenario.ExpectedPath, StringComparison.Ordinal));
        }
        AssertFrozenIssues(issues);
    }

    private static void AssertFrozenIssues(IReadOnlyList<ValidationIssue> issues)
    {
        Assert.DoesNotContain(
            issues.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public),
            static property => property.CanWrite);
        if (issues is IList mutableIssues)
            Assert.ThrowsAny<Exception>(() => mutableIssues.Add(null));
        else
            Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(issues);
    }

    private static object Invoke(MethodInfo method, object?[] arguments)
    {
        try
        {
            var value = method.Invoke(null, arguments);
            Assert.NotNull(value);
            return value;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static object? ReadPropertyAllowingNull(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return property!.GetValue(instance);
    }

    private static object ReadRequiredProperty(object instance, string propertyName)
    {
        var value = ReadPropertyAllowingNull(instance, propertyName);
        Assert.NotNull(value);
        return value;
    }

    private static IEnumerable<object> ReadEnumerableProperty(object instance, string propertyName) =>
        Assert.IsAssignableFrom<IEnumerable>(ReadRequiredProperty(instance, propertyName))
            .Cast<object>();

    private static CapabilityScenario DescribeScenario(string name, bool publication)
    {
        var (owner, skillArray) = name switch
        {
            "player_active_skill_exports_physical_capability" or
            "untouched_player_skill_reads_current_lease_bound_root" or
            "touched_player_active_skill_reads_exact_final_after_image" => ("player", "activeSkills"),
            "player_passive_skill_exports_physical_capability" or
            "touched_player_passive_skill_reads_exact_final_after_image" => ("player", "passiveSkills"),
            "target_owned_combatant_requires_promotion" => ("player", "activeSkills"),
            "npc_passive_skill_exports_physical_capability" => ("npc", "passiveSkills"),
            _ => ("npc", "activeSkills")
        };
        var actorRole = name.Contains("target_owned", StringComparison.Ordinal)
            ? "target"
            : "provider";
        // The accepted context always starts with the real provider binding.  The
        // wrong-owner row changes only the live canonical NPC root after that state is
        // sealed, so it cannot accidentally test an earlier context rejection.
        var providerKind = owner;
        var providerId = providerKind == "player" ? "player_current" : "npc_field_medic_01";
        var targetKind = name.Contains("combatant_member", StringComparison.Ordinal)
            ? "combatant_member"
            : name.Contains("combatant", StringComparison.Ordinal)
                ? "combatant"
            : "player";
        var targetId = targetKind == "combatant_member"
            ? "combatant_member_wounded_01"
            : targetKind == "combatant" ? "combatant_wounded_01" : "player_current";
        if (name == "wrong_role_rejects")
            actorRole = "target";
        var expectedBoundary = name switch
        {
            "wrong_realm_rejects" => CapabilityFailureBoundary.Context,
            "idless_extension_bearing_skill_rejects" or
            "duplicate_skill_id_across_active_and_passive_rejects" or
            "case_changed_skill_id_rejects" or
            "unicode_confusable_skill_id_rejects" or
            "duplicate_capability_ref_across_skill_kinds_rejects" or
            "unicode_confusable_capability_ref_rejects" or
            "spiritual_domain_rejects" or
            "unknown_domain_rejects" or
            "invalid_severity_envelope_rejects" or
            "open_extension_field_rejects" or
            "open_operation_limits_field_rejects" or
            "aggregate_operation_limit_overflow_rejects" or
            "all_zero_operation_limit_rejects" => CapabilityFailureBoundary.AcceptedState,
            "inactive_skill_rejects" or
            "retired_skill_rejects" or
            "wrong_owner_rejects" or
            "wrong_role_rejects" or
            "target_owned_combatant_requires_promotion" or
            "removed_final_skill_rejects" or
            "retired_final_skill_rejects" or
            "changed_final_skill_id_rejects" or
            "changed_final_capability_ref_rejects" or
            "changed_final_domain_rejects" or
            "changed_final_operation_limits_reject" or
            "duplicate_or_confusable_final_skill_row_rejects" or
            "confusable_final_skill_sibling_rejects" or
            "stale_final_source_rejects" => CapabilityFailureBoundary.Exporter,
            _ => CapabilityFailureBoundary.None
        };
        (string? expectedCode, string? expectedPath) = name switch
        {
            "target_owned_combatant_requires_promotion" =>
                ("mortal_wound_treatment_capability_actor_promotion_required", "treatmentCapability.target"),
            "wrong_role_rejects" =>
                ("mortal_wound_treatment_capability_binding_mismatch", "treatmentCapability.actorRole"),
            "wrong_owner_rejects" =>
                ("mortal_wound_treatment_capability_binding_mismatch", "treatmentCapability.sourceOwner"),
            "removed_final_skill_rejects" =>
                ("mortal_wound_treatment_capability_source_missing", "treatmentCapability.source"),
            "inactive_skill_rejects" or
            "retired_skill_rejects" or
            "retired_final_skill_rejects" or
            "stale_final_source_rejects" =>
                ("mortal_wound_treatment_capability_source_inactive", "treatmentCapability.source.lifecycle"),
            "duplicate_or_confusable_final_skill_row_rejects" or
            "confusable_final_skill_sibling_rejects" =>
                ("mortal_wound_treatment_capability_source_ambiguous", "treatmentCapability.source.skillId"),
            "changed_final_skill_id_rejects" or
            "changed_final_capability_ref_rejects" or
            "changed_final_domain_rejects" or
            "changed_final_operation_limits_reject" =>
                ("mortal_wound_treatment_capability_publication_mismatch", "treatmentCapability.publicationPlan"),
            _ => (null, null)
        };
        return new CapabilityScenario(
            name,
            owner,
            skillArray,
            owner == "player" && skillArray == "activeSkills"
                ? SkillId
                : $"skill_{owner}_{(skillArray == "activeSkills" ? "active" : "passive")}_medicine_01",
            actorRole,
            providerKind,
            providerId,
            targetKind,
            targetId,
            expectedBoundary == CapabilityFailureBoundary.None,
            publication,
            expectedBoundary,
            expectedCode,
            expectedPath);
    }

    private static JsonObject Skill(JsonObject fixture, string owner, string skillKind) =>
        Assert.IsType<JsonObject>(Assert.Single(
            fixture[owner]![skillKind]!.AsArray()));

    private static JsonObject Capability(JsonObject skill) =>
        Assert.IsType<JsonObject>(Assert.Single(
            skill["mortalWoundTreatmentCapabilities"]!.AsArray()));

    private static JsonObject Limits(JsonObject capability) =>
        Assert.IsType<JsonObject>(capability["operationLimits"]);

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(" | ", issues.Select(issue =>
            $"{issue.Code}@{issue.FilePath}: {issue.Message}"));

    private sealed record CapabilityProofView(
        bool IsValid,
        IReadOnlyList<ValidationIssue> Issues,
        object? Proof);

    private sealed record TypedResultView(
        bool IsValid,
        IReadOnlyList<ValidationIssue> Issues,
        object? Value);

    private sealed record AcceptedStateView(
        bool IsValid,
        IReadOnlyList<ValidationIssue> Issues,
        object? Authority);

    public enum CapabilityFailureBoundary
    {
        None,
        Context,
        AcceptedState,
        Exporter
    }

    public sealed record CapabilityScenario(
        string Name,
        string SourceOwner,
        string SourceSkillArray,
        string SkillId,
        string ActorRole,
        string ProviderKind,
        string ProviderId,
        string TargetKind,
        string TargetId,
        bool ExpectedValid,
        bool Publication,
        CapabilityFailureBoundary ExpectedBoundary,
        string? ExpectedCode,
        string? ExpectedPath);

    private sealed class CapabilityAuthorityFixture : IDisposable
    {
        private CapabilityAuthorityFixture(
            string root,
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease,
            WoundAcceptedTurnBinding binding,
            WoundMaterializationEnvelope before,
            JsonObject contextRoot,
            CapabilityScenario scenario,
            JsonObject currentSource,
            JsonObject finalSource)
        {
            Root = root;
            FileSystem = fileSystem;
            Lease = lease;
            Binding = binding;
            Before = before;
            ContextRoot = contextRoot;
            Scenario = scenario;
            CurrentSource = currentSource;
            FinalSource = finalSource;
        }

        internal const string WoundId = "wound_test_torn_side";
        internal const string RouteId = "guaranteed_v1";
        internal const string EventRef = "turn_42:wound_treatment";
        internal const string OperationKey = "capability_authority_operation_001";

        internal string Root { get; }
        internal FileSystemManager FileSystem { get; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; }
        internal WoundAcceptedTurnBinding Binding { get; }
        internal WoundMaterializationEnvelope Before { get; }
        internal JsonObject ContextRoot { get; }
        internal CapabilityScenario Scenario { get; }
        internal JsonObject CurrentSource { get; }
        internal JsonObject FinalSource { get; }

        internal static CapabilityAuthorityFixture Create(CapabilityScenario scenario)
        {
            var root = Path.Combine(Path.GetTempPath(), "boe-capability-authority-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var fileSystem = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath(WoundIdentityState.StatePath))!);
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath(WoundHistoryState.HistoryPath))!);
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath(LiveTurnPreparationService.TurnRequestPath))!);

            var currentSource = CreateCanonicalSkillFixture();
            ApplyScenario(currentSource, scenario with { Name = "baseline_current_source" });
            var finalSource = scenario.Publication
                ? currentSource.DeepClone().AsObject()
                : currentSource;
            if (scenario.Publication)
            {
                ApplyScenario(finalSource, scenario);
                if (scenario.Name.StartsWith("untouched_", StringComparison.Ordinal))
                {
                    // The untouched plan deliberately contains no skill-root after-image.
                    // A distinct still-valid live root proves that ExportForPublication
                    // must read the lease-bound canonical source rather than any final root.
                    Limits(Capability(Skill(
                        currentSource,
                        scenario.SourceOwner,
                        scenario.SourceSkillArray)))["maximumRecoveryPoints"] = 2;
                }
            }
            else if (scenario.ExpectedBoundary != CapabilityFailureBoundary.Exporter)
                ApplyScenario(currentSource, scenario);
            AssertCanonicalFixtureShape(currentSource);
            AssertCanonicalFixtureShape(finalSource);
            WriteCanonicalSkillSources(fileSystem, currentSource);
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath("game_state/world/world_time.json"))!);
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/world/world_time.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["currentTimeInMinutes"] = 1_260
                }.ToJsonString());
            var combatTarget = scenario.TargetKind is "combatant" or "combatant_member";
            var carrierPath = scenario.TargetKind == "combatant_member"
                ? WoundCarrierCatalog.AlliesPath
                : WoundCarrierCatalog.EnemiesPath;
            var woundRoot = combatTarget
                ? WoundContractTestData.CreateActiveWound(
                    woundId: WoundId,
                    ownerKind: scenario.TargetKind,
                    ownerId: scenario.TargetId,
                    carrierPath: carrierPath)
                : WoundContractTestData.CreateActiveWound(woundId: WoundId);
            woundRoot["treatment"]!["routes"] = new JsonArray(
                CreateGuaranteedRoute(scenario));
            woundRoot["treatment"]!["knownRouteIds"] = new JsonArray(RouteId);
            var parsed = WoundMaterializationContract.Parse(woundRoot.ToJsonString(), "wound");
            Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
            if (combatTarget)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(
                    fileSystem.ResolvePath(carrierPath))!);
                var combatant = scenario.TargetKind == "combatant_member"
                    ? new JsonObject
                    {
                        ["combatantId"] = "combatant_group_01",
                        ["isGroup"] = true,
                        ["members"] = new JsonArray(new JsonObject
                        {
                            ["memberId"] = scenario.TargetId,
                            ["activeWounds"] = new JsonArray(woundRoot.DeepClone())
                        })
                    }
                    : new JsonObject
                    {
                        ["combatantId"] = scenario.TargetId,
                        ["activeWounds"] = new JsonArray(woundRoot.DeepClone())
                    };
                File.WriteAllText(
                    fileSystem.ResolvePath(carrierPath),
                    new JsonObject
                    {
                        [scenario.TargetKind == "combatant_member" ? "alliesData" : "enemiesData"] =
                            new JsonArray(combatant)
                    }.ToJsonString());
            }
            else
            {
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/wounds.json"),
                    WoundContractTestData.CreatePlayerCarrier(woundRoot).ToJsonString());
            }
            File.WriteAllText(
                fileSystem.ResolvePath(WoundIdentityState.StatePath),
                WoundContractTestData.CreateIdentityIndex(
                    WoundContractTestData.CreateIdentityEntry(
                        woundId: WoundId,
                        ownerKind: combatTarget ? scenario.TargetKind : "player",
                        ownerId: combatTarget ? scenario.TargetId : "player_current",
                        carrierPath: combatTarget ? carrierPath : "game_state/player/wounds.json")).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath(WoundHistoryState.HistoryPath),
                WoundContractTestData.CreateHistory(WoundContractTestData.CreateTransition()).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath(LiveTurnPreparationService.TurnRequestPath),
                new JsonObject
                {
                    ["sessionId"] = "session_capability_authority",
                    ["requestId"] = "request_capability_authority",
                    ["turnNumber"] = 42,
                    ["gameMode"] = "normal",
                    ["preGeneratedDices1d20"] = new JsonArray(17)
                }.ToJsonString());

            var acceptedEvents = new[]
            {
                new WoundAcceptedEventAuthority(
                    EventRef,
                    "mortal_wound_treatment",
                    "mortal_wound_treatment_authority_001",
                    "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
            };
            var binding = new WoundAcceptedTurnBinding(
                "session_capability_authority",
                "request_capability_authority",
                "snapshot_capability_authority",
                "mortal_world",
                42,
                acceptedEvents,
                WoundAcceptedEventSetFingerprint.Compute(acceptedEvents));
            var lease = fileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            return new CapabilityAuthorityFixture(
                root,
                fileSystem,
                lease,
                binding,
                parsed.Wound!,
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["realm"] = currentSource["realm"]!.GetValue<string>(),
                    ["targetKind"] = scenario.TargetKind,
                    ["targetId"] = scenario.TargetId,
                    ["providerKind"] = scenario.ProviderKind,
                    ["providerId"] = scenario.ProviderId,
                    ["currentLocationId"] = "loc_field_clinic_001"
                },
                scenario,
                currentSource,
                finalSource);
        }

        public void Dispose()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Directory.Delete(Root, recursive: true);
        }

        internal void MutatePersistedSelectedSourceAfterExport()
        {
            var skill = Skill(CurrentSource, Scenario.SourceOwner, Scenario.SourceSkillArray);
            skill["displayName"] = "Mutated only after proof export";
            WriteCanonicalSkillSources(FileSystem, CurrentSource);
        }

        internal void ApplyLiveExporterMutation()
        {
            if (Scenario.ExpectedBoundary != CapabilityFailureBoundary.Exporter || Scenario.Publication)
                return;

            var source = Skill(CurrentSource, Scenario.SourceOwner, Scenario.SourceSkillArray);
            switch (Scenario.Name)
            {
                case "inactive_skill_rejects":
                    source["active"] = false;
                    break;
                case "retired_skill_rejects":
                    source["lifecycle"] = "retired";
                    break;
                case "wrong_owner_rejects":
                    Assert.Equal("npc", Scenario.SourceOwner);
                    Assert.IsType<JsonObject>(CurrentSource["npc"])["ownerId"] = "npc_wrong_owner_01";
                    break;
                // Wrong role and an unpromoted combat target retain the sealed valid
                // context/root and are rejected solely by the capability exporter.
                case "wrong_role_rejects":
                case "target_owned_combatant_requires_promotion":
                    break;
                default:
                    throw new InvalidOperationException(
                        $"No live exporter mutation is defined for {Scenario.Name}.");
            }

            WriteCanonicalSkillSources(FileSystem, CurrentSource);
        }

        internal void AssertAcceptedCombatWoundCarrier()
        {
            Assert.True(Scenario.TargetKind is "combatant" or "combatant_member");
            var enemy = ReadRoot(WoundCarrierCatalog.EnemiesPath);
            var ally = ReadRoot(WoundCarrierCatalog.AlliesPath);
            var catalog = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
                PlayerWounds: null,
                NpcWounds: null,
                EnemyCombatants: enemy,
                AllyCombatants: ally,
                AfterlifeProfiles: null));
            Assert.Empty(catalog.Issues);
            Assert.True(catalog.TryResolveOne(WoundId, out var occurrence));
            Assert.Equal(Scenario.TargetKind, occurrence.Coordinate.OwnerKind);
            Assert.Equal(Scenario.TargetId, occurrence.Coordinate.OwnerId);
            Assert.Equal(
                Scenario.TargetKind == "combatant_member"
                    ? WoundCarrierCatalog.AlliesPath
                    : WoundCarrierCatalog.EnemiesPath,
                occurrence.Coordinate.CarrierPath);
        }

        internal void AssertPristineEffectMechanicsSnapshot()
        {
            var snapshot = EffectMechanicsSnapshot.LoadAsync(FileSystem, Lease)
                .GetAwaiter().GetResult();
            Assert.True(snapshot.IsAccepted, DescribeIssues(snapshot.Issues));
            Assert.Empty(snapshot.Issues);
            Assert.Empty(snapshot.Components);
            Assert.Empty(snapshot.Effects);
        }

        internal void AssertProductionValidSkillShapes()
        {
            var player = Assert.IsType<JsonObject>(CurrentSource["player"]);
            var npc = Assert.IsType<JsonObject>(CurrentSource["npc"]);
            Assert.True(
                ValidationService.IsProductionValidMortalActiveSkill(ParseFirstSkill(player, "activeSkills")));
            Assert.True(
                ValidationService.IsProductionValidMortalPassiveSkill(ParseFirstSkill(player, "passiveSkills")));
            Assert.True(
                ValidationService.IsProductionValidMortalActiveSkill(ParseFirstSkill(npc, "activeSkills")));
            Assert.True(
                ValidationService.IsProductionValidMortalPassiveSkill(ParseFirstSkill(npc, "passiveSkills")));

            var npcRoot = ReadRoot("game_state/npcs/npc_core.json");
            var persistedNpc = Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(npcRoot!["NPCs"])));
            Assert.Equal("npc_field_medic_01", persistedNpc["npcId"]!.GetValue<string>());
            Assert.True(ValidationService.IsProductionValidMortalActiveSkill(
                ParseFirstSkill(persistedNpc, "activeSkills")));
            Assert.True(ValidationService.IsProductionValidMortalPassiveSkill(
                ParseFirstSkill(persistedNpc, "passiveSkills")));
        }

        internal void AssertLiveTurnRequestMatchesBinding()
        {
            var turnRequest = ReadRoot(LiveTurnPreparationService.TurnRequestPath);
            Assert.NotNull(turnRequest);
            Assert.Equal("session_capability_authority", turnRequest!["sessionId"]!.GetValue<string>());
            Assert.Equal("request_capability_authority", turnRequest["requestId"]!.GetValue<string>());
            Assert.Equal(42, turnRequest["turnNumber"]!.GetValue<int>());
            Assert.Equal("normal", turnRequest["gameMode"]!.GetValue<string>());
            Assert.Equal(new[] { 17 }, turnRequest["preGeneratedDices1d20"]!.AsArray()
                .Select(static die => die!.GetValue<int>()));
        }

        private static JsonElement ParseFirstSkill(JsonObject owner, string skillArray)
        {
            using var document = JsonDocument.Parse(owner[skillArray]!.AsArray()[0]!.ToJsonString());
            return document.RootElement.Clone();
        }

        internal void AssertPublicationPlanBindsOnlyTheSelectedRoot(
            object plan)
        {
            var afterImages = Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonObject>>(
                ReadRequiredProperty(plan, "OwnerCompanionAfterImages"));
            if (!UsesFinalAfterImage)
            {
                Assert.DoesNotContain(
                    SelectedSkillRootPath,
                    afterImages.Keys);
                return;
            }

            Assert.True(afterImages.TryGetValue(SelectedSkillRootPath, out var actual));
            Assert.Equal(
                SelectedSkillRoot(FinalSource).ToJsonString(),
                actual!.ToJsonString());
        }

        internal void AssertCachedPublicationPlanBinding(AcceptedMechanicsPlanBinding binding)
        {
            Assert.Equal(Binding.SessionId, binding.SessionId);
            Assert.Equal(Binding.RequestId, binding.RequestId);
            Assert.Equal(Binding.SnapshotToken, binding.SnapshotToken);
            if (UsesFinalAfterImage)
                Assert.True(binding.BeforeImages.ContainsKey(SelectedSkillRootPath));
            else
                Assert.DoesNotContain(SelectedSkillRootPath, binding.BeforeImages.Keys);
        }

        internal void AssertPublicationExportReadsSelectedRoot(CapabilityProofView result)
        {
            if (!Scenario.ExpectedValid)
                return;

            Assert.NotNull(result.Proof);
            var proof = result.Proof!;
            Assert.Equal(
                SelectedSkillRootPath,
                Assert.IsType<string>(ReadRequiredProperty(proof, "SourcePath")));
            var limits = ReadRequiredProperty(proof, "OperationLimits");
            Assert.Equal(
                (long)ExpectedPublicationRecoveryPoints,
                Convert.ToInt64(ReadRequiredProperty(limits, "MaximumRecoveryPoints")));
        }

        private bool UsesFinalAfterImage =>
            Scenario.Publication &&
            !Scenario.Name.StartsWith("untouched_", StringComparison.Ordinal);

        private int ExpectedPublicationRecoveryPoints =>
            Limits(Capability(Skill(
                UsesFinalAfterImage ? FinalSource : CurrentSource,
                Scenario.SourceOwner,
                Scenario.SourceSkillArray)))["maximumRecoveryPoints"]!.GetValue<int>();

        private string SelectedSkillRootPath => Scenario.SourceOwner switch
        {
            "player" when Scenario.SourceSkillArray == "activeSkills" =>
                "game_state/player/skills_active.json",
            "player" => "game_state/player/skills_passive.json",
            _ => "game_state/npcs/npc_core.json"
        };

        private JsonObject SelectedSkillRoot(JsonObject source)
        {
            var player = Assert.IsType<JsonObject>(source["player"]);
            var npc = Assert.IsType<JsonObject>(source["npc"]);
            return Scenario.SourceOwner switch
            {
                "player" when Scenario.SourceSkillArray == "activeSkills" => new JsonObject
                {
                    ["activeSkillChanges"] = player["activeSkills"]!.DeepClone()
                },
                "player" => new JsonObject
                {
                    ["passiveSkillChanges"] = player["passiveSkills"]!.DeepClone()
                },
                _ => new JsonObject
                {
                    ["NPCs"] = new JsonArray(new JsonObject
                    {
                        ["npcId"] = npc["ownerId"]!.DeepClone(),
                        ["activeSkills"] = npc["activeSkills"]!.DeepClone(),
                        ["passiveSkills"] = npc["passiveSkills"]!.DeepClone()
                    })
                }
            };
        }

        private JsonObject? ReadRoot(string path)
        {
            var fullPath = FileSystem.ResolvePath(path);
            return File.Exists(fullPath)
                ? JsonNode.Parse(File.ReadAllText(fullPath))!.AsObject()
                : null;
        }

        private static JsonObject CreateGuaranteedRoute(CapabilityScenario scenario) => new()
        {
            ["routeId"] = RouteId,
            ["displayName"] = "Guaranteed canonical care",
            ["visibility"] = "known_to_player",
            ["mode"] = "guaranteed",
            ["requirements"] = new JsonArray(
                new JsonObject
                {
                    ["kind"] = "source_capability",
                    ["capabilityRef"] = CapabilityRef,
                    ["actorRole"] = scenario.ActorRole
                },
                new JsonObject
                {
                    ["kind"] = "skill_tier",
                    ["capabilityRef"] = scenario.SkillId,
                    ["minimumTier"] = 2,
                    ["actorRole"] = scenario.ActorRole
                }),
            ["resourcePolicy"] = new JsonObject
            {
                ["reserveBeforeResolution"] = true,
                ["consumeOn"] = new JsonArray("success"),
                ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"),
                ["mutations"] = new JsonArray()
            },
            ["resolution"] = new JsonObject
            {
                ["capabilityRef"] = CapabilityRef,
                ["actorRole"] = scenario.ActorRole
            },
            ["outcomes"] = new JsonArray(new JsonObject
            {
                ["category"] = "success",
                ["result"] = new JsonArray(new JsonObject { ["kind"] = "stabilize" })
            }),
            ["interruption"] = null
        };

        private static void WriteCanonicalSkillSources(FileSystemManager fileSystem, JsonObject source)
        {
            var player = Assert.IsType<JsonObject>(source["player"]);
            var npc = Assert.IsType<JsonObject>(source["npc"]);
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/player/skills_active.json"),
                new JsonObject { ["activeSkillChanges"] = player["activeSkills"]!.DeepClone() }.ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/player/skills_passive.json"),
                new JsonObject { ["passiveSkillChanges"] = player["passiveSkills"]!.DeepClone() }.ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/npcs/npc_core.json"),
                new JsonObject
                {
                    ["NPCs"] = new JsonArray(new JsonObject
                    {
                        ["npcId"] = npc["ownerId"]!.DeepClone(),
                        ["activeSkills"] = npc["activeSkills"]!.DeepClone(),
                        ["passiveSkills"] = npc["passiveSkills"]!.DeepClone()
                    })
                }.ToJsonString());
        }
    }
}
