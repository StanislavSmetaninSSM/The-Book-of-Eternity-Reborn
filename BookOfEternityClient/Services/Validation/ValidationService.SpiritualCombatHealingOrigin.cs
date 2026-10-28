using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
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
