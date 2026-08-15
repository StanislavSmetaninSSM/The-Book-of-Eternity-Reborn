using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class EffectAcceptedTurnPlanner
{
    private static readonly HashSet<string> RootFields = Set("effectChanges", "effectResolutionReceipts");
    private static readonly HashSet<string> ApplyFields = Set(
        "operation", "target", "source", "parameters", "eventRef", "reason");
    private static readonly HashSet<string> TerminalFields = Set(
        "operation", "effectId", "target", "authority", "eventRef", "reason");
    private static readonly HashSet<string> AuthorityFields = Set("kind", "authorityId");
    private static readonly HashSet<string> ClientOwnedOrLegacyFields = Set(
        "effectId", "currentStacks", "remainingTurns", "remainingUses", "deadline",
        "transitionId", "receiptId", "components", "carrierPath", "duration",
        "activeEffects", "activeBuffs", "activeDebuffs", "combatConditions",
        "effectIdentityIndex", "playerActiveEffectsChanges", "NPCEffectChanges");

    internal static EffectAcceptedTurnPlanningResult Build(
        EffectAcceptedTurnInput input,
        string fingerprint,
        EffectIdentityFactory identityFactory)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(identityFactory);
        var issues = new List<ValidationIssue>();
        issues.AddRange(input.SourceAuthority.Issues);
        if (!TryExact(input.SessionId) || !TryExact(input.SnapshotToken) || !TryExact(input.Realm))
            Add(issues, "effectAcceptedTurn", "effect_plan_input_invalid", "exact session, snapshot, and realm authority", input.SessionId + "/" + input.SnapshotToken + "/" + input.Realm);

        ValidateRoot(input.RawCommands, issues);
        var turn = 0;
        if (!TryReadPositiveInt(input.EventInput["turn"], out turn))
            Add(issues, "eventInput.turn", "effect_plan_event_authority_invalid", "positive accepted turn", Describe(input.EventInput["turn"]));
        var acceptedEvents = ParseAcceptedEvents(input.EventInput["events"], issues);
        if (issues.Count > 0)
            return Failed(issues);

        var carriers = input.PreTurnCarriers ??
            new EffectCarrierCatalogInput(null, null, null, null, null, null);
        var publicationCarrierBaselines = carriers;
        var carrierAuthorityFingerprint =
            EffectCarrierCatalog.CreateAuthorityFingerprint(publicationCarrierBaselines);
        PrepareCombatantTargets(
            input,
            carriers,
            identityFactory,
            issues,
            out var preparedCarriers,
            out var targetAuthority,
            out var combatantIds);
        issues.AddRange(targetAuthority.Issues);
        issues.AddRange(targetAuthority.ValidateNamedCombatantBindings(
            preparedCarriers));
        if (issues.Count > 0)
            return Failed(issues);

        var effectiveInput = input with
        {
            TargetAuthority = targetAuthority,
            PreTurnCarriers = preparedCarriers
        };

        carriers = effectiveInput.PreTurnCarriers!;
        var carrierCatalog = EffectCarrierCatalog.Build(carriers);
        issues.AddRange(carrierCatalog.Issues);
        var identityBeforeImage = input.PreTurnIdentityIndex?.DeepClone().AsObject();
        var identityRoot = identityBeforeImage?.DeepClone().AsObject() ?? EmptyIdentityIndex();
        var identityState = ParseIdentity(identityRoot);
        issues.AddRange(identityState.Issues);
        if (issues.Count > 0)
            return Failed(issues);

        var processedEventRefs = identityState.State!.Entries
            .SelectMany(static entry => entry.Transitions)
            .Select(static transition => transition.EventRef)
            .ToHashSet(StringComparer.Ordinal);
        ParseOperations(
            effectiveInput,
            acceptedEvents,
            processedEventRefs,
            issues,
            out var applications,
            out var terminalOperations);
        if (issues.Count > 0)
            return Failed(issues);

        var workspace = new CarrierWorkspace(carriers);
        workspace.IncludeRewrittenCombatantRoots(publicationCarrierBaselines);
        var effectIds = new List<string>();
        var transitionIds = new List<string>();
        var activeEffects = new List<JsonObject>();
        var usedSources = new List<EffectSourceAuthorityEntry>();
        var usedTargets = new List<EffectTargetKey>();

        ApplyDueLifecycleEvents(
            input.EventInput,
            input.SourceAuthority,
            workspace,
            identityRoot,
            identityFactory,
            transitionIds,
            activeEffects,
            processedEventRefs,
            issues,
            boundContinuationsOnly: true);
        if (issues.Count > 0)
            return Failed(issues);

        foreach (var operation in terminalOperations)
        {
            ApplyTerminalOperation(
                operation,
                workspace,
                identityState.State,
                identityRoot,
                identityFactory,
                turn,
                transitionIds,
                processedEventRefs,
                issues);
        }
        if (issues.Count > 0)
            return Failed(issues);

        foreach (var application in applications)
        {
            ApplyApplication(
                application,
                input.Realm,
                input.EventInput,
                workspace,
                identityRoot,
                identityFactory,
                turn,
                effectIds,
                transitionIds,
                activeEffects,
                processedEventRefs,
                issues);
            usedSources.Add(application.Source);
            usedTargets.Add(application.Target);
        }
        if (issues.Count > 0)
            return Failed(issues);

        ApplyDueLifecycleEvents(
            input.EventInput,
            input.SourceAuthority,
            workspace,
            identityRoot,
            identityFactory,
            transitionIds,
            activeEffects,
            processedEventRefs,
            issues,
            boundContinuationsOnly: false);

        ValidateAfterImages(workspace, identityRoot, activeEffects, issues);
        if (issues.Count > 0)
            return Failed(issues);

        var afterImages = workspace.AfterImages;
        var carrierBeforeImages = afterImages.Keys.ToDictionary(
            static path => path,
            path => GetCarrierRoot(publicationCarrierBaselines, path)?.DeepClone().AsObject(),
            StringComparer.Ordinal);
        var touchedPaths = afterImages.Keys
            .Append(EffectAcceptedTurnPlan.IdentityIndexPath)
            .Append(EffectAcceptedTurnPlan.CommandPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        return new EffectAcceptedTurnPlanningResult(
            new EffectAcceptedTurnPlan(
                fingerprint,
                carrierAuthorityFingerprint,
                input.SourceAuthority.CanonicalFingerprint,
                targetAuthority.CanonicalFingerprint,
                combatantIds,
                effectIds,
                transitionIds,
                usedSources.Select(static item => item.Key).ToArray(),
                usedTargets,
                usedSources
                    .DistinctBy(static entry => entry.Key)
                    .ToArray(),
                activeEffects,
                carrierBeforeImages,
                afterImages,
                identityBeforeImage,
                identityRoot,
                touchedPaths,
                new[] { EffectAcceptedTurnPlan.CommandPath }),
            Array.Empty<ValidationIssue>());
    }

    private static JsonObject? GetCarrierRoot(
        EffectCarrierCatalogInput carriers,
        string path) => path switch
    {
        EffectCarrierCatalog.PlayerPath => carriers.PlayerEffects,
        EffectCarrierCatalog.NpcPath => carriers.NpcEffects,
        EffectCarrierCatalog.EnemiesPath => carriers.EnemyCombatants,
        EffectCarrierCatalog.AlliesPath => carriers.AllyCombatants,
        EffectCarrierCatalog.AfterlifeProfilesPath => carriers.AfterlifeProfiles,
        EffectCarrierCatalog.SpiritualConflictPath => carriers.SpiritualConflict,
        _ => throw new InvalidOperationException(
            $"Unsupported effect carrier baseline path '{path}'.")
    };

    private static void PrepareCombatantTargets(
        EffectAcceptedTurnInput input,
        EffectCarrierCatalogInput carriers,
        EffectIdentityFactory identityFactory,
        List<ValidationIssue> issues,
        out EffectCarrierCatalogInput preparedCarriers,
        out EffectTargetAuthority targetAuthority,
        out IReadOnlyList<string> allocatedCombatantIds)
    {
        preparedCarriers = carriers;
        targetAuthority = input.TargetAuthority;
        allocatedCombatantIds = Array.Empty<string>();
        if (input.TargetAuthorityInput == null)
            return;

        var candidates = new JsonArray();
        var coordinates = new List<(bool Enemy, int Index)>();
        CollectNewCombatantCandidates(
            carriers.EnemyCombatants,
            "enemiesData",
            enemy: true,
            candidates,
            coordinates);
        CollectNewCombatantCandidates(
            carriers.AllyCombatants,
            "alliesData",
            enemy: false,
            candidates,
            coordinates);
        if (candidates.Count == 0)
            return;

        var identityBuild = CombatantIdentityState.BuildNew(
            candidates,
            identityFactory);
        issues.AddRange(identityBuild.Issues);
        if (identityBuild.State == null || identityBuild.Issues.Count > 0)
            return;

        var enemies = carriers.EnemyCombatants?.DeepClone().AsObject();
        var allies = carriers.AllyCombatants?.DeepClone().AsObject();
        for (var candidateIndex = 0; candidateIndex < coordinates.Count; candidateIndex++)
        {
            var coordinate = coordinates[candidateIndex];
            var root = coordinate.Enemy ? enemies : allies;
            var collection = coordinate.Enemy ? "enemiesData" : "alliesData";
            if (root?[collection] is not JsonArray combatants)
                continue;
            combatants[coordinate.Index] =
                identityBuild.RewrittenCombatants[candidateIndex]?.DeepClone();
        }

        preparedCarriers = carriers with
        {
            EnemyCombatants = enemies,
            AllyCombatants = allies
        };
        targetAuthority = EffectTargetAuthority.Build(
            input.TargetAuthorityInput with
            {
                CombatantIdentities = identityBuild.State
            });
        allocatedCombatantIds = identityBuild.State.CombatantIdsByRef
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Value)
            .ToArray();
    }

    private static void CollectNewCombatantCandidates(
        JsonObject? root,
        string collection,
        bool enemy,
        JsonArray candidates,
        List<(bool Enemy, int Index)> coordinates)
    {
        if (root?[collection] is not JsonArray combatants)
            return;
        for (var index = 0; index < combatants.Count; index++)
        {
            if (combatants[index] is not JsonObject combatant ||
                !combatant.ContainsKey("combatantRef"))
            {
                continue;
            }
            candidates.Add(combatant.DeepClone());
            coordinates.Add((enemy, index));
        }
    }

    private static void ParseOperations(
        EffectAcceptedTurnInput input,
        IReadOnlyList<EventAuthority> acceptedEvents,
        IReadOnlySet<string> processedEventRefs,
        List<ValidationIssue> issues,
        out List<Application> applications,
        out List<TerminalOperation> terminalOperations)
    {
        applications = new List<Application>();
        terminalOperations = new List<TerminalOperation>();
        var eventRefs = new HashSet<string>(StringComparer.Ordinal);
        if (input.RawCommands["effectChanges"] is not JsonArray changes)
            return;

        for (var index = 0; index < changes.Count; index++)
        {
            var path = $"effectChanges[{index}]";
            if (changes[index] is not JsonObject change)
            {
                Add(issues, path, "effect_plan_command_invalid", "closed effect operation", Describe(changes[index]));
                continue;
            }
            var issueCount = issues.Count;
            if (!TryReadExact(change["operation"], out var operation) ||
                operation is not ("apply" or "dispel" or "remove"))
            {
                Add(
                    issues,
                    path + ".operation",
                    "effect_plan_operation_unsupported",
                    "apply, dispel, or remove",
                    Describe(change["operation"]));
                continue;
            }

            var allowedFields = string.Equals(operation, "apply", StringComparison.Ordinal)
                ? ApplyFields
                : TerminalFields;
            foreach (var property in change)
            {
                if (ClientOwnedOrLegacyFields.Contains(property.Key) &&
                    !(property.Key == "effectId" &&
                      !string.Equals(operation, "apply", StringComparison.Ordinal)))
                {
                    Add(issues, path + "." + property.Key, "effect_plan_client_field_forbidden", "client-owned/post-state field absent", property.Key);
                }
                else if (!allowedFields.Contains(property.Key))
                {
                    Add(issues, path + "." + property.Key, "effect_plan_unknown_field", "registered " + operation + " field", property.Key);
                }
            }
            foreach (var field in allowedFields.Where(field =>
                         !(field == "parameters" &&
                           string.Equals(operation, "apply", StringComparison.Ordinal))))
            {
                if (!change.ContainsKey(field))
                    Add(issues, path + "." + field, "effect_plan_command_invalid", "required " + operation + " field", "missing");
            }
            if (!TryReadReadable(change["reason"], out _))
                Add(issues, path + ".reason", "effect_plan_command_invalid", "non-empty readable reason", Describe(change["reason"]));

            var acceptedEventRef = string.Empty;
            if (TryResolveEventRef(
                    change["eventRef"],
                    index < acceptedEvents.Count ? acceptedEvents[index] : null,
                    path + ".eventRef",
                    issues,
                    out acceptedEventRef))
            {
                var eventAlias = MortalLocationIdentityState.BuildConfusableKey(
                    acceptedEventRef);
                if (!eventRefs.Add(eventAlias))
                {
                    Add(
                        issues,
                        path + ".eventRef",
                        "effect_plan_event_replay_conflict",
                        "one effect operation for the exact accepted event",
                        acceptedEventRef);
                }
                if (processedEventRefs.Any(processed => string.Equals(
                        MortalLocationIdentityState.BuildConfusableKey(processed),
                        eventAlias,
                        StringComparison.Ordinal)))
                {
                    Add(
                        issues,
                        path + ".eventRef",
                        "effect_lifecycle_event_replay",
                        "accepted event absent from immutable effect history",
                        acceptedEventRef);
                }
            }

            if (change["target"] is not JsonObject target ||
                !TryReadExact(target["kind"], out var targetKind))
            {
                Add(issues, path + ".target", "effect_plan_command_invalid", "closed exact target selector", Describe(change["target"]));
                continue;
            }
            var targetResolution = input.TargetAuthority.Resolve(target, input.Realm);
            issues.AddRange(targetResolution.Issues.Select(issue => Prefix(issue, path + ".target")));

            if (string.Equals(operation, "apply", StringComparison.Ordinal))
            {
                ParseApplication(
                    input,
                    change,
                    targetKind,
                    targetResolution,
                    acceptedEventRef,
                    path,
                    issueCount,
                    issues,
                    applications);
                continue;
            }

            if (!TryReadExact(change["effectId"], out var effectId))
            {
                Add(issues, path + ".effectId", "effect_plan_command_invalid", "exact active effectId", Describe(change["effectId"]));
            }
            var authorityKind = string.Empty;
            var authorityId = string.Empty;
            if (change["authority"] is not JsonObject authority ||
                !HasOnly(authority, AuthorityFields.ToArray()) ||
                !TryReadExact(authority["kind"], out authorityKind) ||
                !TryReadExact(authority["authorityId"], out authorityId))
            {
                Add(issues, path + ".authority", "effect_plan_command_invalid", "closed exact removal authority", Describe(change["authority"]));
            }
            if (issues.Count == issueCount && targetResolution.Success)
            {
                terminalOperations.Add(new TerminalOperation(
                    operation,
                    effectId,
                    targetResolution.Target!,
                    authorityKind,
                    authorityId,
                    acceptedEventRef));
            }
        }
    }

    private static void ParseApplication(
        EffectAcceptedTurnInput input,
        JsonObject change,
        string targetKind,
        EffectTargetResolution targetResolution,
        string acceptedEventRef,
        string path,
        int issueCount,
        List<ValidationIssue> issues,
        List<Application> applications)
    {
        JsonObject? parameters = null;
        if (change.ContainsKey("parameters") && change["parameters"] != null)
        {
            if (change["parameters"] is JsonObject parameterObject)
                parameters = parameterObject;
            else
                Add(issues, path + ".parameters", "effect_plan_command_invalid", "closed object or null", Describe(change["parameters"]));
        }
        if (change["source"] is not JsonObject source ||
            !TryReadExact(source["kind"], out _) ||
            !TryReadExact(source["definitionKey"], out _))
        {
            Add(issues, path + ".source", "effect_plan_command_invalid", "closed exact source selector", Describe(change["source"]));
            return;
        }
        var sourceResolution = input.SourceAuthority.Resolve(
            source,
            input.Realm,
            targetKind,
            parameters);
        issues.AddRange(sourceResolution.Issues.Select(issue => Prefix(issue, path + ".source")));
        if (issues.Count == issueCount && sourceResolution.Success && targetResolution.Success)
        {
            applications.Add(new Application(
                sourceResolution.Source!,
                targetResolution.Target!,
                parameters?.DeepClone().AsObject(),
                acceptedEventRef));
        }
    }

    private static void ApplyTerminalOperation(
        TerminalOperation operation,
        CarrierWorkspace workspace,
        EffectIdentityState identityState,
        JsonObject identityRoot,
        EffectIdentityFactory identityFactory,
        int turn,
        List<string> transitionIds,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues)
    {
        var catalog = EffectCarrierCatalog.Build(workspace.ToInput());
        issues.AddRange(catalog.Issues);
        if (!catalog.TryResolveOne(operation.EffectId, out var occurrence) ||
            !identityState.TryGetEntry(operation.EffectId, out var identity))
        {
            Add(
                issues,
                "effectChanges.effectId",
                "effect_lifecycle_terminal_effect_unresolved",
                "one exact active/suspended carrier and identity entry",
                operation.EffectId);
            return;
        }
        var targetKind = string.Empty;
        var targetId = string.Empty;
        if (occurrence.Effect["target"] is not JsonObject target ||
            !TryReadExact(target["kind"], out targetKind) ||
            !TryReadExact(target["targetId"], out targetId) ||
            !string.Equals(targetKind, operation.Target.Kind, StringComparison.Ordinal) ||
            !string.Equals(targetId, operation.Target.TargetId, StringComparison.Ordinal) ||
            !string.Equals(identity.Target["kind"]?.GetValue<string>(), operation.Target.Kind, StringComparison.Ordinal) ||
            !string.Equals(identity.Target["targetId"]?.GetValue<string>(), operation.Target.TargetId, StringComparison.Ordinal))
        {
            Add(
                issues,
                "effectChanges.target",
                "effect_lifecycle_terminal_target_mismatch",
                $"exact effect owner {targetKind}:{targetId}",
                $"{operation.Target.Kind}:{operation.Target.TargetId}");
            return;
        }
        if (identity.State is not ("active" or "suspended") ||
            !IsTerminalAuthorityAllowed(
                occurrence.Effect,
                operation.Operation,
                operation.AuthorityKind))
        {
            Add(
                issues,
                "effectChanges.authority",
                "effect_lifecycle_terminal_authority_forbidden",
                "exact source-declared dispel, cure, or manual authority for an active effect",
                operation.AuthorityKind + ":" + operation.AuthorityId);
            return;
        }
        if (!workspace.TryRemoveEffect(operation.EffectId, out _))
        {
            Add(
                issues,
                "effectChanges.effectId",
                "effect_lifecycle_terminal_effect_unresolved",
                "one mutable active carrier occurrence",
                operation.EffectId);
            return;
        }

        var transitionId = identityFactory.CreateTransitionId();
        transitionIds.Add(transitionId);
        AppendIdentityTransition(
            identityRoot,
            operation.EffectId,
            operation.Operation == "dispel" ? "dispelled" : "removed",
            CreateTransition(
                transitionId,
                operation.Operation,
                turn,
                operation.EventRef,
                new[] { operation.EffectId },
                Array.Empty<string>()),
            issues);
        processedEventRefs.Add(operation.EventRef);
    }

    private static bool IsTerminalAuthorityAllowed(
        JsonObject effect,
        string operation,
        string authorityKind)
    {
        if (effect["removal"] is not JsonObject removal)
            return false;
        var fields = string.Equals(operation, "dispel", StringComparison.Ordinal)
            ? new[] { "dispelCategories" }
            : new[] { "cureKinds", "manualAuthorities" };
        if (fields.Any(field => removal[field] is JsonArray values &&
            values.OfType<JsonValue>().Any(value =>
                value.TryGetValue<string>(out var candidate) &&
                string.Equals(candidate, authorityKind, StringComparison.Ordinal))))
        {
            return true;
        }
        return string.Equals(operation, "remove", StringComparison.Ordinal) &&
            effect["lifetime"] is JsonObject lifetime &&
            string.Equals(
                lifetime["mode"]?.GetValue<string>(),
                "manual",
                StringComparison.Ordinal) &&
            lifetime["authorities"] is JsonArray authorities &&
            authorities.OfType<JsonValue>().Any(value =>
                value.TryGetValue<string>(out var candidate) &&
                string.Equals(candidate, authorityKind, StringComparison.Ordinal));
    }

    private static void ApplyApplication(
        Application application,
        string realm,
        JsonObject eventInput,
        CarrierWorkspace workspace,
        JsonObject identityRoot,
        EffectIdentityFactory identityFactory,
        int turn,
        List<string> effectIds,
        List<string> transitionIds,
        List<JsonObject> activeEffects,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues)
    {
        var definition = application.Source.Definition;
        if (definition["display"] is not JsonObject display ||
            !TryReadExact(display["category"], out var category) ||
            definition["stacking"] is not JsonObject sourceStacking ||
            !TryReadExact(sourceStacking["stackKey"], out var stackKey) ||
            !workspace.TryLocate(application.Target, category, issues, out var slot) ||
            !TryBuildLifetime(
                definition,
                application.Source.Key,
                eventInput,
                issues,
                out var lifetime))
        {
            return;
        }

        var components = definition["components"]!.DeepClone().AsArray();
        BindParameters(components, application.Parameters);
        var existing = workspace.FindStackEffects(
            realm,
            application.Target,
            application.Source.Key,
            stackKey);
        var resolution = EffectLifecycleScheduler.ResolveApplication(
            new EffectStackApplicationInput(
                existing,
                definition,
                components,
                lifetime,
                application.EventRef,
                processedEventRefs));
        issues.AddRange(resolution.Issues);
        if (!resolution.Success)
            return;

        if (resolution.TerminatesExisting)
        {
            if (!TryExact(resolution.ExistingEffectId ?? string.Empty) ||
                !workspace.TryRemoveEffect(resolution.ExistingEffectId!, out _))
            {
                Add(
                    issues,
                    "activeEffects",
                    "effect_lifecycle_replace_target_unresolved",
                    "one exact existing stack-coordinate effect",
                    resolution.ExistingEffectId ?? "missing");
                return;
            }
        }

        if (resolution.CreatesNewIdentity)
        {
            var effectId = identityFactory.CreateEffectId();
            effectIds.Add(effectId);
            if (resolution.TerminatesExisting)
            {
                var replaceTransitionId = identityFactory.CreateTransitionId();
                transitionIds.Add(replaceTransitionId);
                AppendIdentityTransition(
                    identityRoot,
                    resolution.ExistingEffectId!,
                    "replaced",
                    CreateTransition(
                        replaceTransitionId,
                        "replace",
                        turn,
                        application.EventRef,
                        new[] { resolution.ExistingEffectId! },
                        new[] { effectId }),
                    issues);
            }

            var createTransitionId = identityFactory.CreateTransitionId();
            transitionIds.Add(createTransitionId);
            var createEventRef = resolution.TerminatesExisting
                ? application.EventRef + ":replacement:" + effectId
                : application.EventRef;
            var candidate = new PreparedApplication(application, slot, resolution.NewEffectLifetime);
            var effect = CreateEffect(
                candidate,
                realm,
                effectId,
                createTransitionId,
                turn,
                createEventRef,
                resolution.NewEffectComponents,
                resolution.NewEffectStacking);
            slot.Collection.Add(effect.DeepClone());
            workspace.Touch(slot);
            activeEffects.Add(effect);
            identityRoot["entries"]!.AsArray().Add(CreateIdentityEntry(
                effect,
                slot,
                effectId,
                createTransitionId,
                turn,
                createEventRef));
            processedEventRefs.Add(application.EventRef);
            return;
        }

        if (!TryExact(resolution.ExistingEffectId ?? string.Empty) ||
            resolution.UpdatedExistingEffect is not JsonObject updated)
        {
            Add(
                issues,
                "activeEffects",
                "effect_lifecycle_stack_result_invalid",
                "one updated existing effect for non-create stack result",
                resolution.Outcome);
            return;
        }

        var transitionKind = resolution.Outcome == "no_change"
            ? "stack"
            : resolution.Outcome;
        var transitionId = identityFactory.CreateTransitionId();
        transitionIds.Add(transitionId);
        UpdateEffectChronology(updated, transitionId, turn);
        if (!workspace.TryReplaceEffect(resolution.ExistingEffectId!, updated))
        {
            Add(
                issues,
                "activeEffects",
                "effect_lifecycle_stack_target_unresolved",
                "one exact mutable existing effect",
                resolution.ExistingEffectId!);
            return;
        }
        AppendIdentityTransition(
            identityRoot,
            resolution.ExistingEffectId!,
            updated["state"]!.GetValue<string>(),
            CreateTransition(
                transitionId,
                transitionKind,
                turn,
                application.EventRef,
                new[] { resolution.ExistingEffectId! },
                new[] { resolution.ExistingEffectId! }),
            issues);
        AddOrReplaceAffected(activeEffects, updated);
        processedEventRefs.Add(application.EventRef);
    }

    private static void ApplyDueLifecycleEvents(
        JsonObject eventInput,
        EffectSourceAuthority sourceAuthority,
        CarrierWorkspace workspace,
        JsonObject identityRoot,
        EffectIdentityFactory identityFactory,
        List<string> transitionIds,
        List<JsonObject> activeEffects,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues,
        bool boundContinuationsOnly)
    {
        if (eventInput["lifecycleEvents"] is not JsonArray lifecycleEvents)
            return;
        foreach (var node in lifecycleEvents)
        {
            if (!TryParseLifecycleAuthority(node, issues, out var authority))
                continue;
            var occurrences = EffectCarrierCatalog.Build(workspace.ToInput()).Occurrences
                .Where(occurrence => occurrence.Effect["target"] is JsonObject target &&
                    string.Equals(
                        target["kind"]?.GetValue<string>(),
                        authority.Target.Kind,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        target["targetId"]?.GetValue<string>(),
                        authority.Target.TargetId,
                        StringComparison.Ordinal))
                .OrderBy(static occurrence => occurrence.EffectId, StringComparer.Ordinal)
                .ToArray();
            foreach (var occurrence in occurrences)
            {
                var mode = occurrence.Effect["lifetime"]?["mode"]?.GetValue<string>();
                var isBoundContinuation = mode is "source_bound" or "condition_bound";
                if (boundContinuationsOnly != isBoundContinuation)
                    continue;
                var eventRef = authority.EventRef + ":" + occurrence.EffectId;
                var sourceSatisfied = authority.SourceSatisfied;
                if (mode == "source_bound" && !sourceSatisfied.HasValue)
                {
                    sourceSatisfied = IsSourceBindingSatisfied(
                        occurrence.Effect,
                        sourceAuthority);
                }
                var reduction = EffectLifecycleScheduler.AdvanceLifetime(
                    new EffectLifetimeReductionInput(
                        occurrence.Effect,
                        new EffectLifecycleEvent(
                            eventRef,
                            authority.Turn,
                            authority.Phase,
                            authority.TriggerId,
                            authority.CurrentTime,
                            authority.CurrentSceneId,
                            authority.SceneClosed,
                            sourceSatisfied,
                            authority.ConditionSatisfied,
                            authority.CurrentRealm),
                        processedEventRefs));
                issues.AddRange(reduction.Issues);
                if (!reduction.Success || reduction.Outcome == "no_change")
                    continue;

                var transitionId = identityFactory.CreateTransitionId();
                transitionIds.Add(transitionId);
                if (reduction.Outcome == "expire")
                {
                    if (!workspace.TryRemoveEffect(occurrence.EffectId, out _))
                    {
                        Add(
                            issues,
                            "activeEffects",
                            "effect_lifecycle_expiry_target_unresolved",
                            "one exact mutable active effect",
                            occurrence.EffectId);
                        continue;
                    }
                    AppendIdentityTransition(
                        identityRoot,
                        occurrence.EffectId,
                        "expired",
                        CreateTransition(
                            transitionId,
                            "expire",
                            authority.Turn,
                            eventRef,
                            new[] { occurrence.EffectId },
                            Array.Empty<string>()),
                        issues);
                    RemoveAffected(activeEffects, occurrence.EffectId);
                }
                else if (reduction.UpdatedEffect is JsonObject updated)
                {
                    UpdateEffectChronology(updated, transitionId, authority.Turn);
                    workspace.TryReplaceEffect(occurrence.EffectId, updated);
                    var transitionKind = reduction.Outcome == "advance"
                        ? "consume"
                        : reduction.Outcome;
                    AppendIdentityTransition(
                        identityRoot,
                        occurrence.EffectId,
                        updated["state"]!.GetValue<string>(),
                        CreateTransition(
                            transitionId,
                            transitionKind,
                            authority.Turn,
                            eventRef,
                            new[] { occurrence.EffectId },
                            new[] { occurrence.EffectId }),
                        issues);
                    AddOrReplaceAffected(activeEffects, updated);
                }
                processedEventRefs.Add(eventRef);
            }
        }
    }

    private static bool IsSourceBindingSatisfied(
        JsonObject effect,
        EffectSourceAuthority sourceAuthority)
    {
        if (effect["source"] is not JsonObject source ||
            effect["target"] is not JsonObject target ||
            effect["lifetime"] is not JsonObject lifetime ||
            !TryReadExact(effect["realm"], out var realm) ||
            !TryReadExact(source["kind"], out var sourceKind) ||
            !TryReadExact(source["sourceId"], out var sourceId) ||
            !TryReadExact(source["definitionKey"], out var definitionKey) ||
            !TryReadExact(target["kind"], out var targetKind) ||
            !TryReadExact(lifetime["activePredicate"], out var predicate))
        {
            return false;
        }
        var resolution = sourceAuthority.ResolveCanonicalBinding(
            new EffectSourceKey(realm, sourceKind, sourceId, definitionKey),
            targetKind);
        return resolution.Source is { Active: true, Materializable: true } binding &&
            (string.Equals(predicate, "active", StringComparison.Ordinal) ||
             binding.SatisfiedPredicates.Contains(predicate));
    }

    private static bool TryParseLifecycleAuthority(
        JsonNode? node,
        List<ValidationIssue> issues,
        out LifecycleAuthority authority)
    {
        authority = null!;
        if (node is not JsonObject value ||
            !TryReadExact(value["eventRef"], out var eventRef) ||
            !TryReadPositiveInt(value["turn"], out var turn) ||
            !TryReadExact(value["phase"], out var phase) ||
            value["target"] is not JsonObject target ||
            !TryReadExact(target["kind"], out var targetKind) ||
            !TryReadExact(target["targetId"], out var targetId))
        {
            Add(
                issues,
                "eventInput.lifecycleEvents",
                "effect_lifecycle_event_invalid",
                "closed client-derived lifecycle event authority",
                Describe(node));
            return false;
        }
        authority = new LifecycleAuthority(
            eventRef,
            turn,
            phase,
            new EffectTargetKey("mortal_world", targetKind, targetId),
            ReadOptionalExact(value["triggerId"]),
            TryReadNonNegativeLong(value["currentTime"], out var currentTime)
                ? currentTime
                : null,
            ReadOptionalExact(value["currentSceneId"]),
            value["sceneClosed"]?.GetValue<bool>() == true,
            value["sourceSatisfied"]?.GetValue<bool>(),
            value["conditionSatisfied"]?.GetValue<bool>(),
            ReadOptionalExact(value["currentRealm"]));
        return true;
    }

    private static void AppendIdentityTransition(
        JsonObject identityRoot,
        string effectId,
        string state,
        JsonObject transition,
        List<ValidationIssue> issues)
    {
        var matches = identityRoot["entries"]!.AsArray()
            .OfType<JsonObject>()
            .Where(entry => string.Equals(
                entry["effectId"]?.GetValue<string>(),
                effectId,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 || matches[0]["transitions"] is not JsonArray transitions)
        {
            Add(
                issues,
                EffectAcceptedTurnPlan.IdentityIndexPath,
                "effect_lifecycle_identity_unresolved",
                "one exact identity entry with transition history",
                effectId);
            return;
        }
        matches[0]["state"] = state;
        transitions.Add(transition);
    }

    private static JsonObject CreateTransition(
        string transitionId,
        string kind,
        int turn,
        string eventRef,
        IReadOnlyList<string> sourceEffectIds,
        IReadOnlyList<string> resultEffectIds) =>
        new()
        {
            ["transitionId"] = transitionId,
            ["kind"] = kind,
            ["turn"] = turn,
            ["eventRef"] = eventRef,
            ["sourceEffectIds"] = new JsonArray(sourceEffectIds
                .Select(static value => (JsonNode)value)
                .ToArray()),
            ["resultEffectIds"] = new JsonArray(resultEffectIds
                .Select(static value => (JsonNode)value)
                .ToArray()),
            ["receiptId"] = null
        };

    private static void UpdateEffectChronology(
        JsonObject effect,
        string transitionId,
        int turn)
    {
        var chronology = effect["chronology"]!.AsObject();
        chronology["lastTransitionId"] = transitionId;
        chronology["lastTransitionTurn"] = turn;
    }

    private static void AddOrReplaceAffected(
        List<JsonObject> activeEffects,
        JsonObject effect)
    {
        var effectId = effect["effectId"]!.GetValue<string>();
        RemoveAffected(activeEffects, effectId);
        activeEffects.Add(effect.DeepClone().AsObject());
    }

    private static void RemoveAffected(
        List<JsonObject> activeEffects,
        string effectId) =>
        activeEffects.RemoveAll(effect => string.Equals(
            effect["effectId"]?.GetValue<string>(),
            effectId,
            StringComparison.Ordinal));

    private static JsonObject CreateEffect(
        PreparedApplication candidate,
        string realm,
        string effectId,
        string transitionId,
        int turn,
        string eventRef,
        JsonArray components,
        JsonObject stacking)
    {
        var definition = candidate.Application.Source.Definition;
        return new JsonObject
        {
            ["schemaVersion"] = EffectMaterializationContract.SchemaVersion,
            ["entityKind"] = "active_effect",
            ["effectId"] = effectId,
            ["state"] = "active",
            ["realm"] = realm,
            ["target"] = new JsonObject
            {
                ["kind"] = candidate.Application.Target.Kind,
                ["targetId"] = candidate.Application.Target.TargetId
            },
            ["display"] = definition["display"]!.DeepClone(),
            ["source"] = new JsonObject
            {
                ["kind"] = candidate.Application.Source.Key.Kind,
                ["sourceId"] = candidate.Application.Source.Key.SourceId,
                ["definitionKey"] = candidate.Application.Source.Key.DefinitionKey
            },
            ["components"] = components.DeepClone(),
            ["lifetime"] = candidate.Lifetime.DeepClone(),
            ["stacking"] = stacking.DeepClone(),
            ["triggers"] = definition["triggers"]!.DeepClone(),
            ["removal"] = definition["removal"]!.DeepClone(),
            ["links"] = definition["links"]!.DeepClone(),
            ["chronology"] = new JsonObject
            {
                ["createdAtTurn"] = turn,
                ["createdEventRef"] = eventRef,
                ["lastTransitionId"] = transitionId,
                ["lastTransitionTurn"] = turn
            }
        };
    }

    private static JsonObject CreateIdentityEntry(
        JsonObject effect,
        CarrierSlot slot,
        string effectId,
        string transitionId,
        int turn,
        string eventRef)
    {
        var target = effect["target"]!.DeepClone().AsObject();
        var source = effect["source"]!.DeepClone().AsObject();
        return new JsonObject
        {
            ["effectId"] = effectId,
            ["state"] = "active",
            ["realm"] = effect["realm"]!.GetValue<string>(),
            ["owner"] = new JsonObject
            {
                ["kind"] = target["kind"]!.GetValue<string>(),
                ["ownerId"] = target["targetId"]!.GetValue<string>(),
                ["carrierPath"] = slot.Path,
                ["collection"] = slot.CollectionName
            },
            ["target"] = target,
            ["source"] = source.DeepClone(),
            ["stackCoordinate"] = new JsonObject
            {
                ["realm"] = effect["realm"]!.GetValue<string>(),
                ["targetKind"] = target["kind"]!.GetValue<string>(),
                ["targetId"] = target["targetId"]!.GetValue<string>(),
                ["sourceKind"] = source["kind"]!.GetValue<string>(),
                ["sourceId"] = source["sourceId"]!.GetValue<string>(),
                ["stackKey"] = effect["stacking"]!["stackKey"]!.GetValue<string>()
            },
            ["createdAtTurn"] = turn,
            ["transitions"] = new JsonArray(new JsonObject
            {
                ["transitionId"] = transitionId,
                ["kind"] = "create",
                ["turn"] = turn,
                ["eventRef"] = eventRef,
                ["sourceEffectIds"] = new JsonArray(),
                ["resultEffectIds"] = new JsonArray(effectId),
                ["receiptId"] = null
            })
        };
    }

    private static bool TryBuildLifetime(
        JsonObject definition,
        EffectSourceKey source,
        JsonObject eventInput,
        List<ValidationIssue> issues,
        out JsonObject lifetime)
    {
        lifetime = new JsonObject();
        if (definition["lifetime"] is not JsonObject policy || !TryReadExact(policy["mode"], out var mode))
        {
            Add(issues, "source.lifetime", "effect_plan_lifetime_invalid", "complete source-owned lifetime policy", Describe(definition["lifetime"]));
            return false;
        }
        lifetime["mode"] = mode;
        switch (mode)
        {
            case "turns":
                lifetime["remainingTurns"] = policy["initialTurns"]!.DeepClone();
                lifetime["advancePhase"] = policy["advancePhase"]!.DeepClone();
                return true;
            case "uses":
                lifetime["remainingUses"] = policy["initialUses"]!.DeepClone();
                var consumingTypes = policy["consumingEventTypes"]!.AsArray()
                    .Select(static item => item!.GetValue<string>())
                    .ToHashSet(StringComparer.Ordinal);
                lifetime["consumingTriggerIds"] = new JsonArray(definition["triggers"]!.AsArray()
                    .OfType<JsonObject>()
                    .Where(trigger =>
                        trigger["consumeUses"]?.GetValue<bool>() == true &&
                        consumingTypes.Contains(trigger["eventType"]!.GetValue<string>()))
                    .Select(trigger => (JsonNode)trigger["triggerId"]!.GetValue<string>())
                    .ToArray());
                return true;
            case "until_time":
                if (!TryReadPositiveInt(policy["duration"], out var duration) ||
                    !TryReadExact(policy["timeAuthority"], out var timeAuthority) ||
                    !string.Equals(
                        timeAuthority,
                        EffectSourceDefinitionContract.CanonicalWorldTimeAuthority,
                        StringComparison.Ordinal) ||
                    !TryReadNonNegativeLong(eventInput["currentTime"], out var currentTime) ||
                    !TryReadExact(eventInput["timeAuthority"], out var acceptedTimeAuthority) ||
                    !string.Equals(timeAuthority, acceptedTimeAuthority, StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        "eventInput.currentTime",
                        "effect_plan_lifetime_context_missing",
                        "client-derived non-negative canonical world time matching the source timeAuthority",
                        Describe(eventInput["currentTime"]));
                    return false;
                }
                try
                {
                    lifetime["deadline"] = checked(currentTime + duration);
                }
                catch (OverflowException)
                {
                    Add(
                        issues,
                        "source.lifetime.duration",
                        "effect_plan_lifetime_overflow",
                        "canonical deadline within Int64 range",
                        $"currentTime={currentTime}; duration={duration}");
                    return false;
                }
                return true;
            case "scene":
                if (!TryReadExact(eventInput["sceneId"], out var sceneId))
                {
                    Add(issues, "eventInput.sceneId", "effect_plan_lifetime_context_missing", "exact accepted sceneId", Describe(eventInput["sceneId"]));
                    return false;
                }
                lifetime["sceneId"] = sceneId;
                lifetime["onSceneExit"] = policy["onSceneExit"]!.DeepClone();
                return true;
            case "source_bound":
                lifetime["linkKind"] = EffectSourceAuthority.CanonicalLinkKind(source.Kind);
                lifetime["targetId"] = source.SourceId;
                lifetime["activePredicate"] = policy["activePredicate"]!.DeepClone();
                lifetime["onSourceLoss"] = policy["onSourceLoss"]!.DeepClone();
                return true;
            case "condition_bound":
                if (eventInput["conditionOperands"] is not JsonObject operands)
                {
                    Add(issues, "eventInput.conditionOperands", "effect_plan_lifetime_context_missing", "validated condition operands", Describe(eventInput["conditionOperands"]));
                    return false;
                }
                lifetime["conditionKey"] = policy["conditionKey"]!.DeepClone();
                lifetime["operands"] = operands.DeepClone();
                lifetime["onConditionLoss"] = policy["onConditionLoss"]!.DeepClone();
                return true;
            case "permanent":
                return true;
            case "manual":
                lifetime["authorities"] = policy["authorities"]!.DeepClone();
                return true;
            default:
                Add(issues, "source.lifetime.mode", "effect_plan_lifetime_invalid", "registered lifetime mode", mode);
                return false;
        }
    }

    private static void BindParameters(JsonArray components, JsonObject? parameters)
    {
        if (parameters == null)
            return;
        foreach (var component in components.OfType<JsonObject>())
        {
            if (component["payload"] is not JsonObject payload)
                continue;
            foreach (var parameter in parameters)
            {
                if (payload.ContainsKey(parameter.Key))
                    payload[parameter.Key] = parameter.Value?.DeepClone();
            }
        }
    }

    private static void ValidateAfterImages(
        CarrierWorkspace workspace,
        JsonObject identityRoot,
        IReadOnlyList<JsonObject> activeEffects,
        List<ValidationIssue> issues)
    {
        foreach (var effect in activeEffects)
        {
            using var document = JsonDocument.Parse(effect.ToJsonString());
            issues.AddRange(EffectMaterializationContract.Validate(
                document.RootElement,
                "plannedActiveEffect",
                EffectMaterializationPhase.CanonicalActive));
        }
        issues.AddRange(EffectCarrierCatalog.Build(workspace.ToInput()).Issues);
        issues.AddRange(ParseIdentity(identityRoot).Issues);
    }

    private static void ValidateRoot(JsonObject root, List<ValidationIssue> issues)
    {
        foreach (var property in root)
        {
            if (ClientOwnedOrLegacyFields.Contains(property.Key))
                Add(issues, property.Key, "effect_plan_client_field_forbidden", "client-owned/post-state field absent", property.Key);
            else if (!RootFields.Contains(property.Key))
                Add(issues, property.Key, "effect_plan_unknown_field", "effectChanges or effectResolutionReceipts", property.Key);
        }
        if (root.ContainsKey("effectChanges") && root["effectChanges"] is not JsonArray)
            Add(issues, "effectChanges", "effect_plan_input_invalid", "effectChanges array", Describe(root["effectChanges"]));
        if (root.ContainsKey("effectResolutionReceipts") && root["effectResolutionReceipts"] is not JsonArray)
            Add(issues, "effectResolutionReceipts", "effect_plan_input_invalid", "effectResolutionReceipts array", Describe(root["effectResolutionReceipts"]));
        else if (root["effectResolutionReceipts"] is JsonArray receipts && receipts.Count > 0)
            Add(issues, "effectResolutionReceipts", "effect_plan_receipt_not_supported", "empty until bounded receipt planning is enabled", receipts.ToJsonString());
    }

    private static bool TryResolveEventRef(
        JsonNode? node,
        EventAuthority? expectedEvent,
        string path,
        List<ValidationIssue> issues,
        out string acceptedEventRef)
    {
        acceptedEventRef = string.Empty;
        if (node is not JsonObject eventRef || !HasOnly(eventRef, "kind", "authorityId") ||
            !TryReadExact(eventRef["kind"], out var kind) ||
            !TryReadExact(eventRef["authorityId"], out var authorityId))
        {
            Add(issues, path, "effect_plan_event_authority_invalid", "closed exact event authority", Describe(node));
            return false;
        }

        if (expectedEvent == null ||
            !string.Equals(kind, expectedEvent.Kind, StringComparison.Ordinal) ||
            !string.Equals(authorityId, expectedEvent.AuthorityId, StringComparison.Ordinal))
        {
            Add(
                issues,
                path,
                "effect_plan_event_authority_mismatch",
                expectedEvent == null
                    ? "accepted event authority at the same effectChanges ordinal"
                    : expectedEvent.Kind + ":" + expectedEvent.AuthorityId,
                kind + ":" + authorityId);
            return false;
        }

        acceptedEventRef = expectedEvent.EventRef;
        return true;
    }

    private static IReadOnlyList<EventAuthority> ParseAcceptedEvents(
        JsonNode? node,
        List<ValidationIssue> issues)
    {
        var result = new List<EventAuthority>();
        var authorityKeys = new HashSet<string>(StringComparer.Ordinal);
        if (node is not JsonArray events || events.Count == 0)
        {
            Add(issues, "eventInput.events", "effect_plan_event_authority_invalid", "non-empty accepted event authority array", Describe(node));
            return result;
        }

        var authorityAliases = new HashSet<string>(StringComparer.Ordinal);
        var eventRefs = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < events.Count; index++)
        {
            var path = $"eventInput.events[{index}]";
            if (events[index] is not JsonObject value ||
                !HasOnly(value, "kind", "authorityId", "eventRef") ||
                !TryReadExact(value["kind"], out var kind) ||
                !TryReadExact(value["authorityId"], out var authorityId) ||
                !TryReadExact(value["eventRef"], out var eventRef))
            {
                Add(issues, path, "effect_plan_event_authority_invalid", "closed exact accepted event authority", Describe(events[index]));
                continue;
            }

            var key = EventAuthorityKey(kind, authorityId);
            var authorityAlias = EventAuthorityKey(
                MortalLocationIdentityState.BuildConfusableKey(kind),
                MortalLocationIdentityState.BuildConfusableKey(authorityId));
            var eventAlias = MortalLocationIdentityState.BuildConfusableKey(eventRef);
            if (!authorityKeys.Add(key) ||
                !authorityAliases.Add(authorityAlias) ||
                !eventRefs.Add(eventAlias))
            {
                Add(issues, path, "effect_plan_event_authority_ambiguous", "globally exact/confusable-unique accepted event authority and eventRef", value.ToJsonString());
                continue;
            }
            result.Add(new EventAuthority(kind, authorityId, eventRef));
        }
        return result;
    }

    private static string EventAuthorityKey(string kind, string authorityId) =>
        kind + "\u001f" + authorityId;

    private static bool HasOnly(JsonObject value, params string[] fields) =>
        value.Count == fields.Length && fields.All(value.ContainsKey);

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text : string.Empty;
        return TryExact(value);
    }

    private static bool TryReadReadable(JsonNode? node, out string value) =>
        TryReadExact(node, out value) && value.Any(char.IsLetterOrDigit);

    private static bool TryReadPositiveInt(JsonNode? node, out int value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var number) ? number : 0;
        return value > 0;
    }

    private static bool TryReadNonNegativeLong(JsonNode? node, out long value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<long>(out var number)
            ? number
            : -1;
        return value >= 0;
    }

    private static string? ReadOptionalExact(JsonNode? node) =>
        TryReadExact(node, out var value) ? value : null;

    private static bool TryExact(string value) =>
        value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static JsonObject EmptyIdentityIndex() => new()
    {
        ["schemaVersion"] = EffectIdentityState.SchemaVersion,
        ["entries"] = new JsonArray()
    };

    private static EffectIdentityParseResult ParseIdentity(JsonObject root)
    {
        using var document = JsonDocument.Parse(root.ToJsonString());
        return EffectIdentityState.Parse(document.RootElement, EffectAcceptedTurnPlan.IdentityIndexPath);
    }

    private static EffectAcceptedTurnPlanningResult Failed(List<ValidationIssue> issues) =>
        new(null, issues.ToArray());

    private static ValidationIssue Prefix(ValidationIssue issue, string prefix) =>
        new(
            prefix + "." + issue.FilePath,
            issue.Severity,
            issue.Message,
            code: issue.Code,
            section: issue.Section,
            expected: issue.Expected,
            actual: issue.Actual,
            repairHint: issue.RepairHint);

    private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "missing";

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);

    private static void Add(List<ValidationIssue> issues, string path, string code, string expected, string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Accepted effect command cannot produce one exact complete after-image.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Resubmit one closed source-authorized effect operation and omit all client-owned identity, carrier, stack, lifetime, receipt, and post-state fields."));

    private sealed record Application(
        EffectSourceAuthorityEntry Source,
        EffectTargetKey Target,
        JsonObject? Parameters,
        string EventRef);

    private sealed record TerminalOperation(
        string Operation,
        string EffectId,
        EffectTargetKey Target,
        string AuthorityKind,
        string AuthorityId,
        string EventRef);

    private sealed record LifecycleAuthority(
        string EventRef,
        int Turn,
        string Phase,
        EffectTargetKey Target,
        string? TriggerId,
        long? CurrentTime,
        string? CurrentSceneId,
        bool SceneClosed,
        bool? SourceSatisfied,
        bool? ConditionSatisfied,
        string? CurrentRealm);

    private sealed record EventAuthority(string Kind, string AuthorityId, string EventRef);

    private sealed record PreparedApplication(
        Application Application,
        CarrierSlot Slot,
        JsonObject Lifetime);

    private sealed record CarrierSlot(
        string Path,
        string CollectionName,
        JsonArray Collection,
        JsonObject Root);

    private sealed class CarrierWorkspace
    {
        private JsonObject? _player = null;
        private JsonObject? _npcs = null;
        private readonly JsonObject? _enemies;
        private readonly JsonObject? _allies;
        private readonly JsonObject? _afterlifeProfiles;
        private readonly JsonObject? _spiritualConflict;
        private readonly Dictionary<string, JsonObject> _afterImages = new(StringComparer.Ordinal);

        internal CarrierWorkspace(EffectCarrierCatalogInput input)
        {
            _player = input.PlayerEffects?.DeepClone().AsObject();
            _npcs = input.NpcEffects?.DeepClone().AsObject();
            _enemies = input.EnemyCombatants?.DeepClone().AsObject();
            _allies = input.AllyCombatants?.DeepClone().AsObject();
            _afterlifeProfiles = input.AfterlifeProfiles?.DeepClone().AsObject();
            _spiritualConflict = input.SpiritualConflict?.DeepClone().AsObject();
        }

        internal IReadOnlyDictionary<string, JsonObject> AfterImages => _afterImages;

        internal void Touch(CarrierSlot slot) =>
            _afterImages[slot.Path] = slot.Root;

        internal IReadOnlyList<JsonObject> FindStackEffects(
            string realm,
            EffectTargetKey target,
            EffectSourceKey source,
            string stackKey) =>
            EffectCarrierCatalog.Build(ToInput()).Occurrences
                .Where(occurrence =>
                    string.Equals(
                        occurrence.Effect["realm"]?.GetValue<string>(),
                        realm,
                        StringComparison.Ordinal) &&
                    occurrence.Effect["target"] is JsonObject effectTarget &&
                    string.Equals(
                        effectTarget["kind"]?.GetValue<string>(),
                        target.Kind,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        effectTarget["targetId"]?.GetValue<string>(),
                        target.TargetId,
                        StringComparison.Ordinal) &&
                    occurrence.Effect["source"] is JsonObject effectSource &&
                    string.Equals(
                        effectSource["kind"]?.GetValue<string>(),
                        source.Kind,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        effectSource["sourceId"]?.GetValue<string>(),
                        source.SourceId,
                        StringComparison.Ordinal) &&
                    occurrence.Effect["stacking"] is JsonObject effectStacking &&
                    string.Equals(
                        effectStacking["stackKey"]?.GetValue<string>(),
                        stackKey,
                        StringComparison.Ordinal))
                .OrderBy(static occurrence => occurrence.EffectId, StringComparer.Ordinal)
                .Select(static occurrence => occurrence.Effect.DeepClone().AsObject())
                .ToArray();

        internal bool TryRemoveEffect(string effectId, out JsonObject removed)
        {
            removed = null!;
            var matches = FindMutableOccurrences(effectId);
            if (matches.Count != 1)
                return false;
            var match = matches[0];
            removed = match.Effect.DeepClone().AsObject();
            match.Slot.Collection.RemoveAt(match.Index);
            Touch(match.Slot);
            return true;
        }

        internal bool TryReplaceEffect(string effectId, JsonObject replacement)
        {
            ArgumentNullException.ThrowIfNull(replacement);
            var matches = FindMutableOccurrences(effectId);
            if (matches.Count != 1)
                return false;
            var match = matches[0];
            match.Slot.Collection[match.Index] = replacement.DeepClone();
            Touch(match.Slot);
            return true;
        }

        internal void IncludeRewrittenCombatantRoots(
            EffectCarrierCatalogInput publicationBaselines)
        {
            if (_enemies != null &&
                !JsonNode.DeepEquals(_enemies, publicationBaselines.EnemyCombatants))
            {
                _afterImages[EffectCarrierCatalog.EnemiesPath] = _enemies;
            }
            if (_allies != null &&
                !JsonNode.DeepEquals(_allies, publicationBaselines.AllyCombatants))
            {
                _afterImages[EffectCarrierCatalog.AlliesPath] = _allies;
            }
        }

        internal bool TryLocate(
            EffectTargetKey target,
            string category,
            List<ValidationIssue> issues,
            out CarrierSlot slot)
        {
            slot = null!;
            switch (target.Kind)
            {
                case "player":
                    if (!string.Equals(target.TargetId, "player_current", StringComparison.Ordinal))
                    {
                        Add(issues, "target.targetId", "effect_plan_target_carrier_unresolved", "player_current logical player owner", target.TargetId);
                        return false;
                    }
                    _player ??= new JsonObject
                    {
                        ["schemaVersion"] = EffectMaterializationContract.SchemaVersion,
                        ["activeEffects"] = new JsonArray()
                    };
                    if (_player["activeEffects"] is not JsonArray playerEffects)
                        return InvalidCarrier(issues, EffectCarrierCatalog.PlayerPath, _player, out slot);
                    _afterImages[EffectCarrierCatalog.PlayerPath] = _player;
                    slot = new CarrierSlot(EffectCarrierCatalog.PlayerPath, "activeEffects", playerEffects, _player);
                    return true;
                case "npc":
                    _npcs ??= new JsonObject
                    {
                        ["schemaVersion"] = EffectMaterializationContract.SchemaVersion,
                        ["entries"] = new JsonArray()
                    };
                    if (_npcs["entries"] == null &&
                        _npcs["NPCWoundChanges"] is JsonArray)
                    {
                        _npcs["schemaVersion"] = EffectMaterializationContract.SchemaVersion;
                        _npcs["entries"] = new JsonArray();
                    }
                    if (_npcs["entries"] is not JsonArray entries)
                        return InvalidCarrier(issues, EffectCarrierCatalog.NpcPath, _npcs, out slot);
                    var matches = entries.OfType<JsonObject>()
                        .Where(entry => entry["NPCId"] is JsonValue value &&
                            value.TryGetValue<string>(out var npcId) &&
                            string.Equals(npcId, target.TargetId, StringComparison.Ordinal))
                        .ToArray();
                    if (matches.Length > 1)
                    {
                        Add(issues, EffectCarrierCatalog.NpcPath, "effect_plan_target_carrier_ambiguous", "one exact NPC effect entry", target.TargetId);
                        return false;
                    }
                    var npcEntry = matches.SingleOrDefault();
                    if (npcEntry == null)
                    {
                        npcEntry = new JsonObject
                        {
                            ["NPCId"] = target.TargetId,
                            ["activeEffects"] = new JsonArray()
                        };
                        entries.Add(npcEntry);
                    }
                    if (npcEntry["activeEffects"] is not JsonArray npcEffects)
                        return InvalidCarrier(issues, EffectCarrierCatalog.NpcPath, npcEntry, out slot);
                    _afterImages[EffectCarrierCatalog.NpcPath] = _npcs;
                    slot = new CarrierSlot(EffectCarrierCatalog.NpcPath, "activeEffects", npcEffects, _npcs);
                    return true;
                case "combatant":
                    return TryLocateCombatant(target.TargetId, category, issues, out slot);
                default:
                    Add(issues, "target.kind", "effect_plan_target_carrier_unsupported", "Mortal player, NPC, or combatant carrier", target.Kind);
                    return false;
            }
        }

        internal EffectCarrierCatalogInput ToInput() => new(
            _player,
            _npcs,
            _enemies,
            _allies,
            _afterlifeProfiles,
            _spiritualConflict);

        private bool TryLocateCombatant(
            string targetId,
            string category,
            List<ValidationIssue> issues,
            out CarrierSlot slot)
        {
            slot = null!;
            if (category is not ("buff" or "debuff"))
            {
                Add(
                    issues,
                    "source.display.category",
                    "effect_plan_combat_category_unsupported",
                    "buff or debuff for a category-separated Mortal combatant carrier",
                    category);
                return false;
            }
            var matches = new List<(string Path, JsonObject Root, JsonObject Combatant)>();
            AddCombatantMatches(_enemies, EffectCarrierCatalog.EnemiesPath, "enemiesData", targetId, matches);
            AddCombatantMatches(_allies, EffectCarrierCatalog.AlliesPath, "alliesData", targetId, matches);
            if (matches.Count != 1)
            {
                Add(issues, "target.targetId", matches.Count == 0 ? "effect_plan_target_carrier_unresolved" : "effect_plan_target_carrier_ambiguous", "one exact accepted combatant carrier", targetId);
                return false;
            }
            var match = matches[0];
            var collectionName = string.Equals(category, "buff", StringComparison.Ordinal)
                ? "activeBuffs"
                : "activeDebuffs";
            if (match.Combatant[collectionName] is not JsonArray effects)
                return InvalidCarrier(issues, match.Path, match.Combatant, out slot);
            _afterImages[match.Path] = match.Root;
            slot = new CarrierSlot(match.Path, collectionName, effects, match.Root);
            return true;
        }

        private List<MutableOccurrence> FindMutableOccurrences(string effectId)
        {
            var result = new List<MutableOccurrence>();
            foreach (var slot in EnumerateSlots())
            {
                for (var index = 0; index < slot.Collection.Count; index++)
                {
                    if (slot.Collection[index] is JsonObject effect &&
                        string.Equals(
                            effect["effectId"]?.GetValue<string>(),
                            effectId,
                            StringComparison.Ordinal))
                    {
                        result.Add(new MutableOccurrence(slot, index, effect));
                    }
                }
            }
            return result;
        }

        private IEnumerable<CarrierSlot> EnumerateSlots()
        {
            if (_player?["activeEffects"] is JsonArray playerEffects)
                yield return new CarrierSlot(EffectCarrierCatalog.PlayerPath, "activeEffects", playerEffects, _player);
            if (_npcs?["entries"] is JsonArray npcEntries)
            {
                foreach (var entry in npcEntries.OfType<JsonObject>())
                {
                    if (entry["activeEffects"] is JsonArray effects)
                        yield return new CarrierSlot(EffectCarrierCatalog.NpcPath, "activeEffects", effects, _npcs);
                }
            }
            foreach (var slot in EnumerateCombatantSlots(
                         _enemies,
                         EffectCarrierCatalog.EnemiesPath,
                         "enemiesData"))
            {
                yield return slot;
            }
            foreach (var slot in EnumerateCombatantSlots(
                         _allies,
                         EffectCarrierCatalog.AlliesPath,
                         "alliesData"))
            {
                yield return slot;
            }
            if (_afterlifeProfiles?["profiles"] is JsonArray profiles)
            {
                foreach (var profile in profiles.OfType<JsonObject>())
                {
                    if (profile["activeEffects"] is JsonArray effects)
                        yield return new CarrierSlot(EffectCarrierCatalog.AfterlifeProfilesPath, "activeEffects", effects, _afterlifeProfiles);
                }
            }
            if (_spiritualConflict?["activeConflict"] is JsonObject conflict &&
                conflict["combatConditions"] is JsonArray conditions)
            {
                yield return new CarrierSlot(
                    EffectCarrierCatalog.SpiritualConflictPath,
                    "combatConditions",
                    conditions,
                    _spiritualConflict);
            }
        }

        private static IEnumerable<CarrierSlot> EnumerateCombatantSlots(
            JsonObject? root,
            string path,
            string collection)
        {
            if (root?[collection] is not JsonArray combatants)
                yield break;
            foreach (var combatant in combatants.OfType<JsonObject>())
            {
                if (combatant["activeBuffs"] is JsonArray buffs)
                    yield return new CarrierSlot(path, "activeBuffs", buffs, root);
                if (combatant["activeDebuffs"] is JsonArray debuffs)
                    yield return new CarrierSlot(path, "activeDebuffs", debuffs, root);
            }
        }

        private static void AddCombatantMatches(
            JsonObject? root,
            string path,
            string collection,
            string targetId,
            List<(string Path, JsonObject Root, JsonObject Combatant)> matches)
        {
            if (root?[collection] is not JsonArray combatants)
                return;
            foreach (var combatant in combatants.OfType<JsonObject>())
            {
                if (combatant["combatantId"] is JsonValue value &&
                    value.TryGetValue<string>(out var combatantId) &&
                    string.Equals(combatantId, targetId, StringComparison.Ordinal))
                {
                    matches.Add((path, root, combatant));
                }
            }
        }

        private static bool InvalidCarrier(
            List<ValidationIssue> issues,
            string path,
            JsonObject actual,
            out CarrierSlot slot)
        {
            slot = null!;
            Add(issues, path, "effect_plan_target_carrier_invalid", "current canonical owner carrier", actual.ToJsonString());
            return false;
        }

        private sealed record MutableOccurrence(
            CarrierSlot Slot,
            int Index,
            JsonObject Effect);
    }
}
