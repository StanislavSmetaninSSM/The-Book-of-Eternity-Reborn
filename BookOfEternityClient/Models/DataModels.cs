using System.Text.Json.Serialization;

namespace BookOfEternityClient.Models;

public class TurnCompleteSignal
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("turnNumber")]
    public int TurnNumber { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("filesModified")]
    public string[]? FilesModified { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class SaveMetadata
{
    [JsonPropertyName("saveName")]
    public string SaveName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("gameVersion")]
    public string GameVersion { get; set; } = "1.0";

    [JsonPropertyName("turnNumber")]
    public int TurnNumber { get; set; }

    [JsonPropertyName("playerLevel")]
    public int PlayerLevel { get; set; }

    [JsonPropertyName("currentLocation")]
    public string CurrentLocation { get; set; } = string.Empty;

    [JsonPropertyName("worldName")]
    public string WorldName { get; set; } = string.Empty;

    [JsonPropertyName("incarnation")]
    public int Incarnation { get; set; }

    [JsonPropertyName("inkFeathers")]
    public int InkFeathers { get; set; }

    [JsonPropertyName("sessionDuration")]
    public string SessionDuration { get; set; } = string.Empty;

    [JsonPropertyName("characterName")]
    public string CharacterName { get; set; } = string.Empty;
}

public class SaveInfo
{
    public string FileName { get; set; } = string.Empty;
    public SaveMetadata? Metadata { get; set; }
    public long FileSize { get; set; }
}
