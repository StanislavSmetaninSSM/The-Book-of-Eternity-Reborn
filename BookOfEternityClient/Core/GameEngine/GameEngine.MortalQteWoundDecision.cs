using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Only bytes actually issued by this engine may be revoked during its repair loop.
    // A foreign command is always preserved and refused by the command-only distributor.
    private byte[]? _mortalQteOwnedCommand;
    private IReadOnlyDictionary<string, CanonicalBeforeImage>? _mortalQteDecisionImages;
    private MortalWoundOpportunityReceiptConsumePlanResult? _mortalQteExactReplay;
    private WoundOpportunityAuthority? _mortalQteIssuedOpportunity;
    private MortalQteRejectedRepairInput? _mortalQteRejectedRepairInput;
    private MortalQteRejectedRepairInput? _mortalQteRepairConstraint;

    // The two provenance variants are a rejected public draft with an adapter-issued
    // opportunity, or an actual client-issued command captured before revocation.
    // Neither variant is accepted publication authority.
    private sealed record MortalQteRejectedRepairInput(
        WoundOpportunityAuthority Opportunity, WoundRepairPacket Packet, string Generation, int Turn,
        byte[] DraftBytes, byte[] SceneBytes, JsonObject Decision, JsonObject Scene,
        JsonObject? IssuedCommandRoot, byte[]? IssuedCommandBytes);


    private Task<IReadOnlyList<ValidationIssue>> ValidateResourceAndMortalQteDecisionAsync(bool ordinaryUpstreamAccepted = true) =>
        ValidateResourceAndMortalQteDecisionCoreAsync(ordinaryUpstreamAccepted, null, null);

    private Task<IReadOnlyList<ValidationIssue>> ValidateResourceAndMortalGuaranteedTreatmentAsync(
        bool ordinaryUpstreamAccepted, ValidatedPendingTurnSnapshotContext? activeSnapshotContext, int expectedTurn) =>
        ValidateResourceAndMortalQteDecisionCoreAsync(ordinaryUpstreamAccepted, activeSnapshotContext, expectedTurn);

    private async Task<IReadOnlyList<ValidationIssue>> ValidateResourceAndMortalQteDecisionCoreAsync(
        bool ordinaryUpstreamAccepted, ValidatedPendingTurnSnapshotContext? activeSnapshotContext, int? expectedTurn)
    {
        if (ordinaryUpstreamAccepted && activeSnapshotContext is not null && expectedTurn is not null)
        {
            var narrative = await NormalizeMortalJointNarrativeBeforeIntakeAsync(activeSnapshotContext, expectedTurn.Value);
            if (narrative.Any(issue => issue.Severity == IssueSeverity.Error)) return narrative;
        }
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        _mortalQteRejectedRepairInput = null;
        if (_mortalJointRepairCheckpoint is not null && activeSnapshotContext is not null && expectedTurn is not null &&
            !MatchesMortalJointRepairFrame(activeSnapshotContext, expectedTurn.Value))
        {
            // Detached metadata cannot make a later/foreign original a joint turn.
            // Physical command/wave bytes still undergo their ordinary strict read.
            _mortalJointRepairCheckpoint = null;
            _mortalJointRepairCorrectionReady = false;
            _mortalJointRepairCanonicalRefresh = null;
        }
        try
        {
            if (!ordinaryUpstreamAccepted) await RevokeOwnedMortalQteCommandAsync(lease);
            var stageRan = false;
            async Task<IReadOnlyList<ValidationIssue>> StageCurrentMortalDecision(FileSystemManager.CanonicalWriteLease currentLease)
            {
                stageRan = true;
                return await StageMortalQteDecisionAsync(currentLease);
            }
            MortalJointRepairRawAdmission? jointIntake = null;
            _mortalJointRepairCorrectionReady = false;
            async Task<MortalJointRepairRawAdmission> InspectJoint(FileSystemManager.CanonicalWriteLease currentLease)
            {
                jointIntake = await InspectOrdinaryMortalJointRepairAsync(currentLease, activeSnapshotContext!, expectedTurn!.Value);
                return jointIntake;
            }
            var issues = await _validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease,
                ordinaryUpstreamAccepted ? StageCurrentMortalDecision : null,
                ordinaryUpstreamAccepted ? SealMortalQteDecisionAsync : null,
                ordinaryUpstreamAccepted ? () => _mortalQteExactReplay : null,
                ordinaryUpstreamAccepted && activeSnapshotContext is not null && expectedTurn is not null
                    ? currentLease => PrepareOrdinaryMortalGuaranteedTreatmentAsync(currentLease, activeSnapshotContext, expectedTurn.Value)
                    : null,
                ordinaryUpstreamAccepted && activeSnapshotContext is not null && expectedTurn is not null ? InspectJoint : null);
            _mortalJointRepairCorrectionReady = jointIntake?.Disposition == MortalJointRepairRawDisposition.Owned &&
                !issues.Any(issue => issue.Severity == IssueSeverity.Error);
            if (stageRan && _mortalQteDecisionImages is not null)
                issues = issues.Select(issue => IsMortalQtePrivateCanonicalIssue(issue)
                        ? MortalWoundOpportunityAdapter.PrivateSourceIssue(issue) : BindMortalQteDraftRepairTarget(issue)).ToArray();
            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            {
                if (stageRan && _mortalQteDecisionImages is not null && _mortalQteIssuedOpportunity is not null)
                {
                    var currentDraft = await _fs.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath);
                    var currentScene = await _fs.ReadFileBytesAsync(lease, WoundNarrativeOutputPath);
                    var currentCommand = await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
                    if (currentDraft is not null && currentScene is not null && currentCommand is not null &&
                        _mortalQteOwnedCommand is not null && currentCommand.AsSpan().SequenceEqual(_mortalQteOwnedCommand) &&
                        _mortalQteDecisionImages[MortalWoundOpportunityAdapter.DecisionPath].Bytes is { } issuedDraft &&
                        _mortalQteDecisionImages[WoundNarrativeOutputPath].Bytes is { } issuedScene &&
                        currentDraft.AsSpan().SequenceEqual(issuedDraft) && currentScene.AsSpan().SequenceEqual(issuedScene))
                        RetainMortalQteRejectedRepair(lease, issues, _mortalQteIssuedOpportunity, currentDraft, currentScene, currentCommand);
                }
                await RevokeOwnedMortalQteCommandAsync(lease);
            }
            return issues;
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException)
        {
            await RevokeOwnedMortalQteCommandAsync(lease);
            throw;
        }
    }

    private async Task<IReadOnlyList<ValidationIssue>> StageMortalQteDecisionAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        await RevokeOwnedMortalQteCommandAsync(lease);
        try
        {
            var rawBytes = await _fs.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath);
            var raw = rawBytes is null ? null : new UTF8Encoding(false, true).GetString(rawBytes).TrimStart('\uFEFF');
            var narrativeBytes = await _fs.ReadFileBytesAsync(lease, WoundNarrativeOutputPath);
            var narrative = narrativeBytes is null ? null : new UTF8Encoding(false, true).GetString(narrativeBytes).TrimStart('\uFEFF');
            var sceneRoot = narrative is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(
                narrative.TrimStart('\uFEFF'), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "Mortal wound ordinary scene");
            var scene = sceneRoot?["response"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            var composed = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(_fs, lease, raw, scene, out var replay, out var repairOpportunity);
            if (composed is null) return Array.Empty<ValidationIssue>();
            if (!composed.Success)
            {
                var errors = composed.Issues.Select(BindMortalQteDraftRepairTarget).ToArray();
                if (repairOpportunity is not null && rawBytes is not null && narrativeBytes is not null)
                    RetainMortalQteRejectedRepair(lease, errors, repairOpportunity, rawBytes, narrativeBytes, null);
                return errors;
            }
            if (_mortalQteRepairConstraint is { } constraint &&
                (repairOpportunity is null || sceneRoot is null || rawBytes is null ||
                 !MatchesMortalQteRepairCorrection(lease, constraint, repairOpportunity,
                     StrictJsonAuthority.Deserialize<JsonObject>(raw!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "Mortal repair draft")!, sceneRoot)))
                return new[] { MortalWoundOpportunityAdapter.PrivateSourceIssue(new ValidationIssue(
                    MortalWoundOpportunityAdapter.DecisionPath, IssueSeverity.Error,
                    "The correction changed source/generation, sealed proposal siblings or the required scene.",
                    "wound_repair_retry_authority_unavailable")) };
            var decision = StrictJsonAuthority.Deserialize<JsonElement>(raw!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "accepted Mortal wound decision");
            _mortalQteOwnedCommand = await StateDistributor.StageAcceptedWoundCommandAsync(_fs, lease,
                new GameResponse { Response = scene ?? string.Empty, WoundDecisions = [decision] }, composed);
            _mortalQteExactReplay = replay;
            _mortalQteIssuedOpportunity = repairOpportunity is null ? null : WoundAcceptedTurnData.CloneOpportunity(repairOpportunity);
            _mortalQteDecisionImages = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
            {
                [MortalWoundOpportunityAdapter.DecisionPath] = new(rawBytes is not null, rawBytes),
                [WoundNarrativeOutputPath] = new(narrativeBytes is not null, narrativeBytes),
                [AcceptedMechanicsPlan.WoundCommandPath] = new(_mortalQteOwnedCommand is not null, _mortalQteOwnedCommand)
            };
            return Array.Empty<ValidationIssue>();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or DecoderFallbackException)
        {
            return new[] { new ValidationIssue(MortalWoundOpportunityAdapter.DecisionPath, IssueSeverity.Error,
                "Mortal wound decision staging refused: " + error.Message, "mortal_wound_decision_staging_invalid") };
        }
    }

    private void RetainMortalQteRejectedRepair(FileSystemManager.CanonicalWriteLease lease,
        IReadOnlyList<ValidationIssue> issues, WoundOpportunityAuthority opportunity,
        byte[] draftBytes, byte[] sceneBytes, byte[]? issuedCommandBytes)
    {
        if (issues.Any(issue => issue.Section == MortalWoundOpportunityAdapter.PrivateSourceIssueSection)) return;
        var packets = WoundRepairPacketBuilder.Build(issues);
        if (packets.Count != 1 || !packets[0].MatchesOpportunity(opportunity)) return;
        try
        {
            var draft = StrictJsonAuthority.Deserialize<JsonObject>(new UTF8Encoding(false, true).GetString(draftBytes).TrimStart('\uFEFF'),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "rejected Mortal public draft");
            var scene = StrictJsonAuthority.Deserialize<JsonObject>(new UTF8Encoding(false, true).GetString(sceneBytes).TrimStart('\uFEFF'),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "rejected Mortal ordinary scene");
            if (draft is null || scene is null || !packets[0].MatchesRejectedDecision(draft)) return;
            var source = MortalQteWoundOpportunityProjection.ReadSnapshot(_fs, lease);
            if (source.SessionId != opportunity.SessionId || source.RequestId != opportunity.RequestId ||
                source.SnapshotToken != opportunity.SnapshotToken) return;
            JsonObject? command = null;
            if (issuedCommandBytes is not null)
            {
                command = StrictJsonAuthority.Deserialize<JsonObject>(new UTF8Encoding(false, true).GetString(issuedCommandBytes).TrimStart('\uFEFF'),
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual issued Mortal command before revocation");
                if (command is null) return;
                var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(command, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                if (!parsed.Success || parsed.Commands.Count != 1 || !packets[0].MatchesOpportunity(parsed.Commands[0].Opportunity) ||
                    !TryReadWoundRepairDecision(parsed.Commands[0].Decision, out var decision) || !packets[0].MatchesRejectedDecision(decision)) return;
            }
            _mortalQteRejectedRepairInput = new(WoundAcceptedTurnData.CloneOpportunity(opportunity), packets[0],
                _fs.GetOrCreateSessionGeneration(lease), source.TurnNumber, draftBytes.ToArray(), sceneBytes.ToArray(),
                draft.DeepClone().AsObject(), scene.DeepClone().AsObject(), command?.DeepClone().AsObject(), issuedCommandBytes?.ToArray());
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or DecoderFallbackException)
        {
            // Incomplete/changed provenance remains unavailable to the existing
            // fail-closed retry path; never bless it from current parseable bytes.
        }
    }

    private bool MatchesMortalQteRepairCorrection(FileSystemManager.CanonicalWriteLease lease,
        MortalQteRejectedRepairInput constraint, WoundOpportunityAuthority opportunity, JsonObject decision, JsonObject scene)
    {
        if (!_fs.IsCurrentSessionGeneration(lease, constraint.Generation) || !constraint.Packet.MatchesOpportunity(opportunity) ||
            !constraint.Packet.MatchesCorrectedDecision(decision, scene["response"]?.GetValue<string>())) return false;
        var source = MortalQteWoundOpportunityProjection.ReadSnapshot(_fs, lease);
        if (source.SessionId != constraint.Packet.SessionId || source.RequestId != constraint.Packet.RequestId ||
            source.SnapshotToken != constraint.Packet.SnapshotToken || source.TurnNumber != constraint.Turn) return false;
        if (constraint.Packet.RequiresResponseCorrection && !JsonNode.DeepEquals(decision, constraint.Decision)) return false;
        var actualScene = scene.DeepClone().AsObject();
        var expectedScene = constraint.Scene.DeepClone().AsObject();
        actualScene.Remove("timestamp"); expectedScene.Remove("timestamp");
        if (constraint.Packet.RequiresResponseCorrection) { actualScene.Remove("response"); expectedScene.Remove("response"); }
        return JsonNode.DeepEquals(actualScene, expectedScene);
    }

    private async Task<IReadOnlyList<WoundRepairRetryObligation>> CaptureMortalQteRepairRetryAsync(
        IReadOnlyList<WoundRepairPacket> packets, MortalQteRejectedRepairInput retained)
    {
        if (packets.Count != 1 || packets[0].CandidateRef != retained.Packet.CandidateRef ||
            packets[0].SemanticFingerprint != retained.Packet.SemanticFingerprint ||
            !packets[0].MatchesOpportunity(retained.Opportunity) || !packets[0].MatchesRejectedDecision(retained.Decision))
            return Array.Empty<WoundRepairRetryObligation>();
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var draft = await _fs.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath);
        var scene = await _fs.ReadFileBytesAsync(lease, WoundNarrativeOutputPath);
        if (!_fs.IsCurrentSessionGeneration(lease, retained.Generation) || draft is null || scene is null ||
            !draft.AsSpan().SequenceEqual(retained.DraftBytes) || !scene.AsSpan().SequenceEqual(retained.SceneBytes))
            return Array.Empty<WoundRepairRetryObligation>();
        try
        {
            var source = MortalQteWoundOpportunityProjection.ReadSnapshot(_fs, lease);
            if (source.SessionId != retained.Packet.SessionId || source.RequestId != retained.Packet.RequestId ||
                source.SnapshotToken != retained.Packet.SnapshotToken || source.TurnNumber != retained.Turn)
                return Array.Empty<WoundRepairRetryObligation>();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or DecoderFallbackException)
        { return Array.Empty<WoundRepairRetryObligation>(); }
        if ((retained.IssuedCommandRoot is null) != (retained.IssuedCommandBytes is null))
            return Array.Empty<WoundRepairRetryObligation>();
        _mortalQteRepairConstraint = retained;
        return new[] { new WoundRepairRetryObligation(0, packets[0], retained.IssuedCommandRoot?.DeepClone().AsObject(), retained) };
    }

    private async Task<bool> HasExactMortalQteRepairResubmissionAsync(WoundRepairRetryObligation obligation)
    {
        if (obligation.MortalQteInput is not { } constraint || _mortalQteIssuedOpportunity is null || _mortalQteOwnedCommand is null)
            return false;
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var actualCommand = await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        var rawDraft = await _fs.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath);
        var rawScene = await _fs.ReadFileBytesAsync(lease, WoundNarrativeOutputPath);
        if (actualCommand is null || rawDraft is null || rawScene is null || !actualCommand.AsSpan().SequenceEqual(_mortalQteOwnedCommand)) return false;
        try
        {
            var draft = StrictJsonAuthority.Deserialize<JsonObject>(new UTF8Encoding(false, true).GetString(rawDraft).TrimStart('\uFEFF'),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "corrected Mortal repair draft");
            var scene = StrictJsonAuthority.Deserialize<JsonObject>(new UTF8Encoding(false, true).GetString(rawScene).TrimStart('\uFEFF'),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "corrected Mortal repair scene");
            return draft is not null && scene is not null &&
                MatchesMortalQteRepairCorrection(lease, constraint, _mortalQteIssuedOpportunity, draft, scene);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or DecoderFallbackException)
        { return false; }
    }

    private void ClearMortalQteRepairState()
    {
        _mortalQteRejectedRepairInput = null;
        _mortalQteRepairConstraint = null;
    }

    private static bool IsMortalQtePrivateCanonicalIssue(ValidationIssue issue)
    {
        if (issue.Category == IssueCategory.ClientOwnedSurface) return true;
        var privatePaths = new[] { MortalQteDecisionPublication.StatePath, MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath,
            WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, WoundCarrierCatalog.PlayerPath,
            WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath,
            WoundCarrierCatalog.AfterlifeProfilesPath, EffectIdentityState.StatePath, EffectCarrierCatalog.PlayerPath,
            EffectCarrierCatalog.NpcPath, EffectCarrierCatalog.EnemiesPath, EffectCarrierCatalog.AlliesPath,
            EffectCarrierCatalog.AfterlifeProfilesPath, EffectCarrierCatalog.SpiritualConflictPath };
        // Ownership comes from this admitted Mortal pass and the fixed canonical
        // root, including diagnostics emitted by shared strict/duplicate parsers.
        return privatePaths.Any(path => issue.FilePath == path || issue.FilePath.StartsWith(path + ".", StringComparison.Ordinal) ||
            issue.FilePath.StartsWith(path + "[", StringComparison.Ordinal));
    }

    private static ValidationIssue BindMortalQteDraftRepairTarget(ValidationIssue issue)
    {
        if (issue.FilePath != "woundDecisions[0]" && !issue.FilePath.StartsWith("woundDecisions[0].", StringComparison.Ordinal) &&
            !(issue.WoundRepairContext is not null && WoundRepairPacketBuilder.IsRepairableIssue(issue))) return issue;
        // Logical proposal/scene coordinates remain available to the existing bounded
        // repair packet. Proposal errors target the draft; narration-only errors
        // target the ordinary scene with the unchanged draft as read-only context.
        return new ValidationIssue(issue.FilePath, issue.Severity, issue.Message, issue.Code, issue.Actor,
            issue.Section, issue.Expected, issue.Actual, issue.RepairHint, issue.Category,
            issue.FilePath == WoundNarrativeOutputPath + ".response"
                ? [WoundNarrativeOutputPath, MortalWoundOpportunityAdapter.DecisionPath]
                : [MortalWoundOpportunityAdapter.DecisionPath])
        {
            FactionRepairClassification = issue.FactionRepairClassification,
            MortalItemRepairContext = issue.MortalItemRepairContext,
            MortalLocationRepairContext = issue.MortalLocationRepairContext,
            EffectRepairContext = issue.EffectRepairContext,
            WoundRepairContext = issue.WoundRepairContext?.Clone()
        };
    }

    private async Task<IReadOnlyList<ValidationIssue>> SealMortalQteDecisionAsync(
        FileSystemManager.CanonicalWriteLease lease, IReadOnlyDictionary<string, CanonicalBeforeImage> commonBeforeImages)
    {
        if (_mortalQteDecisionImages is null) return Array.Empty<ValidationIssue>();
        foreach (var pair in _mortalQteDecisionImages)
        {
            var current = await _fs.ReadFileBytesAsync(lease, pair.Key);
            if (!commonBeforeImages.TryGetValue(pair.Key, out var common) || common.Existed != pair.Value.Existed ||
                !Same(common.Bytes, pair.Value.Bytes) || !Same(current, pair.Value.Bytes))
                return new[] { new ValidationIssue(pair.Key, IssueSeverity.Error,
                    "The raw decision, ordinary scene or issued wound command changed before the common seal.",
                    "mortal_wound_decision_staged_input_changed") };
        }
        return Array.Empty<ValidationIssue>();

        static bool Same(byte[]? left, byte[]? right) => left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
    }

    private async Task RevokeOwnedMortalQteCommandAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, lease);
        EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, lease);
        _mortalQteDecisionImages = null;
        _mortalQteExactReplay = null;
        _mortalQteIssuedOpportunity = null;
        if (_mortalQteOwnedCommand is not { } issued) return;
        var current = await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        if (current is not null && current.AsSpan().SequenceEqual(issued))
        {
            var removed = await CoordinatedStateWriteHelper.TryCommitAsync(_fs, lease,
                new CoordinatedStateWriteHelper.PlannedWrite(AcceptedMechanicsPlan.WoundCommandPath, null, null,
                    RequireCurrentBaseline: true, ExactPrevious: new CanonicalBeforeImage(true, issued)));
            if (!removed) throw new InvalidDataException("mortal_wound_decision_owned_command_changed: revocation failed closed.");
        }
        _mortalQteOwnedCommand = null;
    }
}
