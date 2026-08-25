using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal enum ResourceProjectionAudience
{
    Player,
    GameMaster
}

internal sealed record ResourceProjectionOwnerScope(
    ResourceOwnerKey Owner,
    string SafeOwnerSelector,
    bool IsOwningPlayer);

internal sealed record ResourceProjectionRequest(
    ResourceProjectionInput Snapshot,
    IReadOnlyList<ResourceProjectionOwnerScope> OwnerScopes);

internal sealed record ResourceProjectionRow(
    string SafeOwnerSelector,
    string ResourceKey,
    string DisplayName,
    string Unit,
    decimal Current,
    decimal Maximum,
    decimal? Percentage,
    ResourceLifecycleState State,
    decimal? RecentVisibleDelta,
    IReadOnlyList<string> AvailableOperations,
    ResourceVisibility Visibility);

internal sealed record ResourceProjectionResult(
    bool IsAvailable,
    string? UnavailableMessage,
    IReadOnlyList<ResourceProjectionRow> Rows);

internal static class ResourceProjectionService
{
    private static readonly IReadOnlyDictionary<string, string> LocalizedUnits =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["percent_point"] = "%",
                ["point"] = "ед.",
                ["charge"] = "зар.",
                ["round"] = "шт.",
                ["attempt"] = "попытка",
                ["reroll"] = "переброс"
            });

    private static readonly string[] ProtectedSelectorFragments =
    [
        "sha256:",
        "game_state",
        ".json",
        "transition",
        "operation",
        "eventref",
        "receipt",
        "fingerprint",
        "validation",
        "repair",
        "rollback",
        "agent"
    ];

    internal static ResourceProjectionResult Project(
        ResourceProjectionRequest request,
        ResourceProjectionAudience audience)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Snapshot == null || request.OwnerScopes == null)
        {
            return Unavailable();
        }

        var parsed = ParseAcceptedSnapshot(request.Snapshot);
        if (parsed == null)
            return Unavailable();

        return ProjectParsed(parsed, request.OwnerScopes, audience);
    }

    private static ResourceProjectionResult ProjectParsed(
        ParsedProjectionSnapshot parsed,
        IReadOnlyList<ResourceProjectionOwnerScope> ownerScopes,
        ResourceProjectionAudience audience)
    {

        var scopes = new Dictionary<ResourceOwnerKey, ResourceProjectionOwnerScope>();
        foreach (var scope in ownerScopes)
        {
            if (scope?.Owner == null ||
                !IsSafeSelector(scope.SafeOwnerSelector, scope.Owner.ResourceOwnerId) ||
                !scopes.TryAdd(scope.Owner, scope))
            {
                return Unavailable();
            }
        }

        var rows = new List<ResourceProjectionRow>();
        foreach (var entry in parsed.State.Entries)
        {
            var owner = new ResourceOwnerKey(
                entry.Coordinate.Realm,
                entry.Coordinate.OwnerKind,
                entry.Coordinate.ResourceOwnerId);
            if (!scopes.TryGetValue(owner, out var scope))
                continue;
            if (!parsed.Definitions.TryResolveExact(entry.Coordinate.ResourceKey, out var definition) ||
                definition == null)
            {
                return Unavailable();
            }

            if (!IsSafeProjectedText(
                    definition.DisplayName,
                    entry.Coordinate.ResourceOwnerId,
                    maximumLength: 160))
            {
                return Unavailable();
            }

            if (!IsVisible(definition.Visibility, audience, scope.IsOwningPlayer))
                continue;

            if (!TryCalculatePercentage(entry, definition, out var percentage))
                return Unavailable();

            rows.Add(new ResourceProjectionRow(
                scope.SafeOwnerSelector,
                entry.Coordinate.ResourceKey,
                definition.DisplayName,
                LocalizeUnit(definition.Unit),
                entry.Current,
                entry.Maximum,
                percentage,
                entry.State,
                FindRecentVisibleDelta(parsed.History, entry.Coordinate),
                BuildAvailableOperations(entry, definition),
                definition.Visibility));
        }

        return new ResourceProjectionResult(
            IsAvailable: true,
            UnavailableMessage: null,
            Rows: new ReadOnlyCollection<ResourceProjectionRow>(rows
                .OrderBy(static row => row.SafeOwnerSelector, StringComparer.Ordinal)
                .ThenBy(static row => row.ResourceKey, StringComparer.Ordinal)
                .ToArray()));
    }

    internal static ResourceProjectionResult ProjectCanonical(
        string? definitionsJson,
        string? stateJson,
        string? historyJson,
        IReadOnlyList<ResourceProjectionOwnerScope> ownerScopes,
        ResourceProjectionAudience audience)
    {
        var parsed = ParseAcceptedSnapshot(definitionsJson, stateJson, historyJson);
        if (parsed == null)
            return Unavailable();
        return ProjectParsed(parsed, ownerScopes, audience);
    }

    internal static async Task<ResourceProjectionResult> ProjectCanonicalAsync(
        FileSystemManager fs,
        IReadOnlyList<ResourceProjectionOwnerScope> ownerScopes,
        ResourceProjectionAudience audience)
    {
        ArgumentNullException.ThrowIfNull(fs);
        await using var readLease = await fs.AcquireCanonicalWriteLeaseAsync();
        return ProjectCanonical(
            await fs.ReadFileAsync(readLease, ResourceMaterializationContract.DefinitionsPath),
            await fs.ReadFileAsync(readLease, ResourceMaterializationContract.StatePath),
            await fs.ReadFileAsync(readLease, ResourceMaterializationContract.HistoryPath),
            ownerScopes,
            audience);
    }

    internal static async Task<ResourceProjectionResult> ProjectCanonicalAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease readLease,
        IReadOnlyList<ResourceProjectionOwnerScope> ownerScopes,
        ResourceProjectionAudience audience)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(readLease);
        return ProjectCanonical(
            await fs.ReadFileAsync(readLease, ResourceMaterializationContract.DefinitionsPath),
            await fs.ReadFileAsync(readLease, ResourceMaterializationContract.StatePath),
            await fs.ReadFileAsync(readLease, ResourceMaterializationContract.HistoryPath),
            ownerScopes,
            audience);
    }

    internal static Task<ResourceProjectionResult> ProjectOwnerAsync(
        FileSystemManager fs,
        string realm,
        ResourceOwnerKind ownerKind,
        string ownerId,
        string safeOwnerSelector,
        bool isOwningPlayer,
        ResourceProjectionAudience audience = ResourceProjectionAudience.Player) =>
        ProjectCanonicalAsync(
            fs,
            new[]
            {
                new ResourceProjectionOwnerScope(
                    new ResourceOwnerKey(realm, ownerKind, ownerId),
                    safeOwnerSelector,
                    isOwningPlayer)
            },
            audience);

    internal static Task<ResourceProjectionResult> ProjectAfterlifeConflictAsync(
        FileSystemManager fs,
        JsonObject? activeConflict,
        ResourceProjectionAudience audience = ResourceProjectionAudience.Player)
    {
        if (activeConflict == null)
        {
            return Task.FromResult(new ResourceProjectionResult(
                true,
                null,
                Array.Empty<ResourceProjectionRow>()));
        }

        var rawRealm = AfterlifeSpiritualConflictState.GetNodeString(activeConflict["realm"]);
        var oppositionOwnerId = AfterlifeSpiritualConflictState.GetNodeString(
            activeConflict[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]?
                ["opposition"]?["resourceOwnerId"]);
        if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(rawRealm, out var realm) ||
            !ResourceMaterializationContract.IsExactIdentifier(oppositionOwnerId))
        {
            return Task.FromResult(Unavailable());
        }

        return ProjectCanonicalAsync(
            fs,
            new[]
            {
                new ResourceProjectionOwnerScope(
                    new ResourceOwnerKey(realm, ResourceOwnerKind.AfterlifeActor, "player_soul"),
                    "душа игрока",
                    IsOwningPlayer: true),
                new ResourceProjectionOwnerScope(
                    new ResourceOwnerKey(
                        realm,
                        ResourceOwnerKind.AfterlifeConflictSide,
                        oppositionOwnerId!),
                    "противник",
                    IsOwningPlayer: false)
            },
            audience);
    }

    internal static async Task<ResourceProjectionResult> ProjectShiningGachaAsync(
        FileSystemManager fs,
        ResourceProjectionAudience audience = ResourceProjectionAudience.Player)
    {
        await using var readLease = await fs.AcquireCanonicalWriteLeaseAsync();
        var shiningRoot = ParseObjectOrNull(
            await fs.ReadFileAsync(readLease, ShiningAbodeState.StatePath));
        if (shiningRoot?["gachaSystem"] is not JsonObject gacha ||
            !TryReadExactIdentifier(gacha["currentReturnCycleId"], out var returnCycleId) ||
            shiningRoot["resourceOwnerBindings"]?["gachaReturn"] is not JsonObject binding ||
            !TryReadExactIdentifier(binding["resourceOwnerId"], out var ownerId) ||
            !TryReadExactIdentifier(binding["returnCycleId"], out var boundCycleId) ||
            !string.Equals(returnCycleId, boundCycleId, StringComparison.Ordinal))
        {
            return Unavailable();
        }

        return await ProjectCanonicalAsync(
            fs,
            readLease,
            new[]
            {
                new ResourceProjectionOwnerScope(
                    new ResourceOwnerKey(
                        "shining_abode",
                        ResourceOwnerKind.AfterlifeScope,
                        ownerId!),
                    "сияющая гача",
                    IsOwningPlayer: true)
            },
            audience);
    }

    internal static Task<ResourceProjectionResult> ProjectMemoryRerollsAsync(
        FileSystemManager fs,
        ResourceProjectionAudience audience = ResourceProjectionAudience.Player) =>
        ProjectBlessingAllocationAsync(fs, "memorySelection", audience);

    internal static Task<ResourceProjectionResult> ProjectRelicRerollsAsync(
        FileSystemManager fs,
        ResourceProjectionAudience audience = ResourceProjectionAudience.Player) =>
        ProjectBlessingAllocationAsync(fs, "relicRefinementEntitlements", audience);

    internal static Task<ResourceProjectionResult> ProjectRelicRerollsAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease readLease,
        JsonObject? soulRoot,
        ResourceProjectionAudience audience = ResourceProjectionAudience.Player) =>
        ProjectBlessingAllocationAsync(
            fs,
            readLease,
            soulRoot,
            "relicRefinementEntitlements",
            audience);

    private static async Task<ResourceProjectionResult> ProjectBlessingAllocationAsync(
        FileSystemManager fs,
        string entitlementProperty,
        ResourceProjectionAudience audience = ResourceProjectionAudience.Player)
    {
        await using var readLease = await fs.AcquireCanonicalWriteLeaseAsync();
        var soulRoot = ParseObjectOrNull(
            await fs.ReadFileAsync(readLease, "game_state/meta/soul_state.json"));
        return await ProjectBlessingAllocationAsync(
            fs,
            readLease,
            soulRoot,
            entitlementProperty,
            audience);
    }

    private static async Task<ResourceProjectionResult> ProjectBlessingAllocationAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease readLease,
        JsonObject? soulRoot,
        string entitlementProperty,
        ResourceProjectionAudience audience)
    {
        var entitlement = soulRoot?[ShiningBlessingEffectState.SoulStateProperty]?
            [entitlementProperty] as JsonObject;
        if (entitlement == null)
            return Unavailable();

        var allocation = await ShiningBlessingRerollResourceService.ReadAllocationAsync(
            fs,
            readLease,
            entitlement);
        if (allocation.Coordinate == null || allocation.Issues.Count != 0)
            return Unavailable();

        var projection = await ProjectCanonicalAsync(
            fs,
            readLease,
            new[]
            {
                new ResourceProjectionOwnerScope(
                    new ResourceOwnerKey(
                        allocation.Coordinate.Realm,
                        allocation.Coordinate.OwnerKind,
                        allocation.Coordinate.ResourceOwnerId),
                    "перебросы благословения",
                    IsOwningPlayer: true)
            },
            audience);
        var row = projection.Rows.SingleOrDefault(static candidate =>
            string.Equals(candidate.ResourceKey, "blessing_rerolls", StringComparison.Ordinal));
        if (!projection.IsAvailable || row == null || allocation.Remaining > row.Current)
        {
            return Unavailable();
        }

        return new ResourceProjectionResult(
            true,
            null,
            new[]
            {
                row with
                {
                    Current = allocation.Remaining,
                    Percentage = null,
                    RecentVisibleDelta = null
                }
            });
    }

    internal static string FormatValue(ResourceProjectionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var current = row.Current.ToString("0.############################", CultureInfo.InvariantCulture);
        var maximum = row.Maximum.ToString("0.############################", CultureInfo.InvariantCulture);
        var suffix = string.IsNullOrWhiteSpace(row.Unit) ? string.Empty : " " + row.Unit;
        return $"{current}/{maximum}{suffix}";
    }

    internal static string FormatSignedDelta(decimal value)
    {
        var formatted = value.ToString(
            "0.############################",
            CultureInfo.InvariantCulture);
        return value > 0m ? "+" + formatted : formatted;
    }

    private static bool TryReadExactIdentifier(JsonNode? node, out string? value)
    {
        value = null;
        return node is JsonValue scalar &&
               scalar.TryGetValue<string>(out value) &&
               ResourceMaterializationContract.IsExactIdentifier(value);
    }

    private static JsonObject? ParseObjectOrNull(string? json)
    {
        if (json == null)
            return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static ParsedProjectionSnapshot? ParseAcceptedSnapshot(
        ResourceProjectionInput input) =>
        ParseAcceptedSnapshot(
            input.Definitions.ToJsonString(),
            input.State.ToJsonString(),
            input.History.ToJsonString(),
            input.OwnerAuthorityFingerprint);

    private static ParsedProjectionSnapshot? ParseAcceptedSnapshot(
        string? definitionsJson,
        string? stateJson,
        string? historyJson,
        string ownerAuthorityFingerprint =
            "sha256:0000000000000000000000000000000000000000000000000000000000000000")
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            definitionsJson,
            allowMissingPristine: false);
        if (!definitions.IsValid || definitions.Catalog == null)
            return null;

        var state = ResourceStateContract.ParseCanonical(
            stateJson,
            definitions.Catalog,
            allowMissingPristine: false);
        if (!state.IsValid || state.Ledger == null)
            return null;

        var history = ResourceHistoryState.ParseCanonical(
            historyJson,
            definitions.Catalog,
            allowMissingPristine: false);
        if (!history.IsValid || history.History == null ||
            history.History.ValidateStateAgreement(state.Ledger).Count != 0)
        {
            return null;
        }

        return new ParsedProjectionSnapshot(
            definitions.Catalog,
            state.Ledger,
            history.History,
            ownerAuthorityFingerprint);
    }

    private static bool IsVisible(
        ResourceVisibility visibility,
        ResourceProjectionAudience audience,
        bool isOwningPlayer) => audience switch
    {
        ResourceProjectionAudience.Player => visibility switch
        {
            ResourceVisibility.PlayerVisible => true,
            ResourceVisibility.OwnerVisible => isOwningPlayer,
            _ => false
        },
        ResourceProjectionAudience.GameMaster => true,
        _ => false
    };

    private static bool TryCalculatePercentage(
        ResourceStateEntry entry,
        ResourceDefinition definition,
        out decimal? percentage)
    {
        percentage = null;
        if (!ResourceMaterializationContract.TrySubtractExact(
                entry.Maximum,
                definition.MinimumPolicy.Value,
                out var range) ||
            !ResourceMaterializationContract.TrySubtractExact(
                entry.Current,
                definition.MinimumPolicy.Value,
                out var progress))
        {
            return false;
        }

        if (range <= 0m)
            return true;
        try
        {
            percentage = decimal.Round(
                progress / range * 100m,
                2,
                MidpointRounding.AwayFromZero);
            return percentage is >= 0m and <= 100m;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (DivideByZeroException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> BuildAvailableOperations(
        ResourceStateEntry entry,
        ResourceDefinition definition)
    {
        if (entry.State != ResourceLifecycleState.Active)
            return Array.Empty<string>();

        var operations = new List<string>();
        AddIfAvailable(ResourceOperation.Damage, entry.Current > definition.MinimumPolicy.Value, "damage");
        AddIfAvailable(ResourceOperation.Restore, entry.Current < entry.Maximum, "restore");
        AddIfAvailable(ResourceOperation.Spend, entry.Current > definition.MinimumPolicy.Value, "spend");
        AddIfAvailable(ResourceOperation.Gain, entry.Current < entry.Maximum, "gain");
        return new ReadOnlyCollection<string>(operations);

        void AddIfAvailable(ResourceOperation operation, bool hasRoom, string token)
        {
            if (hasRoom && definition.AllowedOperations.Contains(operation))
                operations.Add(token);
        }
    }

    private static decimal? FindRecentVisibleDelta(
        ResourceHistoryState history,
        ResourceCoordinate coordinate)
    {
        var transition = history.Transitions
            .Where(candidate =>
                ResourceCoordinateComparer.Instance.Equals(candidate.Coordinate, coordinate) &&
                candidate.Operation is ResourceTransitionOperation.Damage or
                    ResourceTransitionOperation.Restore or
                    ResourceTransitionOperation.Spend or
                    ResourceTransitionOperation.Gain)
            .LastOrDefault();
        if (transition == null)
            return null;

        return transition.Operation is ResourceTransitionOperation.Damage or
            ResourceTransitionOperation.Spend
            ? -transition.AppliedAmount
            : transition.AppliedAmount;
    }

    private static string LocalizeUnit(string unit) =>
        LocalizedUnits.TryGetValue(unit, out var localized)
            ? localized
            : "ед.";

    private static bool IsSafeSelector(string selector, string ownerId) =>
        IsSafeProjectedText(selector, ownerId, maximumLength: 80);

    private static bool IsSafeProjectedText(
        string value,
        string ownerId,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > maximumLength ||
            value.Any(char.IsControl) ||
            value.Contains(ownerId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !ProtectedSelectorFragments.Any(fragment =>
            value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static ResourceProjectionResult Unavailable() =>
        new(
            IsAvailable: false,
            UnavailableMessage: ResourcePlayerFailureMessages.Unavailable,
            Rows: Array.Empty<ResourceProjectionRow>());

    private sealed record ParsedProjectionSnapshot(
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State,
        ResourceHistoryState History,
        string OwnerAuthorityFingerprint);
}
