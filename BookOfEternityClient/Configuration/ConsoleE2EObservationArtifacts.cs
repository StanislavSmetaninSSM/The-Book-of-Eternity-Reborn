using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Configuration;

public enum ConsoleE2EInputMode
{
    Menu,
    TextPrompt,
    Confirmation,
    Loading,
    Error,
    Exit
}

public sealed record ConsoleE2EObservationSnapshot(
    string RunId,
    int StepIndex,
    DateTimeOffset CapturedAtUtc,
    ConsoleE2EInputMode InputMode,
    string ScreenTitle,
    string PlayerFacingText,
    IReadOnlyList<string> Options,
    string? SelectedOption,
    string ArtifactRoot,
    string? LogPath,
    string? ErrorType = null,
    string? ErrorMessage = null)
{
    public int SchemaVersion => 1;
}

public sealed record ConsoleE2EObservationArtifact(string TextPath, string JsonPath);

public sealed class ConsoleE2EObservationArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _screenDirectory;

    public ConsoleE2EObservationArtifactWriter(string artifactRoot, string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        ArtifactRoot = artifactRoot;
        RunId = runId;
        _screenDirectory = Path.Combine(artifactRoot, "screens");
        Directory.CreateDirectory(_screenDirectory);
    }

    public string ArtifactRoot { get; }

    public string RunId { get; }

    public ConsoleE2EObservationArtifact WriteSnapshot(ConsoleE2EObservationSnapshot snapshot, string slug)
    {
        if (!StringComparer.Ordinal.Equals(snapshot.RunId, RunId))
            throw new ArgumentException($"Snapshot run id '{snapshot.RunId}' does not match writer run id '{RunId}'.", nameof(snapshot));

        var safeSlug = ToSafeSlug(slug);
        var fileBase = $"{snapshot.StepIndex:000}-{safeSlug}";
        var textPath = Path.Combine(_screenDirectory, fileBase + ".txt");
        var jsonPath = Path.Combine(_screenDirectory, fileBase + ".json");

        File.WriteAllText(textPath, ToPlainText(snapshot), Encoding.UTF8);
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(snapshot, JsonOptions), Encoding.UTF8);

        return new ConsoleE2EObservationArtifact(textPath, jsonPath);
    }

    public ConsoleE2EObservationArtifact WriteExceptionSnapshot(
        int stepIndex,
        string screenTitle,
        string playerFacingText,
        Exception exception,
        string slug)
    {
        var snapshot = new ConsoleE2EObservationSnapshot(
            RunId: RunId,
            StepIndex: stepIndex,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            InputMode: ConsoleE2EInputMode.Error,
            ScreenTitle: screenTitle,
            PlayerFacingText: playerFacingText,
            Options: Array.Empty<string>(),
            SelectedOption: null,
            ArtifactRoot: ArtifactRoot,
            LogPath: null,
            ErrorType: exception.GetType().Name,
            ErrorMessage: exception.Message);

        return WriteSnapshot(snapshot, slug);
    }

    private static string ToPlainText(ConsoleE2EObservationSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"schemaVersion: {snapshot.SchemaVersion}");
        builder.AppendLine($"runId: {snapshot.RunId}");
        builder.AppendLine($"stepIndex: {snapshot.StepIndex}");
        builder.AppendLine($"capturedAtUtc: {snapshot.CapturedAtUtc:O}");
        builder.AppendLine($"inputMode: {ToWireValue(snapshot.InputMode)}");
        builder.AppendLine($"screenTitle: {snapshot.ScreenTitle}");

        if (!string.IsNullOrWhiteSpace(snapshot.SelectedOption))
            builder.AppendLine($"selectedOption: {snapshot.SelectedOption}");

        builder.AppendLine("options:");
        foreach (var option in snapshot.Options)
            builder.AppendLine($"- {option}");

        builder.AppendLine("playerFacingText:");
        builder.AppendLine(snapshot.PlayerFacingText);

        if (!string.IsNullOrWhiteSpace(snapshot.LogPath))
            builder.AppendLine($"logPath: {snapshot.LogPath}");

        if (!string.IsNullOrWhiteSpace(snapshot.ErrorType))
            builder.AppendLine($"errorType: {snapshot.ErrorType}");

        if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
            builder.AppendLine($"errorMessage: {snapshot.ErrorMessage}");

        return builder.ToString();
    }

    private static string ToWireValue(ConsoleE2EInputMode mode) => mode switch
    {
        ConsoleE2EInputMode.Menu => "menu",
        ConsoleE2EInputMode.TextPrompt => "textPrompt",
        ConsoleE2EInputMode.Confirmation => "confirmation",
        ConsoleE2EInputMode.Loading => "loading",
        ConsoleE2EInputMode.Error => "error",
        ConsoleE2EInputMode.Exit => "exit",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private static string ToSafeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return "snapshot";

        var builder = new StringBuilder(slug.Length);
        foreach (var ch in slug.Trim().ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(ch) || ch == '-' ? ch : '-');

        return builder.ToString().Trim('-') is { Length: > 0 } safe ? safe : "snapshot";
    }
}
