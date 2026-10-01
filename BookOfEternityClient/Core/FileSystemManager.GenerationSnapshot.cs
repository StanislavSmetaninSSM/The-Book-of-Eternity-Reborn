using System.Text;
using System.Text.Json;

namespace BookOfEternityClient.Core;

internal sealed record LocalSessionGenerationSnapshot(TrustedLocalGeneration Binding, byte[]? Bytes);

public partial class FileSystemManager
{
    // This reader is below the generation fence: it must not verify that fence
    // or enter the ordinary canonical byte reader, which calls it in turn.
    internal LocalSessionGenerationSnapshot ReadLocalGenerationSnapshot(CanonicalWriteLease lease)
    {
        EnsureCanonicalWriteLeaseActive(lease);
        var scope = new TrustedLocalFileScope([RuntimeRootPath]);
        var path = scope.ValidateFile(SessionGenerationPath);
        if (!File.Exists(path)) return new(TrustedLocalGeneration.Absent, null);

        var bytes = File.ReadAllBytes(path);
        scope.ValidateFile(path, allowMissing: false);
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var generation = ParseSessionGenerationText(reader.ReadToEnd());
        return new(TrustedLocalGeneration.Existing(generation), bytes);
    }

    // The original load reader keeps its physical open/completion contract and
    // shares only this existing BOM-decoded schema interpretation.
    private static string ParseSessionGenerationText(string json)
    {
        try
        {
            var document = StrictJsonAuthority.Deserialize<SessionGenerationDocument>(
                json, RecoveryJsonOptions, "Session generation authority");
            if (document is null || document.SchemaVersion != 1 ||
                !Guid.TryParseExact(document.GenerationId, "N", out var parsedGeneration) ||
                !string.Equals(document.GenerationId, parsedGeneration.ToString("N"), StringComparison.Ordinal))
                throw new InvalidDataException("Session generation authority is invalid.");
            return document.GenerationId;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Session generation authority is invalid.", ex);
        }
    }
}
