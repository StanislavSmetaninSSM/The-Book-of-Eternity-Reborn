using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static class ShiningPatientMemoryTransport
{
    // Classifies transport once before any distribution writes. The actual
    // Continuation independently admits every row; transport never grants it.
    internal static async Task<bool> RoutesOrdinaryResidentsAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, GameResponse response)
    {
        if (!response.ResidentThoughtJournalUpdatesSpecified) return false;
        var requiresPatientRoute = response.ResidentThoughtsFromPatientInterface ||
            response.SpiritualShiningPatientMemory.ValueKind != JsonValueKind.Undefined;
        var requestJson = await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath);
        if (requestJson is null)
        {
            if (requiresPatientRoute)
                throw new InvalidDataException("Patient memory transport lost its turn.");
            return false;
        }
        var turn = StrictJsonAuthority.Deserialize<TurnRequest>(requestJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "resident memory transport turn")!;
        if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(turn.ProgressionControl?.CurrentRealm, out var realm) || realm != "shining_abode")
        {
            if (requiresPatientRoute)
                throw new InvalidDataException("A private patient response cannot fall back outside its Shining turn.");
            return false;
        }
        var observed = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
            new PendingTurnSnapshotPathSelection(new[] { WoundCarrierCatalog.AfterlifeProfilesPath }, new[] { AcceptedMechanicsPlan.WoundCommandPath }));
        if (!observed.Success || observed.Snapshot is not { } snapshot || observed.Evidence is not { } evidence ||
            snapshot.SessionId != turn.SessionId || snapshot.RequestId != turn.RequestId || snapshot.TurnNumber != turn.TurnNumber)
            throw new InvalidDataException("Resident memory transport requires its actual signed turn.");
        evidence.RequireCurrent(files, lease, snapshot);
        bool route;
        if (snapshot.CoveredLogicalPaths.Contains(AcceptedMechanicsPlan.WoundCommandPath, StringComparer.Ordinal))
        {
            var retained = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)), new JsonSerializerOptions(), "resident memory retained request")!;
            if (SpiritualDiagnosisRequestCodec.IsKind(retained))
            {
                if (turn.ProgressionControl is null || !SpiritualDiagnosisRequestCodec.IsBound(retained,
                    new(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber, turn.ProgressionControl)) || requiresPatientRoute)
                    throw new InvalidDataException("Diagnosis cannot use private patient memory transport.");
                evidence.RequireCurrent(files, lease, snapshot);
                return false; // Ordinary resident commands keep their existing canonical owner.
            }
            var selection = SpiritualSelfTreatmentRequestCodec.ReadActiveSelection(retained);
            if (turn.ProgressionControl is null || !SpiritualSelfTreatmentRequestCodec.IsBoundActive(retained,
                new(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber, turn.ProgressionControl), selection,
                realm, turn.ProgressionControl.NextChaosSeaTurnOrdinal))
                throw new InvalidDataException("Resident memory transport has an invalid retained treatment family.");
            route = selection.Aid?.ShiningPatient is not null;
        }
        else
        {
            var original = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                snapshot.ReadRequiredBytes(WoundCarrierCatalog.AfterlifeProfilesPath)), new JsonSerializerOptions(), "original memory profiles")!;
            var current = StrictJsonAuthority.Deserialize<JsonObject>((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!,
                new JsonSerializerOptions(), "current memory profiles")!;
            var changes = SpiritualTreatmentParticipation.ReadChanges(current, original);
            route = changes.Count == 1 && SpiritualTreatmentParticipation.IsShiningPatientParticipation(changes[0].Proposal);
            if (!route && changes.Any(c => SpiritualTreatmentParticipation.IsShiningPatientParticipation(c.Proposal)))
                throw new InvalidDataException("Resident memory transport requires one exact Shining patient.");
        }
        evidence.RequireCurrent(files, lease, snapshot);
        if (requiresPatientRoute && !route)
            throw new InvalidDataException("A private patient response cannot fall back to canonical resident writes.");
        if (route && response.ResidentThoughtJournalUpdates is null)
            throw new InvalidDataException("Patient ordinary resident memory cannot be JSON null.");
        return route;
    }
}
