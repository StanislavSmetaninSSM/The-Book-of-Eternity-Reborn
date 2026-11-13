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
                await ReadSameTurnOwnerCurrentTextAsync("game_state/misc/characteristics.json"),
                await ReadSameTurnOwnerCurrentTextAsync(MortalNpcWorldProgressionPolicy.Path));
        authority = authority with { ProgressionSource = CreateNpcProgressionSource(lookup.Manifest) };
        if (_sameTurnOwnerNpcSkillInput is { } completed)
        {
            // Source assembly has already authenticated and composed this exact selected
            // input. Reuse its result, not a permission token or a cached direct-mutation
            // exemption. Compare the raw/original roots and authority already read here.
            if (!JsonNode.DeepEquals(currentRoot, completed.RawCore) ||
                !JsonNode.DeepEquals(preTurnRoot, completed.OriginalCore) ||
                !string.Equals(MortalNpcAcceptedSkillInput.ComputeAuthorityFingerprint(authority),
                    completed.AuthorityFingerprint, StringComparison.Ordinal))
                issues.Add(CreateInvalidNpcCoreChangesAuthorityIssue("completed NPC skill candidate differs from selected raw/original authority"));
            return issues;
        }

        var sidecarText = await ReadSameTurnOwnerCurrentTextAsync(MortalNpcAcceptedSkillInput.SidecarPath);
        JsonObject? currentSidecar = null;
        if (sidecarText is not null && !TryReadNpcProgressionOriginal(sidecarText, out currentSidecar))
        {
            issues.Add(new ValidationIssue(MortalNpcAcceptedSkillInput.SidecarPath, IssueSeverity.Error,
                "Ordinary NPC skill commands require one exact object with unique properties.",
                code: "npc_skill_candidate_invalid_root", section: "NPCSkills"));
            return issues;
        }
        var acceptedTurnAuthority = MortalActorAcceptedTurnAuthority.Create(
            currentRoot,
            await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                NpcTradeRequestState.PendingRequestPath),
            await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                TrainingRequestState.PendingRequestPath));
        if (currentSidecar is not null && MortalNpcAcceptedSkillInput.HasPending(currentSidecar))
        {
            if (authority.ProgressionSource?.Realm != "mortal_world")
            {
                issues.Add(CreateInvalidNpcCoreChangesAuthorityIssue("NPC skills require current authenticated Mortal source"));
                return issues;
            }
            var originalSidecar = await ReadAuthenticatedNpcSkillOriginalAsync(lookup.Manifest, writeLease: null);
            if (originalSidecar is null)
            {
                issues.Add(new ValidationIssue(MortalNpcAcceptedSkillInput.SidecarPath, IssueSeverity.Error,
                    "Ordinary NPC skills require an authenticated original sidecar or signed absence.",
                    code: "npc_skill_candidate_original_unavailable", section: "NPCSkills"));
                return issues;
            }
            var candidate = MortalNpcAcceptedSkillCandidate.Compose(preTurnRoot, originalSidecar.Root,
                currentRoot, currentSidecar, authority, acceptedTurnAuthority);
            issues.AddRange(candidate.Issues);
            return issues;
        }
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
        private readonly byte[]? _rawCoreBytes;
        private readonly byte[]? _rawSidecarBytes;
        private readonly string? _originalTradePending;
        private readonly string? _originalTrainingPending;
        private readonly string? _worldPolicyFingerprint;
        private readonly NpcProgressionSettlementWitness? _settlementWitness;
        internal MortalNpcAcceptedSkillInput? SkillInput { get; }
        internal NpcCoreChangesContract.ProgressionSourceContext? Context { get; }
        internal IReadOnlyList<ValidationIssue> Issues { get; }

        internal NpcProgressionPublicationSource(ValidationService owner,
            FileSystemManager.CanonicalWriteLease lease, NpcCoreChangesContract.ProgressionSourceContext? context,
            IReadOnlyList<ValidationIssue> issues, JsonObject? originalNpcCore = null,
            MortalNpcAcceptedSkillInput? skillInput = null, byte[]? rawCoreBytes = null, byte[]? rawSidecarBytes = null,
            string? originalTradePending = null, string? originalTrainingPending = null,
            string? worldPolicyFingerprint = null)
        {
            _owner = owner; _lease = lease; Context = context; Issues = issues;
            _originalNpcCore = originalNpcCore?.DeepClone().AsObject();
            SkillInput = skillInput;
            _rawCoreBytes = rawCoreBytes?.ToArray(); _rawSidecarBytes = rawSidecarBytes?.ToArray();
            _originalTradePending = originalTradePending; _originalTrainingPending = originalTrainingPending;
            _worldPolicyFingerprint = worldPolicyFingerprint;
            if (issues.Count == 0 && context is not null && _originalNpcCore is not null && worldPolicyFingerprint is not null)
                _settlementWitness = new NpcProgressionSettlementWitness(context, _originalNpcCore,
                    worldPolicyFingerprint, skillInput?.AuthorityFingerprint, skillInput?.OriginalSidecar,
                    skillInput?.OriginalSidecarExists ?? false, originalTradePending, originalTrainingPending);
        }

        /// <summary>Compares the reducer's retained policy authority with this publication source.</summary>
        internal void RequirePolicyAuthority(NpcCoreChangesContract.Authority actualReductionAuthority)
        {
            ArgumentNullException.ThrowIfNull(actualReductionAuthority);
            if (Context is null || Issues.Count != 0 || _worldPolicyFingerprint is null ||
                !actualReductionAuthority.NpcCharacteristicPolicy.IsValid ||
                !string.Equals(_worldPolicyFingerprint,
                    MortalNpcWorldProgressionPolicy.Fingerprint(actualReductionAuthority), StringComparison.Ordinal))
                throw new InvalidDataException("NPC progression reduction policy differs from the current-world publication authority.");
        }

        /// <summary>Requires the reducer's actual backup to be the authenticated original, before item-only overlays.</summary>
        internal void RequireBaseline(JsonObject? baseline)
        {
            if (Context is null || _originalNpcCore is null || baseline is null ||
                !JsonNode.DeepEquals(_originalNpcCore, baseline))
                throw new InvalidDataException("NPC progression reduction baseline differs from the authenticated original NPC core.");
        }

        internal void RequireSkillBaseline(JsonObject? core, JsonObject? sidecar, bool sidecarBackupEntryExists)
        {
            if (SkillInput is null) return;
            RequireBaseline(core);
            if (!SkillInput.OriginalSidecarExists)
            {
                if (sidecarBackupEntryExists)
                    throw new InvalidDataException("Proved-absent original NPC skill sidecar must have no reducer backup entry.");
                return;
            }
            if (!sidecarBackupEntryExists || sidecar is null || !JsonNode.DeepEquals(SkillInput.OriginalSidecar, sidecar))
                throw new InvalidDataException("NPC skill backup differs from the authenticated original sidecar.");
        }

        internal async Task RequireRawSkillInputsAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease? lease)
        {
            if (SkillInput is null) return;
            await RequireCurrentAsync(files, lease);
            var core = await files.ReadFileBytesAsync(_lease, NpcCoreChangesContract.NpcCorePath);
            var sidecar = await files.ReadFileBytesAsync(_lease, MortalNpcAcceptedSkillInput.SidecarPath);
            if (core is null || sidecar is null || _rawCoreBytes is null || _rawSidecarBytes is null ||
                !core.AsSpan().SequenceEqual(_rawCoreBytes) || !sidecar.AsSpan().SequenceEqual(_rawSidecarBytes))
                throw new InvalidDataException("Raw NPC skill candidate inputs changed before consumption.");
        }

        internal async Task RequireItemPhaseSkillInputsAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease? lease)
        {
            await RequireCurrentAsync(files, lease);
            if (SkillInput is null) throw new InvalidDataException("Missing NPC skill input.");
            var core = await files.ReadFileAsync(_lease, NpcCoreChangesContract.NpcCorePath);
            var sidecar = await files.ReadFileBytesAsync(_lease, MortalNpcAcceptedSkillInput.SidecarPath);
            if (!TryReadNpcProgressionOriginal(core, out var root) ||
                !JsonNode.DeepEquals(root, SkillInput.ItemPhaseCore) || sidecar is null || _rawSidecarBytes is null ||
                !sidecar.AsSpan().SequenceEqual(_rawSidecarBytes))
                throw new InvalidDataException("NPC skill item-phase input differs from its admitted exact intermediate.");
        }

        internal async Task RequirePublishedSkillsAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease? lease,
            MortalWoundTreatmentPublicationTakeReceipt? treatmentReceipt = null)
        {
            await RequireCurrentAsync(files, lease);
            if (SkillInput is null) return;
            var core = await files.ReadFileAsync(_lease, NpcCoreChangesContract.NpcCorePath);
            var sidecar = await files.ReadFileAsync(_lease, MortalNpcAcceptedSkillInput.SidecarPath);
            if (!TryReadNpcProgressionOriginal(core, out var root) ||
                !TryReadNpcProgressionOriginal(sidecar, out var skills) ||
                !JsonNode.DeepEquals(SkillInput.ConsumedSidecar, skills))
                throw new InvalidDataException("Published NPC skill sidecar differs from the admitted consumed result.");
            var expected = SkillInput.CandidateCore;
            if (treatmentReceipt is not null)
            {
                expected = CanonicalStateNormalizer.RequireTreatmentNpcSkillResult(
                    files, _lease, this, treatmentReceipt.Plan, treatmentReceipt);
                if (!JsonNode.DeepEquals(expected, root))
                    throw new InvalidDataException("Published treatment NPC root differs from its sealed final owner result.");
            }
            foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
            {
                if (expected.ContainsKey(section) != root!.ContainsKey(section))
                    throw new InvalidDataException("Published NPC skill carrier presence changed.");
                if (expected[section] is not JsonArray actors) continue;
                if (root![section] is not JsonArray actualActors || actualActors.Count != actors.Count)
                    throw new InvalidDataException("Published NPC skill carrier topology changed.");
                for (var index = 0; index < actors.Count; index++)
                {
                    if (actors[index] is not JsonObject actor || actualActors[index] is not JsonObject actual ||
                        !GuardianPolicyContracts.TryResolveStrictPermanentNpcId(actor, out var id) ||
                        !GuardianPolicyContracts.TryResolveStrictPermanentNpcId(actual, out var actualId) || id != actualId)
                        throw new InvalidDataException("Published NPC skill identity differs from admitted candidate.");
                    foreach (var field in new[] { "activeSkills", "passiveSkills", "level", "experience", "experienceForNextLevel", "progressionProfile", "progressionType", "progressionTrackers", "characteristics", "inventory", "equippedItems", "equipment" })
                        if (actor.ContainsKey(field) != actual.ContainsKey(field) || !JsonNode.DeepEquals(actor[field], actual[field]))
                            throw new InvalidDataException("Published NPC skill/progression result differs from admitted candidate: " + field);
                }
            }
            if (root!.ContainsKey(NpcCoreChangesContract.PropertyName))
                throw new InvalidDataException("NPC core command was not consumed.");
        }

        /// <summary>Exports detached expected DATA for fresh checks across transaction leases.</summary>
        internal NpcProgressionSettlementWitness CreateSettlementWitness() =>
            _settlementWitness ?? throw new InvalidDataException("Missing accepted NPC progression settlement witness.");

        internal async Task RequireCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease? lease)
        {
            if (!ReferenceEquals(files, _owner._fs) || !ReferenceEquals(lease, _lease))
                throw new InvalidDataException("NPC progression source belongs to a different publication lease.");
            files.EnsureCanonicalWriteLeaseActive(_lease);
            if (Issues.Count != 0) throw new InvalidDataException("NPC progression publication source was refused.");
            if (Context is null) return;
            await _owner.RequireNpcProgressionWitnessAsync(_lease, CreateSettlementWitness());
        }
    }

    /// <summary>
    /// Reuses already captured NPC bytes and the existing authenticated lookup under the exact publication
    /// lease. Progression freshly authenticates original NPC snapshot bytes at opening and closing fences;
    /// the detached opening roots also bind reducer backups. Turns without progression or pending skills skip authentication.
    /// </summary>
    internal async Task<NpcProgressionPublicationSource> PrepareNpcProgressionPublicationAsync(
        FileSystemManager.CanonicalWriteLease writeLease, byte[]? npcCoreBytes, byte[]? npcSkillBytes = null)
    {
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        JsonObject? rawSidecar = null;
        if (npcSkillBytes is not null && !TryReadNpcProgressionOriginal(DecodePendingSnapshotText(npcSkillBytes), out rawSidecar))
            return new(this, writeLease, null, [CreateInvalidNpcCoreChangesAuthorityIssue("malformed NPC skill sidecar")]);
        var hasSkills = rawSidecar is not null && MortalNpcAcceptedSkillInput.HasPending(rawSidecar);
        if (npcCoreBytes is null)
            return new(this, writeLease, null, hasSkills
                ? [CreateInvalidNpcCoreChangesAuthorityIssue("pending NPC skills require current NPC core")]
                : []);
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
        if (!NpcCoreChangesContract.HasProgressionCommand(root) && !hasSkills) return new(this, writeLease, null, []);
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync(writeLease, requireFreshPhysicalSource: true);
        var context = lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable && lookup.Manifest is not null
            ? CreateNpcProgressionSource(lookup.Manifest) : null;
        var issues = NpcCoreChangesContract.ValidateProgressionSources(root, context);
        if (issues.Count != 0) return new(this, writeLease, context, issues);
        if (hasSkills && (context is null || context.Realm != "mortal_world"))
            return new(this, writeLease, context, [CreateInvalidNpcCoreChangesAuthorityIssue("NPC skills require current authenticated Mortal source")]);
        using var fresh = FileSystemManager.SuppressValidationReuse();
        var original = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest!, NpcCoreChangesContract.NpcCorePath, writeLease);
        if (!TryReadNpcProgressionOriginal(original, out var originalRoot))
            return new(this, writeLease, null, [new ValidationIssue(NpcCoreChangesContract.NpcCorePath,
                IssueSeverity.Error, "NPC progression requires authenticated original NPC core bytes.",
                code: "npc_core_changes_progression_source_unavailable", section: "NPCCoreChanges",
                repairHint: "Restore the original authenticated snapshot; do not rewrite it or invent source hashes.")]);
        var authority = hasSkills
            ? await NpcCoreChangesContract.ReadAuthorityAsync(_fs)
            : await NpcCoreChangesContract.ReadProgressionPolicyAuthorityAsync(_fs);
        authority = authority with { ProgressionSource = context };
        if (!authority.NpcCharacteristicPolicy.IsValid)
            return new(this, writeLease, context, [new ValidationIssue(MortalNpcWorldProgressionPolicy.Path,
                IssueSeverity.Error, "Current-world NPC progression policy must be a valid closed declaration.",
                code: "npc_progression_consequence_policy_invalid", section: "NPCProgression")]);
        var policyFingerprint = MortalNpcWorldProgressionPolicy.Fingerprint(authority);
        if (!hasSkills) return new(this, writeLease, context, issues, originalRoot,
            worldPolicyFingerprint: policyFingerprint);
        var originalSidecar = await ReadAuthenticatedNpcSkillOriginalAsync(lookup.Manifest!, writeLease);
        if (originalSidecar is null)
            return new(this, writeLease, context, [CreateInvalidNpcCoreChangesAuthorityIssue("authenticated original NPC skill sidecar unavailable")]);
        var originalTrade = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest!, NpcTradeRequestState.PendingRequestPath, writeLease);
        var originalTraining = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest!, TrainingRequestState.PendingRequestPath, writeLease);
        var input = MortalNpcAcceptedSkillInput.Create(originalRoot!, originalSidecar.Root, root, rawSidecar!, authority,
            originalTrade, originalTraining, out var candidateIssues, originalSidecar.Exists,
            CaptureNpcSkillItemSnapshot(writeLease, context));
        return new(this, writeLease, context, candidateIssues, originalRoot, input,
            npcCoreBytes, npcSkillBytes, originalTrade, originalTraining, policyFingerprint);
    }

    private sealed record AuthenticatedNpcSkillOriginal(bool Exists, JsonObject Root);

    // Call only with the existing authenticated manifest. Null content alone never proves absence.
    private async Task<AuthenticatedNpcSkillOriginal?> ReadAuthenticatedNpcSkillOriginalAsync(
        ValidationPendingTurnSnapshotManifest manifest, FileSystemManager.CanonicalWriteLease? writeLease)
    {
        const string path = MortalNpcAcceptedSkillInput.SidecarPath;
        if (manifest.OriginalPathPresenceV1 is { } presence &&
            presence.TryGetValue(path, out var existed) && !existed)
            return manifest.Files?.ContainsKey(path) != true && manifest.SnapshotFileHashes?.ContainsKey(path) != true
                ? new AuthenticatedNpcSkillOriginal(false, new JsonObject()) : null;
        if (manifest.Files?.ContainsKey(path) != true) return null;
        var text = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, path, writeLease);
        return TryReadNpcProgressionOriginal(text, out var root)
            ? new AuthenticatedNpcSkillOriginal(true, root!) : null;
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
