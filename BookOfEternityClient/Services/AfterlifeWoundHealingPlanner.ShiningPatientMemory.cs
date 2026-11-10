using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal sealed partial class Continuation
    {
        private JsonArray? _ordinaryResidentThoughtCommands;
        private JsonObject? _radiantThoughtCommand;
        private SpiritualTreatmentParticipation.ShiningPatientMemoryProgram? _patientMemory;
        private PendingTurnSnapshotReadAuthority? _memoryRosterSnapshot;
        private PendingTurnSnapshotReader.OriginalReadEvidence? _memoryRosterEvidence;
        private CanonicalBeforeImage? _memoryRosterImage;
        private bool HasPatientMemoryResponse => _ordinaryResidentThoughtCommands is not null || _radiantThoughtCommand is not null;

        private async Task BindShiningPatientMemoryAsync(JsonObject? roster)
        {
            var record = ResidentThoughtRecord;
            if (record is null || !SpiritualTreatmentParticipation.IsShiningPatientParticipation(record))
            {
                if (HasPatientMemoryResponse) throw new InvalidDataException("Patient memory requires one actual schema5 participation.");
                return;
            }
            if (SpiritualTreatmentParticipation.RecordOwner(record).OwnerKind == "radiant_actor" && _radiantThoughtCommand is null)
                throw new InvalidDataException("The actual radiant patient must supply its explicit subjective memory program.");
            if (_residentThoughtCommands is null && !HasPatientMemoryResponse) return;
            if (_patientMemory is not null) throw new InvalidDataException("Patient memory is bound once per continuation.");
            // Radiant admission has no roster dependency. Only an actual ordinary
            // resident append adds this independently signed, frozen source.
            if (roster is null && _ordinaryResidentThoughtCommands is not null)
            {
                var path = GuardianAbodeResidentState.StatePath;
                var observed = PendingTurnSnapshotReader.ReadObservedCurrent(_files, _lease, new[] { path });
                if (!observed.Success || observed.Snapshot is not { } snapshot || observed.Evidence is not { } evidence ||
                    snapshot.SessionId != _snapshot.SessionId || snapshot.RequestId != _snapshot.RequestId ||
                    snapshot.TurnNumber != _snapshot.TurnNumber || snapshot.SnapshotToken != _snapshot.SnapshotToken)
                    throw new InvalidDataException("Ordinary patient memory lacks its same signed roster.");
                evidence.RequireCurrent(_files, _lease, snapshot);
                var original = snapshot.ReadRequiredBytes(path);
                var current = await _files.ReadFileBytesAsync(_lease, path);
                if (current is null || !current.AsSpan().SequenceEqual(original))
                    throw new InvalidDataException("Ordinary patient memory requires its unchanged original roster.");
                roster = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(original), new JsonSerializerOptions(), path)!;
                _memoryRosterSnapshot = snapshot; _memoryRosterEvidence = evidence; _memoryRosterImage = new(true, current);
            }
            _patientMemory = SpiritualTreatmentParticipation.PrepareShiningPatientMemory(
                SpiritualTreatmentParticipation.RecordOwner(record), SpiritualTreatmentParticipation.RecordLocality(record),
                (_parsedSourceRoots[WoundCarrierCatalog.AfterlifeProfilesPath] as JsonObject)!, roster,
                (_parsedSourceRoots[ShiningAbodeState.StatePath] as JsonObject)!, _input.Binding.Turn,
                _ordinaryResidentThoughtCommands, _residentThoughtCommands, _radiantThoughtCommand);
        }

        internal void RequireMemoryProgramsBound()
        {
            if (HasPatientMemoryResponse && _patientMemory is null)
                throw new InvalidDataException("Patient memory has no actual bound schema5 natural8/active8 participant.");
        }

        private void RequireConditionalMemoryCurrent()
        {
            if (_memoryRosterImage is not { } image) return;
            _memoryRosterEvidence!.RequireCurrent(_files, _lease, _memoryRosterSnapshot!);
            var current = _files.ReadFileBytesSync(_lease, GuardianAbodeResidentState.StatePath);
            if (new CanonicalBeforeImage(current is not null, current).Fingerprint != image.Fingerprint)
                throw new InvalidDataException("The conditional patient-memory roster changed.");
        }
    }
}
