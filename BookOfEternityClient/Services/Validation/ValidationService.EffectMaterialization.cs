using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static void RejectLegacyEffectRouteIfPresent(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out _))
            return;

        var path = $"{contextPrefix}.{propertyName}";
        if (issues.Any(issue =>
                issue.Code == "effect_materialization_legacy_route_unsupported" &&
                string.Equals(issue.FilePath, path, StringComparison.Ordinal)))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            $"{propertyName} больше не является маршрутом применения эффектов",
            code: "effect_materialization_legacy_route_unsupported",
            section: "EffectMaterialization",
            expected: "effectChanges[] in game_state/effects/effect_commands.json",
            actual: "legacy effect route present",
            repairHint: "Передавай apply/dispel/remove только через общий effectChanges[]; не записывай legacy player/NPC effect aliases."));
    }

    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawEffectMaterializationAsync()
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        return await ValidateAcceptedTurnRawEffectMaterializationAsync(writeLease);
    }

    internal async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawEffectMaterializationAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, writeLease);
        EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, writeLease);
        var issues = new List<ValidationIssue>();
        var keepEffectHandoff = false;
        try
        {
            await ValidateAcceptedTurnRawEffectMaterializationAsync(
                issues,
                null,
                false,
                validatedManifest: null,
                writeLease);
            keepEffectHandoff =
                issues.All(static issue => issue.Severity != IssueSeverity.Error) &&
                EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                    _fs,
                    writeLease,
                    out _);
            return issues;
        }
        finally
        {
            AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, writeLease);
            if (!keepEffectHandoff)
                EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, writeLease);
        }
    }

    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalEffectMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalEffectMaterializationAsync(issues, null);
        return issues;
    }

    internal async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalEffectMaterializationAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalEffectMaterializationAsync(
            issues,
            writeLease);
        return issues;
    }

    private async Task ValidateAcceptedTurnRawEffectMaterializationAsync(
        List<ValidationIssue> issues,
        ResourceOwnerCompositionResult? resourceOwners,
        bool suppressEffectExecutionForTerminalReceiptReplay,
        ValidationPendingTurnSnapshotManifest? validatedManifest,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, writeLease);
        var commandJson = await _fs.ReadFileAsync(EffectAcceptedTurnPlan.CommandPath);
        var currentCarriers = await ReadEffectCarriersAsync(null, issues);
        if (currentCarriers.SpiritualConflict?[AfterlifeSpiritualConflictState.ResponseField]
            is JsonNode spiritualConflictUpdate)
        {
            using var updateDocument = JsonDocument.Parse(
                spiritualConflictUpdate.ToJsonString());
            ValidateNoDirectCombatConditionAuthoring(
                updateDocument.RootElement,
                EffectCarrierCatalog.SpiritualConflictPath + "." +
                AfterlifeSpiritualConflictState.ResponseField,
                issues);
        }
        var currentIndexJson = await _fs.ReadFileAsync(EffectAcceptedTurnPlan.IdentityIndexPath);
        _ = ParseEffectObjectRoot(
            currentIndexJson,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            "effect_identity_invalid_root",
            issues);
        var lookup = validatedManifest == null
            ? await LoadValidatedPendingTurnSnapshotLookupAsync()
            : null;
        var hasCommand = commandJson != null;
        var hasCurrentEffectAuthority = HasAnyEffectAuthority(currentCarriers, currentIndexJson);

        if (validatedManifest == null &&
            (lookup?.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
             lookup?.Manifest == null))
        {
            if (hasCommand || hasCurrentEffectAuthority)
            {
                issues.Add(NewEffectIssue(
                    EffectAcceptedTurnPlan.CommandPath,
                    "effect_materialization_snapshot_required",
                    "usable validated pending-turn snapshot before effect validation",
                    lookup?.Status.ToString() ?? "Missing"));
            }
            return;
        }

        var manifest = validatedManifest ?? lookup!.Manifest!;
        var requestJson = await _fs.ReadFileAsync("input/turn_request.json");
        if (!TryParseResourceTurnRequest(
                requestJson,
                manifest.SessionId,
                manifest.RequestId,
                manifest.TurnNumber,
                out _,
                out var acceptedRealm))
        {
            issues.Add(NewEffectIssue(
                "input/turn_request.json",
                "effect_materialization_turn_authority_invalid",
                "exact pending snapshot session/request/turn/current-realm authority",
                "missing or mismatched request"));
            return;
        }
        var builtInApplicationAuthorities = EffectBuiltInSourceCatalog
            .ResolveAcceptedTurnApplicationAuthorities(
                manifest.PlayerAction,
                acceptedRealm);
        var preTurnCarriers = await ReadSnapshotEffectCarriersAsync(manifest, issues);
        var plannedCarriers = ProjectAcceptedSpiritualConflictCarrier(
            ApplyResourceOwnerAfterImages(
                currentCarriers,
                resourceOwners),
            preTurnCarriers.SpiritualConflict);
        var acceptedCombatTargets = EffectAcceptedTurnInputComposer
            .CollectCombatMemberPlanTargets(
                preTurnCarriers,
                plannedCarriers,
                resourceOwners?.CombatantIdentities);
        var preTurnIndexJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            manifest,
            EffectAcceptedTurnPlan.IdentityIndexPath);
        var preTurnIndex = ParseEffectObjectRoot(
            preTurnIndexJson,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            "effect_identity_invalid_root",
            issues);
        ValidateEffectOwnedContinuity(
            preTurnCarriers,
            currentCarriers,
            preTurnIndexJson,
            currentIndexJson,
            issues);
        var preTurnSources = await ReadSnapshotEffectSourceRootsAsync(manifest, issues);
        var acceptedSources = await ReadCurrentEffectSourceRootsAsync(issues);
        var currentWorldTime = EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
            await _fs.ReadFileAsync(EffectAcceptedTurnInputComposer.WorldTimePath));
        issues.AddRange(EffectAcceptedTurnInputComposer
            .ValidateAcceptedSkillComposition(preTurnSources, acceptedSources));
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return;
        if (!hasCommand)
        {
            ValidateEffectSourceDefinitions(acceptedSources, issues);
            issues.AddRange(EffectAcceptedTurnInputComposer
                .ValidateRegisteredSourceOwners(acceptedSources));
            var sourceAuthority = EffectAcceptedTurnInputComposer
                .BuildCanonicalSourceAuthority(acceptedSources);
            issues.AddRange(sourceAuthority.Issues);
            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error) ||
                (!EffectAcceptedTurnInputComposer.HasPendingCombatantRefs(currentCarriers) &&
                 EffectCarrierCatalog.Build(currentCarriers).Occurrences.Count == 0))
            {
                return;
            }

            var identityOwnerExports = await ValidateAndCollectAcceptedEffectOwnerExportsAsync(
                preTurnSources,
                acceptedSources,
                issues);
            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
                return;

            var identityLocationPlanningIssues = new List<ValidationIssue>();
            var identityLocationPlan = await ValidateRawMortalLocationAcceptedTurnPlanAsync(
                identityLocationPlanningIssues);
            issues.AddRange(identityLocationPlanningIssues);
            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
                return;

            var identityItemSources = MortalItemAcceptedTurnAuthority.GetValidatedEffectSources(
                _fs,
                writeLease,
                manifest.SessionId,
                manifest.ManifestPayloadHash);
            var identityAcceptedPlanSources =
                EffectAcceptedTurnInputComposer.CollectLocationPlanSources(identityLocationPlan)
                    .Concat(identityItemSources)
                    .Concat(identityOwnerExports.Sources)
                    .ToArray();
            var identityAcceptedPlanTargets = identityOwnerExports.Targets
                .Concat(EffectAcceptedTurnInputComposer.CollectAfterlifePlanTargets(
                    resourceOwners?.Authority,
                    plannedCarriers.AfterlifeProfiles))
                .Concat(acceptedCombatTargets.Targets)
                .ToArray();
            var identityReplacedSourceOwners = identityOwnerExports.ReplacedSourceOwners
                .Concat(MortalItemAcceptedTurnAuthority.GetReplacedEffectSourceOwners(
                    _fs,
                    writeLease,
                    manifest.SessionId,
                    manifest.ManifestPayloadHash))
                .ToHashSet();

            var emptyCommands = EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot();
            var identityInput = EffectAcceptedTurnInputComposer.Compose(
                manifest.SessionId,
                manifest.ManifestPayloadHash,
                manifest.TurnNumber,
                emptyCommands,
                preTurnCarriers,
                plannedCarriers,
                preTurnIndex,
                preTurnSources,
                identityAcceptedPlanSources,
                identityAcceptedPlanTargets,
                identityReplacedSourceOwners,
                identityOwnerExports.ReplacedTargets
                    .Concat(acceptedCombatTargets.ReplacedTargets)
                    .ToHashSet(),
                currentWorldTime,
                publicationCarrierBaselines: currentCarriers,
                preallocatedCombatantIdentities: resourceOwners?.CombatantIdentities,
                realm: acceptedRealm,
                grantedBuiltInApplicationAuthorities: builtInApplicationAuthorities);
            var identityResult = EffectAcceptedTurnPlanAuthority.GetOrBuildValidated(
                _fs,
                writeLease,
                identityInput);
            issues.AddRange(identityResult.Issues);
            return;
        }

        if (!TryParseEffectCommands(commandJson!, issues, out var commands))
            return;

        var reportedEvents = EffectAcceptedEventReportCatalog.Compose(
            commands[EffectAcceptedEventReportCatalog.ResponseField],
            manifest.TurnNumber,
            acceptedRealm,
            manifest.PreGeneratedDices1d20 ?? Array.Empty<int>(),
            plannedCarriers);
        issues.AddRange(reportedEvents.Issues);
        if (!reportedEvents.IsValid)
            return;

        ValidateEffectSourceDefinitions(acceptedSources, issues);
        issues.AddRange(EffectAcceptedTurnInputComposer
            .ValidateRegisteredSourceOwners(acceptedSources));
        var ownerExports = await ValidateAndCollectAcceptedEffectOwnerExportsAsync(
            preTurnSources,
            acceptedSources,
            issues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return;

        var locationPlanningIssues = new List<ValidationIssue>();
        var locationPlan = await ValidateRawMortalLocationAcceptedTurnPlanAsync(
            locationPlanningIssues);
        issues.AddRange(locationPlanningIssues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return;

        var itemSources = MortalItemAcceptedTurnAuthority.GetValidatedEffectSources(
            _fs,
            writeLease,
            manifest.SessionId,
            manifest.ManifestPayloadHash);
        var acceptedPlanSources =
            EffectAcceptedTurnInputComposer.CollectLocationPlanSources(locationPlan)
                .Concat(itemSources)
                .Concat(ownerExports.Sources)
                .ToArray();
        var acceptedPlanTargets = ownerExports.Targets
            .Concat(EffectAcceptedTurnInputComposer.CollectAfterlifePlanTargets(
                resourceOwners?.Authority,
                plannedCarriers.AfterlifeProfiles))
            .Concat(acceptedCombatTargets.Targets)
            .ToArray();
        var replacedSourceOwners = ownerExports.ReplacedSourceOwners
            .Concat(MortalItemAcceptedTurnAuthority.GetReplacedEffectSourceOwners(
                _fs,
                writeLease,
                manifest.SessionId,
                manifest.ManifestPayloadHash))
            .ToHashSet();
        var input = EffectAcceptedTurnInputComposer.Compose(
            manifest.SessionId,
            manifest.ManifestPayloadHash,
            manifest.TurnNumber,
            commands,
            preTurnCarriers,
            plannedCarriers,
            preTurnIndex,
            preTurnSources,
            acceptedPlanSources,
            acceptedPlanTargets,
            replacedSourceOwners,
            ownerExports.ReplacedTargets
                .Concat(acceptedCombatTargets.ReplacedTargets)
                .ToHashSet(),
            currentWorldTime,
            publicationCarrierBaselines: currentCarriers,
            preallocatedCombatantIdentities: resourceOwners?.CombatantIdentities,
            realm: acceptedRealm,
            grantedBuiltInApplicationAuthorities: builtInApplicationAuthorities,
            acceptedReportedLifecycleEvents: reportedEvents.LifecycleEvents);
        if (suppressEffectExecutionForTerminalReceiptReplay)
        {
            var replayEventInput = input.EventInput;
            replayEventInput["lifecycleEvents"] = new JsonArray();
            var replayCommands = input.RawCommands.DeepClone().AsObject();
            replayCommands["effectChanges"] = new JsonArray();
            input = input with
            {
                RawCommands = replayCommands,
                EventInput = replayEventInput
            };
        }
        var result = EffectAcceptedTurnPlanAuthority.GetOrBuildValidated(
            _fs,
            writeLease,
            input);
        issues.AddRange(result.Issues);
    }

    private static EffectCarrierCatalogInput ApplyResourceOwnerAfterImages(
        EffectCarrierCatalogInput current,
        ResourceOwnerCompositionResult? resourceOwners)
    {
        var afterImages = resourceOwners?.OwnerCompanionAfterImages;
        return new EffectCarrierCatalogInput(
            current.PlayerEffects?.DeepClone().AsObject(),
            current.NpcEffects?.DeepClone().AsObject(),
            ReadPreparedCarrier(
                EffectCarrierCatalog.EnemiesPath,
                current.EnemyCombatants,
                afterImages),
            ReadPreparedCarrier(
                EffectCarrierCatalog.AlliesPath,
                current.AllyCombatants,
                afterImages),
            ReadPreparedCarrier(
                AfterlifeEntityProfileState.StatePath,
                current.AfterlifeProfiles,
                afterImages),
            ReadPreparedCarrier(
                AfterlifeSpiritualConflictState.StatePath,
                current.SpiritualConflict,
                afterImages));
    }

    private static EffectCarrierCatalogInput ProjectAcceptedSpiritualConflictCarrier(
        EffectCarrierCatalogInput carriers,
        JsonObject? preTurnSpiritualConflict)
    {
        var current = carriers.SpiritualConflict;
        if (current?[AfterlifeSpiritualConflictState.ResponseField] is not JsonObject update)
            return carriers;

        var baseline = current.DeepClone().AsObject();
        baseline.Remove(AfterlifeSpiritualConflictState.ResponseField);
        if (baseline["activeConflict"] is not JsonObject &&
            preTurnSpiritualConflict != null)
        {
            baseline = preTurnSpiritualConflict.DeepClone().AsObject();
        }

        return carriers with
        {
            SpiritualConflict = AfterlifeSpiritualConflictState.ApplyUpdate(
                baseline,
                update)
        };
    }

    private static JsonObject? ReadPreparedCarrier(
        string path,
        JsonObject? current,
        IReadOnlyDictionary<string, JsonObject>? afterImages) =>
        afterImages != null && afterImages.TryGetValue(path, out var afterImage)
            ? afterImage.DeepClone().AsObject()
            : current?.DeepClone().AsObject();

    private async Task<EffectAcceptedOwnerExports>
        ValidateAndCollectAcceptedEffectOwnerExportsAsync(
            IReadOnlyDictionary<string, JsonNode?> preTurnSources,
            IReadOnlyDictionary<string, JsonNode?> acceptedSources,
            List<ValidationIssue> issues)
    {
        var changedOwnerPaths = EffectAcceptedTurnInputComposer
            .SameTurnOwnerAuthorityPaths
            .Where(path => EffectOwnerPathChanged(
                path,
                preTurnSources,
                acceptedSources))
            .ToArray();
        if (changedOwnerPaths.Length == 0)
            return EffectAcceptedOwnerExports.Empty;

        var genericOwnerPaths = changedOwnerPaths
            .Where(static path => !string.Equals(
                path,
                "game_state/factions/faction_core.json",
                StringComparison.Ordinal))
            .ToArray();
        var ownerIssues = genericOwnerPaths.Length == 0
            ? new List<ValidationIssue>()
            : await ValidateGameStateAsync(
                new GameStateValidationSelection(
                    GameStateValidationPhase.PlayerStateFiles |
                    GameStateValidationPhase.NpcStateFiles |
                    GameStateValidationPhase.WorldQuestCombatFactionStateFiles,
                    genericOwnerPaths));

        if (changedOwnerPaths.Contains(
                "game_state/npcs/npc_core.json",
                StringComparer.Ordinal))
        {
            await ValidateAcceptedTurnMortalActorMaterializationCompletenessAsync(
                ownerIssues);
        }

        if (changedOwnerPaths.Contains(
                "game_state/factions/faction_core.json",
                StringComparer.Ordinal))
        {
            ownerIssues.AddRange(
                await ValidateAcceptedTurnRawFactionMaterializationAsync());
        }

        foreach (var issue in ownerIssues)
        {
            if (!issues.Any(existing =>
                    string.Equals(existing.Code, issue.Code, StringComparison.Ordinal) &&
                    string.Equals(existing.FilePath, issue.FilePath, StringComparison.Ordinal)))
            {
                issues.Add(issue);
            }
        }

        return ownerIssues.Any(static issue => issue.Severity == IssueSeverity.Error)
            ? EffectAcceptedOwnerExports.Empty
            : EffectAcceptedTurnInputComposer.CollectValidatedSameTurnOwnerExports(
                preTurnSources,
                acceptedSources
                    .Where(pair => changedOwnerPaths.Contains(
                        pair.Key,
                        StringComparer.Ordinal))
                    .ToDictionary(
                        static pair => pair.Key,
                        static pair => pair.Value,
                        StringComparer.Ordinal));
    }

    private static bool EffectOwnerPathChanged(
        string path,
        IReadOnlyDictionary<string, JsonNode?> preTurnSources,
        IReadOnlyDictionary<string, JsonNode?> acceptedSources)
    {
        preTurnSources.TryGetValue(path, out var before);
        acceptedSources.TryGetValue(path, out var after);
        return !JsonNode.DeepEquals(before, after);
    }

    private async Task ValidateAcceptedTurnCanonicalEffectMaterializationAsync(
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var sourceRoots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var path in EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
        {
            sourceRoots[path] = ParseEffectNode(
                await ReadEffectFileAsync(path, writeLease),
                path,
                issues);
        }
        ValidateEffectSourceDefinitions(sourceRoots, issues);
        issues.AddRange(EffectAcceptedTurnInputComposer
            .ValidateRegisteredSourceOwners(sourceRoots));

        var carriers = await ReadEffectCarriersAsync(writeLease, issues);
        var catalog = EffectCarrierCatalog.Build(carriers);
        issues.AddRange(catalog.Issues);
        var sourceAuthority = EffectAcceptedTurnInputComposer
            .BuildCanonicalSourceAuthority(sourceRoots);
        issues.AddRange(sourceAuthority.Issues);
        var targetAuthority = EffectAcceptedTurnInputComposer
            .BuildCanonicalTargetAuthority(
                carriers,
                sourceRoots);
        issues.AddRange(targetAuthority.Issues);
        issues.AddRange(targetAuthority.ValidateNamedCombatantBindings(carriers));
        ValidateCanonicalEffectBindings(
            catalog,
            sourceAuthority,
            targetAuthority,
            issues);

        var indexJson = await ReadEffectFileAsync(
            EffectAcceptedTurnPlan.IdentityIndexPath,
            writeLease);
        var indexRoot = ParseEffectObjectRoot(
                indexJson,
                EffectAcceptedTurnPlan.IdentityIndexPath,
                "effect_identity_invalid_root",
                issues) ?? new JsonObject
        {
            ["schemaVersion"] = EffectIdentityState.SchemaVersion,
            ["entries"] = new JsonArray()
        };
        using var indexDocument = JsonDocument.Parse(indexRoot.ToJsonString());
        var parsedIndex = EffectIdentityState.Parse(
            indexDocument.RootElement,
            EffectAcceptedTurnPlan.IdentityIndexPath);
        issues.AddRange(parsedIndex.Issues);
        if (parsedIndex.State != null)
            ValidateEffectCarrierIndexAgreement(catalog, parsedIndex.State, issues);

        var commandJson = await ReadEffectFileAsync(
            EffectAcceptedTurnPlan.CommandPath,
            writeLease);
        if (commandJson != null)
        {
            issues.Add(NewEffectIssue(
                EffectAcceptedTurnPlan.CommandPath,
                "effect_materialization_command_not_consumed",
                "transient effect command absent after canonical publication",
                "effect command remains"));
        }
    }

    private async Task<EffectCarrierCatalogInput> ReadSnapshotEffectCarriersAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        List<ValidationIssue> issues) =>
        new(
            await ReadSnapshotEffectCarrierRootAsync(manifest, EffectCarrierCatalog.PlayerPath, issues),
            await ReadSnapshotEffectCarrierRootAsync(manifest, EffectCarrierCatalog.NpcPath, issues),
            await ReadSnapshotEffectCarrierRootAsync(manifest, EffectCarrierCatalog.EnemiesPath, issues),
            await ReadSnapshotEffectCarrierRootAsync(manifest, EffectCarrierCatalog.AlliesPath, issues),
            await ReadSnapshotEffectCarrierRootAsync(manifest, EffectCarrierCatalog.AfterlifeProfilesPath, issues),
            await ReadSnapshotEffectCarrierRootAsync(manifest, EffectCarrierCatalog.SpiritualConflictPath, issues));

    private async Task<JsonObject?> ReadSnapshotEffectCarrierRootAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        string path,
        List<ValidationIssue> issues) =>
        ParseEffectObjectRoot(
            await ReadValidatedPendingTurnSnapshotFileAsync(manifest, path),
            path,
            "effect_materialization_invalid_carrier_root",
            issues);

    private async Task<IReadOnlyDictionary<string, JsonNode?>>
        ReadSnapshotEffectSourceRootsAsync(
            ValidationPendingTurnSnapshotManifest manifest,
            List<ValidationIssue> issues)
    {
        var result = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var path in EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
        {
            result[path] = ParseEffectNode(
                await ReadValidatedPendingTurnSnapshotFileAsync(manifest, path),
                path,
                issues);
        }
        return result;
    }

    private async Task<IReadOnlyDictionary<string, JsonNode?>>
        ReadCurrentEffectSourceRootsAsync(List<ValidationIssue> issues)
    {
        var result = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var path in EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
            result[path] = ParseEffectNode(
                await _fs.ReadFileAsync(path),
                path,
                issues);
        return result;
    }

    private async Task<EffectCarrierCatalogInput> ReadEffectCarriersAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        List<ValidationIssue> issues) =>
        new(
            await ReadEffectCarrierRootAsync(EffectCarrierCatalog.PlayerPath, writeLease, issues),
            await ReadEffectCarrierRootAsync(EffectCarrierCatalog.NpcPath, writeLease, issues),
            await ReadEffectCarrierRootAsync(EffectCarrierCatalog.EnemiesPath, writeLease, issues),
            await ReadEffectCarrierRootAsync(EffectCarrierCatalog.AlliesPath, writeLease, issues),
            await ReadEffectCarrierRootAsync(EffectCarrierCatalog.AfterlifeProfilesPath, writeLease, issues),
            await ReadEffectCarrierRootAsync(EffectCarrierCatalog.SpiritualConflictPath, writeLease, issues));

    private async Task<JsonObject?> ReadEffectCarrierRootAsync(
        string path,
        FileSystemManager.CanonicalWriteLease? writeLease,
        List<ValidationIssue> issues) =>
        ParseEffectObjectRoot(
            await ReadEffectFileAsync(path, writeLease),
            path,
            "effect_materialization_invalid_carrier_root",
            issues);

    private Task<string?> ReadEffectFileAsync(
        string path,
        FileSystemManager.CanonicalWriteLease? writeLease) =>
        writeLease == null
            ? _fs.ReadFileAsync(path)
            : _fs.ReadFileAsync(writeLease, path);

    private static void ValidateEffectOwnedContinuity(
        EffectCarrierCatalogInput before,
        EffectCarrierCatalogInput current,
        string? beforeIndexJson,
        string? currentIndexJson,
        List<ValidationIssue> issues)
    {
        ValidateEffectCarrierContinuity(
            EffectCarrierCatalog.PlayerPath,
            before.PlayerEffects,
            current.PlayerEffects,
            EffectCarrierKind.Player,
            issues);
        ValidateEffectCarrierContinuity(
            EffectCarrierCatalog.NpcPath,
            before.NpcEffects,
            current.NpcEffects,
            EffectCarrierKind.Npc,
            issues);
        ValidateEffectCarrierContinuity(
            EffectCarrierCatalog.EnemiesPath,
            before.EnemyCombatants,
            current.EnemyCombatants,
            EffectCarrierKind.Combatant,
            issues);
        ValidateEffectCarrierContinuity(
            EffectCarrierCatalog.AlliesPath,
            before.AllyCombatants,
            current.AllyCombatants,
            EffectCarrierKind.Combatant,
            issues);
        ValidateEffectCarrierContinuity(
            EffectCarrierCatalog.AfterlifeProfilesPath,
            before.AfterlifeProfiles,
            current.AfterlifeProfiles,
            EffectCarrierKind.AfterlifeProfile,
            issues);
        ValidateEffectCarrierContinuity(
            EffectCarrierCatalog.SpiritualConflictPath,
            before.SpiritualConflict,
            current.SpiritualConflict,
            EffectCarrierKind.SpiritualConflict,
            issues);

        var beforeIndex = ParseNode(beforeIndexJson);
        var currentIndex = ParseNode(currentIndexJson);
        if (!JsonNode.DeepEquals(beforeIndex, currentIndex))
        {
            issues.Add(NewEffectIssue(
                EffectAcceptedTurnPlan.IdentityIndexPath,
                "effect_identity_direct_mutation",
                "client-owned effect identity index unchanged before normalization",
                currentIndex?.ToJsonString() ?? "missing"));
        }
    }

    private static void ValidateEffectCarrierContinuity(
        string path,
        JsonObject? before,
        JsonObject? current,
        EffectCarrierKind kind,
        List<ValidationIssue> issues)
    {
        if (kind == EffectCarrierKind.Combatant)
            ValidateCombatantIdentityContinuity(path, before, current, issues);

        var beforeProjection = ProjectEffectOwnedState(before, kind);
        var currentProjection = ProjectEffectOwnedState(current, kind);
        if (JsonNode.DeepEquals(beforeProjection, currentProjection))
            return;

        issues.Add(NewEffectIssue(
            path,
            "effect_materialization_direct_carrier_mutation",
            "effect-owned collections unchanged before accepted effect planning",
            currentProjection?.ToJsonString() ?? "missing"));
    }

    private static void ValidateCombatantIdentityContinuity(
        string path,
        JsonObject? before,
        JsonObject? current,
        List<ValidationIssue> issues)
    {
        var beforeProjection = ProjectCombatantIdentities(before);
        var currentProjection = ProjectCombatantIdentities(current);
        if (JsonNode.DeepEquals(beforeProjection, currentProjection))
            return;

        issues.Add(NewEffectIssue(
            path,
            "effect_target_combatant_identity_direct_mutation",
            "client-owned combatantId identities and exact NPC bindings unchanged before accepted effect planning",
            currentProjection.ToJsonString()));
    }

    private static JsonArray ProjectCombatantIdentities(JsonObject? root)
    {
        if (root == null)
            return new JsonArray();
        var collection = root.ContainsKey("enemiesData")
            ? "enemiesData"
            : "alliesData";
        var identities = (root[collection] as JsonArray)?
            .OfType<JsonObject>()
            .Where(static combatant => combatant.ContainsKey("combatantId"))
            .Select(combatant => new JsonObject
            {
                ["combatantId"] = combatant["combatantId"]?.DeepClone(),
                ["NPCId"] = combatant["NPCId"]?.DeepClone()
            })
            .OrderBy(
                static identity => identity["combatantId"]?.ToJsonString(),
                StringComparer.Ordinal)
            .ThenBy(
                static identity => identity["NPCId"]?.ToJsonString(),
                StringComparer.Ordinal)
            .Select(static identity => (JsonNode?)identity)
            .ToArray() ?? Array.Empty<JsonNode?>();
        return new JsonArray(identities);
    }

    private static JsonNode? ProjectEffectOwnedState(JsonObject? root, EffectCarrierKind kind)
    {
        var projected = new JsonArray();
        if (root == null)
            return projected;
        switch (kind)
        {
            case EffectCarrierKind.Player:
                AddProjectedCollection(
                    projected,
                    "player_current",
                    "activeEffects",
                    root["activeEffects"] as JsonArray);
                return SortProjectedCollections(projected);
            case EffectCarrierKind.Npc:
                foreach (var entry in (root["entries"] as JsonArray)?.OfType<JsonObject>() ??
                         Enumerable.Empty<JsonObject>())
                {
                    AddProjectedCollection(
                        projected,
                        ReadExact(entry["NPCId"]) ?? "invalid",
                        "activeEffects",
                        entry["activeEffects"] as JsonArray);
                }
                return SortProjectedCollections(projected);
            case EffectCarrierKind.Combatant:
                var collection = root.ContainsKey("enemiesData") ? "enemiesData" : "alliesData";
                foreach (var entry in (root[collection] as JsonArray)?.OfType<JsonObject>() ??
                         Enumerable.Empty<JsonObject>())
                {
                    AddProjectedCombatOwner(projected, entry);
                    if (entry["isGroup"] is not JsonValue groupNode ||
                        !groupNode.TryGetValue<bool>(out var isGroup) ||
                        !isGroup ||
                        entry["members"] is not JsonArray members)
                    {
                        continue;
                    }
                    foreach (var member in members.OfType<JsonObject>())
                        AddProjectedCombatOwner(projected, member);
                }
                return SortProjectedCollections(projected);
            case EffectCarrierKind.AfterlifeProfile:
                foreach (var entry in (root["profiles"] as JsonArray)?.OfType<JsonObject>() ??
                         Enumerable.Empty<JsonObject>())
                {
                    AddProjectedCollection(
                        projected,
                        ReadExact(entry["actorId"]) ?? "invalid",
                        "activeEffects",
                        entry["activeEffects"] as JsonArray);
                }
                return SortProjectedCollections(projected);
            case EffectCarrierKind.SpiritualConflict:
                if (root["activeConflict"] is JsonObject conflict)
                {
                    AddProjectedCollection(
                        projected,
                        ReadExact(conflict["conflictId"]) ?? "invalid",
                        "combatConditions",
                        conflict["combatConditions"] as JsonArray);
                }
                return SortProjectedCollections(projected);
            default:
                return SortProjectedCollections(projected);
        }
    }

    private static void AddProjectedCombatOwner(
        JsonArray projected,
        JsonObject owner)
    {
        var ownerId = ReadExact(owner["memberId"]) ??
                      ReadExact(owner["combatantId"]) ??
                      "invalid";
        AddProjectedCollection(
            projected,
            ownerId,
            "activeBuffs",
            owner["activeBuffs"] as JsonArray);
        AddProjectedCollection(
            projected,
            ownerId,
            "activeDebuffs",
            owner["activeDebuffs"] as JsonArray);
    }

    private static JsonArray SortProjectedCollections(JsonArray projected) =>
        new(projected
            .OfType<JsonObject>()
            .OrderBy(
                static entry => ReadExact(entry["ownerId"]),
                StringComparer.Ordinal)
            .ThenBy(
                static entry => ReadExact(entry["collection"]),
                StringComparer.Ordinal)
            .Select(static entry => (JsonNode?)entry.DeepClone())
            .ToArray());

    private static void AddProjectedCollection(
        JsonArray projected,
        string ownerId,
        string collection,
        JsonArray? effects)
    {
        if (effects is not { Count: > 0 })
            return;
        projected.Add(new JsonObject
        {
            ["ownerId"] = ownerId,
            ["collection"] = collection,
            ["effects"] = effects.DeepClone()
        });
    }

    internal static void ValidateEffectCarrierIndexAgreement(
        EffectCarrierCatalog catalog,
        EffectIdentityState identity,
        List<ValidationIssue> issues)
    {
        var indexed = identity.Entries.ToDictionary(entry => entry.EffectId, StringComparer.Ordinal);
        foreach (var occurrence in catalog.Occurrences)
        {
            if (!indexed.TryGetValue(occurrence.EffectId, out var entry))
            {
                issues.Add(NewEffectIssue(
                    occurrence.JsonPath,
                    "effect_materialization_index_entry_missing",
                    "one exact identity-index entry for every active carrier occurrence",
                    occurrence.EffectId));
                continue;
            }
            ValidateEffectOccurrenceAgreement(occurrence, entry, issues);
        }

        foreach (var entry in identity.Entries)
        {
            var hasOccurrence = catalog.TryResolveOne(entry.EffectId, out _);
            var isLive = entry.State is "active" or "suspended";
            if (isLive != hasOccurrence)
            {
                issues.Add(NewEffectIssue(
                    EffectAcceptedTurnPlan.IdentityIndexPath + ".entries",
                    isLive
                        ? "effect_materialization_active_carrier_missing"
                        : "effect_materialization_terminal_carrier_present",
                    isLive
                        ? "one logical active carrier occurrence"
                        : "no carrier occurrence for terminal identity",
                    entry.EffectId));
            }
        }
    }

    internal static void ValidateCanonicalEffectBindings(
        EffectCarrierCatalog catalog,
        EffectSourceAuthority sourceAuthority,
        EffectTargetAuthority targetAuthority,
        List<ValidationIssue> issues)
    {
        foreach (var occurrence in catalog.Occurrences)
        {
            var effect = occurrence.Effect;
            var realm = ReadExact(effect["realm"]);
            var source = effect["source"] as JsonObject;
            var target = effect["target"] as JsonObject;
            var sourceKind = ReadExact(source?["kind"]);
            var sourceId = ReadExact(source?["sourceId"]);
            var definitionKey = ReadExact(source?["definitionKey"]);
            var targetKind = ReadExact(target?["kind"]);
            if (realm == null ||
                source == null ||
                target == null ||
                sourceKind == null ||
                sourceId == null ||
                definitionKey == null ||
                targetKind == null)
            {
                continue;
            }

            var sourceResolution = sourceAuthority.ResolveCanonicalBinding(
                new EffectSourceKey(
                    realm,
                    sourceKind,
                    sourceId,
                    definitionKey),
                targetKind);
            AddCanonicalBindingIssues(
                sourceResolution.Issues,
                occurrence.JsonPath + ".source",
                issues);
            if (sourceResolution.Success)
            {
                ValidateCanonicalSourceDefinitionAgreement(
                    occurrence,
                    sourceResolution.Source!,
                    sourceAuthority,
                    issues);
            }

            var targetResolution = targetAuthority.Resolve(target, realm);
            AddCanonicalBindingIssues(
                targetResolution.Issues,
                occurrence.JsonPath + ".target",
                issues);
        }
    }

    private static void ValidateCanonicalSourceDefinitionAgreement(
        EffectCarrierOccurrence occurrence,
        EffectSourceAuthorityEntry source,
        EffectSourceAuthority sourceAuthority,
        List<ValidationIssue> issues)
    {
        var effect = occurrence.Effect;
        var definition = source.Definition;
        var displayMatches = MatchObjectIgnoringFields(
            effect["display"],
            definition["display"],
            "sourceLabel");
        AddSourceDefinitionMismatchIfNeeded(
            displayMatches,
            occurrence.JsonPath + ".display",
            definition["display"],
            effect["display"],
            issues);

        var componentsMatch = TryMatchSourceComponents(
            effect["components"],
            definition,
            out var inferredParameters);
        AddSourceDefinitionMismatchIfNeeded(
            componentsMatch,
            occurrence.JsonPath + ".components",
            definition["components"],
            effect["components"],
            issues);
        AddCanonicalBindingIssues(
            sourceAuthority.ValidateCanonicalParameters(source, inferredParameters),
            occurrence.JsonPath + ".components",
            issues);

        AddSourceDefinitionMismatchIfNeeded(
            LifetimeMatchesSourcePolicy(effect["lifetime"], definition, source.Key),
            occurrence.JsonPath + ".lifetime",
            definition["lifetime"],
            effect["lifetime"],
            issues);
        AddSourceDefinitionMismatchIfNeeded(
            MatchStackingPolicy(effect["stacking"], definition["stacking"]),
            occurrence.JsonPath + ".stacking",
            definition["stacking"],
            effect["stacking"],
            issues);
        foreach (var section in new[] { "triggers", "removal", "links" })
        {
            AddSourceDefinitionMismatchIfNeeded(
                JsonNode.DeepEquals(effect[section], definition[section]),
                occurrence.JsonPath + "." + section,
                definition[section],
                effect[section],
                issues);
        }
    }

    private static bool MatchObjectIgnoringFields(
        JsonNode? actual,
        JsonNode? expected,
        params string[] ignoredFields)
    {
        if (actual is not JsonObject actualObject || expected is not JsonObject expectedObject)
            return false;
        var projectedActual = actualObject.DeepClone().AsObject();
        var projectedExpected = expectedObject.DeepClone().AsObject();
        foreach (var field in ignoredFields)
        {
            projectedActual.Remove(field);
            projectedExpected.Remove(field);
        }
        return JsonNode.DeepEquals(projectedActual, projectedExpected);
    }

    private static bool TryMatchSourceComponents(
        JsonNode? actualNode,
        JsonObject definition,
        out JsonObject inferredParameters)
    {
        inferredParameters = new JsonObject();
        if (actualNode is not JsonArray actual ||
            definition["components"] is not JsonArray expected ||
            actual.Count != expected.Count ||
            definition["parameterBounds"] is not JsonObject parameterBounds)
        {
            return false;
        }

        var normalizedActual = actual.DeepClone().AsArray();
        for (var index = 0; index < expected.Count; index++)
        {
            if (expected[index] is not JsonObject expectedComponent ||
                normalizedActual[index] is not JsonObject actualComponent ||
                expectedComponent["payload"] is not JsonObject expectedPayload ||
                actualComponent["payload"] is not JsonObject actualPayload)
            {
                return false;
            }

            foreach (var parameter in parameterBounds)
            {
                if (!expectedPayload.ContainsKey(parameter.Key))
                    continue;
                if (!actualPayload.TryGetPropertyValue(parameter.Key, out var actualValue))
                    return false;
                if (inferredParameters.TryGetPropertyValue(parameter.Key, out var priorValue) &&
                    !JsonNode.DeepEquals(priorValue, actualValue))
                {
                    return false;
                }
                inferredParameters[parameter.Key] = actualValue?.DeepClone();
                actualPayload[parameter.Key] = expectedPayload[parameter.Key]?.DeepClone();
            }
        }
        return JsonNode.DeepEquals(normalizedActual, expected);
    }

    private static bool LifetimeMatchesSourcePolicy(
        JsonNode? actualNode,
        JsonObject definition,
        EffectSourceKey source)
    {
        if (actualNode is not JsonObject actual ||
            definition["lifetime"] is not JsonObject policy ||
            !string.Equals(ReadExact(actual["mode"]), ReadExact(policy["mode"]), StringComparison.Ordinal))
        {
            return false;
        }

        return ReadExact(policy["mode"]) switch
        {
            "turns" => JsonNode.DeepEquals(actual["advancePhase"], policy["advancePhase"]),
            "uses" => ConsumingTriggerIdsMatch(actual, policy),
            "until_time" => true,
            "scene" => JsonNode.DeepEquals(actual["onSceneExit"], policy["onSceneExit"]),
            "source_bound" =>
                string.Equals(
                    ReadExact(actual["linkKind"]),
                    EffectSourceAuthority.CanonicalLinkKind(source.Kind),
                    StringComparison.Ordinal) &&
                string.Equals(ReadExact(actual["targetId"]), source.SourceId, StringComparison.Ordinal) &&
                JsonNode.DeepEquals(actual["activePredicate"], policy["activePredicate"]) &&
                JsonNode.DeepEquals(actual["onSourceLoss"], policy["onSourceLoss"]),
            "condition_bound" =>
                JsonNode.DeepEquals(actual["conditionKey"], policy["conditionKey"]) &&
                JsonNode.DeepEquals(actual["onConditionLoss"], policy["onConditionLoss"]),
            "permanent" => true,
            "manual" => JsonNode.DeepEquals(actual["authorities"], policy["authorities"]),
            _ => false
        };

        bool ConsumingTriggerIdsMatch(JsonObject lifetime, JsonObject lifetimePolicy)
        {
            if (lifetimePolicy["consumingEventTypes"] is not JsonArray eventTypes ||
                lifetime["consumingTriggerIds"] is not JsonArray actualIds ||
                definition["triggers"] is not JsonArray triggers)
            {
                return false;
            }
            var acceptedEventTypes = eventTypes
                .Select(ReadExact)
                .Where(static value => value != null)
                .ToHashSet(StringComparer.Ordinal);
            var expectedIds = new JsonArray(triggers
                .OfType<JsonObject>()
                .Where(trigger => acceptedEventTypes.Contains(ReadExact(trigger["eventType"])))
                .Select(trigger => trigger["triggerId"]?.DeepClone())
                .ToArray());
            return JsonNode.DeepEquals(actualIds, expectedIds);
        }
    }

    private static bool MatchStackingPolicy(JsonNode? actualNode, JsonNode? policyNode)
    {
        if (actualNode is not JsonObject actual || policyNode is not JsonObject policy)
            return false;
        var projectedActual = actual.DeepClone().AsObject();
        var projectedPolicy = policy.DeepClone().AsObject();
        projectedActual.Remove("currentStacks");
        projectedPolicy.Remove("atMaximum");
        if (string.Equals(
                ReadExact(projectedPolicy["policy"]),
                "independent",
                StringComparison.Ordinal))
        {
            projectedPolicy["maxStacks"] = 1;
        }
        return JsonNode.DeepEquals(projectedActual, projectedPolicy);
    }

    private static void AddSourceDefinitionMismatchIfNeeded(
        bool matches,
        string path,
        JsonNode? expected,
        JsonNode? actual,
        List<ValidationIssue> issues)
    {
        if (matches)
            return;
        issues.Add(NewEffectIssue(
            path,
            "effect_materialization_source_definition_mismatch",
            expected?.ToJsonString() ?? "complete resolved source definition section",
            actual?.ToJsonString() ?? "missing"));
    }

    private static void AddCanonicalBindingIssues(
        IReadOnlyList<ValidationIssue> bindingIssues,
        string path,
        List<ValidationIssue> issues)
    {
        foreach (var issue in bindingIssues)
        {
            issues.Add(NewEffectIssue(
                path,
                issue.Code ?? "effect_materialization_binding_invalid",
                issue.Expected ?? "one exact current effect authority binding",
                issue.Actual ?? "unresolved"));
        }
    }

    private static void ValidateEffectOccurrenceAgreement(
        EffectCarrierOccurrence occurrence,
        EffectIdentityEntry entry,
        List<ValidationIssue> issues)
    {
        var effect = occurrence.Effect;
        var expectedCollection = occurrence.Coordinate.Category switch
        {
            "buff" => "activeBuffs",
            "debuff" => "activeDebuffs",
            _ when occurrence.Coordinate.Kind == "spiritual_conflict" => "combatConditions",
            _ => "activeEffects"
        };
        var chronology = effect["chronology"] as JsonObject;
        var target = effect["target"] as JsonObject;
        var source = effect["source"] as JsonObject;
        var stacking = effect["stacking"] as JsonObject;
        var targetKind = ReadExact(target?["kind"]);
        var expectedOwnerKind = occurrence.Coordinate.Kind is
            "afterlife_profile" or "spiritual_conflict"
                ? targetKind
                : occurrence.Coordinate.Kind;
        var firstTransition = entry.Transitions.FirstOrDefault();
        var lastTransition = entry.Transitions.LastOrDefault();
        var agrees = string.Equals(entry.State, ReadExact(effect["state"]), StringComparison.Ordinal) &&
                     string.Equals(entry.Realm, ReadExact(effect["realm"]), StringComparison.Ordinal) &&
                     JsonNode.DeepEquals(entry.Target, target) &&
                     JsonNode.DeepEquals(entry.Source, source) &&
                     string.Equals(entry.Owner.Kind, expectedOwnerKind, StringComparison.Ordinal) &&
                     string.Equals(entry.Owner.OwnerId, occurrence.Coordinate.OwnerId, StringComparison.Ordinal) &&
                     string.Equals(entry.Owner.CarrierPath, occurrence.FilePath, StringComparison.Ordinal) &&
                     string.Equals(entry.Owner.Collection, expectedCollection, StringComparison.Ordinal) &&
                     string.Equals(entry.StackCoordinate.Realm, ReadExact(effect["realm"]), StringComparison.Ordinal) &&
                     string.Equals(entry.StackCoordinate.TargetKind, ReadExact(target?["kind"]), StringComparison.Ordinal) &&
                     string.Equals(entry.StackCoordinate.TargetId, ReadExact(target?["targetId"]), StringComparison.Ordinal) &&
                     string.Equals(entry.StackCoordinate.SourceKind, ReadExact(source?["kind"]), StringComparison.Ordinal) &&
                     string.Equals(entry.StackCoordinate.SourceId, ReadExact(source?["sourceId"]), StringComparison.Ordinal) &&
                     string.Equals(entry.StackCoordinate.StackKey, ReadExact(stacking?["stackKey"]), StringComparison.Ordinal) &&
                     entry.CreatedAtTurn == ReadInt(chronology?["createdAtTurn"]) &&
                     firstTransition != null &&
                     string.Equals(
                         firstTransition.EventRef,
                         ReadExact(chronology?["createdEventRef"]),
                         StringComparison.Ordinal) &&
                     lastTransition != null &&
                     string.Equals(
                         lastTransition.TransitionId,
                         ReadExact(chronology?["lastTransitionId"]),
                         StringComparison.Ordinal) &&
                     lastTransition.Turn == ReadInt(
                         chronology?["lastTransitionTurn"]);
        if (!agrees)
        {
            issues.Add(NewEffectIssue(
                occurrence.JsonPath,
                "effect_materialization_index_carrier_mismatch",
                "exact owner/source/target/state/chronology agreement",
                occurrence.EffectId));
        }
    }

    private static bool TryParseEffectCommands(
        string json,
        List<ValidationIssue> issues,
        out JsonObject commands)
    {
        commands = null!;
        try
        {
            using var document = JsonDocument.Parse(json);
            FindDuplicateEffectCommandProperties(
                document.RootElement,
                EffectAcceptedTurnPlan.CommandPath,
                issues);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(NewEffectIssue(
                    EffectAcceptedTurnPlan.CommandPath,
                    "effect_plan_command_invalid",
                    "closed effect command root object",
                    document.RootElement.ValueKind.ToString()));
                return false;
            }
            commands = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
            return issues.Count == 0;
        }
        catch (JsonException exception)
        {
            issues.Add(NewEffectIssue(
                EffectAcceptedTurnPlan.CommandPath,
                "effect_plan_command_invalid",
                "valid duplicate-free JSON",
                exception.Message));
            return false;
        }
    }

    private static void FindDuplicateEffectCommandProperties(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    issues.Add(NewEffectIssue(
                        path + "." + property.Name,
                        "effect_plan_duplicate_property",
                        "one occurrence of each exact command property",
                        property.Name));
                }
                FindDuplicateEffectCommandProperties(property.Value, path + "." + property.Name, issues);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
                FindDuplicateEffectCommandProperties(item, $"{path}[{index++}]", issues);
        }
    }

    private static void ValidateEffectSourceDefinitions(
        IReadOnlyDictionary<string, JsonNode?> roots,
        List<ValidationIssue> issues)
    {
        foreach (var pair in roots)
        {
            if (pair.Value == null)
                continue;
            using var document = JsonDocument.Parse(pair.Value.ToJsonString());
            ValidateEffectSourceDefinitions(
                document.RootElement,
                pair.Key,
                InferEffectSourceRealm(pair.Key),
                issues);
        }
    }

    private static void ValidateActiveEffectDefinitionsIfPresent(
        JsonElement owner,
        string path,
        string realm,
        List<ValidationIssue> issues)
    {
        if (owner.ValueKind != JsonValueKind.Object ||
            !owner.TryGetProperty("activeEffectDefinitions", out var definitions))
        {
            return;
        }

        issues.AddRange(EffectSourceDefinitionContract.ValidateArray(
            definitions,
            path + ".activeEffectDefinitions",
            realm));
    }

    private static void ValidateEffectSourceDefinitions(
        JsonElement value,
        string path,
        string realm,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var effectiveRealm = ResolveEffectDefinitionRealm(value, path, realm);
            foreach (var property in value.EnumerateObject())
            {
                var propertyPath = path + "." + property.Name;
                if (string.Equals(
                        property.Name,
                        "activeEffectDefinitions",
                        StringComparison.Ordinal))
                {
                    issues.AddRange(EffectSourceDefinitionContract.ValidateArray(
                        property.Value,
                        propertyPath,
                        effectiveRealm));
                }
                else
                {
                    ValidateEffectSourceDefinitions(
                        property.Value,
                        propertyPath,
                        effectiveRealm,
                        issues);
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                ValidateEffectSourceDefinitions(
                    item,
                    $"{path}[{index++}]",
                    realm,
                    issues);
            }
        }
    }

    private static string ResolveEffectDefinitionRealm(
        JsonElement owner,
        string path,
        string inheritedRealm)
    {
        if (!(string.Equals(
                  path,
                  EffectCarrierCatalog.AfterlifeProfilesPath,
                  StringComparison.Ordinal) ||
              path.StartsWith(
                  EffectCarrierCatalog.AfterlifeProfilesPath + ".",
                  StringComparison.Ordinal) ||
              path.StartsWith(
                  EffectCarrierCatalog.AfterlifeProfilesPath + "[",
                  StringComparison.Ordinal)) ||
            !owner.TryGetProperty("actorType", out var actorType) ||
            actorType.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(actorType.GetString()) ||
            !owner.TryGetProperty("actorId", out var actorId) ||
            actorId.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(actorId.GetString()) ||
            !owner.TryGetProperty("realm", out var realm) ||
            realm.ValueKind != JsonValueKind.String)
        {
            return inheritedRealm;
        }

        return AfterlifeEntityProfileState.TryNormalizeEffectRealm(
            realm.GetString(),
            out var normalizedRealm)
            ? normalizedRealm
            : inheritedRealm;
    }

    private static string InferEffectSourceRealm(string path) =>
        path.Contains("afterlife", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("shining", StringComparison.OrdinalIgnoreCase)
            ? "shining_abode"
            : "mortal_world";

    private static bool HasAnyEffectAuthority(
        EffectCarrierCatalogInput carriers,
        string? indexJson) =>
        carriers.PlayerEffects != null ||
        carriers.NpcEffects != null ||
        carriers.EnemyCombatants != null ||
        carriers.AllyCombatants != null ||
        carriers.AfterlifeProfiles != null ||
        carriers.SpiritualConflict != null ||
        indexJson != null;

    private static JsonObject? ParseEffectObjectRoot(
        string? json,
        string path,
        string code,
        List<ValidationIssue> issues)
    {
        if (json == null)
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (TryFindDuplicateProperty(document.RootElement, path, out var duplicatePath))
            {
                issues.Add(NewEffectIssue(
                    duplicatePath,
                    "effect_materialization_duplicate_property",
                    "every JSON property occurs exactly once",
                    "duplicate property"));
                return null;
            }
            var node = JsonNode.Parse(json);
            if (node is JsonObject root)
                return root;
            issues.Add(NewEffectIssue(
                path,
                code,
                "object root or absent pristine carrier",
                node?.ToJsonString() ?? "null"));
        }
        catch (JsonException exception)
        {
            issues.Add(NewEffectIssue(
                path,
                code,
                "valid object root or absent pristine carrier",
                exception.Message));
        }
        return null;
    }

    private static JsonNode? ParseNode(string? json)
    {
        if (json == null)
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (TryFindDuplicateProperty(document.RootElement, "$", out _))
                return null;
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonNode? ParseEffectNode(
        string? json,
        string path,
        List<ValidationIssue> issues)
    {
        if (json == null)
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (TryFindDuplicateProperty(document.RootElement, path, out var duplicatePath))
            {
                issues.Add(NewEffectIssue(
                    duplicatePath,
                    "effect_materialization_duplicate_property",
                    "every JSON property occurs exactly once",
                    "duplicate property"));
                return null;
            }
            return JsonNode.Parse(json);
        }
        catch (JsonException exception)
        {
            issues.Add(NewEffectIssue(
                path,
                "effect_materialization_invalid_source_root",
                "valid source owner JSON or absent source root",
                exception.Message));
            return null;
        }
    }

    private static bool TryFindDuplicateProperty(
        JsonElement value,
        string path,
        out string duplicatePath)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var propertyPath = $"{path}.{property.Name}";
                if (!names.Add(property.Name))
                {
                    duplicatePath = propertyPath;
                    return true;
                }
                if (TryFindDuplicateProperty(
                        property.Value,
                        propertyPath,
                        out duplicatePath))
                {
                    return true;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                if (TryFindDuplicateProperty(
                        item,
                        $"{path}[{index++}]",
                        out duplicatePath))
                {
                    return true;
                }
            }
        }
        duplicatePath = string.Empty;
        return false;
    }

    private static string? ReadExact(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) &&
        text.Length > 0 && string.Equals(text, text.Trim(), StringComparison.Ordinal)
            ? text
            : null;

    private static int ReadInt(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<int>(out var number)
            ? number
            : 0;

    private static ValidationIssue NewEffectIssue(
        string path,
        string code,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            "Active effect materialization authority failed closed.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Resubmit one complete source-authorized effect operation; do not author active carriers, identities, lifecycle state, or receipts directly.");
}
