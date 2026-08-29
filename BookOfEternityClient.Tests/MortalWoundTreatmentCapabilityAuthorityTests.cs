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

    [Theory]
    [MemberData(nameof(CurrentExportRows))]
    public void ExportCurrent_UsesOnlyCanonicalPlayerOrNpcSkillCapabilitySource(
        CapabilityScenario scenario)
    {
        using var fixture = CapabilityAuthorityFixture.Create(scenario);

        var result = InvokeExportCurrent(fixture, scenario);
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
        var publicationPlan = BuildPublicationPlan(fixture);
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
        return result;
    }

    private static object BuildAcceptedState(CapabilityAuthorityFixture fixture)
    {
        var treatmentAuthorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(treatmentAuthorityType);

        var context = ParseAuthorityInput(
            treatmentAuthorityType,
            "ParseContext",
            "Context",
            fixture.ContextRoot,
            "treatmentContext");
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
        return ReadValidTypedResult(result, "Authority", "accepted state");
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

    private static object BuildPublicationPlan(CapabilityAuthorityFixture fixture)
    {
        var result = AcceptedMechanicsPlanAuthority.GetOrBuildValidated(
            fixture.FileSystem,
            fixture.Lease,
            fixture.CreatePublicationPlanningInput());
        Assert.True(result.Success, DescribeIssues(result.Issues));
        Assert.NotNull(result.Plan);
        fixture.AssertPublicationPlanBindsOnlyTheSelectedRoot(result.Plan!);
        return result.Plan!;
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

    private static JsonObject CreateSkill(string kind) => new()
    {
        ["skillId"] = kind == "active" ? SkillId : "skill_triage_passive_01",
        ["displayName"] = kind == "active" ? "Field Medicine" : "Triage Discipline",
        ["lifecycle"] = "active",
        ["active"] = true,
        ["tier"] = 3
    };

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
            Assert.Null(result.Proof);
            Assert.NotEmpty(result.Issues);
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
                AssertGuaranteedRequirementBundleUsesUnchangedT060(fixture);
                break;
        }
    }

    private static void AssertGuaranteedRequirementBundleUsesUnchangedT060(
        CapabilityAuthorityFixture fixture)
    {
        var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(authorityType);
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
        var serialized = JsonSerializer.Serialize(authority, authority.GetType());
        Assert.Contains(CapabilityRef, serialized, StringComparison.Ordinal);
        Assert.Contains(fixture.Scenario.SkillId, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("mortalWoundTreatmentCapabilities", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("ProofFingerprint", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("maximumRecoveryPoints", serialized, StringComparison.Ordinal);
    }

    private static object ParseAuthorityInput(
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
        var value = ReadValidTypedResult(parsed, valueProperty, methodName);
        Assert.False(value is JsonNode or JsonElement or JsonDocument or string);
        return value;
    }

    private static object ReadValidTypedResult(object result, string valueProperty, string boundary)
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
        Assert.True(isValid, $"{boundary} failed: {DescribeIssues(issues)}");
        Assert.Empty(issues);
        var value = ReadPropertyAllowingNull(result, valueProperty);
        Assert.NotNull(value);
        return value;
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
        var providerKind = name == "wrong_owner_rejects" ? "player" : owner;
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
        var expectedValid = !name.Contains("reject", StringComparison.Ordinal) &&
                            name != "target_owned_combatant_requires_promotion";
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
            expectedValid,
            publication);
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
        bool Publication);

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
                fileSystem.ResolvePath("game_state/wounds/identity_index.json"))!);

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
            else
                ApplyScenario(currentSource, scenario);
            AssertCanonicalFixtureShape(currentSource);
            AssertCanonicalFixtureShape(finalSource);
            WriteCanonicalSkillSources(fileSystem, currentSource);
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
                fileSystem.ResolvePath("game_state/wounds/identity_index.json"),
                WoundContractTestData.CreateIdentityIndex(
                    WoundContractTestData.CreateIdentityEntry(
                        woundId: WoundId,
                        ownerKind: combatTarget ? scenario.TargetKind : "player",
                        ownerId: combatTarget ? scenario.TargetId : "player_current",
                        carrierPath: combatTarget ? carrierPath : "game_state/player/wounds.json")).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/wounds/history.json"),
                WoundContractTestData.CreateHistory(WoundContractTestData.CreateTransition()).ToJsonString());

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

        internal AcceptedMechanicsInput CreatePublicationPlanningInput()
        {
            var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
            var history = ResourceHistoryState.CreateValidated(
                Array.Empty<ResourceTransition>(),
                definitions).History!;
            var owners = ResourceOwnerAuthority.Build(
                new ResourceOwnerAuthorityInput(
                    Array.Empty<ResourceOwnerExport>(),
                    Array.Empty<ResourceOwnerExport>(),
                    Array.Empty<ResourceOwnerKey>()));
            var sources = ResourceMutationSourceCatalog.Create(
                Array.Empty<ResourceMutationSourceExport>()).Catalog!;
            var commands = ResourceAcceptedTurnInputComposer.Parse(null);
            var afterImages = UsesFinalAfterImage
                ? new Dictionary<string, JsonObject>(StringComparer.Ordinal)
                {
                    [SelectedSkillRootPath] = SelectedSkillRoot(FinalSource)
                }
                : new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            var planningContext = new AcceptedMechanicsPlanningContext(
                definitions.ToCanonicalRoot(),
                definitions,
                new ResourceStateLedger(Array.Empty<ResourceStateEntry>()),
                history,
                owners,
                sources,
                commands,
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["entries"] = new JsonArray()
                },
                effectPlan: null,
                ownerCompanionAfterImages: afterImages);
            var beforeImages = PublicationBeforeImages(afterImages.Keys);
            var fingerprint = WoundAcceptedEventSetFingerprint.Compute(new[]
            {
                new WoundAcceptedEventAuthority(
                    EventRef,
                    "mortal_wound_treatment",
                    "mortal_wound_treatment_publication_plan",
                    "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")
            });
            var fingerprints = new AcceptedMechanicsAuthorityFingerprints(
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint,
                fingerprint);
            return new AcceptedMechanicsInput(
                "session_capability_publication",
                "request_capability_publication",
                "snapshot_capability_publication",
                "mortal_world",
                42,
                new JsonObject { ["acceptedEvents"] = new JsonArray() },
                commands.Root,
                new JsonObject(),
                new JsonObject(),
                new JsonObject { ["authorityKind"] = "capability_publication" },
                fingerprints,
                beforeImages,
                Array.Empty<ValidationIssue>(),
                planningContext);
        }

        internal void AssertPublicationPlanBindsOnlyTheSelectedRoot(
            AcceptedMechanicsPlan plan)
        {
            var afterImages = plan.OwnerCompanionAfterImages;
            if (!UsesFinalAfterImage)
            {
                Assert.False(
                    afterImages.ContainsKey(SelectedSkillRootPath),
                    "An untouched publication must leave its selected skill root lease-bound.");
                return;
            }

            Assert.True(afterImages.TryGetValue(SelectedSkillRootPath, out var actual));
            Assert.Equal(
                SelectedSkillRoot(FinalSource).ToJsonString(),
                actual!.ToJsonString());
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

        private Dictionary<string, CanonicalBeforeImage> PublicationBeforeImages(
            IEnumerable<string> touchedSkillPaths)
        {
            var paths = new[]
            {
                ResourceMaterializationContract.DefinitionsPath,
                ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                EffectAcceptedTurnPlan.IdentityIndexPath
            }.Concat(touchedSkillPaths);
            return paths.Distinct(StringComparer.Ordinal).ToDictionary(
                static path => path,
                path => ReadCanonicalBeforeImage(path),
                StringComparer.Ordinal);
        }

        private CanonicalBeforeImage ReadCanonicalBeforeImage(string path)
        {
            var fullPath = FileSystem.ResolvePath(path);
            return File.Exists(fullPath)
                ? new CanonicalBeforeImage(true, File.ReadAllBytes(fullPath))
                : new CanonicalBeforeImage(false, null);
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
