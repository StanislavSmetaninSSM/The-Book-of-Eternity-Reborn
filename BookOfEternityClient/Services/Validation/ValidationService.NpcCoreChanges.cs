using System.Text.Json;
using BookOfEternityClient.Core;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    public async Task<IReadOnlyList<ValidationIssue>> ValidateNpcCoreChangesBeforeNormalizationAsync()
    {
        var issues = new List<ValidationIssue>();
        var currentJson = await ReadSameTurnOwnerCurrentTextAsync(NpcCoreChangesContract.NpcCorePath);
        if (string.IsNullOrWhiteSpace(currentJson))
            return issues;

        JsonObject currentRoot;
        try
        {
            using var currentDocument = JsonDocument.Parse(currentJson);
            if (currentDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(CreateInvalidNpcCoreChangesAuthorityIssue("non-object root"));
                return issues;
            }

            if (TryFindDuplicateJsonProperty(currentDocument.RootElement, out var duplicatePath))
            {
                issues.Add(new ValidationIssue(
                    $"{NpcCoreChangesContract.NpcCorePath}{duplicatePath}",
                    IssueSeverity.Error,
                    "Current npc_core authority contains a duplicate JSON property.",
                    code: "npc_core_changes_duplicate_property",
                    section: "NPCCoreChanges",
                    expected: "unique JSON property names",
                    actual: duplicatePath,
                    repairHint: "Rewrite npc_core.json with one authoritative value for every property before retrying the turn."));
                return issues;
            }

            currentRoot = JsonNode.Parse(currentDocument.RootElement.GetRawText())!.AsObject();
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            issues.Add(CreateInvalidNpcCoreChangesAuthorityIssue("malformed"));
            return issues;
        }

        var topLevelNameIssues = NpcCoreChangesContract.ValidateCommandTopLevelNames(currentRoot);
        if (topLevelNameIssues.Count > 0)
        {
            issues.AddRange(topLevelNameIssues);
            return issues;
        }

        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
        {
            if (NpcCoreChangesContract.HasCommandLikeProperty(currentRoot))
            {
                issues.Add(new ValidationIssue(
                    $"{NpcCoreChangesContract.NpcCorePath}.{NpcCoreChangesContract.PropertyName}",
                    IssueSeverity.Error,
                    "NPCCoreChanges requires usable validated pre-turn NPC authority before normalization.",
                    code: "npc_core_changes_pre_turn_authority_unavailable",
                    section: "NPCCoreChanges",
                    expected: "usable validated pre-turn game_state/npcs/npc_core.json snapshot",
                    actual: DescribeValidatedPendingTurnSnapshotStatus(lookup.Status),
                    repairHint: "Restore the validated pending-turn snapshot authority before retrying NPCCoreChanges."));
            }

            return issues;
        }

        var preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest,
            NpcCoreChangesContract.NpcCorePath);
        JsonObject? preTurnRoot = null;
        var preTurnActual = "missing or malformed";
        try
        {
            if (!string.IsNullOrWhiteSpace(preTurnJson))
            {
                using var preTurnDocument = JsonDocument.Parse(preTurnJson);
                if (preTurnDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    preTurnActual = "non-object root";
                }
                else if (TryFindDuplicateJsonProperty(preTurnDocument.RootElement, out var duplicatePath))
                {
                    preTurnActual = $"duplicate property {duplicatePath}";
                }
                else
                {
                    preTurnRoot = JsonNode.Parse(preTurnDocument.RootElement.GetRawText())!.AsObject();
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            preTurnActual = "malformed";
        }

        if (preTurnRoot == null)
        {
            if (NpcCoreChangesContract.HasCommandLikeProperty(currentRoot))
            {
                issues.Add(new ValidationIssue(
                    $"{NpcCoreChangesContract.NpcCorePath}.{NpcCoreChangesContract.PropertyName}",
                    IssueSeverity.Error,
                    "NPCCoreChanges validated pre-turn NPC authority is missing or malformed.",
                    code: "npc_core_changes_pre_turn_authority_unavailable",
                    section: "NPCCoreChanges",
                    expected: "readable validated pre-turn npc_core object",
                    actual: preTurnActual,
                    repairHint: "Restore the exact validated pre-turn npc_core snapshot before retrying the command."));
            }

            return issues;
        }

        var authority = _sameTurnOwnerInputs is null
            ? await NpcCoreChangesContract.ReadAuthorityAsync(_fs)
            : NpcCoreChangesContract.CreateAuthorityFromCanonicalJson(
                await ReadSameTurnOwnerCurrentTextAsync(MortalLocationMaterializationContract.WorldMapPath),
                await ReadSameTurnOwnerCurrentTextAsync(MortalLocationMaterializationContract.CurrentLocationPath),
                await ReadSameTurnOwnerCurrentTextAsync("game_state/factions/faction_core.json"),
                await ReadSameTurnOwnerCurrentTextAsync("game_state/misc/characteristics.json"));
        authority = authority with { ProgressionSource = CreateNpcProgressionSource(lookup.Manifest) };
        var acceptedTurnAuthority = MortalActorAcceptedTurnAuthority.Create(
            currentRoot,
            await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                NpcTradeRequestState.PendingRequestPath),
            await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                TrainingRequestState.PendingRequestPath));
        var evaluation = NpcCoreChangesContract.Evaluate(
            currentRoot,
            preTurnRoot,
            authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract,
            detectDirectMutations: true,
            acceptedTurnAuthority);
        issues.AddRange(evaluation.Issues);
        return issues;
    }


    /// <summary>Detached source-check result; it grants no ownership beyond the caller's existing lease.</summary>
    internal sealed class NpcProgressionPublicationSource
    {
        private readonly ValidationService _owner;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly JsonObject? _originalNpcCore;
        internal NpcCoreChangesContract.ProgressionSourceContext? Context { get; }
        internal IReadOnlyList<ValidationIssue> Issues { get; }

        internal NpcProgressionPublicationSource(ValidationService owner,
            FileSystemManager.CanonicalWriteLease lease, NpcCoreChangesContract.ProgressionSourceContext? context,
            IReadOnlyList<ValidationIssue> issues, JsonObject? originalNpcCore = null)
        {
            _owner = owner; _lease = lease; Context = context; Issues = issues;
            _originalNpcCore = originalNpcCore?.DeepClone().AsObject();
        }

        /// <summary>Requires the reducer's actual backup to be the authenticated original, before item-only overlays.</summary>
        internal void RequireBaseline(JsonObject? baseline)
        {
            if (Context is null || _originalNpcCore is null || baseline is null ||
                !JsonNode.DeepEquals(_originalNpcCore, baseline))
                throw new InvalidDataException("NPC progression reduction baseline differs from the authenticated original NPC core.");
        }

        /// <summary>Checks the same live lease and existing snapshot authentication again, never extending authority.</summary>
        internal async Task RequireCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease? lease)
        {
            if (!ReferenceEquals(files, _owner._fs) || !ReferenceEquals(lease, _lease))
                throw new InvalidDataException("NPC progression source belongs to a different publication lease.");
            files.EnsureCanonicalWriteLeaseActive(_lease);
            if (Issues.Count != 0) throw new InvalidDataException("NPC progression publication source was refused.");
            if (Context is null) return;
            var lookup = await _owner.LoadValidatedPendingTurnSnapshotLookupAsync(_lease, requireFreshPhysicalSource: true);
            if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest is null ||
                CreateNpcProgressionSource(lookup.Manifest) != Context)
                throw new InvalidDataException("NPC progression authenticated source changed before publication completed.");
            using var fresh = FileSystemManager.SuppressValidationReuse();
            var original = await _owner.ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest, NpcCoreChangesContract.NpcCorePath, _lease);
            if (!TryReadNpcProgressionOriginal(original, out var originalRoot))
                throw new InvalidDataException("NPC progression authenticated original NPC core is missing or changed.");
            RequireBaseline(originalRoot);
        }
    }

    /// <summary>
    /// Reuses already captured NPC bytes and the existing authenticated lookup under the exact publication
    /// lease. Progression freshly authenticates original NPC snapshot bytes at opening and closing fences;
    /// the detached opening root also binds the reducer backup. Turns without progression skip authentication.
    /// </summary>
    internal async Task<NpcProgressionPublicationSource> PrepareNpcProgressionPublicationAsync(
        FileSystemManager.CanonicalWriteLease writeLease, byte[]? npcCoreBytes)
    {
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        if (npcCoreBytes is null) return new(this, writeLease, null, []);
        JsonObject root;
        try
        {
            using var document = JsonDocument.Parse(DecodePendingSnapshotText(npcCoreBytes));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                TryFindDuplicateJsonProperty(document.RootElement, out _))
                return new(this, writeLease, null, [CreateInvalidNpcCoreChangesAuthorityIssue("non-object or duplicate property")]);
            root = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
        }
        catch (JsonException)
        {
            return new(this, writeLease, null, [CreateInvalidNpcCoreChangesAuthorityIssue("malformed")]);
        }
        if (!NpcCoreChangesContract.HasProgressionCommand(root)) return new(this, writeLease, null, []);
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync(writeLease, requireFreshPhysicalSource: true);
        var context = lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable && lookup.Manifest is not null
            ? CreateNpcProgressionSource(lookup.Manifest) : null;
        var issues = NpcCoreChangesContract.ValidateProgressionSources(root, context);
        if (issues.Count != 0) return new(this, writeLease, context, issues);
        using var fresh = FileSystemManager.SuppressValidationReuse();
        var original = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest!, NpcCoreChangesContract.NpcCorePath, writeLease);
        if (!TryReadNpcProgressionOriginal(original, out var originalRoot))
            return new(this, writeLease, null, [new ValidationIssue(NpcCoreChangesContract.NpcCorePath,
                IssueSeverity.Error, "NPC progression requires authenticated original NPC core bytes.",
                code: "npc_core_changes_progression_source_unavailable", section: "NPCCoreChanges",
                repairHint: "Restore the original authenticated snapshot; do not rewrite it or invent source hashes.")]);
        return new(this, writeLease, context, issues, originalRoot);
    }

    /// <summary>Parses already authenticated original text without accepting duplicate or non-object authority.</summary>
    private static bool TryReadNpcProgressionOriginal(string? text, out JsonObject? root)
    {
        root = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                TryFindDuplicateJsonProperty(document.RootElement, out _)) return false;
            root = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
            return true;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Projects already authenticated manifest DATA without exposing its token to the GM.</summary>
    private static NpcCoreChangesContract.ProgressionSourceContext CreateNpcProgressionSource(
        ValidationPendingTurnSnapshotManifest manifest) => new(
            manifest.SessionId, manifest.RequestId, manifest.TurnNumber, manifest.ManifestPayloadHash,
            manifest.ProgressionControl?.CurrentRealm switch
            {
                "Mortal World" or "mortal_world" => "mortal_world",
                "Chaos Sea" or "chaos_sea" => "chaos_sea",
                "Shining Abode" or "shining_abode" => "shining_abode",
                _ => ""
            });

    private static ValidationIssue CreateInvalidNpcCoreChangesAuthorityIssue(string actual) =>
        new(
            NpcCoreChangesContract.NpcCorePath,
            IssueSeverity.Error,
            "Current npc_core authority must be one readable JSON object before NPCCoreChanges validation.",
            code: "npc_core_changes_invalid_json",
            section: "NPCCoreChanges",
            expected: "one readable JSON object with unique property names",
            actual: actual,
            repairHint: "Restore valid npc_core.json authority before retrying the turn; do not bypass or discard pending NPC changes.");

    internal static IReadOnlyList<ValidationIssue> ValidateNpcCoreFateCardsAgainstProductionContract(
        JsonArray cards,
        string context)
    {
        using var document = JsonDocument.Parse(cards.ToJsonString());
        var issues = new List<ValidationIssue>();
        ValidateNpcFateCardArray(document.RootElement, context, issues);
        return issues;
    }

    internal static bool IsProductionValidMortalActiveSkill(JsonElement skill)
    {
        var issues = new List<ValidationIssue>();
        ValidateActiveSkillObject(skill, "activeSkill", issues);
        return issues.All(issue => issue.Severity != IssueSeverity.Error);
    }

    internal static bool IsProductionValidMortalPassiveSkill(JsonElement skill)
    {
        var issues = new List<ValidationIssue>();
        ValidatePassiveSkillObject(skill, "passiveSkill", issues);
        return issues.All(issue => issue.Severity != IssueSeverity.Error);
    }

    private async Task<bool> NpcCoreChangesCommandIsPresentAsync()
    {
        try
        {
            var json = await ReadSameTurnOwnerCurrentTextAsync(NpcCoreChangesContract.NpcCorePath);
            return !string.IsNullOrWhiteSpace(json) &&
                   JsonNode.Parse(json) is JsonObject root &&
                   NpcCoreChangesContract.HasCommandLikeProperty(root);
        }
        catch
        {
            return false;
        }
    }
}
