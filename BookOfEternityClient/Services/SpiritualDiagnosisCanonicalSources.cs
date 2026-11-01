using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Detached, operation-owned canonical DATA. No IO, lease or permission survives here.</summary>
internal sealed class SpiritualDiagnosisCanonicalSources
{
    internal const string SoulPath = "game_state/meta/soul_state.json";
    internal const string QuestsPath = "game_state/quests/soul_quests.json";
    internal const string GuardiansPath = "game_state/meta/guardians.json";
    private readonly string _realm;
    private readonly IReadOnlyDictionary<string, JsonObject?> _roots;
    private readonly IReadOnlyDictionary<string, string> _fingerprints;
    private readonly EffectMechanicsSnapshot? _effects;
    private readonly ResourceStateLedger? _resources;
    private readonly ResourceHistoryState? _resourceHistory;
    private readonly ResourceOwnerAuthority? _resourceOwners;
    private readonly LocalInteractionScope? _scope;
    private readonly MortalWoundTreatmentSceneAuthorityResult? _scene;
    internal int ResourceOwnerCompositions { get; private set; }
    internal int ResourceOwnerSourceReads { get; private set; }

    internal static bool IsCanonicalKind(string kind) => kind is "item_quantity" or "resource_quantity" or
        "consent" or "facility" or "location" or "quest_state" or "effect_state" or "environment";

