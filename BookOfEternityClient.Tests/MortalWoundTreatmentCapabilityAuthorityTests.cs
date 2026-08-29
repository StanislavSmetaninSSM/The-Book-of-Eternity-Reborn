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
        "wrong_owner_role_or_realm_rejects",
        "spiritual_or_unknown_domain_rejects",
        "invalid_severity_envelope_rejects",
        "open_extension_or_limit_fields_reject",
        "aggregate_operation_limit_overflow_rejects",
        "all_zero_operation_limit_rejects",
        "source_semantic_change_changes_proof",
        "display_name_change_never_grants_authority",
        "proof_is_detached_and_immutable",
        "proof_contains_only_context_coordinates_owner_skill_capability_and_fingerprints"
    }.Select(static row => new object[] { row });

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
    }.Select(static row => new object[] { row });

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
    [MemberData(nameof(CurrentExportRows))]
    public void ExportCurrent_UsesOnlyCanonicalPlayerOrNpcSkillCapabilitySource(string scenario)
    {
        using var fixture = CapabilityAuthorityFixture.Create(scenario);

        var result = InvokeExportCurrent(fixture, scenario);
        AssertCapabilityResult(result, ExpectedValidity(scenario), scenario);
    }

    [Theory]
    [MemberData(nameof(PublicationExportRows))]
    public void ExportForPublication_RevalidatesTheExactCurrentOrFinalSkillSource(string scenario)
    {
        using var fixture = CapabilityAuthorityFixture.Create(scenario);

        var result = InvokeExportForPublication(fixture, scenario);
        AssertCapabilityResult(result, ExpectedValidity(scenario), scenario);
    }

    private static CapabilityProofView InvokeExportCurrent(
        CapabilityAuthorityFixture fixture,
        string scenario)
    {
        var acceptedState = BuildAcceptedState(fixture);
        var coordinates = CreateCoordinates(fixture, acceptedState);
        return InvokeCapabilityExporter(
            "ExportCurrent",
            acceptedState,
            coordinates,
            CapabilityRef,
            ActorRole(scenario));
    }

    private static CapabilityProofView InvokeExportForPublication(
        CapabilityAuthorityFixture fixture,
        string scenario)
    {
        var acceptedState = BuildAcceptedState(fixture);
        var coordinates = CreateCoordinates(fixture, acceptedState);
        var publicationPlan = BuildPublicationPlan(fixture);
        return InvokeCapabilityExporter(
            "ExportForPublication",
            acceptedState,
            coordinates,
            CapabilityRef,
            ActorRole(scenario),
            publicationPlan);
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
        var method = Assert.Single(
            typeof(AcceptedMechanicsPlanAuthority).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate =>
                candidate.Name == "TryPeekValidated" &&
                candidate.GetParameters().Length == 4);
        var arguments = new object?[] { fixture.FileSystem, fixture.Lease, null, null };
        var found = Assert.IsType<bool>(Invoke(method, arguments));
        Assert.True(found, "The fixture must obtain the publication plan through the production accepted-plan authority.");
        Assert.NotNull(arguments[3]);
        var planningResult = arguments[3]!;
        return ReadValidTypedResult(planningResult, "Plan", "publication plan");
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
        ["mortalWoundTreatmentCapabilities"] = new JsonArray(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["capabilityRef"] = kind == "active"
                ? CapabilityRef
                : "triage_support_capability",
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
        })
    };

    private static void ApplyScenario(JsonObject fixture, string scenario)
    {
        var playerActive = Skill(fixture, "player", "activeSkills");
        var playerPassive = Skill(fixture, "player", "passiveSkills");
        var npcActive = Skill(fixture, "npc", "activeSkills");
        var capability = Capability(playerActive);

        switch (scenario)
        {
            case "idless_extension_bearing_skill_rejects":
                playerActive.Remove("skillId");
                break;
            case "duplicate_skill_id_across_active_and_passive_rejects":
                playerPassive["skillId"] = SkillId;
                break;
            case "case_changed_skill_id_rejects":
                playerPassive["skillId"] = "SKILL_FIELD_MEDICINE_01";
                break;
            case "unicode_confusable_skill_id_rejects":
                playerPassive["skillId"] = "skill_fieлd_medicine_01";
                break;
            case "duplicate_capability_ref_across_skill_kinds_rejects":
                Capability(playerPassive)["capabilityRef"] = CapabilityRef;
                break;
            case "unicode_confusable_capability_ref_rejects":
                Capability(playerPassive)["capabilityRef"] = "fieӁd_medicine_guaranteed_care";
                break;
            case "inactive_skill_rejects":
                playerActive["active"] = false;
                break;
            case "retired_skill_rejects":
                playerActive["lifecycle"] = "retired";
                break;
            case "wrong_owner_role_or_realm_rejects":
                fixture["realm"] = "chaos_sea";
                break;
            case "spiritual_or_unknown_domain_rejects":
                capability["woundDomain"] = "spiritual";
                break;
            case "invalid_severity_envelope_rejects":
                capability["minimumSeverityRank"] = 4;
                capability["maximumSeverityRank"] = 1;
                break;
            case "open_extension_or_limit_fields_reject":
                capability["callerMayOverride"] = true;
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
                playerActive["displayName"] = "Unrelated wording only";
                break;
            case "removed_final_skill_rejects":
                fixture["player"]!["activeSkills"] = new JsonArray();
                break;
            case "retired_final_skill_rejects":
                playerActive["lifecycle"] = "retired";
                break;
            case "changed_final_skill_id_rejects":
                playerActive["skillId"] = "skill_after_image_changed_01";
                break;
            case "changed_final_capability_ref_rejects":
                capability["capabilityRef"] = "after_image_changed_capability";
                break;
            case "changed_final_domain_rejects":
                capability["woundDomain"] = "spiritual";
                break;
            case "duplicate_or_confusable_final_skill_row_rejects":
                npcActive["skillId"] = SkillId;
                break;
            case "stale_final_source_rejects":
                playerActive["active"] = false;
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
        bool expectedValid,
        string scenario)
    {
        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Null(result.Proof);
            Assert.NotEmpty(result.Issues);
            return;
        }

        Assert.Empty(result.Issues);
        Assert.NotNull(result.Proof);
        var proof = result.Proof!;
        AssertProofShape(proof, scenario);
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

    private static bool ExpectedValidity(string scenario) => scenario switch
    {
        "target_owned_combatant_requires_promotion" => false,
        _ when scenario.Contains("reject", StringComparison.Ordinal) => false,
        _ => true
    };

    private static string ActorRole(string scenario) =>
        scenario.Contains("target_owned", StringComparison.Ordinal) ? "target" : "provider";

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

    private sealed class CapabilityAuthorityFixture : IDisposable
    {
        private CapabilityAuthorityFixture(
            string root,
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease,
            WoundAcceptedTurnBinding binding,
            WoundMaterializationEnvelope before,
            JsonObject contextRoot)
        {
            Root = root;
            FileSystem = fileSystem;
            Lease = lease;
            Binding = binding;
            Before = before;
            ContextRoot = contextRoot;
        }

        internal const string WoundId = "wound_test_torn_side";
        internal const string RouteId = "clean_and_suture";
        internal const string EventRef = "turn_42:wound_treatment";
        internal const string OperationKey = "capability_authority_operation_001";

        internal string Root { get; }
        internal FileSystemManager FileSystem { get; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; }
        internal WoundAcceptedTurnBinding Binding { get; }
        internal WoundMaterializationEnvelope Before { get; }
        internal JsonObject ContextRoot { get; }

        internal static CapabilityAuthorityFixture Create(string scenario)
        {
            var root = Path.Combine(Path.GetTempPath(), "boe-capability-authority-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var fileSystem = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath("game_state/wounds/identity_index.json"))!);

            var source = CreateCanonicalSkillFixture();
            ApplyScenario(source, scenario);
            AssertCanonicalFixtureShape(source);
            WriteCanonicalSkillSources(fileSystem, source);
            var woundRoot = WoundContractTestData.CreateActiveWound(woundId: WoundId);
            var parsed = WoundMaterializationContract.Parse(woundRoot.ToJsonString(), "wound");
            Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/player/wounds.json"),
                WoundContractTestData.CreatePlayerCarrier(woundRoot).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/wounds/identity_index.json"),
                WoundContractTestData.CreateIdentityIndex(
                    WoundContractTestData.CreateIdentityEntry(woundId: WoundId)).ToJsonString());
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
                    ["realm"] = "mortal_world",
                    ["targetKind"] = "player",
                    ["targetId"] = "player_current",
                    ["providerKind"] = "npc",
                    ["providerId"] = "npc_field_medic_01",
                    ["currentLocationId"] = "loc_field_clinic_001"
                });
        }

        public void Dispose()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Directory.Delete(Root, recursive: true);
        }

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
