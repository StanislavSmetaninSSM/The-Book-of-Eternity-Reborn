using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Wire classification for this one original intake operation only. It is
    // issued from observed signed bytes, not from the live command kind, and
    // cannot create an effect plan or authorize healing. Full admission remains
    // PrepareCombatHealingOriginAsync after the real owners are constructed.
    private sealed class SpiritualOriginalCombatCommandClassification
    {
        private readonly PendingTurnSnapshotReadAuthority _snapshot;
        private readonly PendingTurnSnapshotReader.OriginalReadEvidence _evidence;
        private readonly SpiritualCombatHealingIntent _intent;
        private readonly CanonicalBeforeImage _image;

        private SpiritualOriginalCombatCommandClassification(PendingTurnSnapshotReadAuthority snapshot,
            PendingTurnSnapshotReader.OriginalReadEvidence evidence, SpiritualCombatHealingIntent intent, byte[] bytes)
        { _snapshot = snapshot; _evidence = evidence; _intent = intent; _image = new(true, bytes); }

        internal static SpiritualOriginalCombatCommandClassification? Read(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, PendingTurnSnapshotReadAuthority expectedOriginal,
            SpiritualOriginalDraftInputs current)
        {
            if (!expectedOriginal.DeclaredOriginalLogicalPaths.Contains(AcceptedMechanicsPlan.WoundCommandPath, StringComparer.Ordinal))
                return null;
            var read = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease, new[] { AcceptedMechanicsPlan.WoundCommandPath });
            if (!read.Success || read.Snapshot is not { } actual || read.Evidence is not { } evidence ||
                actual.SessionId != expectedOriginal.SessionId || actual.RequestId != expectedOriginal.RequestId ||
                actual.SnapshotToken != expectedOriginal.SnapshotToken || actual.TurnNumber != expectedOriginal.TurnNumber ||
                actual.Realm != expectedOriginal.Realm)
                throw new InvalidDataException("Original command classification lost its actual signed input.");
            var bytes = actual.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath);
            JsonObject? root;
            try
            {
                root = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                    new JsonSerializerOptions(), "original command classification");
            }
            catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
            { return null; } // The unchanged ordinary parser owns malformed noncombat input.
            if (root is null || !SpiritualCombatHealingIntent.IsKind(root)) return null;
            var intent = SpiritualCombatHealingIntent.Parse(root);
            if (!intent.Matches(actual.SessionId, actual.RequestId, actual.TurnNumber))
                throw new InvalidDataException("Original combat command has a foreign tuple.");
            evidence.RequireCurrent(files, lease, actual);
            // A different effective GM command still goes through ordinary
            // parsing; only the exact retained client marker is omitted there.
            if (current.ReadImage(AcceptedMechanicsPlan.WoundCommandPath).Fingerprint != new CanonicalBeforeImage(true, bytes).Fingerprint)
                return null;
            return new(actual, evidence, intent, bytes);
        }

        internal bool Matches(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            ValidationPendingTurnSnapshotManifest manifest, CanonicalBeforeImage effective)
        {
            if (_snapshot.SessionId != manifest.SessionId || _snapshot.RequestId != manifest.RequestId ||
                _snapshot.SnapshotToken != manifest.ManifestPayloadHash || _snapshot.TurnNumber != manifest.TurnNumber ||
                manifest.ProgressionControl is null || !_intent.IsBound(new(manifest.SessionId, manifest.RequestId,
                    manifest.TurnNumber, manifest.ProgressionControl), _intent.Selection))
                throw new InvalidDataException("Original combat wire differs from the actual validated control.");
            RequireCurrent(files, lease);
            return effective.Fingerprint == _image.Fingerprint;
        }

        internal void RequireCurrent(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
            _evidence.RequireCurrent(files, lease, _snapshot);

        internal bool MatchesExactImage(CanonicalBeforeImage current) => current.Fingerprint == _image.Fingerprint;
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        // Inventory metadata selects an actual read; it grants no byte authority.
        internal bool HasDeclaredCombatInput => _original.DeclaredOriginalLogicalPaths.Contains(
            AcceptedMechanicsPlan.WoundCommandPath, StringComparer.Ordinal);
        internal byte[]? OriginalTurnRequestBytes => _originalRequestBytes?.ToArray();
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private CombatHealingOrigin? _combatHealingOrigin;
        private SpiritualOriginalCombatCommandClassification? _originalCombatCommand;

        /// <summary>
        /// Identifies only this current capture's unchanged signed client marker before resource execution.
        /// Full healing origin admission remains mandatory inside BeginResourceExecutionAsync.
        /// </summary>
        /// <param name="lease">Current canonical lease borrowed from the initial continuation entry.</param>
        /// <returns>Whether fresh original evidence and physical command bytes retain the classified marker.</returns>
        internal async Task<bool> HasCurrentOriginalCombatCommandAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                EnsureCurrent(lease);
                if (_originalCombatCommand is not { } classified) return false;
                classified.RequireCurrent(_validator._fs, lease);
                var bytes = await _validator._fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
                classified.RequireCurrent(_validator._fs, lease);
                EnsureCurrent(lease);
                return classified.MatchesExactImage(new CanonicalBeforeImage(bytes is not null, bytes));
            }
            finally { _gate.Release(); }
        }

        // Immutable parsed DATA survives the C2 capture. Original read evidence is
        // always reissued for the actual current lease; a prior lease is not reused.
        private sealed class CombatHealingOrigin(SpiritualCombatHealingIntent intent,
            AfterlifeWoundTreatmentAuthority.SelectedWoundSourceData source, ProgressionControl control,
            PendingTurnSnapshotReadAuthority snapshot)
        {
            internal SpiritualCombatHealingIntent Intent { get; } = intent;
            internal AfterlifeWoundTreatmentAuthority.SelectedWoundSourceData Source { get; } = source;
            internal ProgressionControl Control { get; } = control;
            internal IReadOnlyList<int> AcceptedDice => _snapshot.AcceptedD20EventValues;
            private readonly PendingTurnSnapshotReadAuthority _snapshot = snapshot;

            internal ProgressionScheduleService.CombatSchedulerOriginal ExportSchedulerOriginal() => new(Intent,
                new(true, _snapshot.ReadRequiredBytes(ProgressionScheduleService.SchedulePath)),
                new(false, null), new(_snapshot.CoveredLogicalPaths.Contains(EffectAcceptedTurnInputComposer.WorldTimePath, StringComparer.Ordinal),
                    _snapshot.CoveredLogicalPaths.Contains(EffectAcceptedTurnInputComposer.WorldTimePath, StringComparer.Ordinal)
                        ? _snapshot.ReadRequiredBytes(EffectAcceptedTurnInputComposer.WorldTimePath) : null));

            internal void RequireCurrent(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
            {
                var read = Read(files, lease);
                if (!read.Success || read.Snapshot is not { } current || read.Evidence is not { } evidence ||
                    current.SessionId != _snapshot.SessionId || current.RequestId != _snapshot.RequestId ||
                    current.SnapshotToken != _snapshot.SnapshotToken || current.TurnNumber != _snapshot.TurnNumber ||
                    current.Realm != _snapshot.Realm || !current.AcceptedD20EventValues.SequenceEqual(_snapshot.AcceptedD20EventValues) ||
                    !current.CoveredLogicalPaths.SequenceEqual(_snapshot.CoveredLogicalPaths) ||
                    !current.AbsentLogicalPaths.SequenceEqual(_snapshot.AbsentLogicalPaths) ||
                    current.CoveredLogicalPaths.Any(path => !current.ReadRequiredBytes(path).AsSpan().SequenceEqual(_snapshot.ReadRequiredBytes(path))))
                    throw new InvalidDataException("Combat healing lost its actual signed original source.");
                evidence.RequireCurrent(files, lease, current);
            }

            internal static PendingTurnSnapshotObservedReadResult Read(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
                PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
                    PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                        new[] { AcceptedMechanicsPlan.WoundCommandPath }, AfterlifeWoundTreatmentAuthority.CombatSourcePaths));
        }

        private Task<IReadOnlyList<ValidationIssue>> PrepareCombatHealingOriginAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            try
            {
                if (_combatHealingOrigin is not null)
                {
                    _combatHealingOrigin.RequireCurrent(_validator._fs, lease);
                    return Task.FromResult<IReadOnlyList<ValidationIssue>>(Array.Empty<ValidationIssue>());
                }
                if (!_source.HasDeclaredCombatInput) return Task.FromResult<IReadOnlyList<ValidationIssue>>(Array.Empty<ValidationIssue>());
                // Probe only the declared input first. Noncombat paths retain their
                // original source consumers and do not acquire the combat read set.
                var probe = PendingTurnSnapshotReader.ReadObservedCurrent(_validator._fs, lease,
                    new[] { AcceptedMechanicsPlan.WoundCommandPath });
                if (!probe.Success || probe.Snapshot is not { } probeSnapshot || probe.Evidence is not { } probeEvidence)
                    return Task.FromResult(probe.Issues);
                var root = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                    probeSnapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)), new JsonSerializerOptions(), "actual combat intent");
                probeEvidence.RequireCurrent(_validator._fs, lease, probeSnapshot);
                if (root is null || !SpiritualCombatHealingIntent.IsKind(root))
                    return Task.FromResult<IReadOnlyList<ValidationIssue>>(Array.Empty<ValidationIssue>());
                var read = CombatHealingOrigin.Read(_validator._fs, lease);
                if (!read.Success || read.Snapshot is not { } snapshot || read.Evidence is not { } evidence)
                    return Task.FromResult(read.Issues);
                if (snapshot.SessionId != _source.SessionId || snapshot.RequestId != _source.RequestId ||
                    snapshot.SnapshotToken != _source.SnapshotToken || snapshot.TurnNumber != _source.TurnNumber)
                    throw new InvalidDataException("Combat original differs from its actual source capture.");
                var intent = SpiritualCombatHealingIntent.Parse(root);
                var source = AfterlifeWoundTreatmentAuthority.ReadCombatOriginalSource(snapshot, intent, out var control);
                var requestBytes = _source.OriginalTurnRequestBytes ?? throw new InvalidDataException("Combat original request is absent.");
                var request = StrictJsonAuthority.Deserialize<TurnRequest>(CanonicalJsonUtf8.DecodeOneOptionalBom(requestBytes),
                    new JsonSerializerOptions(), "actual combat turn request") ?? throw new InvalidDataException("Combat request is malformed.");
                if (request.ProgressionControl is null || !intent.IsBound(new(request.SessionId, request.RequestId, request.TurnNumber,
                        request.ProgressionControl), intent.Selection) || !JsonNode.DeepEquals(request.SpiritualHealing, intent.CreateGmProjection(source)))
                    throw new InvalidDataException("Combat request/control/projection differs from the actual signed intent.");
                evidence.RequireCurrent(_validator._fs, lease, snapshot);
                _combatHealingOrigin = new(intent, source, control, snapshot);
                return Task.FromResult<IReadOnlyList<ValidationIssue>>(Array.Empty<ValidationIssue>());
            }
            catch (Exception error) when (error is not CoordinatedStatePublicationUncertainException &&
                (error is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException))
            {
                return Task.FromResult<IReadOnlyList<ValidationIssue>>(new[] { SourceIssue(AcceptedMechanicsPlan.WoundCommandPath,
                    "spiritual_combat_healing_original_invalid", error.Message) });
            }
        }
    }
}
