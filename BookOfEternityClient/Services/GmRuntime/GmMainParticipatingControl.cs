using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Services.GmRuntime;

// A cooperating script uses this one retained process/connection for its whole
// operation. It is not a general writer, journal, launcher or cold recovery API.
internal static partial class GmMainParticipatingControl
{
    private const int FrameLimit = 4 * 1024 * 1024;
    private sealed class Command
    {
        [JsonRequired] public long Sequence { get; set; }
        public string? Action { get; set; }
        public string? Path { get; set; }
        public string? Bytes { get; set; }
        public string? Transfer { get; set; }
        public long Length { get; set; }
        public long Offset { get; set; }
        public string? Hash { get; set; }
        public MainOperationOutcome? Outcome { get; set; }
    }

    internal static async Task<int?> TryRunAsync(string[] args)
    {
        if (!args.Contains("--gm-main-operation", StringComparer.Ordinal)) return null;
        try
        {
            if (args.Length != 3 || args[0] != "--gm-main-operation" || args[1] != "--root" || !Directory.Exists(args[2]))
                throw new InvalidDataException("Participating control requires an existing explicit root.");
            // The foreground PowerShell caller owns Ctrl+C and sends the actual
            // immutable close. Keep this retained connection alive for that close;
            // stdin loss still follows the ordinary Unresolved path below.
            using var foregroundInterrupt = OperatingSystem.IsLinux()
                ? PosixSignalRegistration.Create(PosixSignal.SIGINT, context => context.Cancel = true)
                : null;
            return await RunAsync(new FileSystemManager(args[2], NullLogger<FileSystemManager>.Instance),
                Console.OpenStandardInput(), Console.OpenStandardOutput());
        }
        catch (Exception failure)
        {
            // Fixed diagnostic: no source data, credentials or uncontrolled exception text.
            var phase=failure.Data["ParticipatingAdmissionPhase"] is "original-connection" or "read-record" or "read-endpoint" ? (string)failure.Data["ParticipatingAdmissionPhase"]! : "local-admission";
            var site=new System.Diagnostics.StackTrace(failure).GetFrames()?.Select(f=>f.GetMethod()?.DeclaringType?.Name+"."+f.GetMethod()?.Name)
                .Select(n=>n?.Contains("AcquireAsync",StringComparison.Ordinal)==true?"AcquireAsync":n?.Contains("Validate",StringComparison.Ordinal)==true?"Validate":n?.Contains("ReadBounded",StringComparison.Ordinal)==true?"ReadBounded":n?.Contains("GmSessionRunPersistence.Read",StringComparison.Ordinal)==true?"Read":null)
                .FirstOrDefault(n=>n!=null)??"unspecified";
            Console.Error.WriteLine($"Original participating operation refused or continuation unconfirmed ({failure.GetType().Name}; {phase}; {site}).");
            return 2;
        }
    }

