using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawResourceMaterializationAsync()
    {
        AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs);
        var issues = new List<ValidationIssue>();
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        var commandJson = await _fs.ReadFileAsync(ResourceMaterializationContract.CommandPath);
        var definitionsJson = await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath);
        var stateJson = await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath);
        var historyJson = await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath);

        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            lookup.Manifest == null)
        {
            if (commandJson != null || definitionsJson != null || stateJson != null || historyJson != null)
            {
                issues.Add(ResourceIssue(
                    ResourceMaterializationContract.CommandPath,
                    "resource_materialization_snapshot_required",
                    "usable validated pending-turn snapshot",
                    lookup.Status.ToString()));
            }
            return issues;
        }

        var manifest = lookup.Manifest;
        var definitionsResult = ResourceDefinitionCatalog.ParseCanonical(
            definitionsJson,
            allowMissingPristine: false);
        issues.AddRange(definitionsResult.Issues);
        var definitions = definitionsResult.Catalog;
        ResourceStateContractResult? stateResult = null;
        ResourceHistoryStateResult? historyResult = null;
        if (definitions != null)
        {
            stateResult = ResourceStateContract.ParseCanonical(
                stateJson,
                definitions,
                allowMissingPristine: false);
            issues.AddRange(stateResult.Issues);
            historyResult = ResourceHistoryState.ParseCanonical(
                historyJson,
                definitions,
                allowMissingPristine: false);
            issues.AddRange(historyResult.Issues);
            if (stateResult.Ledger != null && historyResult.History != null)
                issues.AddRange(historyResult.History.ValidateStateAgreement(stateResult.Ledger));
        }

        await ValidateResourceSnapshotContinuityAsync(
            manifest,
            issues);
        var commands = ResourceAcceptedTurnInputComposer.Parse(commandJson);
        issues.AddRange(commands.Issues);
        var events = ResourceAcceptedTurnInputComposer.BindAcceptedEvents(
            manifest.TurnNumber,
            commands);
        issues.AddRange(events.Issues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error) ||
            definitions == null || stateResult?.Ledger == null ||
            historyResult?.History == null)
        {
            return issues;
        }

        var owners = ResourceOwnerAuthority.CreateCurrentPlayerAuthority(
            definitions,
            commands.DefinitionCreations
                .Where(static creation =>
                    creation.Definition["allowedOwnerKinds"] is JsonArray kinds &&
                    kinds.Any(static kind =>
                        kind is JsonValue value &&
                        value.TryGetValue<string>(out var token) &&
                        string.Equals(token, "player", StringComparison.Ordinal)))
                .Select(static creation =>
                creation.Definition["resourceKey"]?.GetValue<string>()));
        issues.AddRange(owners.Issues);
        issues.AddRange(owners.ValidateCanonicalAgreement(
            stateResult.Ledger,
            historyResult.History));
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return issues;

        var effectIssues = await ValidateAcceptedTurnRawEffectMaterializationAsync();
        issues.AddRange(effectIssues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return issues;

        EffectAcceptedTurnPlan? effectPlan = null;
        if (EffectAcceptedTurnPlanAuthority.TryPeekValidated(_fs, out var effectResult))
        {
            if (!effectResult.Success || effectResult.Plan == null)
            {
                issues.AddRange(effectResult.Issues);
                return issues;
            }
            effectPlan = effectResult.Plan;
        }
        if (commands.IsMissing && effectPlan == null)
            return issues;

        var requestJson = await _fs.ReadFileAsync("input/turn_request.json");
        if (!TryParseResourceTurnRequest(
                requestJson,
                manifest.SessionId,
                manifest.RequestId,
                manifest.TurnNumber,
                out var request,
                out var realm))
        {
            issues.Add(ResourceIssue(
                "input/turn_request.json",
                "resource_materialization_turn_authority_invalid",
                "exact pending snapshot session/request/turn authority",
                "missing or mismatched request"));
            return issues;
        }

        var sourcesResult = ResourceMutationSourceCatalog.Create(
            Array.Empty<ResourceMutationSourceExport>());
        issues.AddRange(sourcesResult.Issues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error) ||
            sourcesResult.Catalog == null)
        {
            return issues;
        }

        var effectIdentityJson = await _fs.ReadFileAsync(EffectAcceptedTurnPlan.IdentityIndexPath);
        JsonObject effectIdentity;
        if (effectPlan != null)
        {
            effectIdentity = effectPlan.IdentityIndexBeforeImage?.DeepClone().AsObject() ??
                EmptyEffectIdentityRoot();
        }
        else if (effectIdentityJson == null)
        {
            effectIdentity = EmptyEffectIdentityRoot();
        }
        else if (!TryParseStrictResourceObject(effectIdentityJson, out effectIdentity))
        {
            issues.Add(ResourceIssue(
                EffectAcceptedTurnPlan.IdentityIndexPath,
                "resource_materialization_effect_index_invalid",
                "strict effect identity object or proven pristine absence",
                "malformed or non-object root"));
            return issues;
        }

        var manifestJson = await _fs.ReadFileAsync(PendingTurnSnapshotManifestPath);
        var pendingInput = TryParseStrictResourceObject(manifestJson, out var pendingRoot)
            ? pendingRoot
            : new JsonObject();
        var effectCommandJson = await _fs.ReadFileAsync(EffectAcceptedTurnPlan.CommandPath);
        var effectCommands = effectCommandJson == null
            ? EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot()
            : ParseStrictObjectOrEmpty(effectCommandJson);
        var internalInputs = new JsonObject
        {
            ["definitions"] = JsonNode.Parse(definitionsJson!)!.AsObject(),
            ["state"] = JsonNode.Parse(stateJson!)!.AsObject(),
            ["history"] = JsonNode.Parse(historyJson!)!.AsObject(),
            ["ownerFingerprint"] = owners.Fingerprint,
            ["sourceFingerprint"] = HashText("resource-source-empty-v1", string.Empty)
        };
        var beforePaths = new HashSet<string>(StringComparer.Ordinal)
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            ResourceMaterializationContract.CommandPath,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            "input/turn_request.json",
            PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath
        };
        if (effectPlan != null)
        {
            beforePaths.UnionWith(effectPlan.TouchedPaths);
            beforePaths.UnionWith(effectPlan.DeletedPaths);
        }
        var beforeImages = await CaptureResourceBeforeImagesAsync(beforePaths);
        var fingerprints = new AcceptedMechanicsAuthorityFingerprints(
            Definitions: HashText("resource-definitions-v1", definitionsJson!),
            Owners: owners.Fingerprint,
            ResourceState: stateResult.Ledger.Fingerprint,
            ResourceHistory: historyResult.History.Fingerprint,
            EffectSources: HashText(
                "accepted-mechanics-effect-sources-v1",
                effectPlan?.SourceAuthorityFingerprint ?? "<missing>"),
            EffectTargets: HashText(
                "accepted-mechanics-effect-targets-v1",
                effectPlan?.TargetAuthorityFingerprint ?? "<missing>"),
            EffectCarriers: HashText(
                "accepted-mechanics-effect-carriers-v1",
                effectPlan?.CarrierAuthorityFingerprint ?? "<missing>"),
            EffectIdentityIndex: HashText(
                "effect-index-v1",
                effectIdentityJson ?? "<missing>"),
            AcceptedEvents: HashNode("resource-events-v1", events.Root),
            Commands: HashText(
                "accepted-mechanics-commands-v1",
                (commandJson ?? "<missing>") + "\n" +
                (effectCommandJson ?? "<missing>")),
            Pending: HashNode("accepted-mechanics-pending-v1", pendingInput),
            InternalInputs: HashNode("accepted-mechanics-internal-v1", internalInputs));
        var context = new AcceptedMechanicsPlanningContext(
            internalInputs["definitions"]!.AsObject(),
            definitions,
            stateResult.Ledger,
            historyResult.History,
            owners,
            sourcesResult.Catalog,
            commands,
            effectIdentity,
            effectPlan);
        var input = new AcceptedMechanicsInput(
            manifest.SessionId,
            manifest.RequestId,
            manifest.RequestId,
            realm,
            manifest.TurnNumber,
            events.Root,
            commands.Root,
            effectCommands,
            pendingInput,
            internalInputs,
            fingerprints,
            beforeImages,
            issues,
            context);
        var result = AcceptedMechanicsPlanAuthority.GetOrBuildValidated(_fs, input);
        issues.AddRange(result.Issues);
        if (result.Success)
            EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs);
        return issues;
    }

    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalResourceMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateCanonicalResourceRootsAsync(null, issues);
        return issues;
    }

    internal async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalResourceMaterializationAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        var issues = new List<ValidationIssue>();
        await ValidateCanonicalResourceRootsAsync(writeLease, issues);
        return issues;
    }

    private async Task ValidateCanonicalResourceRootsAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        List<ValidationIssue> issues)
    {
        var definitionsJson = writeLease == null
            ? await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath)
            : await _fs.ReadFileAsync(writeLease, ResourceMaterializationContract.DefinitionsPath);
        var stateJson = writeLease == null
            ? await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath)
            : await _fs.ReadFileAsync(writeLease, ResourceMaterializationContract.StatePath);
        var historyJson = writeLease == null
            ? await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath)
            : await _fs.ReadFileAsync(writeLease, ResourceMaterializationContract.HistoryPath);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            definitionsJson,
            allowMissingPristine: false);
        issues.AddRange(definitions.Issues);
        if (definitions.Catalog == null)
        {
            if (stateJson == null)
            {
                issues.Add(ResourceIssue(
                    ResourceMaterializationContract.StatePath,
                    "resource_state_root_missing",
                    "present canonical resource state root",
                    "missing"));
            }
            if (historyJson == null)
            {
                issues.Add(ResourceIssue(
                    ResourceMaterializationContract.HistoryPath,
                    "resource_history_root_missing",
                    "present canonical resource history root",
                    "missing"));
            }
            return;
        }
        var state = ResourceStateContract.ParseCanonical(
            stateJson,
            definitions.Catalog,
            allowMissingPristine: false);
        var history = ResourceHistoryState.ParseCanonical(
            historyJson,
            definitions.Catalog,
            allowMissingPristine: false);
        issues.AddRange(state.Issues);
        issues.AddRange(history.Issues);
        if (state.Ledger != null && history.History != null)
        {
            issues.AddRange(history.History.ValidateStateAgreement(state.Ledger));
            var owners = ResourceOwnerAuthority.CreateCurrentPlayerAuthority(
                definitions.Catalog);
            issues.AddRange(owners.Issues);
            issues.AddRange(owners.ValidateCanonicalAgreement(
                state.Ledger,
                history.History));
        }
        var commandExists = writeLease == null
            ? _fs.FileExists(ResourceMaterializationContract.CommandPath)
            : _fs.FileExists(writeLease, ResourceMaterializationContract.CommandPath);
        if (commandExists)
        {
            issues.Add(ResourceIssue(
                ResourceMaterializationContract.CommandPath,
                "resource_materialization_command_not_consumed",
                "absent consumed command root after canonical publication",
                "present"));
        }
    }

    private async Task ValidateResourceSnapshotContinuityAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        List<ValidationIssue> issues)
    {
        await Compare(ResourceMaterializationContract.DefinitionsPath,
            "resource_materialization_direct_definition_mutation");
        await Compare(ResourceMaterializationContract.StatePath,
            "resource_materialization_direct_state_mutation");
        await Compare(ResourceMaterializationContract.HistoryPath,
            "resource_materialization_direct_history_mutation");
        return;

        async Task Compare(string path, string code)
        {
            var current = await _fs.ReadFileBytesAsync(path);
            byte[]? previous = null;
            if (manifest.Files.TryGetValue(path, out var snapshotPath) &&
                !string.IsNullOrWhiteSpace(snapshotPath))
            {
                previous = await _fs.ReadFileBytesAsync(snapshotPath);
            }
            if (previous == null
                    ? current == null
                    : current != null && previous.AsSpan().SequenceEqual(current))
            {
                return;
            }
            issues.Add(ResourceIssue(
                path,
                code,
                "exact validated pre-turn bytes and prior existence",
                previous == null
                    ? "created after snapshot"
                    : current == null
                        ? "deleted after snapshot"
                        : "changed after snapshot"));
        }
    }

    private async Task<IReadOnlyDictionary<string, CanonicalBeforeImage>>
        CaptureResourceBeforeImagesAsync(IEnumerable<string> paths)
    {
        var result = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            var bytes = await _fs.ReadFileBytesAsync(path);
            result[path] = bytes == null
                ? new CanonicalBeforeImage(false, null)
                : new CanonicalBeforeImage(true, bytes);
        }
        return result;
    }

    private static bool TryParseResourceTurnRequest(
        string? json,
        string expectedSessionId,
        string expectedRequestId,
        int expectedTurn,
        out JsonObject root,
        out string realm)
    {
        root = new JsonObject();
        realm = "mortal_world";
        if (!TryParseStrictResourceObject(json, out root) ||
            !string.Equals(ReadExactResourceString(root["sessionId"]), expectedSessionId, StringComparison.Ordinal) ||
            !string.Equals(ReadExactResourceString(root["requestId"]), expectedRequestId, StringComparison.Ordinal) ||
            root["turnNumber"] is not JsonValue turnValue ||
            !turnValue.TryGetValue<int>(out var turn) || turn != expectedTurn)
        {
            return false;
        }

        var token = ReadExactResourceString(root["currentRealm"]) ??
                    ReadExactResourceString(root["progressionControl"]?["currentRealm"]);
        realm = token switch
        {
            "Chaos Sea" or "chaos_sea" => "chaos_sea",
            "Shining Abode" or "shining_abode" => "shining_abode",
            _ => "mortal_world"
        };
        return true;
    }

    private static JsonObject ParseStrictObjectOrEmpty(string json) =>
        TryParseStrictResourceObject(json, out var root) ? root : new JsonObject();

    private static bool TryParseStrictResourceObject(string? json, out JsonObject root)
    {
        root = new JsonObject();
        if (json == null)
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            var duplicateIssues = new List<ValidationIssue>();
            ResourceMaterializationContract.FindDuplicateProperties(
                document.RootElement,
                "resourceInput",
                duplicateIssues,
                "resource_materialization_duplicate_property");
            if (duplicateIssues.Count != 0)
                return false;
            root = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadExactResourceString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) &&
        ResourceMaterializationContract.IsExactIdentifier(text)
            ? text
            : null;

    private static JsonObject EmptyEffectIdentityRoot() => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray()
    };

    private static string HashNode(string domain, JsonNode node) =>
        HashText(domain, node.ToJsonString());

    private static string HashText(string domain, string value) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(domain + "\0" + value))).ToLowerInvariant();

    private static ValidationIssue ResourceIssue(
        string path,
        string code,
        string expected,
        string actual) => new(
            path,
            IssueSeverity.Error,
            "Resource materialization violates exact accepted-turn authority.",
            code: code,
            section: "resource_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Restore the validated pre-turn resource roots and express resource changes only through the strict resource command envelope.");
}
