using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class MortalQteDecisionPublication
{
    private static readonly string[] JointInterfaceFields = ["version", "outputBasisFingerprint", "interfaceResultFingerprint",
        "publishedRecordFingerprint", "repairCheckpointFingerprint", "commonAuthorityFingerprint", "inputFingerprint",
        "preparedPlanFingerprint", "fingerprint"];

    private static JsonObject CreateJointInterfaceSeal(MortalJointRepairPublicationTakeReceipt taken)
    {
        var basis = taken.Authority.OutputBasis;
        var record = basis.InterfacePublication ?? throw Invalid("joint_interface_taken_result_missing");
        var root = new JsonObject
        {
            ["version"] = 2, ["outputBasisFingerprint"] = basis.Fingerprint,
            ["interfaceResultFingerprint"] = record.ResultFingerprint, ["publishedRecordFingerprint"] = record.Fingerprint,
            ["repairCheckpointFingerprint"] = taken.Authority.RepairCheckpointFingerprint,
            ["commonAuthorityFingerprint"] = taken.Authority.Fingerprint, ["inputFingerprint"] = taken.Plan.InputFingerprint,
            ["preparedPlanFingerprint"] = taken.Plan.PreparedPlanFingerprint
        };
        root["fingerprint"] = JointInterfaceSeal(root);
        return root;
    }

    private static async Task RequireJointInterfaceDataAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, JsonObject marker, MortalJointAcceptedOutputBasis basis)
    {
        var record = basis.InterfacePublication;
        if (record is null)
        {
            if (marker.ContainsKey("niPublication")) throw Invalid("joint_legacy_unexpected_interface_seal");
            return;
        }
        if (record.State != "Published" || record.HasErrors) throw Invalid("joint_interface_not_published_no_error");
        var ni = ClosedMap(marker["niPublication"], JointInterfaceFields);
        var payload = ni.DeepClone().AsObject(); payload.Remove("fingerprint");
        if (JointInteger(ni, "version") != 2 || Text(ni, "fingerprint") != JointInterfaceSeal(payload) ||
            Text(ni, "outputBasisFingerprint") != basis.Fingerprint ||
            Text(ni, "interfaceResultFingerprint") != record.ResultFingerprint ||
            Text(ni, "publishedRecordFingerprint") != record.Fingerprint ||
            Text(ni, "inputFingerprint") != Text(marker, "inputFingerprint") ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text(ni, "commonAuthorityFingerprint")) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text(ni, "preparedPlanFingerprint")))
            throw Invalid("joint_interface_common_links");
        var binding = record.Binding;
        if (Text(binding, "sessionGeneration") != Text(marker, "generation") ||
            Text(binding, "sessionId") != Text(marker, "sessionId") || Text(binding, "requestId") != Text(marker, "requestId") ||
            Text(binding, "snapshotToken") != Text(marker, "snapshotToken") || JointInteger(binding, "turnNumber") != JointInteger(marker, "turn") ||
            Text(binding, "realm") != "mortal_world") throw Invalid("joint_interface_original_generation");
        var treatment = ClosedMap(marker["jointTreatment"], JointTreatmentFields);
        if (new[] { "operationKey", "attemptId", "requestFingerprint" }.Any(name => Text(binding, name) != Text(treatment, name)) ||
            Text(binding, "checkpointFingerprint") != Text(treatment, "repairCheckpointFingerprint") ||
            Text(ni, "repairCheckpointFingerprint") != Text(treatment, "repairCheckpointFingerprint"))
            throw Invalid("joint_interface_treatment_history_links");
        if (!MortalJointInterfacePublication.BytesEqual(record.AfterBytes,
                await files.ReadFileBytesAsync(lease, MortalJointInterfacePublication.InterfacePath)))
            throw Invalid("joint_interface_current_published_image");
        // The complete parent proof checks original/history/physical footprint and
        // progression. Consumed live plans/checkpoints/takes are NOT reconstructed
        // here, and a durable DATA record does not claim an old adapter return.
    }

    internal static async Task<MortalJointInterfacePublication.Record?> ReadCurrentJointInterfaceDataAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        string sessionId, string requestId, string snapshotToken, int turn)
    {
        var proof = await ReadCompletedProofAsync(files, lease, ReadInspectionSnapshot(files, lease));
        if (proof.Root["schemaVersion"]?.GetValue<int>() != 2) return null;
        RequireJointCompletionTuple(proof, sessionId, requestId, snapshotToken, turn);
        return MortalJointAcceptedOutputBasis.Read(proof.Root["outputBinding"]).InterfacePublication;
    }

    internal static async Task<MortalJointInterfacePublication.Record?> ReadJointInterfaceAtOrdinarySlotAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        var bytes = await files.ReadFileBytesAsync(lease, StatePath);
        if (bytes is null) return null;
        var marker = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "current ordinary-slot joint selector")
            ?? throw Invalid("joint_interface_ordinary_marker_null");
        var jointClaim = marker.ContainsKey("outputBinding") || marker.ContainsKey("niPublication") ||
            marker.ContainsKey("jointTreatment") || marker.ContainsKey("consumedPrivatePaths") || marker.ContainsKey("progression");
        if (marker["schemaVersion"]?.GetValue<int>() != 2)
        {
            if (jointClaim) throw Invalid("joint_interface_selected_parent_version_changed");
            return null; // genuine old non-joint publication, not a claimed NI downgrade
        }
        var original = ReadInspectionSnapshot(files, lease);
        if (original.SessionId != Text(marker, "sessionId") || original.RequestId != Text(marker, "requestId") ||
            original.SnapshotToken != Text(marker, "snapshotToken") || original.TurnNumber != JointInteger(marker, "turn"))
        {
            // A tuple mismatch by itself is not a new operation: one corrupt
            // request/turn field must not request another ordinary I projection.
            // A genuine new original already signed the EXACT prior marker as a
            // baseline before its new GM outputs; use that actual source witness.
            if (!original.CoveredLogicalPaths.Contains(StatePath, StringComparer.Ordinal) ||
                !Same(bytes, original.ReadRequiredBytes(StatePath))) throw Invalid("joint_interface_selector_tuple_conflict");
            return null;
        }
        // Even a claimed legacy child is fully checked before permitting its old
        // writer. Partial deletion/downgrade cannot exploit a null memory cache.
        return await ReadCurrentJointInterfaceDataAsync(files, lease, original.SessionId,
            original.RequestId, original.SnapshotToken, original.TurnNumber);
    }

    private static string JointInterfaceSeal(JsonObject root) => WoundAcceptedTurnFingerprintWriter.Compute(new[]
    { "book_of_eternity.mortal_joint_interface.common_publication.v2", WoundAcceptedTurnFingerprintWriter.CanonicalJson(root) });
}
