using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalLivingWorldPreparation(
    IReadOnlyList<ValidationIssue> Issues, MortalLivingWorldSource? Source);

/// <summary>Lease-local capture issued only by the actual authenticated original reader; not serializable permission.</summary>
internal sealed class MortalLivingWorldSource
{
    internal const string ReportPath = "game_state/control/mortal_living_world_report.json";
    internal const string HistoryPath = "game_state/world/mortal_living_world_history.json";
    internal const string WorldTimePath = "game_state/world/world_time.json";
    private MortalLivingWorldSource() { }
    internal MortalLivingWorldAssessmentContext Context => throw new NotImplementedException("#1516 original source RED scaffold");
    internal JsonObject? ReadOriginalDocument(string path) => throw new NotImplementedException("#1516 original source RED scaffold");
    internal static Task<MortalLivingWorldPreparation> PrepareAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease) => throw new NotImplementedException("#1516 original source RED scaffold");
    internal Task RequireUnchangedInputsAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease) => throw new NotImplementedException("#1516 original source RED scaffold");
}
