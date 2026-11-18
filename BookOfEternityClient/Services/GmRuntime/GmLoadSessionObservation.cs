namespace BookOfEternityClient.Services.GmRuntime;

#if DEBUG
/// <summary>Already-validated retained-connection Load DATA; construction does not authenticate a receipt or authorize adoption.</summary>
internal sealed record GmLoadSessionObservation(string OperationId, string SourceKey, string RootKey,
    string? ExpectedGeneration, GmLoadSessionReply? OriginalStop, GmLoadSessionReply? ReplacementFinish,
    bool Committed, bool RefreshConfirmed, string? EstablishedGeneration);

/// <summary>Opt-in fixture output only, never a Load decision or a replacement-owner source.</summary>
internal static class GmLoadSessionObservationWriter
{
    internal const string NonceVariable = "BOE_TEST_LOAD_OBSERVATION_NONCE";

    /// <summary>Emits an eligible completed/disposed Load observation; all diagnostic failures remain isolated from Load.</summary>
    internal static void Write(string? nonce, TextWriter sink, GmLoadSessionObservation observation)
    {
        try
        {
            if (!Guid.TryParseExact(nonce, "N", out var parsed) || parsed.ToString("N") != nonce ||
                !observation.Committed || !observation.RefreshConfirmed ||
                observation.OriginalStop is not { Ok: true, State: GmLoadMainState.Stopped } ||
                observation.ReplacementFinish is not { Ok: true, State: GmLoadMainState.Running or GmLoadMainState.StartedNotReady })
                return;
            // These are the original validated records, not a new status read or an
            // authorization decision. Native consumers must still correlate the full pair.
            sink.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                kind = "boe-load-session-completed", nonce, observation
            }));
        }
        catch { /* Observation cannot change, retry or replay the completed operation. */ }
    }
}
#endif
