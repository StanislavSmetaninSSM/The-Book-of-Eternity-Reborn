using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundRequirementAuthorityTests
{
    private const string RequirementPath =
        "wound.treatment.routes[0].requirements";
    private const string ItemRef = "itm_sterile_thread_001";
    private const string ResourceRef = "res_medical_supply_001";
    private const string SkillRef = "skill_field_medicine_001";
    private const string CapabilityRef = "cap_stitch_tissue_001";
    private const string ProviderRef = "npc_healer_001";
    private const string TargetRef = "player_current";
    private const string ConsentRef = "consent_healer_player_001";
    private const string FacilityRef = "fac_clean_table_001";
    private const string LocationRef = "loc_field_clinic_001";
    private const string QuestRef = "quest_clinic_open_001";
    private const string EffectRef = "effect_infection_controlled_001";
    private const string EnvironmentRef = "env_clean_air_001";

    private static readonly string[] Kinds =
    {
        "item_quantity",
        "resource_quantity",
        "skill_tier",
        "source_capability",
        "provider",
        "consent",
        "facility",
        "location",
        "quest_state",
        "effect_state",
        "environment"
    };

    private static readonly JsonSerializerOptions RuntimeJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEnumerable<object[]> RequirementKindCases =>
        Kinds.Select(static kind => new object[] { kind });

    public static IEnumerable<object[]> RoleBoundRequirementCases =>
        new[]
        {
            "item_quantity",
            "resource_quantity",
            "skill_tier",
            "source_capability"
        }.Select(static kind => new object[] { kind });

    public static IEnumerable<object[]> SnapshotClosedObjectCases =>
        new[]
        {
            ("item", "items[0]"),
            ("resource", "resources[0]"),
            ("actor", "actors[0]"),
            ("skill", "actors[0].skills[0]"),
            ("capability", "actors[0].capabilities[0]"),
            ("consent", "actors[0].consents[0]"),
            ("facility", "facilities[0]"),
            ("location", "locations[0]"),
            ("present_actor", "locations[0].presentActors[0]"),
            ("quest", "quests[0]"),
            ("effect", "effects[0]"),
            ("environment", "environments[0]")
        }.Select(static row => new object[] { row.Item1, row.Item2 });

    public static IEnumerable<object[]> SnapshotActorKindCases =>
        new[]
        {
            ("item", "ownerKind", "items[0].ownerKind"),
            ("resource", "ownerKind", "resources[0].ownerKind"),
            ("actor", "actorKind", "actors[0].actorKind"),
            ("consent", "providerKind", "actors[0].consents[0].providerKind"),
            ("consent", "targetKind", "actors[0].consents[0].targetKind"),
            ("present_actor", "actorKind", "locations[0].presentActors[0].actorKind"),
            ("effect", "targetKind", "effects[0].targetKind")
        }.Select(static row => new object[] { row.Item1, row.Item2, row.Item3 });

    public static IEnumerable<object[]> ContextBindingCases =>
        new[]
        {
            ("duplicate_target", "targetId", "mortal_wound_treatment_context_target_mismatch"),
            ("target_foreign_realm", "targetId", "mortal_wound_treatment_context_target_mismatch"),
            ("target_retired", "targetId", "mortal_wound_treatment_context_target_mismatch"),
            ("target_inactive", "targetId", "mortal_wound_treatment_context_target_mismatch"),
            ("duplicate_provider", "providerId", "mortal_wound_treatment_context_provider_mismatch"),
            ("provider_foreign_realm", "providerId", "mortal_wound_treatment_context_provider_mismatch"),
            ("provider_retired", "providerId", "mortal_wound_treatment_context_provider_mismatch"),
            ("provider_inactive", "providerId", "mortal_wound_treatment_context_provider_mismatch"),
            ("duplicate_location", "currentLocationId", "mortal_wound_treatment_context_location_mismatch"),
            ("location_foreign_realm", "currentLocationId", "mortal_wound_treatment_context_location_mismatch"),
            ("location_retired", "currentLocationId", "mortal_wound_treatment_context_location_mismatch"),
            ("location_inactive", "currentLocationId", "mortal_wound_treatment_context_location_mismatch")
        }.Select(static row => new object[] { row.Item1, row.Item2, row.Item3 });

    public static IEnumerable<object[]> ConfusableReferenceCases
    {
        get
        {
            foreach (var kind in Kinds)
            {
                yield return new object[] { kind, "case_changed" };
                yield return new object[] { kind, "ascii_confusable" };
                yield return new object[] { kind, "cyrillic_confusable" };
                yield return new object[] { kind, "display_name" };
            }
        }
    }

    [Fact]
    public void Fixture_AllRequirementKindsRoundTripInAuthoredOrder()
    {
        var wound = CreateWoundWithRequirements(CreateCompleteRequirements());

        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            "wound");

        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var route = Assert.Single(parsed.Wound!.Treatment.Routes);
        Assert.Equal(
            Kinds,
            route.Requirements.Select(static requirement =>
                requirement.GetProperty("kind").GetString()));

        var canonical = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(parsed.Wound))!.AsObject();
        Assert.Equal(
            Kinds,
            canonical["treatment"]!["routes"]![0]!["requirements"]!.AsArray()
                .Select(static requirement =>
                    requirement!["kind"]!.GetValue<string>()));
    }

    [Fact]
    public void ParseAuthorityProjection_ValidRootsReturnTypedDetachedValues()
    {
        var contextRoot = CreateContext();
        var snapshotRoot = CreateSnapshot();
        var context = InvokeAuthorityParser(
            "ParseContext",
            "Context",
            contextRoot,
            "treatmentContext");
        var snapshot = InvokeAuthorityParser(
            "ParseSnapshot",
            "Snapshot",
            snapshotRoot,
            "treatmentSnapshot");
        Assert.True(context.IsValid, DescribeIssues(context.Issues));
        Assert.True(snapshot.IsValid, DescribeIssues(snapshot.Issues));
        Assert.Empty(context.Issues);
        Assert.Empty(snapshot.Issues);
        Assert.NotNull(context.Value);
        Assert.NotNull(snapshot.Value);
        Assert.False(context.Value is JsonNode or JsonElement or string);
        Assert.False(snapshot.Value is JsonNode or JsonElement or string);
        AssertExternallyImmutableGraph(context.Value, "$context");
        AssertExternallyImmutableGraph(snapshot.Value, "$snapshot");
        var contextBefore = SerializeRuntime(context.Value);
        var snapshotBefore = SerializeRuntime(snapshot.Value);

        contextRoot["providerId"] = "npc_source_mutated_after_parse";
        First(snapshotRoot, "items")["count"] = 999;

        Assert.Equal(contextBefore, SerializeRuntime(context.Value));
        Assert.Equal(snapshotBefore, SerializeRuntime(snapshot.Value));
    }

    [Theory]
    [InlineData("unknown_field", "mortal_wound_treatment_context_unknown_field", "unexpected")]
    [InlineData("missing_field", "mortal_wound_treatment_context_missing_field", "targetId")]
    [InlineData("wrong_version", "mortal_wound_treatment_context_schema_version_invalid", "schemaVersion")]
    [InlineData("wrong_type", "mortal_wound_treatment_context_invalid_field", "providerId")]
    [InlineData("wrong_realm", "mortal_wound_treatment_context_invalid_field", "realm")]
    public void ParseContext_RejectsNonClosedOrMalformedProjection(
        string mutation,
        string expectedCode,
        string expectedField)
    {
        var root = CreateContext();
        switch (mutation)
        {
            case "unknown_field":
                root["unexpected"] = true;
                break;
            case "missing_field":
                root.Remove("targetId");
                break;
            case "wrong_version":
                root["schemaVersion"] = 2;
                break;
            case "wrong_type":
                root["providerId"] = 17;
                break;
            case "wrong_realm":
                root["realm"] = "chaos_sea";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var parsed = InvokeAuthorityParser(
            "ParseContext",
            "Context",
            root,
            "treatmentContext");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Value);
        Assert.Contains(parsed.Issues, issue =>
            issue.Code == expectedCode &&
            issue.FilePath == "treatmentContext." + expectedField);
    }

    [Theory]
    [InlineData("targetKind")]
    [InlineData("providerKind")]
    public void ParseContext_RejectsUnknownActorKind(string field)
    {
        var root = CreateContext();
        root[field] = "creature";

        var parsed = InvokeAuthorityParser(
            "ParseContext",
            "Context",
            root,
            "treatmentContext");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Value);
        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "mortal_wound_treatment_context_invalid_field" &&
            issue.FilePath == "treatmentContext." + field);
    }

    [Fact]
    public void ParseContext_RejectsRawDuplicateKnownRootProperty()
    {
        var root = CreateContext();
        var json = DuplicateFirstKnownProperty(root, root, out var field);

        var parsed = InvokeAuthorityParser(
            "ParseContext",
            "Context",
            json,
            "treatmentContext");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Value);
        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "mortal_wound_treatment_context_duplicate_property" &&
            issue.FilePath == "treatmentContext." + field);
    }

    [Theory]
    [InlineData("unknown_field", "mortal_wound_treatment_snapshot_unknown_field", "unexpected")]
    [InlineData("missing_field", "mortal_wound_treatment_snapshot_missing_field", "actors")]
    [InlineData("wrong_version", "mortal_wound_treatment_snapshot_schema_version_invalid", "schemaVersion")]
    [InlineData("wrong_type", "mortal_wound_treatment_snapshot_invalid_field", "items")]
    [InlineData("nested_missing_field", "mortal_wound_treatment_snapshot_missing_field", "actors[0].actorId")]
    [InlineData("presence_wrong_type", "mortal_wound_treatment_snapshot_invalid_field", "locations[0].presentActors[0].actorId")]
    [InlineData("item_available_exceeds_count", "mortal_wound_treatment_snapshot_numeric_bounds_invalid", "items[0].availableCount")]
    [InlineData("resource_available_exceeds_current", "mortal_wound_treatment_snapshot_numeric_bounds_invalid", "resources[0].availableValue")]
    [InlineData("negative_item_available", "mortal_wound_treatment_snapshot_numeric_bounds_invalid", "items[0].availableCount")]
    [InlineData("negative_resource_available", "mortal_wound_treatment_snapshot_numeric_bounds_invalid", "resources[0].availableValue")]
    public void ParseSnapshot_RejectsNonClosedOrMalformedProjection(
        string mutation,
        string expectedCode,
        string expectedField)
    {
        var root = CreateSnapshot();
        switch (mutation)
        {
            case "unknown_field":
                root["unexpected"] = true;
                break;
            case "missing_field":
                root.Remove("actors");
                break;
            case "wrong_version":
                root["schemaVersion"] = 2;
                break;
            case "wrong_type":
                root["items"] = new JsonObject();
                break;
            case "nested_missing_field":
                Provider(root).Remove("actorId");
                break;
            case "presence_wrong_type":
                First(root, "locations")["presentActors"]![0]!["actorId"] = 17;
                break;
            case "item_available_exceeds_count":
                First(root, "items")["availableCount"] = 5;
                break;
            case "resource_available_exceeds_current":
                First(root, "resources")["availableValue"] = 9;
                break;
            case "negative_item_available":
                First(root, "items")["availableCount"] = -1;
                break;
            case "negative_resource_available":
                First(root, "resources")["availableValue"] = -1;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var parsed = InvokeAuthorityParser(
            "ParseSnapshot",
            "Snapshot",
            root,
            "treatmentSnapshot");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Value);
        Assert.Contains(parsed.Issues, issue =>
            issue.Code == expectedCode &&
            issue.FilePath == "treatmentSnapshot." + expectedField);
    }

    [Fact]
    public void ParseSnapshot_RejectsRawDuplicateKnownRootProperty()
    {
        var root = CreateSnapshot();
        var json = DuplicateFirstKnownProperty(root, root, out var field);

        var parsed = InvokeAuthorityParser(
            "ParseSnapshot",
            "Snapshot",
            json,
            "treatmentSnapshot");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Value);
        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "mortal_wound_treatment_snapshot_duplicate_property" &&
            issue.FilePath == "treatmentSnapshot." + field);
    }

    [Theory]
    [MemberData(nameof(SnapshotClosedObjectCases))]
    public void ParseSnapshot_RejectsUnknownMemberInEveryNestedClosedObject(
        string objectShape,
        string expectedObjectPath)
    {
        var root = CreateSnapshot();
        SnapshotClosedObject(root, objectShape)["unexpected"] = true;

        var parsed = InvokeAuthorityParser(
            "ParseSnapshot",
            "Snapshot",
            root,
            "treatmentSnapshot");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Value);
        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "mortal_wound_treatment_snapshot_unknown_field" &&
            issue.FilePath ==
            $"treatmentSnapshot.{expectedObjectPath}.unexpected");
    }

    [Theory]
    [MemberData(nameof(SnapshotClosedObjectCases))]
    public void ParseSnapshot_RejectsRawDuplicateKnownMemberInEveryNestedClosedObject(
        string objectShape,
        string expectedObjectPath)
    {
        var root = CreateSnapshot();
        var nested = SnapshotClosedObject(root, objectShape);
        var json = DuplicateFirstKnownProperty(root, nested, out var field);

        var parsed = InvokeAuthorityParser(
            "ParseSnapshot",
            "Snapshot",
            json,
            "treatmentSnapshot");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Value);
        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "mortal_wound_treatment_snapshot_duplicate_property" &&
            issue.FilePath ==
            $"treatmentSnapshot.{expectedObjectPath}.{field}");
    }

    [Theory]
    [MemberData(nameof(SnapshotActorKindCases))]
    public void ParseSnapshot_RejectsUnknownActorKindInEveryCoordinate(
        string objectShape,
        string field,
        string expectedPath)
    {
        var root = CreateSnapshot();
        SnapshotClosedObject(root, objectShape)[field] = "creature";

        var parsed = InvokeAuthorityParser(
            "ParseSnapshot",
            "Snapshot",
            root,
            "treatmentSnapshot");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Value);
        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "mortal_wound_treatment_snapshot_invalid_field" &&
            issue.FilePath == "treatmentSnapshot." + expectedPath);
    }

    [Fact]
    public void Resolve_AllFreshExactRequirements_SucceedsWithoutMutationAuthority()
    {
        var requirements = CreateCompleteRequirements();
        var result = Resolve(
            CreateRoute(requirements),
            CreateContext(),
            CreateSnapshot());

        AssertSuccess(result, requirements);
    }

    [Fact]
    public void Resolve_PlayerCanBeTheExactSelfProviderWithoutNpcAssumptions()
    {
        var requirements = CreateCompleteRequirements();
        Requirement(requirements, "provider")["providerRef"] = TargetRef;
        Requirement(requirements, "consent")["providerRef"] = TargetRef;
        var context = CreateContext();
        context["providerKind"] = "player";
        context["providerId"] = TargetRef;
        var snapshot = CreateSnapshot();
        First(snapshot, "items")["ownerKind"] = "player";
        First(snapshot, "items")["ownerId"] = TargetRef;
        First(snapshot, "resources")["ownerKind"] = "player";
        First(snapshot, "resources")["ownerId"] = TargetRef;
        var npcProvider = Provider(snapshot);
        var self = Target(snapshot);
        self["skills"] = npcProvider["skills"]!.DeepClone();
        self["capabilities"] = npcProvider["capabilities"]!.DeepClone();
        self["consents"] = npcProvider["consents"]!.DeepClone();
        npcProvider["skills"] = new JsonArray();
        npcProvider["capabilities"] = new JsonArray();
        npcProvider["consents"] = new JsonArray();
        var consent = First(self, "consents");
        consent["providerKind"] = "player";
        consent["providerId"] = TargetRef;

        var result = Resolve(CreateRoute(requirements), context, snapshot);

        AssertSuccessForContext(result, context, requirements);
    }

    [Fact]
    public void Resolve_NpcCanBeTheExactTargetForConsentLocationAndEffectAuthority()
    {
        const string npcPatientRef = "npc_patient_001";
        var requirements = CreateCompleteRequirements();
        Requirement(requirements, "consent")["targetRef"] = npcPatientRef;
        var context = CreateContext();
        context["targetKind"] = "npc";
        context["targetId"] = npcPatientRef;
        var snapshot = CreateSnapshot();
        var npcPatient = Target(snapshot);
        npcPatient["actorKind"] = "npc";
        npcPatient["actorId"] = npcPatientRef;
        First(Provider(snapshot), "consents")["targetKind"] = "npc";
        First(Provider(snapshot), "consents")["targetId"] = npcPatientRef;
        First(snapshot, "effects")["targetKind"] = "npc";
        First(snapshot, "effects")["targetId"] = npcPatientRef;
        First(snapshot, "locations")["presentActors"] = new JsonArray(
            CreateActorCoordinate("npc", npcPatientRef),
            CreateActorCoordinate("npc", ProviderRef));

        var result = Resolve(CreateRoute(requirements), context, snapshot);

        AssertSuccessForContext(result, context, requirements);
    }

    [Theory]
    [MemberData(nameof(RequirementKindCases))]
    public void Resolve_EachSupportedExactRequirement_SucceedsIndependently(string kind)
    {
        var requirement = CreateRequirement(kind);
        var result = Resolve(
            CreateRoute(requirement),
            CreateContext(),
            CreateSnapshot());

        AssertSuccess(result, requirement);
    }

    [Fact]
    public void Resolve_SkillIdentityIsDistinctFromCapabilityAndFingerprintBound()
    {
        var route = CreateRoute(CreateRequirement("skill_tier"));
        var snapshot = CreateSnapshot();
        var original = Resolve(route, CreateContext(), snapshot);
        Assert.True(original.Success, DescribeIssues(original.Issues));
        var originalRow = Assert.Single(original.ResolvedRequirements);
        Assert.Equal(SkillRef, originalRow.SkillId);

        const string changedSkillId = "skill_field_medicine_canonical_002";
        First(Provider(snapshot), "skills")["skillId"] = changedSkillId;
        var changed = Resolve(route, CreateContext(), snapshot);
        Assert.True(changed.Success, DescribeIssues(changed.Issues));
        var changedRow = Assert.Single(changed.ResolvedRequirements);
        Assert.Equal(SkillRef, changedRow.AuthorityRef);
        Assert.Equal(changedSkillId, changedRow.SkillId);
        Assert.NotEqual(originalRow.AuthorityFingerprint, changedRow.AuthorityFingerprint);
        Assert.NotEqual(original.AuthorityFingerprint, changed.AuthorityFingerprint);
    }

    [Theory]
    [MemberData(nameof(RoleBoundRequirementCases))]
    public void Resolve_OwnerAndActorRolesCanBindTheExactTarget(string kind)
    {
        var requirement = CreateRequirement(kind);
        requirement[kind is "item_quantity" or "resource_quantity"
            ? "ownerRole"
            : "actorRole"] = "target";
        var snapshot = CreateSnapshot();
        MoveRoleBoundAuthorityToTarget(snapshot, kind);

        var result = Resolve(
            CreateRoute(requirement),
            CreateContext(),
            snapshot);

        AssertSuccess(result, requirement);
    }

    [Theory]
    [MemberData(nameof(RoleBoundRequirementCases))]
    public void Resolve_WrongOwnerOrActorRoleNeverFallsBackToTheProvider(string kind)
    {
        var requirement = CreateRequirement(kind);
        requirement[kind is "item_quantity" or "resource_quantity"
            ? "ownerRole"
            : "actorRole"] = "target";

        var result = Resolve(
            CreateRoute(requirement),
            CreateContext(),
            CreateSnapshot());

        AssertFailure(
            result,
            "mortal_wound_requirement_wrong_owner",
            requirementIndex: 0,
            ReferenceField(kind));
    }

    [Theory]
    [InlineData("item_quantity")]
    [InlineData("resource_quantity")]
    [InlineData("skill_tier")]
    public void Resolve_QuantityAndTierEqualitySatisfiesTheDeclaredMinimum(string kind)
    {
        var requirement = CreateRequirement(kind);
        var snapshot = CreateSnapshot();
        switch (kind)
        {
            case "item_quantity":
                First(snapshot, "items")["count"] = 2;
                First(snapshot, "items")["availableCount"] = 2;
                break;
            case "resource_quantity":
                First(snapshot, "resources")["currentValue"] = 3;
                First(snapshot, "resources")["availableValue"] = 3;
                break;
            case "skill_tier":
                requirement["minimumTier"] = 3;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        var result = Resolve(
            CreateRoute(requirement),
            CreateContext(),
            snapshot);

        AssertSuccess(result, requirement);
    }

    [Theory]
    [InlineData("item_quantity", 2, 2, 4)]
    [InlineData("resource_quantity", 4, 4, 8)]
    public void Resolve_CumulativeSameAuthorityDemandEqualToAvailabilitySucceeds(
        string kind,
        int firstQuantity,
        int secondQuantity,
        int available)
    {
        var first = CreateRequirement(kind);
        var second = CreateRequirement(kind);
        first["quantity"] = firstQuantity;
        second["quantity"] = secondQuantity;
        var snapshot = CreateSnapshot();
        if (kind == "item_quantity")
        {
            First(snapshot, "items")["count"] = available;
            First(snapshot, "items")["availableCount"] = available;
        }
        else
        {
            First(snapshot, "resources")["currentValue"] = available;
            First(snapshot, "resources")["availableValue"] = available;
        }

        var result = Resolve(
            CreateRoute(first, second),
            CreateContext(),
            snapshot);

        AssertSuccess(result, first, second);
    }

    [Theory]
    [InlineData("item_quantity", 3, 2, "mortal_wound_requirement_item_quantity_insufficient")]
    [InlineData("resource_quantity", 5, 4, "mortal_wound_requirement_resource_quantity_insufficient")]
    public void Resolve_IndividuallySatisfiableSameAuthorityDemandsCannotOverbook(
        string kind,
        int firstQuantity,
        int secondQuantity,
        string expectedCode)
    {
        var first = CreateRequirement(kind);
        var second = CreateRequirement(kind);
        first["quantity"] = firstQuantity;
        second["quantity"] = secondQuantity;

        var result = Resolve(
            CreateRoute(first, second),
            CreateContext(),
            CreateSnapshot());

        AssertFailure(result, expectedCode, requirementIndex: 1);
    }

    [Theory]
    [MemberData(nameof(RequirementKindCases))]
    public void Resolve_AnyUnsatisfiedConjunct_FailsTheWholeRoute(string kind)
    {
        var snapshot = CreateSnapshot();
        MakeRequirementUnsatisfied(snapshot, kind);
        var requirements = CreateCompleteRequirements();
        var requirementIndex = Array.IndexOf(Kinds, kind);

        var result = Resolve(
            CreateRoute(requirements),
            CreateContext(),
            snapshot);

        AssertFailure(
            result,
            UnsatisfiedIssueCode(kind),
            requirementIndex);
    }

    [Theory]
    [MemberData(nameof(ConfusableReferenceCases))]
    public void Resolve_ReferenceMatchingIsOrdinalAndNeverUsesDisplayFallback(
        string kind,
        string mutation)
    {
        var requirement = CreateRequirement(kind);
        var exact = ExactReference(kind);
        requirement[ReferenceField(kind)] = mutation switch
        {
            "case_changed" => exact.ToUpperInvariant(),
            "ascii_confusable" => exact[..^1] + "l",
            "cyrillic_confusable" => AddCyrillicConfusable(exact),
            "display_name" => DisplayName(kind),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };

        var result = Resolve(
            CreateRoute(requirement),
            CreateContext(),
            CreateSnapshot());

        AssertFailure(
            result,
            "mortal_wound_requirement_reference_unresolved",
            requirementIndex: 0,
            ReferenceField(kind));
    }

    [Theory]
    [MemberData(nameof(RequirementKindCases))]
    public void Resolve_ExactButRetiredAuthority_IsRejectedAsStale(string kind)
    {
        var snapshot = CreateSnapshot();
        SetLifecycle(snapshot, kind, "retired");

        var result = Resolve(
            CreateRoute(CreateRequirement(kind)),
            CreateContext(),
            snapshot);

        AssertFailure(
            result,
            "mortal_wound_requirement_stale_reference",
            requirementIndex: 0,
            ReferenceField(kind));
    }

    [Theory]
    [MemberData(nameof(RequirementKindCases))]
    public void Resolve_ExactAuthorityFromAnotherRealm_IsRejected(string kind)
    {
        var snapshot = CreateSnapshot();
        SetRealm(snapshot, kind, "chaos_sea");

        var result = Resolve(
            CreateRoute(CreateRequirement(kind)),
            CreateContext(),
            snapshot);

        AssertFailure(
            result,
            "mortal_wound_requirement_cross_realm_reference",
            requirementIndex: 0,
            ReferenceField(kind));
    }

    [Theory]
    [MemberData(nameof(RequirementKindCases))]
    public void Resolve_DuplicateExactAuthority_IsRejectedAsAmbiguous(string kind)
    {
        var snapshot = CreateSnapshot();
        DuplicateAuthorityEntry(snapshot, kind);

        var result = Resolve(
            CreateRoute(CreateRequirement(kind)),
            CreateContext(),
            snapshot);

        AssertFailure(
            result,
            "mortal_wound_requirement_reference_ambiguous",
            requirementIndex: 0,
            ReferenceField(kind));
    }

    [Theory]
    [InlineData("insufficient_count", "mortal_wound_requirement_item_quantity_insufficient")]
    [InlineData("insufficient_available", "mortal_wound_requirement_item_quantity_insufficient")]
    [InlineData("reserved", "mortal_wound_requirement_item_reserved")]
    [InlineData("wrong_owner", "mortal_wound_requirement_wrong_owner")]
    [InlineData("wrong_owner_kind", "mortal_wound_requirement_wrong_owner")]
    [InlineData("inactive", "mortal_wound_requirement_item_unavailable")]
    [InlineData("duplicate_identity", "mortal_wound_requirement_reference_ambiguous")]
    public void Resolve_ItemRequirement_RevalidatesQuantityOwnershipAvailabilityAndReservation(
        string mutation,
        string expectedCode)
    {
        var snapshot = CreateSnapshot();
        var item = First(snapshot, "items");
        switch (mutation)
        {
            case "insufficient_count":
                item["count"] = 1;
                item["availableCount"] = 1;
                break;
            case "insufficient_available":
                item["availableCount"] = 1;
                break;
            case "reserved":
                item["reservationState"] = "reserved";
                break;
            case "wrong_owner":
                item["ownerId"] = "npc_other_001";
                break;
            case "wrong_owner_kind":
                item["ownerKind"] = "player";
                break;
            case "inactive":
                item["active"] = false;
                break;
            case "duplicate_identity":
                snapshot["items"]!.AsArray().Add(item.DeepClone());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = Resolve(
            CreateRoute(CreateRequirement("item_quantity")),
            CreateContext(),
            snapshot);

        AssertFailure(result, expectedCode, requirementIndex: 0);
    }

    [Theory]
    [InlineData("insufficient_value", "mortal_wound_requirement_resource_quantity_insufficient")]
    [InlineData("insufficient_available", "mortal_wound_requirement_resource_quantity_insufficient")]
    [InlineData("reserved", "mortal_wound_requirement_resource_reserved")]
    [InlineData("wrong_owner", "mortal_wound_requirement_wrong_owner")]
    [InlineData("wrong_owner_kind", "mortal_wound_requirement_wrong_owner")]
    [InlineData("inactive", "mortal_wound_requirement_resource_unavailable")]
    [InlineData("duplicate_identity", "mortal_wound_requirement_reference_ambiguous")]
    public void Resolve_ResourceRequirement_RevalidatesLedgerOwnerAndAvailableValue(
        string mutation,
        string expectedCode)
    {
        var snapshot = CreateSnapshot();
        var resource = First(snapshot, "resources");
        switch (mutation)
        {
            case "insufficient_value":
                resource["currentValue"] = 2;
                resource["availableValue"] = 2;
                break;
            case "insufficient_available":
                resource["availableValue"] = 2;
                break;
            case "reserved":
                resource["reservationState"] = "reserved";
                break;
            case "wrong_owner":
                resource["ownerId"] = "npc_other_001";
                break;
            case "wrong_owner_kind":
                resource["ownerKind"] = "player";
                break;
            case "inactive":
                resource["active"] = false;
                break;
            case "duplicate_identity":
                snapshot["resources"]!.AsArray().Add(resource.DeepClone());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = Resolve(
            CreateRoute(CreateRequirement("resource_quantity")),
            CreateContext(),
            snapshot);

        AssertFailure(result, expectedCode, requirementIndex: 0);
    }

    [Theory]
    [InlineData("tier_too_low", "mortal_wound_requirement_skill_tier_insufficient")]
    [InlineData("inactive", "mortal_wound_requirement_skill_inactive")]
    [InlineData("wrong_actor", "mortal_wound_requirement_wrong_owner")]
    [InlineData("duplicate_identity", "mortal_wound_requirement_reference_ambiguous")]
    public void Resolve_SkillTier_UsesTheExactActiveProviderSkill(
        string mutation,
        string expectedCode)
    {
        var snapshot = CreateSnapshot();
        var provider = Provider(snapshot);
        var skill = First(provider, "skills");
        switch (mutation)
        {
            case "tier_too_low":
                skill["tier"] = 1;
                break;
            case "inactive":
                skill["active"] = false;
                break;
            case "wrong_actor":
                provider["skills"]!.AsArray().Clear();
                Target(snapshot)["skills"]!.AsArray().Add(skill.DeepClone());
                break;
            case "duplicate_identity":
                provider["skills"]!.AsArray().Add(skill.DeepClone());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = Resolve(
            CreateRoute(CreateRequirement("skill_tier")),
            CreateContext(),
            snapshot);

        AssertFailure(result, expectedCode, requirementIndex: 0);
    }

    [Theory]
    [InlineData("inactive", "mortal_wound_requirement_capability_inactive")]
    [InlineData("wrong_actor", "mortal_wound_requirement_wrong_owner")]
    [InlineData("duplicate_identity", "mortal_wound_requirement_reference_ambiguous")]
    public void Resolve_SourceCapability_UsesTheExactActiveProviderCapability(
        string mutation,
        string expectedCode)
    {
        var snapshot = CreateSnapshot();
        var provider = Provider(snapshot);
        var capability = First(provider, "capabilities");
        switch (mutation)
        {
            case "inactive":
                capability["active"] = false;
                break;
            case "wrong_actor":
                provider["capabilities"]!.AsArray().Clear();
                Target(snapshot)["capabilities"]!.AsArray().Add(capability.DeepClone());
                break;
            case "duplicate_identity":
                provider["capabilities"]!.AsArray().Add(capability.DeepClone());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = Resolve(
            CreateRoute(CreateRequirement("source_capability")),
            CreateContext(),
            snapshot);

        AssertFailure(result, expectedCode, requirementIndex: 0);
    }

    [Theory]
    [InlineData("inactive_provider", "mortal_wound_requirement_provider_inactive")]
    [InlineData("unreachable_provider", "mortal_wound_requirement_provider_unreachable")]
    [InlineData("provider_elsewhere", "mortal_wound_requirement_provider_unreachable")]
    [InlineData("context_substitution", "mortal_wound_requirement_provider_binding_mismatch")]
    public void Resolve_Provider_RevalidatesExactBindingReachabilityAndLocation(
        string mutation,
        string expectedCode)
    {
        var snapshot = CreateSnapshot();
        var context = CreateContext();
        switch (mutation)
        {
            case "inactive_provider":
                Provider(snapshot)["active"] = false;
                break;
            case "unreachable_provider":
                Provider(snapshot)["reachable"] = false;
                break;
            case "provider_elsewhere":
                Provider(snapshot)["currentLocationId"] = "loc_other_001";
                break;
            case "context_substitution":
                context["providerId"] = "npc_other_001";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = Resolve(
            CreateRoute(CreateRequirement("provider")),
            context,
            snapshot);

        AssertFailure(result, expectedCode, requirementIndex: 0);
    }

    [Theory]
    [InlineData("target_kind", "location", "targetKind", "mortal_wound_treatment_context_target_mismatch")]
    [InlineData("target_id", "location", "targetId", "mortal_wound_treatment_context_target_mismatch")]
    [InlineData("provider_kind", "provider", "providerKind", "mortal_wound_treatment_context_provider_mismatch")]
    [InlineData("provider_id", "provider", "providerId", "mortal_wound_treatment_context_provider_mismatch")]
    [InlineData("current_location", "facility", "currentLocationId", "mortal_wound_treatment_context_location_mismatch")]
    public void Resolve_ContextCoordinatesAreExactAuthorityNotHints(
        string mutation,
        string requirementKind,
        string contextField,
        string expectedCode)
    {
        var context = CreateContext();
        switch (mutation)
        {
            case "target_kind":
                context["targetKind"] = "npc";
                break;
            case "target_id":
                context["targetId"] = "player_other";
                break;
            case "provider_kind":
                context["providerKind"] = "player";
                break;
            case "provider_id":
                context["providerId"] = "npc_other_001";
                break;
            case "current_location":
                context["currentLocationId"] = "loc_other_001";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = Resolve(
            CreateRoute(CreateRequirement(requirementKind)),
            context,
            CreateSnapshot());

        AssertFailure(
            result,
            expectedCode,
            requirementIndex: null,
            expectedPath: "treatmentContext." + contextField);
    }

    [Theory]
    [MemberData(nameof(ContextBindingCases))]
    public void Resolve_ContextCoordinatesRequireOneCurrentActiveSameRealmAuthority(
        string mutation,
        string contextField,
        string expectedCode)
    {
        var snapshot = CreateSnapshot();
        switch (mutation)
        {
            case "duplicate_target":
                snapshot["actors"]!.AsArray().Add(Target(snapshot).DeepClone());
                break;
            case "target_foreign_realm":
                Target(snapshot)["realm"] = "chaos_sea";
                break;
            case "target_retired":
                Target(snapshot)["lifecycle"] = "retired";
                break;
            case "target_inactive":
                Target(snapshot)["active"] = false;
                break;
            case "duplicate_provider":
                snapshot["actors"]!.AsArray().Add(Provider(snapshot).DeepClone());
                break;
            case "provider_foreign_realm":
                Provider(snapshot)["realm"] = "chaos_sea";
                break;
            case "provider_retired":
                Provider(snapshot)["lifecycle"] = "retired";
                break;
            case "provider_inactive":
                Provider(snapshot)["active"] = false;
                break;
            case "duplicate_location":
                snapshot["locations"]!.AsArray().Add(
                    First(snapshot, "locations").DeepClone());
                break;
            case "location_foreign_realm":
                First(snapshot, "locations")["realm"] = "chaos_sea";
                break;
            case "location_retired":
                First(snapshot, "locations")["lifecycle"] = "retired";
                break;
            case "location_inactive":
                First(snapshot, "locations")["active"] = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = Resolve(
            CreateRoute(CreateRequirement("item_quantity")),
            CreateContext(),
            snapshot);

        AssertFailure(
            result,
            expectedCode,
            requirementIndex: null,
            expectedPath: "treatmentContext." + contextField);
    }

    [Theory]
    [InlineData("withdrawn", "mortal_wound_requirement_consent_missing")]
    [InlineData("wrong_target", "mortal_wound_requirement_consent_binding_mismatch")]
    [InlineData("wrong_provider", "mortal_wound_requirement_consent_binding_mismatch")]
    [InlineData("wrong_target_kind", "mortal_wound_requirement_consent_binding_mismatch")]
    [InlineData("wrong_provider_kind", "mortal_wound_requirement_consent_binding_mismatch")]
    [InlineData("duplicate_identity", "mortal_wound_requirement_reference_ambiguous")]
    public void Resolve_Consent_IsExactCurrentAndTargetSpecific(
        string mutation,
        string expectedCode)
    {
        var snapshot = CreateSnapshot();
        var consent = First(Provider(snapshot), "consents");
        switch (mutation)
        {
            case "withdrawn":
                consent["status"] = "withdrawn";
                break;
            case "wrong_target":
                consent["targetId"] = "player_other";
                break;
            case "wrong_provider":
                consent["providerId"] = "npc_other_001";
                break;
            case "wrong_target_kind":
                consent["targetKind"] = "npc";
                break;
            case "wrong_provider_kind":
                consent["providerKind"] = "player";
                break;
            case "duplicate_identity":
                Provider(snapshot)["consents"]!.AsArray().Add(consent.DeepClone());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = Resolve(
            CreateRoute(CreateRequirement("consent")),
            CreateContext(),
            snapshot);

        AssertFailure(result, expectedCode, requirementIndex: 0);
    }

    [Theory]
    [InlineData("facility_unavailable", "mortal_wound_requirement_facility_unavailable")]
    [InlineData("facility_elsewhere", "mortal_wound_requirement_facility_location_mismatch")]
    [InlineData("target_absent", "mortal_wound_requirement_target_not_present")]
    [InlineData("target_wrong_kind", "mortal_wound_requirement_target_not_present")]
    [InlineData("provider_absent", "mortal_wound_requirement_provider_unreachable")]
    [InlineData("provider_wrong_kind", "mortal_wound_requirement_provider_unreachable")]
    public void Resolve_FacilityAndLocation_RequireCurrentCoPresence(
        string mutation,
        string expectedCode)
    {
        var snapshot = CreateSnapshot();
        switch (mutation)
        {
            case "facility_unavailable":
                First(snapshot, "facilities")["available"] = false;
                break;
            case "facility_elsewhere":
                First(snapshot, "facilities")["locationId"] = "loc_other_001";
                break;
            case "target_absent":
                First(snapshot, "locations")["presentActors"] =
                    new JsonArray(CreateActorCoordinate("npc", ProviderRef));
                break;
            case "target_wrong_kind":
                First(snapshot, "locations")["presentActors"] = new JsonArray(
                    CreateActorCoordinate("npc", TargetRef),
                    CreateActorCoordinate("npc", ProviderRef));
                break;
            case "provider_absent":
                First(snapshot, "locations")["presentActors"] =
                    new JsonArray(CreateActorCoordinate("player", TargetRef));
                break;
            case "provider_wrong_kind":
                First(snapshot, "locations")["presentActors"] = new JsonArray(
                    CreateActorCoordinate("player", TargetRef),
                    CreateActorCoordinate("player", ProviderRef));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = Resolve(
            CreateRoute(
                CreateRequirement("provider"),
                CreateRequirement("facility"),
                CreateRequirement("location")),
            CreateContext(),
            snapshot);

        var expectedIndex = mutation switch
        {
            "facility_unavailable" or "facility_elsewhere" => 1,
            "target_absent" or "target_wrong_kind" => 2,
            "provider_absent" or "provider_wrong_kind" => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };
        AssertFailure(result, expectedCode, expectedIndex);
    }

    [Theory]
    [InlineData("quest_state", "failed", "mortal_wound_requirement_state_mismatch")]
    [InlineData("effect_state", "expired", "mortal_wound_requirement_state_mismatch")]
    [InlineData("environment", "contaminated", "mortal_wound_requirement_state_mismatch")]
    public void Resolve_QuestEffectAndEnvironment_RequireExactCurrentState(
        string kind,
        string wrongState,
        string expectedCode)
    {
        var snapshot = CreateSnapshot();
        AuthorityEntry(snapshot, kind)["state"] = wrongState;

        var result = Resolve(
            CreateRoute(CreateRequirement(kind)),
            CreateContext(),
            snapshot);

        AssertFailure(result, expectedCode, requirementIndex: 0, "requiredState");
    }

    [Theory]
    [InlineData("effect_target_kind", "mortal_wound_requirement_effect_target_mismatch")]
    [InlineData("effect_target_id", "mortal_wound_requirement_effect_target_mismatch")]
    [InlineData("environment_location", "mortal_wound_requirement_environment_location_mismatch")]
    public void Resolve_EffectAndEnvironmentBindTheExactTargetOrLocation(
        string mutation,
        string expectedCode)
    {
        var snapshot = CreateSnapshot();
        var kind = mutation.StartsWith("effect_", StringComparison.Ordinal)
            ? "effect_state"
            : "environment";
        switch (mutation)
        {
            case "effect_target_kind":
                First(snapshot, "effects")["targetKind"] = "npc";
                break;
            case "effect_target_id":
                First(snapshot, "effects")["targetId"] = "player_other";
                break;
            case "environment_location":
                First(snapshot, "environments")["locationId"] = "loc_other_001";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = Resolve(
            CreateRoute(CreateRequirement(kind)),
            CreateContext(),
            snapshot);

        AssertFailure(result, expectedCode, requirementIndex: 0);
    }

    [Fact]
    public void Resolve_FreshSnapshotIsRecheckedAndChangesAuthorityFingerprint()
    {
        var snapshot = CreateSnapshot();
        var requirements = CreateCompleteRequirements();
        var route = CreateRoute(requirements);
        var accepted = Resolve(route, CreateContext(), snapshot);
        var stale = snapshot.DeepClone().AsObject();
        First(stale, "items")["reservationState"] = "reserved";

        var rejected = Resolve(route, CreateContext(), stale);

        AssertSuccess(accepted, requirements);
        AssertFailure(
            rejected,
            "mortal_wound_requirement_item_reserved",
            requirementIndex: 0);
        Assert.NotEqual(accepted.AuthorityFingerprint, rejected.AuthorityFingerprint);
    }

    [Fact]
    public void Resolve_EquivalentDetachedSnapshotsProduceTheSameAuthorityFingerprint()
    {
        var requirements = CreateCompleteRequirements();
        var route = CreateRoute(requirements);
        var first = Resolve(route, CreateContext(), CreateSnapshot());
        var second = Resolve(
            route,
            CreateContext().DeepClone().AsObject(),
            CreateSnapshot().DeepClone().AsObject());

        AssertSuccess(first, requirements);
        AssertSuccess(second, requirements);
        Assert.Equal(first.AuthorityFingerprint, second.AuthorityFingerprint);
    }

    [Fact]
    public void Resolve_AuthorityFingerprintBindsRequirementQuantityAndOrder()
    {
        var item = CreateRequirement("item_quantity");
        var resource = CreateRequirement("resource_quantity");
        var baseline = Resolve(
            CreateRoute(item, resource),
            CreateContext(),
            CreateSnapshot());
        var largerItem = item.DeepClone().AsObject();
        largerItem["quantity"] = 3;
        var changedQuantity = Resolve(
            CreateRoute(largerItem, resource),
            CreateContext(),
            CreateSnapshot());
        var changedOrder = Resolve(
            CreateRoute(resource, item),
            CreateContext(),
            CreateSnapshot());

        AssertSuccess(baseline, item, resource);
        AssertSuccess(changedQuantity, largerItem, resource);
        AssertSuccess(changedOrder, resource, item);
        Assert.NotEqual(
            baseline.AuthorityFingerprint,
            changedQuantity.AuthorityFingerprint);
        Assert.NotEqual(
            baseline.ResolvedRequirements[0].AuthorityFingerprint,
            changedQuantity.ResolvedRequirements[0].AuthorityFingerprint);
        Assert.NotEqual(
            baseline.AuthorityFingerprint,
            changedOrder.AuthorityFingerprint);
        Assert.NotEqual(
            baseline.ResolvedRequirements[0].AuthorityFingerprint,
            changedOrder.ResolvedRequirements[1].AuthorityFingerprint);
    }

    [Fact]
    public void Resolve_AuthorityFingerprintExcludesDisplayOnlyDiagnostics()
    {
        var requirements = CreateCompleteRequirements();
        var baselineSnapshot = CreateSnapshot();
        var renamedSnapshot = baselineSnapshot.DeepClone().AsObject();
        RewriteDisplayNames(renamedSnapshot, "Диагностический текст изменён");

        var baseline = Resolve(
            CreateRoute(requirements),
            CreateContext(),
            baselineSnapshot);
        var renamed = Resolve(
            CreateRoute(requirements),
            CreateContext(),
            renamedSnapshot);

        AssertSuccess(baseline, requirements);
        AssertSuccess(renamed, requirements);
        Assert.Equal(baseline.AuthorityFingerprint, renamed.AuthorityFingerprint);
        Assert.Equal(
            baseline.ResolvedRequirements.Select(static row => row.AuthorityFingerprint),
            renamed.ResolvedRequirements.Select(static row => row.AuthorityFingerprint));
    }

    [Fact]
    public void Resolve_AuthorityFingerprintBindsExactContextTargetCoordinate()
    {
        const string otherTargetId = "player_other_current";
        var requirement = CreateRequirement("location");
        var snapshot = CreateSnapshot();
        var otherTarget = Target(snapshot).DeepClone().AsObject();
        otherTarget["actorId"] = otherTargetId;
        snapshot["actors"]!.AsArray().Add(otherTarget);
        First(snapshot, "locations")["presentActors"]!.AsArray().Add(
            CreateActorCoordinate("player", otherTargetId));
        var otherContext = CreateContext();
        otherContext["targetId"] = otherTargetId;

        var baseline = Resolve(
            CreateRoute(requirement),
            CreateContext(),
            snapshot);
        var otherTargetResult = Resolve(
            CreateRoute(requirement),
            otherContext,
            snapshot);

        AssertSuccess(baseline, requirement);
        Assert.True(otherTargetResult.Success, DescribeIssues(otherTargetResult.Issues));
        var evidence = Assert.Single(otherTargetResult.ResolvedRequirements);
        Assert.Equal("player", evidence.TargetKind);
        Assert.Equal(otherTargetId, evidence.TargetId);
        Assert.Equal(LocationRef, evidence.LocationId);
        Assert.NotEqual(
            baseline.AuthorityFingerprint,
            otherTargetResult.AuthorityFingerprint);
        Assert.NotEqual(
            baseline.ResolvedRequirements[0].AuthorityFingerprint,
            otherTargetResult.ResolvedRequirements[0].AuthorityFingerprint);
    }

    [Fact]
    public void Resolve_SameDisplayNamesAcrossSettingsNeverSubstituteExactItemIdentity()
    {
        var postApocalyptic = CreateSnapshot();
        var magical = CreateSnapshot();
        const string magicItemRef = "itm_crystal_dust_001";
        First(magical, "items")["itemId"] = magicItemRef;
        First(magical, "items")["displayName"] = DisplayName("item_quantity");
        var postRoute = CreateRoute(CreateRequirement("item_quantity"));
        var magicRequirement = CreateRequirement("item_quantity");
        magicRequirement["itemRef"] = magicItemRef;
        var magicRoute = CreateRoute(magicRequirement);

        var postResult = Resolve(postRoute, CreateContext(), postApocalyptic);
        var magicResult = Resolve(magicRoute, CreateContext(), magical);
        var crossedResult = Resolve(postRoute, CreateContext(), magical);

        AssertSuccess(postResult, CreateRequirement("item_quantity"));
        AssertSuccess(magicResult, magicRequirement);
        Assert.NotEqual(postResult.AuthorityFingerprint, magicResult.AuthorityFingerprint);
        Assert.NotEqual(
            postResult.ResolvedRequirements[0].AuthorityFingerprint,
            magicResult.ResolvedRequirements[0].AuthorityFingerprint);
        AssertFailure(
            crossedResult,
            "mortal_wound_requirement_reference_unresolved",
            requirementIndex: 0,
            "itemRef");
    }

    private static ResolutionView Resolve(
        WoundTreatmentRoute route,
        JsonObject context,
        JsonObject snapshot)
    {
        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(type);
        var contextArgument = ParseAuthorityInput(
            type,
            "ParseContext",
            "Context",
            context,
            "treatmentContext");
        var snapshotArgument = ParseAuthorityInput(
            type,
            "ParseSnapshot",
            "Snapshot",
            snapshot,
            "treatmentSnapshot");
        var method = Assert.Single(
            type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate =>
                candidate.Name == "ResolveRequirements" &&
                candidate.GetParameters().Length == 3);
        var parameters = method.GetParameters();
        Assert.Equal(typeof(WoundTreatmentRoute), parameters[0].ParameterType);
        Assert.Equal(contextArgument.GetType(), parameters[1].ParameterType);
        Assert.Equal(snapshotArgument.GetType(), parameters[2].ParameterType);
        Assert.False(typeof(JsonNode).IsAssignableFrom(parameters[1].ParameterType));
        Assert.False(typeof(JsonNode).IsAssignableFrom(parameters[2].ParameterType));

        var routeBefore = SerializeRuntime(route);
        var contextBefore = SerializeRuntime(contextArgument);
        var snapshotBefore = SerializeRuntime(snapshotArgument);

        object runtimeResult;
        try
        {
            var invoked = method.Invoke(
                null,
                new[] { (object)route, contextArgument, snapshotArgument });
            Assert.NotNull(invoked);
            runtimeResult = invoked;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }

        Assert.Equal(routeBefore, SerializeRuntime(route));
        Assert.Equal(contextBefore, SerializeRuntime(contextArgument));
        Assert.Equal(snapshotBefore, SerializeRuntime(snapshotArgument));
        AssertExternallyImmutableGraph(runtimeResult, "$result");

        var resultType = runtimeResult.GetType();
        Assert.Equal(
            new[]
            {
                "AuthorityFingerprint",
                "Issues",
                "ResolvedRequirements",
                "Success"
            },
            resultType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));
        var success = Assert.IsType<bool>(ReadRequiredProperty(runtimeResult, "Success"));
        var issues = ReadEnumerableProperty(runtimeResult, "Issues")
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        var resolved = ReadEnumerableProperty(runtimeResult, "ResolvedRequirements")
            .Select(ReadResolvedRequirement)
            .ToArray();
        var fingerprint = Assert.IsType<string>(
            ReadRequiredProperty(runtimeResult, "AuthorityFingerprint"));
        var resultJson = JsonSerializer.SerializeToNode(
            runtimeResult,
            resultType,
            RuntimeJson);
        Assert.NotNull(resultJson);
        AssertNoForbiddenIntentMembers(resultJson, "$result");

        return new ResolutionView(success, issues, resolved, fingerprint);
    }

    private static object ParseAuthorityInput(
        Type authorityType,
        string methodName,
        string valueProperty,
        JsonObject root,
        string path)
    {
        var parsed = InvokeAuthorityParser(
            authorityType,
            methodName,
            valueProperty,
            root,
            path);
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        Assert.Empty(parsed.Issues);
        Assert.NotNull(parsed.Value);
        Assert.False(parsed.Value is JsonNode or JsonElement or string);
        return parsed.Value;
    }

    private static AuthorityParseView InvokeAuthorityParser(
        string methodName,
        string valueProperty,
        JsonObject root,
        string path)
        => InvokeAuthorityParser(
            methodName,
            valueProperty,
            root.ToJsonString(),
            path);

    private static AuthorityParseView InvokeAuthorityParser(
        string methodName,
        string valueProperty,
        string json,
        string path)
    {
        var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(authorityType);
        return InvokeAuthorityParser(
            authorityType,
            methodName,
            valueProperty,
            json,
            path);
    }

    private static AuthorityParseView InvokeAuthorityParser(
        Type authorityType,
        string methodName,
        string valueProperty,
        JsonObject root,
        string path)
        => InvokeAuthorityParser(
            authorityType,
            methodName,
            valueProperty,
            root.ToJsonString(),
            path);

    private static AuthorityParseView InvokeAuthorityParser(
        Type authorityType,
        string methodName,
        string valueProperty,
        string json,
        string path)
    {
        var method = Assert.Single(
            authorityType.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate =>
                candidate.Name == methodName &&
                candidate.GetParameters().Length == 2 &&
                candidate.GetParameters().All(static parameter =>
                    parameter.ParameterType == typeof(string)));
        object parseResult;
        try
        {
            var invoked = method.Invoke(null, new object[] { json, path });
            Assert.NotNull(invoked);
            parseResult = invoked;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }

        Assert.Equal(
            new[] { "IsValid", "Issues", valueProperty }.OrderBy(
                static value => value,
                StringComparer.Ordinal),
            parseResult.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static value => value, StringComparer.Ordinal));
        var isValid = Assert.IsType<bool>(ReadRequiredProperty(parseResult, "IsValid"));
        var issues = ReadEnumerableProperty(parseResult, "Issues")
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        var value = ReadPropertyAllowingNull(parseResult, valueProperty);
        return new AuthorityParseView(isValid, issues, value);
    }

    private static string SerializeRuntime(object value) =>
        JsonSerializer.Serialize(value, value.GetType(), RuntimeJson);

    private static void AssertExternallyImmutableGraph(object value, string path)
    {
        AssertExternallyImmutableGraph(
            value,
            path,
            new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static void AssertExternallyImmutableGraph(
        object value,
        string path,
        HashSet<object> visited)
    {
        Assert.False(
            value is JsonNode or JsonElement or JsonDocument,
            $"{path} exposes raw JSON instead of a typed authority value.");
        var type = value.GetType();
        if (type.IsPrimitive ||
            type.IsEnum ||
            value is string or decimal or DateTime or DateTimeOffset or Guid or
                ValidationIssue)
        {
            return;
        }

        if (!type.IsValueType && !visited.Add(value))
            return;

        if (value is IEnumerable enumerable)
        {
            if (value is IList list)
                Assert.True(list.IsReadOnly, $"{path} exposes mutable IList {type.FullName}.");

            foreach (var collectionInterface in type.GetInterfaces().Where(
                         static candidate =>
                             candidate.IsGenericType &&
                             candidate.GetGenericTypeDefinition() ==
                             typeof(ICollection<>)))
            {
                var isReadOnly = Assert.IsType<bool>(
                    collectionInterface.GetProperty("IsReadOnly")!.GetValue(value));
                Assert.True(
                    isReadOnly,
                    $"{path} exposes mutable {collectionInterface.FullName}.");
            }

            var index = 0;
            foreach (var item in enumerable)
            {
                if (item is not null)
                    AssertExternallyImmutableGraph(item, $"{path}[{index}]", visited);
                index++;
            }

            return;
        }

        foreach (var property in type.GetProperties(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length != 0 || property.GetMethod is null)
                continue;

            var setter = property.SetMethod;
            Assert.True(
                setter is null || !setter.IsPublic || IsInitOnly(setter),
                $"{path}.{property.Name} has an ordinary public setter.");
            var child = property.GetValue(value);
            if (child is not null)
                AssertExternallyImmutableGraph(
                    child,
                    path + "." + property.Name,
                    visited);
        }
    }

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(
            typeof(System.Runtime.CompilerServices.IsExternalInit));

    private static void AssertNoForbiddenIntentMembers(JsonNode node, string path)
    {
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "reservations",
            "reservationIntents",
            "itemMutations",
            "itemMutationIntents",
            "resourceMutations",
            "resourceMutationIntents",
            "transitionIntents",
            "consumptionIntents"
        };

        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                Assert.DoesNotContain(property.Key, forbidden);
                if (property.Value is not null)
                    AssertNoForbiddenIntentMembers(property.Value, path + "." + property.Key);
            }

            return;
        }

        if (node is not JsonArray array)
            return;
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not null)
                AssertNoForbiddenIntentMembers(array[index]!, $"{path}[{index}]");
        }
    }

    private static ResolvedRequirementView ReadResolvedRequirement(object value)
    {
        Assert.Equal(
            new[]
            {
                "AuthorityFingerprint",
                "AuthorityRef",
                "CurrentState",
                "CurrentTier",
                "Kind",
                "LocationId",
                "MinimumTier",
                "OwnerId",
                "OwnerKind",
                "ProviderId",
                "ProviderKind",
                "Realm",
                "RequestedQuantity",
                "RequirementIndex",
                "SkillId",
                "TargetId",
                "TargetKind"
            },
            value.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));
        return new ResolvedRequirementView(
            Assert.IsType<int>(ReadRequiredProperty(value, "RequirementIndex")),
            Assert.IsType<string>(ReadRequiredProperty(value, "Kind")),
            Assert.IsType<string>(ReadRequiredProperty(value, "AuthorityRef")),
            ReadNullableStringProperty(value, "SkillId"),
            Assert.IsType<string>(ReadRequiredProperty(value, "Realm")),
            ReadNullableStringProperty(value, "OwnerKind"),
            ReadNullableStringProperty(value, "OwnerId"),
            ReadNullableStringProperty(value, "ProviderKind"),
            ReadNullableStringProperty(value, "ProviderId"),
            ReadNullableStringProperty(value, "TargetKind"),
            ReadNullableStringProperty(value, "TargetId"),
            ReadNullableStringProperty(value, "LocationId"),
            ReadNullableIntProperty(value, "RequestedQuantity"),
            ReadNullableIntProperty(value, "MinimumTier"),
            ReadNullableIntProperty(value, "CurrentTier"),
            ReadNullableStringProperty(value, "CurrentState"),
            Assert.IsType<string>(ReadRequiredProperty(value, "AuthorityFingerprint")));
    }

    private static string? ReadNullableStringProperty(object instance, string name)
    {
        var value = ReadPropertyAllowingNull(instance, name);
        return value is null ? null : Assert.IsType<string>(value);
    }

    private static int? ReadNullableIntProperty(object instance, string name)
    {
        var value = ReadPropertyAllowingNull(instance, name);
        return value is null ? null : Assert.IsType<int>(value);
    }

    private static object? ReadPropertyAllowingNull(object instance, string name)
    {
        var property = instance.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property.GetValue(instance);
    }

    private static object ReadRequiredProperty(object instance, string name)
    {
        var property = instance.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        var value = property.GetValue(instance);
        Assert.NotNull(value);
        return value;
    }

    private static IReadOnlyList<object> ReadEnumerableProperty(
        object instance,
        string name)
    {
        var enumerable = Assert.IsAssignableFrom<IEnumerable>(
            ReadRequiredProperty(instance, name));
        return enumerable.Cast<object>().ToArray();
    }

    private static void AssertSuccess(
        ResolutionView result,
        params JsonObject[] expectedRequirements) =>
        AssertSuccessForContext(result, CreateContext(), expectedRequirements);

    private static void AssertSuccessForContext(
        ResolutionView result,
        JsonObject context,
        params JsonObject[] expectedRequirements)
    {
        Assert.True(result.Success, DescribeIssues(result.Issues));
        Assert.Empty(result.Issues);
        Assert.Equal(expectedRequirements.Length, result.ResolvedRequirements.Count);
        Assert.True(
            ResourceMaterializationContract.IsAuthorityFingerprint(
                result.AuthorityFingerprint));
        for (var index = 0; index < expectedRequirements.Length; index++)
        {
            AssertResolvedCoordinates(
                result.ResolvedRequirements[index],
                expectedRequirements[index],
                index,
                context);
        }
    }

    private static void AssertResolvedCoordinates(
        ResolvedRequirementView actual,
        JsonObject requirement,
        int index,
        JsonObject context)
    {
        var kind = requirement["kind"]!.GetValue<string>();
        var selectedProviderKind = context["providerKind"]!.GetValue<string>();
        var selectedProviderId = context["providerId"]!.GetValue<string>();
        var selectedTargetKind = context["targetKind"]!.GetValue<string>();
        var selectedTargetId = context["targetId"]!.GetValue<string>();
        var selectedLocationId = context["currentLocationId"]!.GetValue<string>();
        Assert.Equal(index, actual.RequirementIndex);
        Assert.Equal(kind, actual.Kind);
        Assert.Equal(
            requirement[ReferenceField(kind)]!.GetValue<string>(),
            actual.AuthorityRef);
        Assert.Equal(kind == "skill_tier" ? SkillRef : null, actual.SkillId);
        Assert.Equal("mortal_world", actual.Realm);
        Assert.True(
            ResourceMaterializationContract.IsAuthorityFingerprint(
                actual.AuthorityFingerprint));

        string? ownerKind = null;
        string? ownerId = null;
        string? providerKind = null;
        string? providerId = null;
        string? targetKind = null;
        string? targetId = null;
        string? locationId = null;
        int? requestedQuantity = null;
        int? minimumTier = null;
        int? currentTier = null;
        string? currentState = null;

        switch (kind)
        {
            case "item_quantity":
            case "resource_quantity":
            {
                var ownerRole = requirement["ownerRole"]!.GetValue<string>();
                ownerKind = ownerRole == "provider"
                    ? selectedProviderKind
                    : selectedTargetKind;
                ownerId = ownerRole == "provider"
                    ? selectedProviderId
                    : selectedTargetId;
                requestedQuantity = requirement["quantity"]!.GetValue<int>();
                break;
            }
            case "skill_tier":
            case "source_capability":
            {
                var actorRole = requirement["actorRole"]!.GetValue<string>();
                ownerKind = actorRole == "provider"
                    ? selectedProviderKind
                    : selectedTargetKind;
                ownerId = actorRole == "provider"
                    ? selectedProviderId
                    : selectedTargetId;
                if (kind == "skill_tier")
                {
                    minimumTier = requirement["minimumTier"]!.GetValue<int>();
                    currentTier = 3;
                }
                break;
            }
            case "provider":
                providerKind = selectedProviderKind;
                providerId = selectedProviderId;
                locationId = selectedLocationId;
                break;
            case "consent":
                providerKind = selectedProviderKind;
                providerId = selectedProviderId;
                targetKind = selectedTargetKind;
                targetId = selectedTargetId;
                currentState = "granted";
                break;
            case "facility":
                locationId = selectedLocationId;
                break;
            case "location":
                targetKind = selectedTargetKind;
                targetId = selectedTargetId;
                locationId = selectedLocationId;
                break;
            case "quest_state":
                currentState = requirement["requiredState"]!.GetValue<string>();
                break;
            case "effect_state":
                targetKind = selectedTargetKind;
                targetId = selectedTargetId;
                currentState = requirement["requiredState"]!.GetValue<string>();
                break;
            case "environment":
                locationId = selectedLocationId;
                currentState = requirement["requiredState"]!.GetValue<string>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        Assert.Equal(ownerKind, actual.OwnerKind);
        Assert.Equal(ownerId, actual.OwnerId);
        Assert.Equal(providerKind, actual.ProviderKind);
        Assert.Equal(providerId, actual.ProviderId);
        Assert.Equal(targetKind, actual.TargetKind);
        Assert.Equal(targetId, actual.TargetId);
        Assert.Equal(locationId, actual.LocationId);
        Assert.Equal(requestedQuantity, actual.RequestedQuantity);
        Assert.Equal(minimumTier, actual.MinimumTier);
        Assert.Equal(currentTier, actual.CurrentTier);
        Assert.Equal(currentState, actual.CurrentState);
    }

    private static void AssertFailure(
        ResolutionView result,
        string expectedCode,
        int? requirementIndex,
        string? field = null,
        string? expectedPath = null)
    {
        Assert.False(result.Success);
        Assert.True(
            ResourceMaterializationContract.IsAuthorityFingerprint(
                result.AuthorityFingerprint));
        Assert.True(requirementIndex is not null || expectedPath is not null);
        var requirementBase = requirementIndex is null
            ? null
            : $"{RequirementPath}[{requirementIndex.Value}]";
        var exactExpectedPath = expectedPath ??
            (field is null ? requirementBase : requirementBase + "." + field);
        Assert.NotNull(exactExpectedPath);
        Assert.Contains(result.Issues, issue =>
            issue.Code == expectedCode &&
            string.Equals(
                issue.FilePath,
                exactExpectedPath,
                StringComparison.Ordinal));
    }

    private static WoundTreatmentRoute CreateRoute(params JsonObject[] requirements)
    {
        var parsed = WoundMaterializationContract.Parse(
            WoundContractTestData.CreateActiveWound().ToJsonString(),
            "wound");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var route = Assert.Single(parsed.Wound!.Treatment.Routes);
        return route with
        {
            Requirements = requirements.Select(ToElement).ToArray()
        };
    }

    private static JsonObject CreateWoundWithRequirements(JsonObject[] requirements)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["treatment"]!["routes"]![0]!["requirements"] = new JsonArray(
            requirements.Select(static requirement => requirement.DeepClone()).ToArray());
        var skillRequirementIndex = Array.FindIndex(
            requirements,
            static requirement => string.Equals(
                requirement["kind"]?.GetValue<string>(),
                "skill_tier",
                StringComparison.Ordinal));
        Assert.True(skillRequirementIndex >= 0);
        wound["treatment"]!["routes"]![0]!["resolution"]!["modifierSource"]![
            "requirementIndex"] = skillRequirementIndex;
        return wound;
    }

    private static JsonElement ToElement(JsonObject value)
    {
        using var document = JsonDocument.Parse(value.ToJsonString());
        return document.RootElement.Clone();
    }

    private static string DuplicateFirstKnownProperty(
        JsonObject root,
        JsonObject target,
        out string field)
    {
        var property = target.First();
        field = property.Key;
        var propertyJson = JsonSerializer.Serialize(property.Key) + ":" +
                           (property.Value?.ToJsonString() ?? "null");
        var targetJson = target.ToJsonString();
        Assert.StartsWith("{" + propertyJson, targetJson, StringComparison.Ordinal);
        var duplicatedTarget = "{" + propertyJson + "," + targetJson[1..];
        var rootJson = root.ToJsonString();
        var targetIndex = rootJson.IndexOf(targetJson, StringComparison.Ordinal);
        Assert.True(targetIndex >= 0, $"Nested target was not found in raw root: {targetJson}");
        return rootJson[..targetIndex] + duplicatedTarget +
               rootJson[(targetIndex + targetJson.Length)..];
    }

    private static JsonObject[] CreateCompleteRequirements() =>
        Kinds.Select(CreateRequirement).ToArray();

    private static JsonObject Requirement(
        IEnumerable<JsonObject> requirements,
        string kind) =>
        Assert.Single(
            requirements,
            requirement =>
                requirement["kind"]!.GetValue<string>() == kind);

    private static JsonObject CreateRequirement(string kind) => kind switch
    {
        "item_quantity" => new JsonObject
        {
            ["kind"] = kind,
            ["itemRef"] = ItemRef,
            ["quantity"] = 2,
            ["ownerRole"] = "provider"
        },
        "resource_quantity" => new JsonObject
        {
            ["kind"] = kind,
            ["resourceRef"] = ResourceRef,
            ["quantity"] = 3,
            ["ownerRole"] = "provider"
        },
        "skill_tier" => new JsonObject
        {
            ["kind"] = kind,
            ["capabilityRef"] = SkillRef,
            ["minimumTier"] = 2,
            ["actorRole"] = "provider"
        },
        "source_capability" => new JsonObject
        {
            ["kind"] = kind,
            ["capabilityRef"] = CapabilityRef,
            ["actorRole"] = "provider"
        },
        "provider" => new JsonObject
        {
            ["kind"] = kind,
            ["providerRef"] = ProviderRef
        },
        "consent" => new JsonObject
        {
            ["kind"] = kind,
            ["consentRef"] = ConsentRef,
            ["providerRef"] = ProviderRef,
            ["targetRef"] = TargetRef
        },
        "facility" => new JsonObject
        {
            ["kind"] = kind,
            ["facilityRef"] = FacilityRef
        },
        "location" => new JsonObject
        {
            ["kind"] = kind,
            ["locationRef"] = LocationRef,
            ["targetRole"] = "target"
        },
        "quest_state" => new JsonObject
        {
            ["kind"] = kind,
            ["questRef"] = QuestRef,
            ["requiredState"] = "active"
        },
        "effect_state" => new JsonObject
        {
            ["kind"] = kind,
            ["effectRef"] = EffectRef,
            ["requiredState"] = "controlled",
            ["targetRole"] = "target"
        },
        "environment" => new JsonObject
        {
            ["kind"] = kind,
            ["environmentRef"] = EnvironmentRef,
            ["requiredState"] = "clean"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static JsonObject CreateContext() => new()
    {
        ["schemaVersion"] = 1,
        ["realm"] = "mortal_world",
        ["targetKind"] = "player",
        ["targetId"] = TargetRef,
        ["providerKind"] = "npc",
        ["providerId"] = ProviderRef,
        ["currentLocationId"] = LocationRef
    };

    private static JsonObject CreateActorCoordinate(string actorKind, string actorId) => new()
    {
        ["actorKind"] = actorKind,
        ["actorId"] = actorId
    };

    private static JsonObject CreateSnapshot() => new()
    {
        ["schemaVersion"] = 1,
        ["snapshotToken"] = "snapshot_mortal_wound_authority_001",
        ["items"] = new JsonArray(new JsonObject
        {
            ["itemId"] = ItemRef,
            ["displayName"] = DisplayName("item_quantity"),
            ["realm"] = "mortal_world",
            ["ownerKind"] = "npc",
            ["ownerId"] = ProviderRef,
            ["count"] = 4,
            ["availableCount"] = 4,
            ["reservationState"] = "available",
            ["lifecycle"] = "active",
            ["active"] = true
        }),
        ["resources"] = new JsonArray(new JsonObject
        {
            ["resourceRef"] = ResourceRef,
            ["displayName"] = DisplayName("resource_quantity"),
            ["realm"] = "mortal_world",
            ["ownerKind"] = "npc",
            ["ownerId"] = ProviderRef,
            ["currentValue"] = 8,
            ["availableValue"] = 8,
            ["reservationState"] = "available",
            ["lifecycle"] = "active",
            ["active"] = true
        }),
        ["actors"] = new JsonArray(
            new JsonObject
            {
                ["actorKind"] = "npc",
                ["actorId"] = ProviderRef,
                ["displayName"] = DisplayName("provider"),
                ["realm"] = "mortal_world",
                ["currentLocationId"] = LocationRef,
                ["lifecycle"] = "active",
                ["active"] = true,
                ["reachable"] = true,
                ["skills"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = SkillRef,
                    ["capabilityRef"] = SkillRef,
                    ["displayName"] = DisplayName("skill_tier"),
                    ["tier"] = 3,
                    ["lifecycle"] = "active",
                    ["active"] = true
                }),
                ["capabilities"] = new JsonArray(new JsonObject
                {
                    ["capabilityRef"] = CapabilityRef,
                    ["displayName"] = DisplayName("source_capability"),
                    ["lifecycle"] = "active",
                    ["active"] = true
                }),
                ["consents"] = new JsonArray(new JsonObject
                {
                    ["consentRef"] = ConsentRef,
                    ["displayName"] = DisplayName("consent"),
                    ["providerKind"] = "npc",
                    ["providerId"] = ProviderRef,
                    ["targetKind"] = "player",
                    ["targetId"] = TargetRef,
                    ["status"] = "granted",
                    ["lifecycle"] = "active",
                    ["active"] = true
                })
            },
            new JsonObject
            {
                ["actorKind"] = "player",
                ["actorId"] = TargetRef,
                ["displayName"] = "Игрок",
                ["realm"] = "mortal_world",
                ["currentLocationId"] = LocationRef,
                ["lifecycle"] = "active",
                ["active"] = true,
                ["reachable"] = true,
                ["skills"] = new JsonArray(),
                ["capabilities"] = new JsonArray(),
                ["consents"] = new JsonArray()
            }),
        ["facilities"] = new JsonArray(new JsonObject
        {
            ["facilityId"] = FacilityRef,
            ["displayName"] = DisplayName("facility"),
            ["realm"] = "mortal_world",
            ["locationId"] = LocationRef,
            ["lifecycle"] = "active",
            ["active"] = true,
            ["available"] = true
        }),
        ["locations"] = new JsonArray(new JsonObject
        {
            ["locationId"] = LocationRef,
            ["displayName"] = DisplayName("location"),
            ["realm"] = "mortal_world",
            ["lifecycle"] = "active",
            ["active"] = true,
            ["presentActors"] = new JsonArray(
                CreateActorCoordinate("player", TargetRef),
                CreateActorCoordinate("npc", ProviderRef))
        }),
        ["quests"] = new JsonArray(new JsonObject
        {
            ["questId"] = QuestRef,
            ["displayName"] = DisplayName("quest_state"),
            ["realm"] = "mortal_world",
            ["state"] = "active",
            ["lifecycle"] = "active",
            ["active"] = true
        }),
        ["effects"] = new JsonArray(new JsonObject
        {
            ["effectId"] = EffectRef,
            ["displayName"] = DisplayName("effect_state"),
            ["realm"] = "mortal_world",
            ["targetKind"] = "player",
            ["targetId"] = TargetRef,
            ["state"] = "controlled",
            ["lifecycle"] = "active",
            ["active"] = true
        }),
        ["environments"] = new JsonArray(new JsonObject
        {
            ["environmentId"] = EnvironmentRef,
            ["displayName"] = DisplayName("environment"),
            ["realm"] = "mortal_world",
            ["locationId"] = LocationRef,
            ["state"] = "clean",
            ["lifecycle"] = "active",
            ["active"] = true
        })
    };

    private static void MakeRequirementUnsatisfied(JsonObject snapshot, string kind)
    {
        switch (kind)
        {
            case "item_quantity":
                First(snapshot, "items")["availableCount"] = 0;
                break;
            case "resource_quantity":
                First(snapshot, "resources")["availableValue"] = 0;
                break;
            case "skill_tier":
                First(Provider(snapshot), "skills")["tier"] = 1;
                break;
            case "source_capability":
                First(Provider(snapshot), "capabilities")["active"] = false;
                break;
            case "provider":
                Provider(snapshot)["active"] = false;
                break;
            case "consent":
                First(Provider(snapshot), "consents")["status"] = "withdrawn";
                break;
            case "facility":
                First(snapshot, "facilities")["available"] = false;
                break;
            case "location":
                First(snapshot, "locations")["presentActors"] =
                    new JsonArray(CreateActorCoordinate("npc", ProviderRef));
                break;
            case "quest_state":
                First(snapshot, "quests")["state"] = "failed";
                break;
            case "effect_state":
                First(snapshot, "effects")["state"] = "expired";
                break;
            case "environment":
                First(snapshot, "environments")["state"] = "contaminated";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private static void MoveRoleBoundAuthorityToTarget(JsonObject snapshot, string kind)
    {
        switch (kind)
        {
            case "item_quantity":
                First(snapshot, "items")["ownerKind"] = "player";
                First(snapshot, "items")["ownerId"] = TargetRef;
                break;
            case "resource_quantity":
                First(snapshot, "resources")["ownerKind"] = "player";
                First(snapshot, "resources")["ownerId"] = TargetRef;
                break;
            case "skill_tier":
            {
                var skill = First(Provider(snapshot), "skills").DeepClone();
                Provider(snapshot)["skills"] = new JsonArray();
                Target(snapshot)["skills"]!.AsArray().Add(skill);
                break;
            }
            case "source_capability":
            {
                var capability = First(Provider(snapshot), "capabilities").DeepClone();
                Provider(snapshot)["capabilities"] = new JsonArray();
                Target(snapshot)["capabilities"]!.AsArray().Add(capability);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private static void RewriteDisplayNames(JsonNode node, string replacement)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (string.Equals(property.Key, "displayName", StringComparison.Ordinal))
                    obj[property.Key] = replacement;
                else if (property.Value is not null)
                    RewriteDisplayNames(property.Value, replacement);
            }

            return;
        }

        if (node is not JsonArray array)
            return;
        foreach (var child in array)
        {
            if (child is not null)
                RewriteDisplayNames(child, replacement);
        }
    }

    private static string UnsatisfiedIssueCode(string kind) => kind switch
    {
        "item_quantity" => "mortal_wound_requirement_item_quantity_insufficient",
        "resource_quantity" => "mortal_wound_requirement_resource_quantity_insufficient",
        "skill_tier" => "mortal_wound_requirement_skill_tier_insufficient",
        "source_capability" => "mortal_wound_requirement_capability_inactive",
        "provider" => "mortal_wound_requirement_provider_inactive",
        "consent" => "mortal_wound_requirement_consent_missing",
        "facility" => "mortal_wound_requirement_facility_unavailable",
        "location" => "mortal_wound_requirement_target_not_present",
        "quest_state" or "effect_state" or "environment" =>
            "mortal_wound_requirement_state_mismatch",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static void SetLifecycle(JsonObject snapshot, string kind, string lifecycle)
    {
        AuthorityEntry(snapshot, kind)["lifecycle"] = lifecycle;
    }

    private static void SetRealm(JsonObject snapshot, string kind, string realm)
    {
        switch (kind)
        {
            case "skill_tier":
            case "source_capability":
            case "consent":
            case "provider":
                Provider(snapshot)["realm"] = realm;
                break;
            default:
                AuthorityEntry(snapshot, kind)["realm"] = realm;
                break;
        }
    }

    private static void DuplicateAuthorityEntry(JsonObject snapshot, string kind)
    {
        switch (kind)
        {
            case "skill_tier":
                Provider(snapshot)["skills"]!.AsArray().Add(
                    AuthorityEntry(snapshot, kind).DeepClone());
                break;
            case "source_capability":
                Provider(snapshot)["capabilities"]!.AsArray().Add(
                    AuthorityEntry(snapshot, kind).DeepClone());
                break;
            case "consent":
                Provider(snapshot)["consents"]!.AsArray().Add(
                    AuthorityEntry(snapshot, kind).DeepClone());
                break;
            case "provider":
                snapshot["actors"]!.AsArray().Add(
                    AuthorityEntry(snapshot, kind).DeepClone());
                break;
            default:
            {
                var arrayName = kind switch
                {
                    "item_quantity" => "items",
                    "resource_quantity" => "resources",
                    "facility" => "facilities",
                    "location" => "locations",
                    "quest_state" => "quests",
                    "effect_state" => "effects",
                    "environment" => "environments",
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
                };
                snapshot[arrayName]!.AsArray().Add(
                    AuthorityEntry(snapshot, kind).DeepClone());
                break;
            }
        }
    }

    private static JsonObject AuthorityEntry(JsonObject snapshot, string kind) => kind switch
    {
        "item_quantity" => First(snapshot, "items"),
        "resource_quantity" => First(snapshot, "resources"),
        "skill_tier" => First(Provider(snapshot), "skills"),
        "source_capability" => First(Provider(snapshot), "capabilities"),
        "provider" => Provider(snapshot),
        "consent" => First(Provider(snapshot), "consents"),
        "facility" => First(snapshot, "facilities"),
        "location" => First(snapshot, "locations"),
        "quest_state" => First(snapshot, "quests"),
        "effect_state" => First(snapshot, "effects"),
        "environment" => First(snapshot, "environments"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static JsonObject SnapshotClosedObject(
        JsonObject snapshot,
        string objectShape) => objectShape switch
        {
            "item" => First(snapshot, "items"),
            "resource" => First(snapshot, "resources"),
            "actor" => Provider(snapshot),
            "skill" => First(Provider(snapshot), "skills"),
            "capability" => First(Provider(snapshot), "capabilities"),
            "consent" => First(Provider(snapshot), "consents"),
            "facility" => First(snapshot, "facilities"),
            "location" => First(snapshot, "locations"),
            "present_actor" => Assert.IsType<JsonObject>(
                First(snapshot, "locations")["presentActors"]![0]),
            "quest" => First(snapshot, "quests"),
            "effect" => First(snapshot, "effects"),
            "environment" => First(snapshot, "environments"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(objectShape),
                objectShape,
                null)
        };

    private static JsonObject First(JsonObject root, string arrayName) =>
        Assert.IsType<JsonObject>(Assert.Single(root[arrayName]!.AsArray()));

    private static JsonObject Provider(JsonObject snapshot) =>
        Assert.IsType<JsonObject>(snapshot["actors"]![0]);

    private static JsonObject Target(JsonObject snapshot) =>
        Assert.IsType<JsonObject>(snapshot["actors"]![1]);

    private static string ReferenceField(string kind) => kind switch
    {
        "item_quantity" => "itemRef",
        "resource_quantity" => "resourceRef",
        "skill_tier" or "source_capability" => "capabilityRef",
        "provider" => "providerRef",
        "consent" => "consentRef",
        "facility" => "facilityRef",
        "location" => "locationRef",
        "quest_state" => "questRef",
        "effect_state" => "effectRef",
        "environment" => "environmentRef",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string ExactReference(string kind) => kind switch
    {
        "item_quantity" => ItemRef,
        "resource_quantity" => ResourceRef,
        "skill_tier" => SkillRef,
        "source_capability" => CapabilityRef,
        "provider" => ProviderRef,
        "consent" => ConsentRef,
        "facility" => FacilityRef,
        "location" => LocationRef,
        "quest_state" => QuestRef,
        "effect_state" => EffectRef,
        "environment" => EnvironmentRef,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string DisplayName(string kind) => kind switch
    {
        "item_quantity" => "Стерильная нить",
        "resource_quantity" => "Медицинские припасы",
        "skill_tier" => "Полевая медицина",
        "source_capability" => "Наложение швов",
        "provider" => "Доктор Лин",
        "consent" => "Согласие доктора Лин",
        "facility" => "Чистый операционный стол",
        "location" => "Полевая лечебница",
        "quest_state" => "Открыть лечебницу",
        "effect_state" => "Инфекция подавлена",
        "environment" => "Чистый воздух",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string AddCyrillicConfusable(string value)
    {
        foreach (var pair in new[]
                 {
                     ('a', '\u0430'),
                     ('c', '\u0441'),
                     ('e', '\u0435'),
                     ('o', '\u043e'),
                     ('p', '\u0440'),
                     ('x', '\u0445'),
                     ('y', '\u0443')
                 })
        {
            var index = value.IndexOf(pair.Item1);
            if (index >= 0)
                return value[..index] + pair.Item2 + value[(index + 1)..];
        }

        throw new InvalidOperationException($"No confusable ASCII character in '{value}'.");
    }

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(static issue =>
                $"{issue.Code} {issue.FilePath}: {issue.Expected} / {issue.Actual}"));

    private sealed record ResolutionView(
        bool Success,
        IReadOnlyList<ValidationIssue> Issues,
        IReadOnlyList<ResolvedRequirementView> ResolvedRequirements,
        string AuthorityFingerprint);

    private sealed record AuthorityParseView(
        bool IsValid,
        IReadOnlyList<ValidationIssue> Issues,
        object? Value);

    private sealed record ResolvedRequirementView(
        int RequirementIndex,
        string Kind,
        string AuthorityRef,
        string? SkillId,
        string Realm,
        string? OwnerKind,
        string? OwnerId,
        string? ProviderKind,
        string? ProviderId,
        string? TargetKind,
        string? TargetId,
        string? LocationId,
        int? RequestedQuantity,
        int? MinimumTier,
        int? CurrentTier,
        string? CurrentState,
        string AuthorityFingerprint);
}
