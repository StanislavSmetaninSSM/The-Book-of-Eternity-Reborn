namespace BookOfEternityClient.Services;

internal sealed record MortalTurnAuditDocument(string Id, string Role, string Path, string Text);
internal sealed class MortalTurnAuditPacket
{
    internal IReadOnlyList<MortalTurnAuditDocument> Documents { get; }
    internal string Digest { get; } = "unimplemented";
    internal MortalTurnAuditPacket(string session, string generation, string request, int turn, int attempt,
        IEnumerable<MortalTurnAuditDocument> documents) => Documents = documents.ToArray();
}
internal sealed record MortalTurnAuditResult(bool IsConsistent, string Error);
internal static class MortalTurnAuditContract
{
    internal static MortalTurnAuditResult Validate(MortalTurnAuditPacket packet, string response) => new(false, "Not implemented");
}
