using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Services.GmRuntime;

// A cooperating script uses this one retained process/connection for its whole
// operation. It is not a general writer, journal, launcher or cold recovery API.
internal static class GmMainParticipatingControl
{
    private const int FrameLimit = 4 * 1024 * 1024;
    private sealed class Command
    {
        [JsonRequired] public long Sequence { get; set; }
        public string? Action { get; set; }
        public string? Path { get; set; }
        public string? Bytes { get; set; }
        public MainOperationOutcome? Outcome { get; set; }
    }

    internal static async Task<int?> TryRunAsync(string[] args)
    {
        if (!args.Contains("--gm-main-operation", StringComparer.Ordinal)) return null;
        try
        {
            if (args.Length != 3 || args[0] != "--gm-main-operation" || args[1] != "--root" || !Directory.Exists(args[2]))
                throw new InvalidDataException("Participating control requires an existing explicit root.");
            return await RunAsync(new FileSystemManager(args[2], NullLogger<FileSystemManager>.Instance),
                Console.OpenStandardInput(), Console.OpenStandardOutput());
        }
        catch (Exception failure)
        {
            // Fixed diagnostic: no source data, credentials or uncontrolled exception text.
            var phase=failure.Data["ParticipatingAdmissionPhase"] is "original-connection" or "read-record" or "read-endpoint" ? (string)failure.Data["ParticipatingAdmissionPhase"]! : "local-admission";
            Console.Error.WriteLine($"Original participating operation refused or continuation unconfirmed ({failure.GetType().Name}; {phase}).");
            return 2;
        }
    }

    internal static async Task<int> RunAsync(FileSystemManager files, Stream input, Stream output)
    {
        var reader = new MainOperationReader(input);
        var explicitClose = false;
        long sequence = 0;
        MainOperationOutcome outcome = MainOperationOutcome.Completed;
        try
        {
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(files, async () =>
            {
                try
                {
                await MainOperationReader.WriteAsync(output, new { ok = true, state = "active", sequence }, CancellationToken.None);
                while (true)
                {
                    var command = await reader.ReadAsync<Command>(CancellationToken.None, FrameLimit)
                        ?? throw new IOException("Participating caller lost before terminal close.");
                    if (command.Sequence != checked(sequence + 1)) throw new InvalidDataException("Unexpected control sequence.");
                    sequence = command.Sequence;
                    if (command.Action == "close")
                    {
                        if (command.Outcome == null || !Enum.IsDefined(command.Outcome.Value)) throw new InvalidDataException("Missing close outcome.");
                        outcome = command.Outcome.Value; explicitClose = true;
                        return 0;
                    }
                    var ok = true;
                    try { await MutateAsync(files, command); }
                    catch (Exception) { ok = false; }
                    // The reply acknowledges this sequence only. A lost reply is never replayed.
                    await MainOperationReader.WriteAsync(output, new { ok, sequence }, CancellationToken.None);
                }
                }
                finally { if (!explicitClose) files.MarkMainOperationUnresolved(); }
            }, () => outcome);
            await MainOperationReader.WriteAsync(output, new { ok = true, state = "closed-observed", sequence }, CancellationToken.None);
            return 0;
        }
        catch
        {
            // SessionOperationContext marks transport loss below before finalization.
            // A reply loss after explicit close cannot reverse an owner receipt.
            if (!explicitClose) files.MarkMainOperationUnresolved();
            throw;
        }
    }

    private static async Task MutateAsync(FileSystemManager files, Command command)
    {
        var path = NormalizeControlPath(command.Path);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.EnsureWorkerGeneralMutationAllowed(lease);
        switch (command.Action)
        {
            case "directory":
                using (PhysicalFileAuthority.EnsureStableDirectory(files.BasePath, files.ResolvePath(path), "participating control")) { }
                break;
            case "write":
                await files.WriteFileAtomicBytesAsync(lease, path, Convert.FromBase64String(command.Bytes ?? throw new InvalidDataException()));
                break;
            case "append":
                await files.AppendFileAtomicAsync(lease, path, new System.Text.UTF8Encoding(false, true).GetString(
                    Convert.FromBase64String(command.Bytes ?? throw new InvalidDataException())));
                break;
            case "delete": files.DeleteFile(lease, path); break;
            default: throw new InvalidDataException("Unknown participating mutation.");
        }
    }

    private static string NormalizeControlPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || Path.IsPathRooted(path) ||
            path.Split('/').Any(s => s is "" or "." or "..")) throw new InvalidDataException("Invalid participating path.");
        if (path is "input" or "ready" or "output" or "game_state/control" ||
            path.StartsWith("game_state/control/", StringComparison.Ordinal) || path.StartsWith("input/", StringComparison.Ordinal) ||
            path.StartsWith("ready/", StringComparison.Ordinal) || path.StartsWith("output/", StringComparison.Ordinal)) return path;
        throw new InvalidDataException("Path is outside participating control scope.");
    }
}
