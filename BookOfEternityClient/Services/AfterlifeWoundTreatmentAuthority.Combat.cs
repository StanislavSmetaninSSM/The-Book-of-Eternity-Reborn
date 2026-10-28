using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundTreatmentAuthority
{
    internal static bool IsSelectedCombatConflict(JsonObject active, SpiritualCombatTreatmentSelection selected) =>
        String(active["conflictId"]) == selected.ConflictId &&
        AfterlifeEntityProfileState.TryNormalizeEffectRealm(String(active["realm"]), out var realm) && realm == "chaos_sea" &&
        String(active["resolutionState"]) == "active" &&
        String(active["sideModel"]) == "direct_duel" &&
        String(active["playerSide"]?["leadContestant"]?["actorType"]) == "player_soul" &&
        String(active["playerSide"]?["leadContestant"]?["actorId"]) == "player_soul" &&
        active["exchangeLog"] is JsonArray log && log.Count < int.MaxValue && selected.NextExchangeOrdinal == log.Count + 1 &&
        SpiritualCombatHealingIntent.ConflictFingerprint(active) == selected.ExpectedConflictFingerprint;

    internal sealed record CombatTurnStageResult(ProgressionControl? Control, SpiritualSelfTreatmentStageResult Stage);

    // No entropy dependency: the ordinary Engine pool is attached only after
    // this intent and its no-cycle control have been staged under one actual lease.
    internal static async Task<CombatTurnStageResult> StagePlayerCombatTurnAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, ProgressionScheduleService scheduler,
        string sessionId, string requestId, int turn, SpiritualSelfTreatmentSelection selection)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        selection = SpiritualSelfTreatmentSelection.Parse(selection.ToJson());
        if (selection.Combat is not { } combat || selection.Aid is not null ||
            !ResourceMaterializationContract.IsExactIdentifier(sessionId) || !ResourceMaterializationContract.IsExactIdentifier(requestId) || turn < 1)
            return Failure("context_invalid");
        try
        {
            var pending = await ReadSourceAsync(files, lease, AcceptedMechanicsPlan.WoundCommandPath);
            if (pending.Image.Existed) return Failure("pending_conflict");
            var schedulerData = await scheduler.BuildCombatControlForNextTurnAsync(lease, combat);
            var sources = new List<SourceFile>();
            foreach (var path in CombatSourcePaths)
            {
                var observed = schedulerData.Sources.SingleOrDefault(s => s.Path == path);
                if (observed is not null) { sources.Add(observed); continue; }
                if (SourcePaths.Contains(path, StringComparer.Ordinal)) sources.Add(await ReadSourceAsync(files, lease, path));
                else
                {
                    var bytes = await files.ReadFileBytesAsync(lease, path);
                    sources.Add(new(path, new(bytes is not null, bytes), null));
                }
            }
            if (SpiritualTreatmentParticipation.HasUnsettledChange(sources.Single(s => s.Path == WoundCarrierCatalog.AfterlifeProfilesPath).Root))
                return Failure("participation_change_unsupported");
            var selected = PrepareSelectedWoundSource(selection, sources, "chaos_sea");
            if (selected.Data is not { } data) return Failure(selected.IssueCode!);
            string? Text(string path)
            {
                var source = sources.Single(s => s.Path == path);
                return source.Image.Bytes is { } bytes ? CanonicalJsonUtf8.DecodeOneOptionalBom(bytes) : null;
            }
            var definitions = ResourceDefinitionCatalog.ParseCanonical(Text(ResourceMaterializationContract.DefinitionsPath), false);
            if (!definitions.IsValid || definitions.Catalog is null) return Failure("resources_invalid");
            var state = ResourceStateContract.ParseCanonical(Text(ResourceMaterializationContract.StatePath), definitions.Catalog, false);
            var history = ResourceHistoryState.ParseCanonical(Text(ResourceMaterializationContract.HistoryPath), definitions.Catalog, false);
            if (!state.IsValid || !history.IsValid || history.History!.ValidateStateAgreement(state.Ledger!).Count != 0)
                return Failure("resources_invalid");
            var owners = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions.Catalog,
                path => Task.FromResult(Text(path)), state.Ledger, history.History, CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
            if (!owners.IsValid || owners.Authority!.ValidateCanonicalAgreement(state.Ledger!, history.History!).Count != 0)
                return Failure("resources_invalid");
            var key = new ResourceOwnerKey("chaos_sea", ResourceOwnerKind.AfterlifeActor, "player_soul");
            var coordinate = new ResourceCoordinate(key.Realm, key.OwnerKind, key.ResourceOwnerId, "spiritual_action_points");
            if (!owners.Authority.ResolveAcceptedCoordinate(key, coordinate.ResourceKey).Success ||
                !state.Ledger!.TryResolveExact(coordinate, out var entry) || entry is not { State: ResourceLifecycleState.Active } ||
                !AfterlifeActionCostRules.TryGetDefinition(AfterlifeActionCostRules.SpiritualHealingOperation, out var definition) ||
                entry.Current < AfterlifeActionCostRules.ResolveStandardEffectiveCost(definition, data.Source.HealingTier))
                return Failure("action_points_insufficient");
            var intent = SpiritualCombatHealingIntent.Create(sessionId, requestId, turn, selection,
                data.Source.HealingTier, JsonSerializer.SerializeToNode(schedulerData.Control)!.AsObject(), sources);
            using var freshClosing = FileSystemManager.SuppressValidationReuse();
            var writes = sources.Select(s => CoordinatedStateWriteHelper.CreateExactGuardWrite(s.Path, s.Image))
                .Append(new(AcceptedMechanicsPlan.WoundCommandPath, null, intent.Root.ToJsonString(),
                    RequireCurrentBaseline: true, ExactPrevious: pending.Image)).ToArray();
            if (!await CoordinatedStateWriteHelper.TryCommitAsync(files, lease, writes)) return Failure("source_changed");
            return new(schedulerData.Control, new(true, false, null, intent.Fingerprint) { GmContext = intent.CreateGmProjection(data) });
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (InvalidDataException or InvalidOperationException or ArgumentException or JsonException or OverflowException))
        { return Failure("source_invalid"); }
    }

    private static CombatTurnStageResult Failure(string suffix) =>
        new(null, new(false, false, "spiritual_combat_treatment_" + suffix, null));

    // Detached original DATA only. The calling capture owns the real signed
    // reader/currentness proof and must keep its actual resource/effect authority.
    internal static SelectedWoundSourceData ReadCombatOriginalSource(PendingTurnSnapshotReadAuthority snapshot,
        SpiritualCombatHealingIntent intent, out ProgressionControl control)
    {
        if (!intent.Matches(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber) || snapshot.Realm != "chaos_sea")
            throw new InvalidDataException("Combat intent belongs to another actual original.");
        var sources = new List<SourceFile>();
        foreach (var path in CombatSourcePaths)
        {
            byte[]? bytes = snapshot.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal) ? snapshot.ReadRequiredBytes(path)
                : snapshot.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal) ? null
                : throw new InvalidDataException("Combat original has no byte or absence proof for " + path);
            var root = SourcePaths.Contains(path, StringComparer.Ordinal) && bytes is not null
                ? StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), path) : null;
            sources.Add(new(path, new(bytes is not null, bytes), root));
        }
        var rootIntent = intent.Root;
        var expected = rootIntent["sourceImages"]!.AsArray();
        if (sources.Where((s, i) => expected[i]!["fingerprint"]!.GetValue<string>() != s.Image.Fingerprint).Any())
            throw new InvalidDataException("Combat intent differs from its exact signed source images.");
        JsonObject? Root(string path) => sources.Single(s => s.Path == path).Root;
        if (Root(AfterlifeSpiritualConflictState.StatePath)?["activeConflict"] is not JsonObject active ||
            !IsSelectedCombatConflict(active, intent.Selection.Combat!) ||
            !ProgressionScheduleService.TryBuildSpiritualCombatControl(Root(ProgressionScheduleService.SchedulePath),
                sources.Single(s => s.Path == EffectAcceptedTurnInputComposer.WorldTimePath).Image.Bytes, out var expectedControl) ||
            !intent.IsBound(new(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber, expectedControl!), intent.Selection))
            throw new InvalidDataException("Combat original does not bind its selected direct duel and exact no-cycle control.");
        var selected = PrepareSelectedWoundSource(intent.Selection, sources, "chaos_sea");
        if (selected.Data is not { } data || data.Source.HealingTier != intent.HealingTier)
            throw new InvalidDataException("Combat original wound/art/effect source differs from staging.");
        control = expectedControl!;
        return data;
    }
}
