using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private sealed record MainMenuOption(string Key, string Title, string Description, string AccentColor, int Index);
    private sealed record OptionsMenuEntry(string Key, string Label);
    private sealed record MenuChoiceItem(string Key, string Label, string? Description = null, string AccentColor = "cyan1");
    private enum MainMenuLayoutMode { VeryCompact, Compact, Medium, Wide }

    private sealed class PendingTurnSnapshotManifest
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = "";
        public string PlayerAction { get; set; } = "";
        public int[]? PreGeneratedDices1d20 { get; set; }
        public JsonObject? GachaBaseResult { get; set; }
        public ProgressionControl? ProgressionControl { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = "";
    }

    private sealed class RollbackSnapshot
    {
        public Dictionary<string, string> BackupFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> BackupHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> BaselineFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ValidationSnapshotFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ValidatedPendingTurnSnapshotContext
    {
        public PendingTurnSnapshotManifest Manifest { get; init; } = new();
        public PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload Payload { get; init; } = new();

        public string SessionId => Manifest.SessionId;
        public string RequestId => Manifest.RequestId;
        public int TurnNumber => Manifest.TurnNumber;
        public string PlayerAction => Manifest.PlayerAction;
        public ProgressionControl? ProgressionControl => Manifest.ProgressionControl;
        public string? SourceLabel => Manifest.SourceLabel;
    }

    private enum PendingTurnSnapshotResolutionStatus
    {
        Missing,
        Unusable,
        Usable
    }

    private sealed class PendingTurnSnapshotResolution
    {
        public PendingTurnSnapshotResolutionStatus Status { get; init; }
        public PendingTurnSnapshotManifest? Manifest { get; init; }
        public ValidatedPendingTurnSnapshotContext? Context { get; init; }
    }

    private sealed class ValidationRepairRequest
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public bool MetadataDiagnosticOnly { get; set; }
        public string Source { get; set; } = "";
        public string DetectedAtUtc { get; set; } = "";
        public int RevalidationAttempt { get; set; }
        public string GmInstructions { get; set; } = "";
        public List<string> SummaryGroups { get; set; } = new();
        public List<ValidationRepairHarnessPacket> HarnessRepairPackets { get; set; } = new();
        public List<ValidationRepairIssue> Errors { get; set; } = new();
    }

    private sealed class ValidationRepairHarnessPacket
    {
        public string Kind { get; set; } = "";
        public string Priority { get; set; } = "";
        public string Title { get; set; } = "";
        public List<string> TargetFiles { get; set; } = new();
        public List<string> CanonicalActorNames { get; set; } = new();
        public List<string> Steps { get; set; } = new();
        public string DebugLogTemplate { get; set; } = "";
        public List<string> DoNotDo { get; set; } = new();
    }

    private sealed class ValidationRepairIssue
    {
        public string Code { get; set; } = "validation_error";
        public string FilePath { get; set; } = "";
        public string Severity { get; set; } = "Error";
        public string Category { get; set; } = IssueCategory.StateConsistency.ToString();
        public string Message { get; set; } = "";
        public string? Actor { get; set; }
        public string? Section { get; set; }
        public string? Expected { get; set; }
        public string? Actual { get; set; }
        public string? RepairHint { get; set; }
    }

    private sealed class ValidationRepairReady
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string UpdatedAtUtc { get; set; } = "";
        public string? Note { get; set; }
    }

    private sealed class TerminalProtocolFailureRequest
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public bool MetadataDiagnosticOnly { get; set; }
        public string Source { get; set; } = "";
        public string DetectedAtUtc { get; set; } = "";
        public string GmInstructions { get; set; } = "";
        public List<string> SummaryGroups { get; set; } = new();
        public List<ValidationRepairIssue> Errors { get; set; } = new();
    }

    private sealed class ReadySignalMetadata
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string Status { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public string? Error { get; set; }
        public bool HasFilesModified { get; set; }
        public bool FilesModifiedValid { get; set; }
    }

    private sealed class ActiveTerminalOutcomeResolution
    {
        public string Kind { get; set; } = "failure";
        public ReadySignalMetadata? Signal { get; set; }
    }
}