    // The whitelist is derived from declarations, never retained UI paths. Absences are dependencies too.
    internal static IReadOnlyList<string> Paths(string realm, IEnumerable<string> kinds)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in kinds)
        {
            if (IsCanonicalKind(kind)) { paths.Add(SoulPath); paths.Add(WoundCarrierCatalog.AfterlifeProfilesPath); }
            switch (kind)
            {
                case "resource_quantity":
                    paths.UnionWith(CanonicalResourceOwnerAuthorityComposer.SourceAuthorityPaths);
                    paths.UnionWith(new[] { ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                        ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath });
                    break;
                case "quest_state": paths.Add(QuestsPath); break;
                case "effect_state":
                    paths.UnionWith(new[] { EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath,
                        EffectCarrierCatalog.EnemiesPath, EffectCarrierCatalog.AlliesPath,
                        EffectCarrierCatalog.AfterlifeProfilesPath, EffectCarrierCatalog.SpiritualConflictPath, EffectIdentityState.StatePath });
                    break;
                case "consent": case "facility": case "environment": case "location":
                    paths.Add(realm == "chaos_sea" ? GuardiansPath : ShiningAbodeState.StatePath);
                    paths.Add(GuardianAbodeResidentState.StatePath);
                    break;
            }
        }
        return Array.AsReadOnly(paths.OrderBy(path => path, StringComparer.Ordinal).ToArray());
    }

    internal SpiritualDiagnosisCanonicalSources(string realm, IEnumerable<string> kinds,
        IReadOnlyDictionary<string, JsonObject?> roots, EffectMechanicsSnapshot? effects)
    {
        _realm = realm;
        var declared = kinds.ToHashSet(StringComparer.Ordinal);
        var owned = Paths(realm, declared).ToDictionary(path => path,
            path => roots.GetValueOrDefault(path)?.DeepClone().AsObject(), StringComparer.Ordinal);
        _roots = owned;
        _fingerprints = owned.ToDictionary(pair => pair.Key, pair => NodeFingerprint(pair.Value), StringComparer.Ordinal);
        _effects = effects;
        if (_effects is null && declared.Contains("effect_state"))
            _effects = EffectMechanicsSnapshot.Build(new(new(Root(EffectCarrierCatalog.PlayerPath), Root(EffectCarrierCatalog.NpcPath),
                Root(EffectCarrierCatalog.EnemiesPath), Root(EffectCarrierCatalog.AlliesPath), Root(EffectCarrierCatalog.AfterlifeProfilesPath),
                Root(EffectCarrierCatalog.SpiritualConflictPath)), Root(EffectIdentityState.StatePath)));
        if (declared.Contains("resource_quantity"))
        {
            try
            {
                var definitions = ResourceDefinitionCatalog.ParseCanonical(Root(ResourceMaterializationContract.DefinitionsPath)?.ToJsonString(), false);
                if (definitions.IsValid && definitions.Catalog is { } catalog)
                {
                    var state = ResourceStateContract.ParseCanonical(Root(ResourceMaterializationContract.StatePath)?.ToJsonString(), catalog, false);
                    var history = ResourceHistoryState.ParseCanonical(Root(ResourceMaterializationContract.HistoryPath)?.ToJsonString(), catalog, false);
                    if (state.IsValid && state.Ledger is { } ledger && history.IsValid && history.History is { } log &&
                        log.ValidateStateAgreement(ledger).Count == 0)
                    {
                        // The existing composer receives completed in-memory reads only. It must
                        // complete synchronously; this API never blocks on filesystem/remote IO.
                        var documents = owned.ToDictionary(pair => pair.Key, pair => pair.Value?.ToJsonString(), StringComparer.Ordinal);
                        ResourceOwnerCompositions++;
                        var operation = CanonicalResourceOwnerAuthorityComposer.ComposeAsync(catalog,
                            path => { ResourceOwnerSourceReads++; return Task.FromResult(documents.GetValueOrDefault(path)); }, ledger, log,
                            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
                        if (!operation.IsCompletedSuccessfully) throw new InvalidOperationException("Canonical DATA composition performed asynchronous work.");
                        var authority = operation.GetAwaiter().GetResult();
                        if (authority.IsValid) { _resources = ledger; _resourceHistory = log; _resourceOwners = authority.Authority; }
                    }
                }
            }
            catch (Exception failure) when (failure is JsonException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
            { /* Malformed current input never supplies resource proof. */ }
        }
        if (declared.Overlaps(new[] { "location", "consent", "facility", "environment" }))
        {
            try
            {
                var current = ReadCurrentLocality();
                if (current is { } locality)
                {
                    _scope = locality.Scope;
                    if (locality.Container["customStates"] is JsonArray rows)
                        _scene = SpiritualDiagnosisSceneAuthorityContract.Parse(JsonSerializer.SerializeToElement(rows), "spiritualDiagnosis.currentLocality.customStates");
                }
            }
            catch (Exception failure) when (failure is JsonException or InvalidDataException or ArgumentException or InvalidOperationException)
            { /* Unresolved locality grants neither membership nor setting authority. */ }
        }
    }

    internal string Fingerprint => WoundAcceptedTurnFingerprintWriter.Compute(new[] { "spiritual_diagnosis.canonical_sources", "1", _realm }
        .Concat(_fingerprints.SelectMany(pair => new[] { pair.Key, pair.Value })));

    internal JsonArray Dependencies(string kind) => new(Paths(_realm, new[] { kind }).Select(path => (JsonNode)new JsonObject
    { ["path"] = path, ["fingerprint"] = _fingerprints[path] }).ToArray());

    internal JsonObject? Prove(MortalWoundTreatmentRequirement requirement, WoundOwnerCoordinate provider,
        WoundOwnerCoordinate target)
    {
        var actor = requirement switch
        {
            MortalWoundItemQuantityRequirement item => item.OwnerRole == "provider" ? provider : target,
            MortalWoundResourceQuantityRequirement resource => resource.OwnerRole == "provider" ? provider : target,
            MortalWoundLocationRequirement location => location.TargetRole == "provider" ? provider : target,
            MortalWoundEffectStateRequirement effect => effect.TargetRole == "provider" ? provider : target,
            _ => provider
        };
        try
        {
            switch (requirement)
            {
                case MortalWoundItemQuantityRequirement item:
                    if (actor.OwnerKind != "player_soul" || actor.OwnerId != "player_soul" || item.Quantity != 1 ||
                        !HasUniqueRelic(item.ItemRef)) return null;
                    return new() { ["itemRef"] = item.ItemRef, ["quantity"] = item.Quantity, ["availableQuantity"] = 1 };
                case MortalWoundResourceQuantityRequirement resource:
                    var profile = UniqueProfile(actor);
                    if (_resources is null || _resourceHistory is null || _resourceOwners is null ||
                        profile?[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty] is not JsonArray bindings) return null;
                    var matching = bindings.OfType<JsonObject>().Where(row => Text(row["realm"]) == _realm).ToArray();
                    if (matching is not { Length: 1 } || Text(matching[0]["state"]) != "active" ||
                        !ExactId(matching[0]["resourceOwnerId"], out var ownerId)) return null;
                    var coordinate = new ResourceCoordinate(_realm, ResourceOwnerKind.AfterlifeActor, ownerId, resource.ResourceRef);
                    var owner = _resourceOwners.Resolve(new(_realm, ResourceOwnerKind.AfterlifeActor, resource.ResourceRef, ownerId, null));
                    if (!owner.Success || owner.Entry is not { Lifecycle: ResourceOwnerLifecycle.Active } entry ||
                        !entry.IsResourceCapabilityActive(resource.ResourceRef) ||
                        !_resources.TryResolveExact(coordinate, out var value) || value is not { State: ResourceLifecycleState.Active } ||
                        value.Current < resource.Quantity) return null;
                    return new() { ["resourceRef"] = resource.ResourceRef, ["quantity"] = resource.Quantity,
                        ["resourceOwnerId"] = ownerId, ["current"] = value.Current, ["maximum"] = value.Maximum,
                        ["resourceStateFingerprint"] = _resources.Fingerprint, ["resourceHistoryFingerprint"] = _resourceHistory.Fingerprint,
                        ["ownerAuthorityFingerprint"] = _resourceOwners.Fingerprint };
                case MortalWoundQuestStateRequirement quest:
                    if (Root(QuestsPath) is not { } questRoot || !ExactProperties(questRoot, "quests") ||
                        questRoot["quests"] is not JsonArray quests || !ExactUniqueRows(quests, "questId")) return null;
                    var found = quests.OfType<JsonObject>().SingleOrDefault(row => Text(row["questId"]) == quest.QuestRef);
                    if (found is null || !ExactProperties(found, "questId", "status", "objectives", "progress", "rewards") || Text(found["status"]) != quest.RequiredState || quest.RequiredState is not ("active" or "completed" or "failed" or "abandoned") ||
                        found["objectives"] is not JsonArray || found["progress"] is not JsonObject || found["rewards"] is not JsonObject ||
                        !new[] { "guardianId", "title", "description" }.All(name => Text(found[name]) is { Length: > 0 })) return null;
                    return new() { ["questRef"] = quest.QuestRef, ["state"] = quest.RequiredState };
                case MortalWoundEffectStateRequirement effect:
                    if (_effects is not { IsAccepted: true }) return null;
                    var candidates = _effects.Effects.Where(row => row.EffectId == effect.EffectRef).ToArray();
                    if (candidates is not { Length: 1 } || candidates[0] is not { } actual || actual.Realm != _realm ||
                        actual.TargetId != actor.OwnerId || actual.TargetKind != (actor.OwnerKind == "player_soul" ? "player" : actor.OwnerKind) ||
                        !actual.CanonicalEffect.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.String || state.GetString() != effect.RequiredState)
                        return null;
                    return new() { ["effectRef"] = effect.EffectRef, ["state"] = effect.RequiredState,
                        ["actor"] = SpiritualHealingRequestProjection.OwnerJson(actor),
                        ["effectFingerprint"] = NodeFingerprint(JsonNode.Parse(actual.CanonicalEffect.GetRawText())) };
                case MortalWoundLocationRequirement location:
                    if (!IsLocal(actor) || _scope!.LocationId != location.LocationRef) return null;
                    return new() { ["locationRef"] = location.LocationRef, ["locationId"] = _scope.LocationId,
                        ["actor"] = SpiritualHealingRequestProjection.OwnerJson(actor) };
                case MortalWoundFacilityRequirement facility:
                    if (!SceneReady(provider, target)) return null;
                    var facilityRow = _scene!.Facilities.SingleOrDefault(row => row.FacilityId == facility.FacilityRef);
                    return facilityRow is { Available: true } ? new() { ["facilityRef"] = facility.FacilityRef,
                        ["locationId"] = _scope!.LocationId, ["available"] = true } : null;
                case MortalWoundEnvironmentRequirement environment:
                    if (!SceneReady(provider, target)) return null;
                    var environmentRow = _scene!.Environments.SingleOrDefault(row => row.EnvironmentId == environment.EnvironmentRef);
                    return environmentRow?.State == environment.RequiredState ? new() { ["environmentRef"] = environment.EnvironmentRef,
                        ["state"] = environment.RequiredState, ["locationId"] = _scope!.LocationId } : null;
                case MortalWoundConsentRequirement consent:
                    if (!SceneReady(provider, target) || consent.ProviderRef != provider.OwnerId || consent.TargetRef != target.OwnerId) return null;
                    var consentRow = _scene!.Consents.SingleOrDefault(row => row.ConsentRef == consent.ConsentRef);
                    if (consentRow is null || consentRow.Status != "granted" || consentRow.ProviderKind != provider.OwnerKind ||
                        consentRow.ProviderId != provider.OwnerId || consentRow.TargetKind != target.OwnerKind || consentRow.TargetId != target.OwnerId) return null;
                    return new() { ["consentRef"] = consent.ConsentRef, ["provider"] = SpiritualHealingRequestProjection.OwnerJson(provider),
                        ["target"] = SpiritualHealingRequestProjection.OwnerJson(target), ["locationId"] = _scope!.LocationId, ["status"] = "granted" };
            }
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or ArgumentException or InvalidOperationException or FormatException)
        { /* Invalid canonical DATA is a refusal, never an inferred permission. */ }
        return null;
    }

    private JsonObject? Root(string path) => _roots.GetValueOrDefault(path);
    private JsonObject? UniqueProfile(WoundOwnerCoordinate owner)
    {
        if (Root(WoundCarrierCatalog.AfterlifeProfilesPath)?["profiles"] is not JsonArray profiles || !ExactUniqueRows(profiles, "actorId")) return null;
        return profiles.OfType<JsonObject>().SingleOrDefault(row => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(row, owner));
    }
    private bool SceneReady(WoundOwnerCoordinate provider, WoundOwnerCoordinate target) =>
        _scene is { Issues.Count: 0 } && IsLocal(provider) && IsLocal(target);
    private bool IsLocal(WoundOwnerCoordinate owner) => _scope is { IsResolved: true } && owner.Realm == _realm && UniqueProfile(owner) is not null &&
        (owner.OwnerKind == "player_soul" && owner.OwnerId == "player_soul" ||
         _scope.LocalActorIds.Any(id => string.Equals(id, owner.OwnerId, StringComparison.Ordinal)));

    private (LocalInteractionScope Scope, JsonObject Container)? ReadCurrentLocality()
    {
        if (_realm == "shining_abode")
        {
            var shining = Root(ShiningAbodeState.StatePath);
            if (shining is null || !ExactProperties(shining, "currentHallId", "halls") ||
                shining["halls"] is not JsonArray halls || !ExactUniqueRows(halls, "hallId")) return null;
            var scope = ShiningCurrentHallContract.RequireCurrentScope(shining, Root(GuardianAbodeResidentState.StatePath));
            var hall = halls.OfType<JsonObject>().SingleOrDefault(row => Text(row["hallId"]) == scope.LocationId);
            return scope.IsResolved && hall is not null && ExactProperties(hall, "hallId", "customStates") ? (scope, hall) : null;
        }
        var guardians = Root(GuardiansPath);
        if (_realm != "chaos_sea" || guardians is null || !ExactProperties(guardians, "guardians", "chaosSeaNavigation", "activeGuardianId") ||
            guardians["guardians"] is not JsonArray rows || !ExactUniqueRows(rows, "guardianId") ||
            guardians["chaosSeaNavigation"] is not JsonObject navigation || !ExactProperties(navigation, "currentGuardianId", "currentAbodeId") ||
            !ExactId(navigation["currentGuardianId"], out var guardianId) || !ExactId(navigation["currentAbodeId"], out var abodeId) ||
            Text(guardians["activeGuardianId"]) is { } active && active != guardianId) return null;
        var guardian = rows.OfType<JsonObject>().SingleOrDefault(row => Text(row["guardianId"]) == guardianId);
        if (guardians["activeGuardian"] is JsonObject activeGuardian &&
            (!ExactProperties(activeGuardian, "guardianId") || Text(activeGuardian["guardianId"]) != guardianId ||
             activeGuardian.ContainsKey("id") || activeGuardian.ContainsKey("actorId"))) return null;
        if (guardian?["abode"] is not JsonObject abode || !ExactProperties(guardian, "guardianId", "abode") ||
            !ExactProperties(abode, "abodeId", "customStates") || Text(abode["abodeId"]) != abodeId) return null;
        var resolved = LocalInteractionScopeService.ResolveChaosSeaScope(guardians, Root(GuardianAbodeResidentState.StatePath), []);
        return resolved.IsResolved && resolved.LocationId == abodeId && resolved.CurrentGuardianId == guardianId ? (resolved, abode) : null;
    }

    private bool HasUniqueRelic(string id)
    {
        if (Root(SoulPath)?["soulRelics"] is not JsonObject relics || relics.Count != 2 ||
            relics["stored"] is not JsonArray stored || relics["equipped"] is not JsonArray equipped) return false;
        var rows = stored.Concat(equipped).ToArray();
        if (rows.Any(row => row is not JsonObject) || !ExactUniqueRows(new JsonArray(rows.Select(row => row!.DeepClone()).ToArray()), "relicId")) return false;
        var relic = rows.OfType<JsonObject>().SingleOrDefault(row => Text(row["relicId"]) == id);
        // A reward reference/stub or a legacy alias never establishes one owned canonical relic.
        return relic is not null && !relic.ContainsKey("id") && !relic.ContainsKey("quality") &&
            Text(relic["name"]) is { Length: > 0 } && Text(relic["rarity"]) is "Common" or "Uncommon" or "Rare" or "Epic" or "Legendary" &&
            Text(relic["category"]) is { Length: > 0 } && new[] { "effects", "equipmentData", "acquisitionData", "gameplayStatus" }.All(key => relic[key] is JsonObject);
    }

    internal static bool ExactProperties(JsonObject row, params string[] fields) => fields.All(field =>
        row.Count(pair => ExactIdentifierConfusableKey.Build(pair.Key) == ExactIdentifierConfusableKey.Build(field)) ==
        (row.ContainsKey(field) ? 1 : 0));
    private static bool ExactUniqueRows(JsonArray rows, string field)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        return rows.All(node => node is JsonObject row && ExactProperties(row, field) && ExactId(row[field], out var id) &&
            identities.Add(ExactIdentifierConfusableKey.Build(id)));
    }
    private static bool ExactId(JsonNode? node, out string value)
    { value = Text(node) ?? ""; return ResourceMaterializationContract.IsExactIdentifier(value); }
    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    private static string NodeFingerprint(JsonNode? node) => WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { node is null ? "absent" : "present", WoundAcceptedTurnFingerprintWriter.CanonicalJson(node) });
}