    internal static async Task<int> RunAsync(FileSystemManager files, Stream input, Stream output)
    {
        var reader = new MainOperationReader(input);
        using var daemon = new DaemonExchange();
        var explicitClose = false;
        long sequence = 0;
        MainOperationOutcome outcome = MainOperationOutcome.Completed;
        var publicationUncertain = false;
        MainOperationClose? terminalClose = null;
        var closeObserved = false;
        var remoteAdmission = false;
        var localScopeCompleted = false;
        var closeReplyAttempted = false;
        async Task ReplyCloseAsync(bool continuationFailed)
        {
            closeReplyAttempted = true;
            await MainOperationReader.WriteAsync(output,
                new { ok = closeObserved || localScopeCompleted, state = closeObserved || localScopeCompleted ? "closed-observed" : "close-unconfirmed", sequence,
                    completionKind = remoteAdmission ? "original-main" : "local", localScopeCompleted, effectiveOutcome = outcome,
                    terminalClose, closeObserved, continuationFailed }, CancellationToken.None);
        }
        try
        {
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(files, async () =>
            {
                try
                {
                await MainOperationReader.WriteAsync(output, new { ok = true, state = "active", sequence, originalClose = files.DescribeMainOperationClose(MainOperationOutcome.Completed,false) }, CancellationToken.None);
                while (true)
                {
                    var command = await reader.ReadAsync<Command>(CancellationToken.None, FrameLimit)
                        ?? throw new IOException("Participating caller lost before terminal close.");
                    if (command.Sequence != checked(sequence + 1)) throw new InvalidDataException("Unexpected control sequence.");
                    sequence = command.Sequence;
                    if (command.Action == "close")
                    {
                        if (command.Outcome == null || !Enum.IsDefined(command.Outcome.Value)) throw new InvalidDataException("Missing close outcome.");
                        outcome = publicationUncertain ? MainOperationOutcome.Uncertain : command.Outcome.Value; explicitClose = true;
                        return 0;
                    }
                    var result = new CommandResult();
                    var blockedByPriorUncertainty = publicationUncertain;
                    if (!blockedByPriorUncertainty)
                    {
                        try {
                            if(command.Action?.StartsWith("daemon-",StringComparison.Ordinal)==true)
                                result.Fields=await daemon.HandleAsync(files,command,result);
                            else {
                                if(daemon.HasPending)throw new InvalidDataException("A daemon exchange is incomplete.");
                                await MutateAsync(files, command, result);
                            }
                        }
                        catch (Exception failure) { result.Failure = failure; }
                        publicationUncertain = result.Publication?.Disposition == TrustedLocalPublicationDisposition.Uncertain;
                    }
                    // Capture belongs to this command, not the whole body. A valid
                    // uncertain reply retains this connection for its original close.
                    var committed = result.Publication?.Disposition == TrustedLocalPublicationDisposition.Committed;
                    var fields=result.Fields??new Dictionary<string,object?>();
                    fields["ok"]=!blockedByPriorUncertainty&&result.Failure==null;
                    fields["sequence"]=sequence;
                    fields["publicationDisposition"]=result.Publication?.Disposition.ToString();
                    fields["cleanupPending"]=committed&&result.Publication?.Failure!=null;
                    fields["commandFollowUpRequired"]=committed&&result.Failure!=null;
                    fields["blockedByPriorUncertainty"]=blockedByPriorUncertainty;
                    fields["daemonReadRefused"]=result.ReadRefused;
                    await MainOperationReader.WriteAsync(output,fields,CancellationToken.None);
                }
                }
                finally { if (!explicitClose) files.MarkMainOperationUnresolved(); }
            }, () => outcome, (close, observed, remote) => { terminalClose = close; closeObserved = observed; remoteAdmission = remote; });
            // Successful return includes actual bound finalization and scoped
            // disposal. A guarded local scope does not invent a remote receipt.
            localScopeCompleted = !remoteAdmission;
            await ReplyCloseAsync(false);
            return 0;
        }
        catch
        {
            // SessionOperationContext marks transport loss below before finalization.
            // A reply loss after explicit close cannot reverse an owner receipt.
            if (!explicitClose) files.MarkMainOperationUnresolved();
            else if (terminalClose != null && !closeReplyAttempted)
            {
                // Actual attempted close and ACK remain separate even when bound
                // finalization failed. Never reconstruct them from the active grant.
                try { await ReplyCloseAsync(true); } catch { /* retain the original failure */ }
            }
            throw;
        }
    }

    private sealed class CommandResult
    {
        internal TrustedLocalPublicationOutcome? Publication;
        internal Exception? Failure;
        internal bool ReadRefused;
        internal Dictionary<string,object?>? Fields;
    }

    private static async Task MutateAsync(FileSystemManager files, Command command, CommandResult result)
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
                result.Publication = await files.PublishOrdinaryFileWithOutcomeAsync(lease, path, Convert.FromBase64String(command.Bytes ?? throw new InvalidDataException()));
                break;
            case "append":
                result.Publication = await files.AppendOrdinaryFileWithOutcomeAsync(lease, path, new System.Text.UTF8Encoding(false, true).GetString(
                    Convert.FromBase64String(command.Bytes ?? throw new InvalidDataException())));
                break;
            case "delete": result.Publication = await files.PublishOrdinaryFileWithOutcomeAsync(lease, path, null); break;
            default: throw new InvalidDataException("Unknown participating mutation.");
        }
        if (result.Publication != null) files.RequireCommittedLocalPublication(result.Publication);
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
