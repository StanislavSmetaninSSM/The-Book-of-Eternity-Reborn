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

    /// <summary>Emits completed/disposed Load DATA without propagating diagnostic failures; RED stub intentionally emits nothing.</summary>
    internal static void Write(string? nonce, TextWriter sink, GmLoadSessionObservation observation)
    {
        // Test-first no-output baseline. Serialization is implemented only after causal RED.
    }
}
#endif
