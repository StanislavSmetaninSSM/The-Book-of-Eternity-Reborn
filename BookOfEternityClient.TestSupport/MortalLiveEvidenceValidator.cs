namespace BookOfEternityClient.Tests;

// Read-only detached DATA check, never admission, source authentication or publication authority.
internal static class MortalLiveEvidenceValidator
{
    internal sealed record Observation(string SessionId, string RequestId, int Turn, string SnapshotToken,
        int PriorCount, int CurrentCount, string DeclarationFingerprint, string ReceiptFingerprint,
        bool PublicationAuthority = false);
    internal static Observation Validate(string? priorHistory, string currentHistory, string manifest, string request)
        => throw new InvalidDataException("Typed live evidence validator is not implemented.");
    internal static Task<Observation> ReadAsync(string baseline, string currentSession, string actualRequest)
        => throw new InvalidDataException("Typed live evidence reader is not implemented.");
    internal static Task<int> RunAsync(string baseline, string currentSession, string actualRequest)
    {
        Console.WriteLine("{\"valid\":false,\"publicationAuthority\":false,\"errorCode\":\"not_implemented\"}");
        return Task.FromResult(2);
    }
}
