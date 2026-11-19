using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;

// Fixture-only DATA fence. Transport coordinates never replace the held owner.
internal sealed class DeclaredBridgeFixtureAdmission
{
    private readonly string root, session, pipe, generation;
    private readonly TerminalIdentity owner;
    private readonly GmSessionRunIdentity identity;
    private BridgeTransportEndpoint? endpoint;

    internal DeclaredBridgeFixtureAdmission(string root, string pipe, TerminalIdentity owner,
        string generationJson, byte[] recordBytes)
    {
        if (!Path.IsPathFullyQualified(root) || Path.GetFullPath(root) != root)
            throw new InvalidDataException("Fixture root must be canonical absolute.");
        this.root=root;session=Path.Combine(root,"game_session");this.pipe=pipe;this.owner=owner;
        generation=FileSystemManager.ParseSessionGenerationText(generationJson);
        var record=GmSessionRunRecordCodec.Decode(recordBytes);identity=record.Identity;
        CheckRecord(record,generationJson);
    }
    private void CheckRecord(GmSessionRunRecord record,string generationJson)
    {
        if (record.Disposition!=GmSessionRunDisposition.Running ||
            !GmSessionRunValidation.AdmissionRootMatches(root,record.Identity.RootKey,GmSessionRunBackend.LinuxSupervisor) ||
            record.Identity.Backend!=GmSessionRunBackend.LinuxSupervisor ||
            record.Identity.RunId!=owner.RunId || record.Identity.GenerationId!=generation ||
            FileSystemManager.ParseSessionGenerationText(generationJson)!=generation ||
            !GmSessionRunValidation.IdentityMatches(identity,record.Identity))
            throw new InvalidDataException("Original durable fixture identity/generation changed.");
    }
    internal string BeforeConnect(string statusJson,string generationJson,byte[] recordBytes,
        bool rootExited,bool authorityLost)
    {
        if(rootExited || authorityLost)throw new InvalidDataException("Original physical owner lost.");
        CheckRecord(GmSessionRunRecordCodec.Decode(recordBytes),generationJson);
        using var doc=JsonDocument.Parse(statusJson);var status=doc.RootElement;
        var observed=BridgeTransportConnector.DecodeStatus(status);
        if(status.GetProperty("sessionPath").GetString()!=session || status.GetProperty("pipeName").GetString()!=pipe ||
            status.GetProperty("terminalRunId").GetString()!=owner.RunId || status.GetProperty("shellPid").GetInt32()!=owner.RootPid ||
            status.GetProperty("terminalUncertain").GetBoolean())throw new InvalidDataException("Published original owner mismatch.");
        if(endpoint is not null && endpoint!=observed)throw new InvalidDataException("Declared endpoint drift.");
        endpoint=observed;
        return statusJson; // Same admitted bytes, not a second status read.
    }
    internal void CheckReply(JsonElement reply,bool shutdown)
    {
        if(!reply.GetProperty("ok").GetBoolean())throw new InvalidDataException("Original RPC refused.");
        var status=reply.GetProperty("status");
        if(BridgeTransportConnector.DecodeStatus(status)!=endpoint || status.GetProperty("sessionPath").GetString()!=session ||
            status.GetProperty("pipeName").GetString()!=pipe)throw new InvalidDataException("Reply endpoint/root changed.");
        // The existing caller checks the exact shutdown receipt. Retirement must
        // not be rejected merely because it correctly relinquished retention.
        if(!shutdown && (status.GetProperty("terminalRunId").GetString()!=owner.RunId ||
            status.GetProperty("shellPid").GetInt32()!=owner.RootPid || status.GetProperty("terminalUncertain").GetBoolean() ||
            !status.GetProperty("terminalOwnerRetained").GetBoolean()))
            throw new InvalidDataException("Reply did not retain original live owner.");
    }
}
