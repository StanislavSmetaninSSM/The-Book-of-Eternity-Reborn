using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PendingTurnSnapshotPresenceContractTests
{
    private const string PropertyName = "OriginalPathPresenceV1";

    private static readonly string[] ExpectedPaths =
    [
        "game_state/meta/soul_state.json",
        "game_state/resources/resource_definitions.json",
        "game_state/resources/resource_state.json",
        "game_state/resources/resource_history.json",
        "game_state/resources/resource_owner_authority.json",
        "game_state/meta/afterlife_spiritual_conflict_state.json",
        "game_state/meta/afterlife_entity_profiles.json",
        "game_state/meta/shining_abode_state.json",
        "game_state/core/game_settings.json",
        "game_state/effects/effect_identity_index.json",
        "game_state/wounds/wound_identity_index.json",
        "game_state/wounds/wound_history.json",
        "game_state/player/wounds.json",
        "game_state/npcs/npc_wounds.json",
        "game_state/combat/enemies.json",
        "game_state/combat/allies.json"
    ];

    [Fact]
    public void ClosedObservationContract_HasExactImmutableSixteenPaths()
    {
        var helper = ClientAssembly.GetType(
            "BookOfEternityClient.Services.PendingTurnSnapshotPathPresenceV1");
        Assert.NotNull(helper);
        var logicalPaths = Assert.IsAssignableFrom<IReadOnlyList<string>>(
            helper!.GetProperty(
                "LogicalPaths",
                BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));

        Assert.Equal(ExpectedPaths, logicalPaths);
        Assert.Throws<NotSupportedException>(
            () => ((IList)logicalPaths).Add("game_state/forged.json"));
    }

    [Fact]
    public void TypedManifestShapes_PopulatedRoundTripsAlignPayloadHashesAndNullOmission()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        string? expectedJson = null;
        string? expectedHash = null;
        foreach (var type in ManifestTypes())
        {
            var property = type.GetProperty(PropertyName);
            Assert.NotNull(property);
            Assert.Equal(typeof(Dictionary<string, bool>), property!.PropertyType);

            var names = type.GetProperties()
                .OrderBy(static candidate => candidate.MetadataToken)
                .Select(static candidate => candidate.Name)
                .ToArray();
            Assert.Equal(
                Array.IndexOf(names, "SnapshotFileHashes") + 1,
                Array.IndexOf(names, PropertyName));

            var instance = Activator.CreateInstance(type, nonPublic: true)!;
            var oldJson = JsonSerializer.Serialize(instance, type, options);
            Assert.DoesNotContain("originalPathPresenceV1", oldJson, StringComparison.Ordinal);

            PopulateManifest(instance, type);
            var json = JsonSerializer.Serialize(instance, type, options);
            Assert.Contains("\"originalPathPresenceV1\"", json, StringComparison.Ordinal);
            var roundTripped = JsonSerializer.Deserialize(json, type, options);
            Assert.NotNull(roundTripped);
            Assert.Equal(json, JsonSerializer.Serialize(roundTripped, type, options));
            expectedJson ??= json;
            Assert.Equal(expectedJson, json);
            var hash = PendingTurnSnapshotAuthority.ComputeSha256(
                Encoding.UTF8.GetBytes(json));
            expectedHash ??= hash;
            Assert.Equal(expectedHash, hash);
        }
    }

    [Fact]
    public void Create_DerivesExactPresenceAndRejectsUntrustworthyCoverage()
    {
        var create = PresenceHelperType.GetMethod(
            "Create",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExpectedPaths[0]] =
                "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json"
        };
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExpectedPaths[0]] = new string('A', 64)
        };
        var map = Assert.IsType<Dictionary<string, bool>>(
            create.Invoke(null, [files, hashes]));

        Assert.Equal(ExpectedPaths, map.Keys);
        Assert.True(map[ExpectedPaths[0]]);
        Assert.All(ExpectedPaths.Skip(1), path => Assert.False(map[path]));

        hashes.Clear();
        var asymmetric = Assert.Throws<TargetInvocationException>(
            () => create.Invoke(null, [files, hashes]));
        Assert.IsType<InvalidDataException>(asymmetric.InnerException);

        files.Clear();
        hashes.Clear();
        var caseVariant = ExpectedPaths[0].ToUpperInvariant();
        files[caseVariant] = "snapshot";
        hashes[caseVariant] = new string('B', 64);
        var confusable = Assert.Throws<TargetInvocationException>(
            () => create.Invoke(null, [files, hashes]));
        Assert.IsType<InvalidDataException>(confusable.InnerException);

        files.Clear();
        hashes.Clear();
        files[ExpectedPaths[0]] = "snapshot";
        hashes[ExpectedPaths[0]] = " ";
        var blankHash = Assert.Throws<TargetInvocationException>(
            () => create.Invoke(null, [files, hashes]));
        Assert.IsType<InvalidDataException>(blankHash.InnerException);
    }

    [Fact]
    public void ReaderPresenceAgreement_FalseRequiresBothCoverageRowsAbsent()
    {
        var manifestType = ClientAssembly.GetType(
            "BookOfEternityClient.Services.LiveTurnPendingSnapshotManifest")!;
        var manifest = Activator.CreateInstance(manifestType, nonPublic: true)!;
        var presence = ExpectedPaths.ToDictionary(
            static path => path,
            static _ => false,
            StringComparer.Ordinal);
        var presenceProperty = manifestType.GetProperty(PropertyName);
        Assert.NotNull(presenceProperty);
        presenceProperty!.SetValue(manifest, presence);
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExpectedPaths[9]] = "snapshot/settings.json"
        };
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        manifestType.GetProperty("Files")!.SetValue(manifest, files);
        manifestType.GetProperty("SnapshotFileHashes")!.SetValue(manifest, hashes);
        var validate = ClientAssembly.GetType(
                "BookOfEternityClient.Services.PendingTurnSnapshotReader")!
            .GetMethod(
                "ValidateOriginalPathPresenceV1",
                BindingFlags.Static | BindingFlags.NonPublic)!;
        var issues = new List<ValidationIssue>();

        validate.Invoke(null, [manifest, issues]);
        Assert.Contains(
            issues,
            static issue => issue.Code == "pending_turn_snapshot_reader_presence_invalid");

        issues.Clear();
        files.Clear();
        hashes[ExpectedPaths[9]] = new string('C', 64);
        validate.Invoke(null, [manifest, issues]);
        Assert.Contains(
            issues,
            static issue => issue.Code == "pending_turn_snapshot_reader_presence_invalid");
    }

    [Fact]
    public void SelectionFactories_PreserveLegacyAndSixtyFourPathBoundContract()
    {
        var selectionType = ClientAssembly.GetType(
            "BookOfEternityClient.Services.PendingTurnSnapshotPathSelection");
        Assert.NotNull(selectionType);
        var constructor = selectionType!.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(IEnumerable<string>), typeof(IEnumerable<string>)],
            modifiers: null);
        var factory = selectionType.GetMethod(
            "CreateWithObservedOptionalPaths",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(constructor);
        Assert.NotNull(factory);

        var legacy = constructor!.Invoke([new[] { ExpectedPaths[0] }, new[] { ExpectedPaths[9] }]);
        var observed = factory!.Invoke(null, [new[] { ExpectedPaths[0] }, new[] { ExpectedPaths[9] }])!;
        var flag = selectionType.GetProperty(
            "RequireSignedAbsenceForMissingOptionalPaths",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.False((bool)flag.GetValue(legacy)!);
        Assert.True((bool)flag.GetValue(observed)!);
        Assert.Equal(2, ((IReadOnlyCollection<string>)observed).Count);
        var authorityType = ClientAssembly.GetType(
            "BookOfEternityClient.Services.PendingTurnSnapshotReadAuthority")!;
        var absentLogicalPaths = authorityType.GetProperty(
            "AbsentLogicalPaths",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(absentLogicalPaths);
        Assert.Equal(
            typeof(IReadOnlyList<string>),
            absentLogicalPaths!.PropertyType);
    }

    private static Assembly ClientAssembly => typeof(FileSystemManager).Assembly;

    private static Type PresenceHelperType
    {
        get
        {
            var type = ClientAssembly.GetType(
                "BookOfEternityClient.Services.PendingTurnSnapshotPathPresenceV1");
            Assert.NotNull(type);
            return type!;
        }
    }

    private static void PopulateManifest(object instance, Type type)
    {
        Set("SessionId", "presence-session");
        Set("RequestId", "presence-request");
        Set("TurnNumber", 42);
        Set("RequestTimestamp", "2026-09-08T00:00:00Z");
        Set("PlayerAction", "Observe source presence.");
        Set("PreGeneratedDices1d20", new[] { 7, 11 });
        Set("GachaBaseResult", new JsonObject { ["baseRarity"] = "rare" });
        Set("ProgressionControl", new ProgressionControl { CurrentRealm = "Chaos Sea" });
        Set("Files", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExpectedPaths[0]] =
                "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json"
        });
        Set("SnapshotFileHashes", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExpectedPaths[0]] = new string('A', 64)
        });
        Set(
            PropertyName,
            ExpectedPaths.ToDictionary(
                static path => path,
                static path => path == ExpectedPaths[0],
                StringComparer.Ordinal));
        Set("ClientOwnedValidationHashes", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["game_state/control/client-owned.json"] = new string('B', 64)
        });
        Set("RollbackBackups", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExpectedPaths[0]] = "game_state/meta/soul_state.json.rollback.presence"
        });
        Set("RollbackBaselineFiles", new List<string> { ExpectedPaths[0] });
        Set("SourceLabel", "presence-contract");
        Set("ManifestPayloadHash", string.Empty);
        return;

        void Set(string propertyName, object value) =>
            type.GetProperty(propertyName)!.SetValue(instance, value);
    }

    private static IEnumerable<Type> ManifestTypes()
    {
        yield return typeof(GameEngine).GetNestedType(
            "PendingTurnSnapshotManifest",
            BindingFlags.NonPublic)!;
        yield return ClientAssembly.GetType(
            "BookOfEternityClient.Services.LiveTurnPendingSnapshotManifest")!;
        yield return typeof(BrowserAfterlifeTurnRequestQueue).GetNestedType(
            "BrowserPendingTurnSnapshotManifest",
            BindingFlags.NonPublic)!;
        yield return typeof(ValidationService).GetNestedType(
            "ValidationPendingTurnSnapshotManifest",
            BindingFlags.NonPublic)!;
        yield return typeof(GuardianPowerEventState).GetNestedType(
            "PendingTurnSnapshotManifest",
            BindingFlags.NonPublic)!;
        yield return typeof(CanonicalStateNormalizer).GetNestedType(
            "PendingTurnSnapshotAuthorityManifest",
            BindingFlags.NonPublic)!;
    }
}
