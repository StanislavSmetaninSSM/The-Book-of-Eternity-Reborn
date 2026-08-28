using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private const int MaximumAcceptedTurnWoundCommands = 128;

    private static readonly string[] CanonicalWoundCarrierPaths =
    {
        WoundCarrierCatalog.PlayerPath,
        WoundCarrierCatalog.NpcPath,
        WoundCarrierCatalog.EnemiesPath,
        WoundCarrierCatalog.AlliesPath,
        WoundCarrierCatalog.AfterlifeProfilesPath
    };

    private static readonly IReadOnlySet<string> WoundCommandRootFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion",
            "sessionId",
            "requestId",
            "snapshotToken",
            "commands"
        };

    private sealed record AcceptedTurnRawWoundDraft(
        JsonObject Commands,
        WoundCarrierCatalogInput PreTurnCarriers,
        JsonObject PreTurnIdentityIndex,
        JsonObject PreTurnHistory,
        IReadOnlyList<WoundOpportunityAuthority> Opportunities,
        IReadOnlyList<WoundAcceptedTransitionDraft> Transitions);

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
        await ValidateAcceptedTurnCanonicalWoundMaterializationAsync(issues);
        return issues;
    }

    private async Task ValidateAcceptedTurnCanonicalWoundMaterializationAsync(
        List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var carrierJson = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in CanonicalWoundCarrierPaths)
            carrierJson[path] = await _fs.ReadFileAsync(path);
        var identityJson = await _fs.ReadFileAsync(WoundIdentityState.StatePath);
        var historyJson = await _fs.ReadFileAsync(WoundHistoryState.HistoryPath);
        var commandJson = await _fs.ReadFileAsync(AcceptedMechanicsPlan.WoundCommandPath);
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

        var commands = ParseAcceptedTurnWoundCommands(commandJson, manifest, issues);
        if (commands is null)
            return null;

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
            commands.DeepClone().AsObject(),
            WoundAcceptedTurnData.CloneWoundCarriers(preTurnCarriers)!,
            JsonNode.Parse(WoundIdentityState.SerializeCanonical(identity.State))!
                .AsObject(),
            JsonNode.Parse(WoundHistoryState.SerializeCanonical(history.State))!
                .AsObject(),
            Array.Empty<WoundOpportunityAuthority>(),
            Array.Empty<WoundAcceptedTransitionDraft>());
    }

    private AcceptedTurnPreparedWoundHandoff? PrepareAcceptedTurnWoundHandoff(
        AcceptedTurnRawWoundDraft? draft,
        ValidationPendingTurnSnapshotManifest manifest,
        string realm,
        JsonObject effectEventInput,
        FileSystemManager.CanonicalWriteLease writeLease,
        List<ValidationIssue> issues)
    {
        if (draft is null)
            return null;
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(effectEventInput);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(issues);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);

        var acceptedEvents = ComposeWoundAcceptedEvents(
            manifest,
            effectEventInput,
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
        var input = new WoundAcceptedTurnInput(
            binding,
            draft.Opportunities,
            draft.Transitions,
            draft.PreTurnCarriers,
            draft.PreTurnIdentityIndex,
            draft.PreTurnHistory);
        var prepared = WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
            _fs,
            writeLease,
            input);
        issues.AddRange(prepared.Issues);
        return prepared.Success && prepared.Plan is not null
            ? new AcceptedTurnPreparedWoundHandoff(
                draft.Commands.DeepClone().AsObject(),
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

    private static JsonObject? ParseAcceptedTurnWoundCommands(
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
            if (root.ValueKind != JsonValueKind.Object)
            {
                issues.Add(WoundIssue(
                    AcceptedMechanicsPlan.WoundCommandPath,
                    "wound_command_invalid_root",
                    "strict schema-version-1 command object",
                    root.ValueKind.ToString()));
                return null;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                var propertyPath = AcceptedMechanicsPlan.WoundCommandPath + "." +
                    property.Name;
                if (!seen.Add(property.Name))
                {
                    issues.Add(WoundIssue(
                        propertyPath,
                        "wound_command_duplicate_field",
                        "one exact occurrence of each root field",
                        property.Name));
                }
                if (!WoundCommandRootFields.Contains(property.Name))
                {
                    issues.Add(WoundIssue(
                        propertyPath,
                        "wound_command_unknown_field",
                        string.Join(", ", WoundCommandRootFields.OrderBy(
                            static value => value,
                            StringComparer.Ordinal)),
                        property.Name));
                }
            }
            foreach (var field in WoundCommandRootFields.Where(field =>
                         !root.TryGetProperty(field, out _)))
            {
                issues.Add(WoundIssue(
                    AcceptedMechanicsPlan.WoundCommandPath + "." + field,
                    "wound_command_missing_field",
                    "required strict command root field",
                    "missing"));
            }

            var schemaValid = root.TryGetProperty("schemaVersion", out var schema) &&
                schema.ValueKind == JsonValueKind.Number &&
                schema.TryGetInt32(out var schemaVersion) &&
                schemaVersion == 1;
            if (!schemaValid)
            {
                issues.Add(WoundIssue(
                    AcceptedMechanicsPlan.WoundCommandPath + ".schemaVersion",
                    "wound_command_invalid_field",
                    "exact integer schemaVersion 1",
                    root.TryGetProperty("schemaVersion", out schema)
                        ? schema.GetRawText()
                        : "missing"));
            }
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

            if (!root.TryGetProperty("commands", out var commands) ||
                commands.ValueKind != JsonValueKind.Array)
            {
                issues.Add(WoundIssue(
                    AcceptedMechanicsPlan.WoundCommandPath + ".commands",
                    "wound_command_invalid_field",
                    "strict command array",
                    root.TryGetProperty("commands", out commands)
                        ? commands.ValueKind.ToString()
                        : "missing"));
            }
            else if (commands.GetArrayLength() > MaximumAcceptedTurnWoundCommands)
            {
                issues.Add(WoundIssue(
                    AcceptedMechanicsPlan.WoundCommandPath + ".commands",
                    "wound_command_limit_exceeded",
                    $"at most {MaximumAcceptedTurnWoundCommands} accepted wound commands",
                    commands.GetArrayLength().ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
            }
            else if (commands.GetArrayLength() != 0)
            {
                issues.Add(WoundIssue(
                    AcceptedMechanicsPlan.WoundCommandPath + ".commands",
                    "wound_command_transition_adapter_unavailable",
                    "an empty foundational command batch until a typed story adapter supplies transitions",
                    $"{commands.GetArrayLength()} unadapted commands"));
            }

            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
                return null;
            return JsonNode.Parse(root.GetRawText())!.AsObject();
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
            List<ValidationIssue> issues)
    {
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
            result.Add(new WoundAcceptedEventAuthority(
                eventRef,
                kind,
                authorityId,
                HashText(
                    "accepted-wound-event-authority-v1",
                    manifest.SessionId + "\n" +
                    manifest.RequestId + "\n" +
                    manifest.ManifestPayloadHash + "\n" +
                    row.ToJsonString())));
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
