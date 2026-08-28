using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static readonly string[] CanonicalWoundCarrierPaths =
    {
        WoundCarrierCatalog.PlayerPath,
        WoundCarrierCatalog.NpcPath,
        WoundCarrierCatalog.EnemiesPath,
        WoundCarrierCatalog.AlliesPath,
        WoundCarrierCatalog.AfterlifeProfilesPath
    };

    private sealed record AcceptedTurnRawWoundDraft(
        WoundResponseCommandParsingResult ParsedCommands,
        WoundCarrierCatalogInput PreTurnCarriers,
        JsonObject PreTurnIdentityIndex,
        JsonObject PreTurnHistory);

    private sealed record AcceptedTurnPreparedWoundHandoff(
        JsonObject Commands,
        WoundAcceptedTurnInput Input,
        WoundPreparedAcceptedTurnPlan PreparedPlan);

    private sealed record AcceptedTurnWoundPlanningHandoff(
        JsonObject Commands,
        WoundAcceptedTurnInput Input,
        AcceptedMechanicsWoundStageBundle StageBundle);

    private sealed class AcceptedTurnWoundHandoffSink
    {
        internal AcceptedTurnWoundPlanningHandoff? Value { get; set; }
    }

    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalWoundMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalWoundMaterializationAsync(
            issues,
            writeLease: null);
        return issues;
    }

    internal async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalWoundMaterializationAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalWoundMaterializationAsync(
            issues,
            writeLease);
        return issues;
    }

    private async Task ValidateAcceptedTurnCanonicalWoundMaterializationAsync(
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var carrierJson = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in CanonicalWoundCarrierPaths)
        {
            carrierJson[path] = writeLease == null
                ? await _fs.ReadFileAsync(path)
                : await _fs.ReadFileAsync(writeLease, path);
        }
        var identityJson = writeLease == null
            ? await _fs.ReadFileAsync(WoundIdentityState.StatePath)
            : await _fs.ReadFileAsync(writeLease, WoundIdentityState.StatePath);
        var historyJson = writeLease == null
            ? await _fs.ReadFileAsync(WoundHistoryState.HistoryPath)
            : await _fs.ReadFileAsync(writeLease, WoundHistoryState.HistoryPath);
        var commandJson = writeLease == null
            ? await _fs.ReadFileAsync(AcceptedMechanicsPlan.WoundCommandPath)
            : await _fs.ReadFileAsync(
                writeLease,
                AcceptedMechanicsPlan.WoundCommandPath);
        if (identityJson is null &&
            historyJson is null &&
            commandJson is null &&
            carrierJson.Values.All(static value => value is null))
        {
            return;
        }

        if (commandJson is not null)
        {
            issues.Add(WoundIssue(
                AcceptedMechanicsPlan.WoundCommandPath,
                "wound_materialization_command_unconsumed",
                "no accepted wound command after canonical publication",
                "command file remains present",
                IssueCategory.ClientOwnedSurface));
        }

        var carriers = new WoundCarrierCatalogInput(
            ParseWoundCarrierRoot(
                carrierJson[WoundCarrierCatalog.PlayerPath],
                WoundCarrierCatalog.PlayerPath,
                issues),
            ParseWoundCarrierRoot(
                carrierJson[WoundCarrierCatalog.NpcPath],
                WoundCarrierCatalog.NpcPath,
                issues),
            ParseWoundCarrierRoot(
                carrierJson[WoundCarrierCatalog.EnemiesPath],
                WoundCarrierCatalog.EnemiesPath,
                issues),
            ParseWoundCarrierRoot(
                carrierJson[WoundCarrierCatalog.AlliesPath],
                WoundCarrierCatalog.AlliesPath,
                issues),
            ParseWoundCarrierRoot(
                carrierJson[WoundCarrierCatalog.AfterlifeProfilesPath],
                WoundCarrierCatalog.AfterlifeProfilesPath,
                issues));
        var catalog = WoundCarrierCatalog.Build(carriers);
        issues.AddRange(catalog.Issues);

        var identity = WoundIdentityState.Parse(
            identityJson,
            WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            historyJson,
            WoundHistoryState.HistoryPath);
        issues.AddRange(identity.Issues);
        issues.AddRange(history.Issues);
        if (catalog.Issues.Count == 0 &&
            identity.State is not null &&
            history.State is not null)
        {
            issues.AddRange(history.State.ValidateAgreement(identity.State, catalog));
        }
    }

    private async Task<AcceptedTurnRawWoundDraft?>
        LoadAcceptedTurnRawWoundDraftAsync(
            ValidationPendingTurnSnapshotManifest manifest,
            List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(issues);
        var commandJson = await _fs.ReadFileAsync(AcceptedMechanicsPlan.WoundCommandPath);
        if (commandJson is null)
            return null;

        var parsedCommands = ParseAcceptedTurnWoundCommands(
            commandJson,
            manifest,
            issues);
        if (parsedCommands is null || parsedCommands.CommandRoot is null)
            return null;

        if (!PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(
                manifest,
                static value => value.Files,
                static value => value.SnapshotFileHashes,
                static value => value.RollbackBaselineFiles,
                WoundAcceptedTurnSnapshotContract.RequiredPaths,
                out var missingSnapshotPath))
        {
            issues.Add(WoundIssue(
                missingSnapshotPath ?? AcceptedMechanicsPlan.WoundCommandPath,
                "wound_materialization_snapshot_before_image_missing",
                "exact signed rollback snapshot evidence for every wound, scheduler, and output authority path",
                "missing, contradictory, or incompletely registered snapshot before-image",
                IssueCategory.ClientOwnedSurface));
            return null;
        }

        var preTurnJson = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in CanonicalWoundCarrierPaths)
        {
            preTurnJson[path] = await ReadValidatedPendingTurnSnapshotFileAsync(
                manifest,
                path);
        }
        var preTurnIdentityJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            manifest,
            WoundIdentityState.StatePath);
        var preTurnHistoryJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            manifest,
            WoundHistoryState.HistoryPath);

        await ValidateWoundClientOwnedBaselineAsync(
            WoundCarrierCatalog.PlayerPath,
            preTurnJson[WoundCarrierCatalog.PlayerPath],
            issues);
        await ValidateWoundClientOwnedBaselineAsync(
            WoundCarrierCatalog.NpcPath,
            preTurnJson[WoundCarrierCatalog.NpcPath],
            issues);
        await ValidateWoundClientOwnedBaselineAsync(
            WoundIdentityState.StatePath,
            preTurnIdentityJson,
            issues);
        await ValidateWoundClientOwnedBaselineAsync(
            WoundHistoryState.HistoryPath,
            preTurnHistoryJson,
            issues);

        var preTurnCarriers = new WoundCarrierCatalogInput(
            ParseWoundCarrierRoot(
                preTurnJson[WoundCarrierCatalog.PlayerPath],
                WoundCarrierCatalog.PlayerPath,
                issues),
            ParseWoundCarrierRoot(
                preTurnJson[WoundCarrierCatalog.NpcPath],
                WoundCarrierCatalog.NpcPath,
                issues),
            ParseWoundCarrierRoot(
                preTurnJson[WoundCarrierCatalog.EnemiesPath],
                WoundCarrierCatalog.EnemiesPath,
                issues),
            ParseWoundCarrierRoot(
                preTurnJson[WoundCarrierCatalog.AlliesPath],
                WoundCarrierCatalog.AlliesPath,
                issues),
            ParseWoundCarrierRoot(
                preTurnJson[WoundCarrierCatalog.AfterlifeProfilesPath],
                WoundCarrierCatalog.AfterlifeProfilesPath,
                issues));
        var carrierCatalog = WoundCarrierCatalog.Build(preTurnCarriers);
        issues.AddRange(carrierCatalog.Issues);
        var identity = WoundIdentityState.Parse(
            preTurnIdentityJson,
            WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            preTurnHistoryJson,
            WoundHistoryState.HistoryPath);
        issues.AddRange(identity.Issues);
        issues.AddRange(history.Issues);
        if (carrierCatalog.Issues.Count == 0 &&
            identity.State is not null &&
            history.State is not null)
        {
            issues.AddRange(history.State.ValidateAgreement(
                identity.State,
                carrierCatalog));
        }
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error) ||
            identity.State is null ||
            history.State is null)
        {
            return null;
        }

        return new AcceptedTurnRawWoundDraft(
            parsedCommands,
            WoundAcceptedTurnData.CloneWoundCarriers(preTurnCarriers)!,
            JsonNode.Parse(WoundIdentityState.SerializeCanonical(identity.State))!
                .AsObject(),
            JsonNode.Parse(WoundHistoryState.SerializeCanonical(history.State))!
                .AsObject());
    }

    private AcceptedTurnPreparedWoundHandoff? PrepareAcceptedTurnWoundHandoff(
        AcceptedTurnRawWoundDraft? draft,
        ValidationPendingTurnSnapshotManifest manifest,
        string realm,
        EffectAcceptedTurnInput effectInput,
        FileSystemManager.CanonicalWriteLease writeLease,
        List<ValidationIssue> issues)
    {
        if (draft is null)
            return null;
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(effectInput);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(issues);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);

        var acceptedEvents = ComposeWoundAcceptedEvents(
            manifest,
            effectInput.EventInput,
            draft.ParsedCommands.Commands
                .Select(static value => value.Opportunity)
                .ToArray(),
            issues);
        if (acceptedEvents.Count == 0 ||
            issues.Any(static issue => issue.Severity == IssueSeverity.Error))
        {
            return null;
        }
        var binding = new WoundAcceptedTurnBinding(
            manifest.SessionId,
            manifest.RequestId,
            manifest.ManifestPayloadHash,
            realm,
            manifest.TurnNumber,
            acceptedEvents,
            WoundAcceptedEventSetFingerprint.Compute(acceptedEvents));
        var recomposed = WoundResponseInputComposer.RecomposeCommandRoot(
            binding,
            draft.ParsedCommands,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        issues.AddRange(recomposed.Issues);
        if (!recomposed.Success || recomposed.CommandRoot is null)
            return null;

        var sourceBinding = WoundAcceptedTurnPlanner.BindAcceptedSourceTargets(
            binding,
            draft.ParsedCommands.Commands
                .Select(static value => value.Opportunity)
                .ToArray(),
            effectInput.TargetAuthority);
        issues.AddRange(sourceBinding.Issues);
        if (!sourceBinding.Success)
            return null;

        var acceptedOwnerCarriers = WoundAcceptedOwnerCarrierAuthority.Compose(
            draft.PreTurnCarriers,
            effectInput.AcceptedCarrierBaselines ??
            effectInput.PreTurnCarriers ??
            new EffectCarrierCatalogInput(null, null, null, null, null, null));
        issues.AddRange(acceptedOwnerCarriers.Issues);
        if (!acceptedOwnerCarriers.Success || acceptedOwnerCarriers.Carriers is not { } carriers)
            return null;

        var input = new WoundAcceptedTurnInput(
            binding,
            recomposed.MaterializedOpportunities,
            recomposed.Transitions,
            carriers,
            draft.PreTurnIdentityIndex,
            draft.PreTurnHistory,
            effectInput.PreTurnCarriers,
            effectInput.PreTurnIdentityIndex);
        var prepared = WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
            _fs,
            writeLease,
            input);
        issues.AddRange(prepared.Issues);
        return prepared.Success && prepared.Plan is not null
            ? new AcceptedTurnPreparedWoundHandoff(
                recomposed.CommandRoot,
                WoundAcceptedTurnData.CloneInput(input)!,
                prepared.Plan)
            : null;
    }

    private AcceptedTurnWoundPlanningHandoff? FinalizeAcceptedTurnWoundHandoff(
        AcceptedTurnPreparedWoundHandoff? prepared,
        WoundEffectBatchPlanningResult? effectResult,
        FileSystemManager.CanonicalWriteLease writeLease,
        List<ValidationIssue> issues)
    {
        if (prepared is null)
            return null;
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(issues);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        if (effectResult is null || !effectResult.Success || effectResult.Plan is null)
            return null;

        var final = WoundAcceptedTurnPlanAuthority.GetOrBuildFinalValidated(
            _fs,
            writeLease,
            prepared.PreparedPlan,
            effectResult);
        issues.AddRange(final.Issues);
        if (!final.Success || final.Plan is null)
            return null;
        try
        {
            return new AcceptedTurnWoundPlanningHandoff(
                prepared.Commands.DeepClone().AsObject(),
                WoundAcceptedTurnData.CloneInput(prepared.Input)!,
                new AcceptedMechanicsWoundStageBundle(
                    prepared.Input,
                    prepared.PreparedPlan,
                    effectResult.Plan,
                    final.Plan));
        }
        catch (ArgumentException exception)
        {
            issues.Add(WoundIssue(
                AcceptedMechanicsPlan.WoundCommandPath,
                "wound_materialization_stage_handoff_invalid",
                "one exact prepared/effect/final wound stage bundle",
                exception.GetType().Name));
            return null;
        }
    }

    private static WoundResponseCommandParsingResult? ParseAcceptedTurnWoundCommands(
        string json,
        ValidationPendingTurnSnapshotManifest manifest,
        List<ValidationIssue> issues)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            issues.Add(WoundIssue(
                AcceptedMechanicsPlan.WoundCommandPath,
                "wound_command_invalid_json",
                "well-formed strict JSON object",
                exception.GetType().Name));
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            var parsed = WoundResponseInputComposer.ParseCommandRoot(root);
            issues.AddRange(parsed.Issues);
            if (!parsed.Success)
                return null;
            ValidateWoundCommandBinding(
                root,
                "sessionId",
                manifest.SessionId,
                issues);
            ValidateWoundCommandBinding(
                root,
                "requestId",
                manifest.RequestId,
                issues);
            ValidateWoundCommandBinding(
                root,
                "snapshotToken",
                manifest.ManifestPayloadHash,
                issues);

            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
                return null;
            return parsed;
        }
    }

    private static void ValidateWoundCommandBinding(
        JsonElement root,
        string field,
        string expected,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            ResourceMaterializationContract.IsExactIdentifier(value.GetString()) &&
            string.Equals(value.GetString(), expected, StringComparison.Ordinal))
        {
            return;
        }

        issues.Add(WoundIssue(
            AcceptedMechanicsPlan.WoundCommandPath + "." + field,
            "wound_command_binding_mismatch",
            expected,
            root.TryGetProperty(field, out value)
                ? value.GetRawText()
                : "missing"));
    }

    private async Task ValidateWoundClientOwnedBaselineAsync(
        string path,
        string? preTurnJson,
        List<ValidationIssue> issues)
    {
        var currentJson = await _fs.ReadFileAsync(path);
        if (string.Equals(currentJson, preTurnJson, StringComparison.Ordinal))
            return;
        issues.Add(WoundIssue(
            path,
            "wound_materialization_client_owned_root_mutated",
            "byte-identical validated pre-turn client-owned wound root",
            currentJson is null
                ? "current root missing"
                : preTurnJson is null
                    ? "current root created outside accepted wound planning"
                    : "current bytes differ from validated snapshot",
            IssueCategory.ClientOwnedSurface));
    }

    private static IReadOnlyList<WoundAcceptedEventAuthority>
        ComposeWoundAcceptedEvents(
            ValidationPendingTurnSnapshotManifest manifest,
            JsonObject eventInput,
            IReadOnlyList<WoundOpportunityAuthority> opportunities,
            List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(opportunities);
        if (eventInput["events"] is not JsonArray events || events.Count == 0)
        {
            issues.Add(WoundIssue(
                AcceptedMechanicsPlan.WoundCommandPath,
                "wound_materialization_event_authority_missing",
                "one or more accepted effect-event authority rows",
                "missing or empty events"));
            return Array.Empty<WoundAcceptedEventAuthority>();
        }

        var result = new List<WoundAcceptedEventAuthority>(events.Count);
        for (var index = 0; index < events.Count; index++)
        {
            if (events[index] is not JsonObject row ||
                !TryReadExactWoundString(row["eventRef"], out var eventRef) ||
                !TryReadExactWoundString(row["kind"], out var kind) ||
                !TryReadExactWoundString(row["authorityId"], out var authorityId))
            {
                issues.Add(WoundIssue(
                    $"{AcceptedMechanicsPlan.WoundCommandPath}.acceptedEvents[{index}]",
                    "wound_materialization_event_authority_invalid",
                    "exact eventRef, kind, and authorityId",
                    events[index]?.ToJsonString() ?? "null"));
                continue;
            }
            var matchingOpportunities = opportunities.Where(value =>
                string.Equals(
                    value.EventRef,
                    eventRef,
                    StringComparison.Ordinal)).ToArray();
            string semanticFingerprint;
            if (matchingOpportunities.Length == 0)
            {
                semanticFingerprint = HashText(
                    "accepted-wound-event-authority-v1",
                    manifest.SessionId + "\n" +
                    manifest.RequestId + "\n" +
                    manifest.ManifestPayloadHash + "\n" +
                    row.ToJsonString());
            }
            else
            {
                var fingerprints = matchingOpportunities
                    .Select(static value => value.InputEvidenceFingerprint)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (fingerprints.Length != 1 ||
                    matchingOpportunities.Any(value =>
                        !string.Equals(value.EventKind, kind, StringComparison.Ordinal) ||
                        !string.Equals(
                            value.EventAuthorityId,
                            authorityId,
                            StringComparison.Ordinal)))
                {
                    issues.Add(WoundIssue(
                        $"{AcceptedMechanicsPlan.WoundCommandPath}.acceptedEvents[{index}]",
                        "wound_materialization_event_authority_mismatch",
                        "the exact accepted event kind, authorityId, and one evidence seal",
                        eventRef));
                    continue;
                }
                semanticFingerprint = fingerprints[0];
            }
            result.Add(new WoundAcceptedEventAuthority(
                eventRef,
                kind,
                authorityId,
                semanticFingerprint));
        }

        foreach (var opportunity in opportunities)
        {
            if (result.Count(value => string.Equals(
                    value.EventRef,
                    opportunity.EventRef,
                    StringComparison.Ordinal)) == 1)
            {
                continue;
            }
            issues.Add(WoundIssue(
                AcceptedMechanicsPlan.WoundCommandPath + ".commands",
                "wound_materialization_event_authority_mismatch",
                "one exact accepted runtime event for every sealed opportunity",
                opportunity.EventRef));
        }
        return result;
    }

    private static bool TryReadExactWoundString(
        JsonNode? node,
        out string value)
    {
        value = string.Empty;
        if (node is not JsonValue scalar ||
            !scalar.TryGetValue<string>(out var candidate) ||
            !ResourceMaterializationContract.IsExactIdentifier(candidate))
        {
            return false;
        }

        value = candidate;
        return true;
    }

    private static string ComputeAcceptedWoundCarrierAuthority(
        WoundCarrierCatalogInput carriers)
    {
        ArgumentNullException.ThrowIfNull(carriers);
        return new JsonObject
        {
            ["player"] = carriers.PlayerWounds?.DeepClone(),
            ["npcs"] = carriers.NpcWounds?.DeepClone(),
            ["enemies"] = carriers.EnemyCombatants?.DeepClone(),
            ["allies"] = carriers.AllyCombatants?.DeepClone(),
            ["afterlifeProfiles"] = carriers.AfterlifeProfiles?.DeepClone()
        }.ToJsonString();
    }

    private static JsonObject? ParseWoundCarrierRoot(
        string? json,
        string path,
        List<ValidationIssue> issues)
    {
        if (json is null)
            return null;
        try
        {
            return JsonNode.Parse(json) is JsonObject root
                ? root
                : AddInvalidRoot();
        }
        catch (JsonException exception)
        {
            issues.Add(WoundIssue(
                path,
                "wound_carrier_invalid_json",
                "well-formed strict JSON object",
                exception.GetType().Name));
            return null;
        }

        JsonObject? AddInvalidRoot()
        {
            issues.Add(WoundIssue(
                path,
                "wound_carrier_invalid_root",
                "strict JSON object",
                "non-object root"));
            return null;
        }
    }

    private static ValidationIssue WoundIssue(
        string path,
        string code,
        string expected,
        string actual,
        IssueCategory category = IssueCategory.StateConsistency) => new(
            path,
            IssueSeverity.Error,
            "Wound materialization violates exact accepted-turn authority.",
            code: code,
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Restore the validated wound roots and use only the strict accepted wound command flow.",
            category: category);
}
