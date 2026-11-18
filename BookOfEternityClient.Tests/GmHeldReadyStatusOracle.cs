#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace BookOfEternityClient.Tests;

// Pure read-only evidence oracle; also replayable against retained failed test runs.
public static class GmHeldReadyStatusOracle
{
    public static string Verify(string scenarioJson,string guardianLog)
    {
        using var evidence=JsonDocument.Parse(scenarioJson);var root=evidence.RootElement;
        Require(root.GetProperty("HeldReadyClassification").GetString()=="CancelledClosedObservedAfterTimeout","Actual cancellation classification missing.");
        Require(root.GetProperty("OriginalStoppedAndEndpointRemoved").GetBoolean(),"Original full stop missing.");
        var nonce=root.GetProperty("StopDiagnosticNonce").GetString();
        var owner=JsonNode.Parse(root.GetProperty("HeldReadyActualTerminalClose").GetProperty("Identity").GetRawText());
        var markers=new List<JsonElement>();
        foreach(var line in guardianLog.Split('\n'))
        {
            const string prefix="BOE_STATUS_PUBLICATION_DATA ";
            if(!line.StartsWith(prefix,StringComparison.Ordinal))continue;
            using var parsed=JsonDocument.Parse(line[prefix.Length..]);var marker=parsed.RootElement;
            if(marker.GetProperty("Nonce").GetString()!=nonce)continue;
            Require(marker.GetProperty("SchemaVersion").GetInt32()==1,"Wrong publication diagnostic schema.");
            Require(JsonNode.DeepEquals(owner,JsonNode.Parse(marker.GetProperty("OriginalOwnerIdentity").GetRawText())),"Publication owner changed.");
            markers.Add(marker.Clone());
        }
        var firstEntry=root.GetProperty("HeldReadyFirstStatusEntryUtc").GetDateTimeOffset();
        var secondEntry=root.GetProperty("HeldReadySecondStatusEntryUtc").GetDateTimeOffset();
        Require(firstEntry<secondEntry,"Controlled publication entries reversed.");
        var interval=markers.Where(m=>Time(m)>=firstEntry&&Time(m)<=secondEntry).ToArray();
        Require(interval.Length==1,"Controlled iteration must have exactly one publication decision.");
        var deferred=interval[0];
        Require(Stage(deferred)=="Deferred"&&deferred.GetProperty("Decision").GetString()=="KeepNewer"&&
            deferred.GetProperty("PriorActualPublication").GetBoolean(),"Controlled iteration did not defer to real newer DATA.");
        Require(deferred.GetProperty("SnapshotUpdatedAtUtc").GetDateTimeOffset()<=firstEntry,"Deferred snapshot was not frozen before first entry.");
        var initialHash=root.GetProperty("HeldReadyInitialStatusSHA256").GetString();
        var latestHash=root.GetProperty("HeldReadyLatestStatusSHA256").GetString();
        var deferredHash=Hash(deferred);
        Require(new[]{initialHash,latestHash,deferredHash}.All(h=>h!=null&&Regex.IsMatch(h,"^[0-9a-f]{64}$")),"Malformed frozen snapshot hash.");
        Require(initialHash!=latestHash&&initialHash!=deferredHash&&latestHash!=deferredHash,"Distinct snapshot provenance collapsed.");
        var initial=markers.Where(m=>Stage(m)=="Published"&&Hash(m)==initialHash&&Time(m)<firstEntry).ToArray();
        var latest=markers.Where(m=>Stage(m)=="Published"&&Hash(m)==latestHash&&Time(m)>secondEntry).ToArray();
        Require(initial.Length==1&&latest.Length==1,"Initial/latest exact frozen publication chain missing or duplicated.");
        Require(latest[0].GetProperty("SnapshotUpdatedAtUtc").GetString()==root.GetProperty("HeldReadyLatestStatusUpdatedAt").GetString(),"Latest readback snapshot identity changed.");
        var release=root.GetProperty("HeldReadyReleaseUtc").GetDateTimeOffset();
        Require(release>=secondEntry&&release>=root.GetProperty("HeldReadyCancellationSentUtc").GetDateTimeOffset(),"Lease released before both original witnesses.");
        Require(Time(latest[0])>=release,"Latest publication preceded release.");
        return JsonSerializer.Serialize(new{Initial=initial[0],Deferred=deferred,Latest=latest[0]});
    }
    private static DateTimeOffset Time(JsonElement m)=>m.GetProperty("ObservedAtUtc").GetDateTimeOffset();
    private static string? Stage(JsonElement m)=>m.GetProperty("Stage").GetString();
    private static string? Hash(JsonElement m)=>m.GetProperty("SnapshotSHA256").GetString();
    private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
}
