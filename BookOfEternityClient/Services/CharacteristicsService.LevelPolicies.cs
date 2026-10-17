using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class CharacteristicsService
{
    internal static int? ReadTrainingExperienceLevel(JsonElement root)
    {
        if (root.TryGetProperty("level", out var level) &&
            level.ValueKind == JsonValueKind.Number)
            return level.GetInt32();
        if (root.TryGetProperty("playerLevel", out var playerLevel) &&
            playerLevel.ValueKind == JsonValueKind.Number)
            return playerLevel.GetInt32();

        return null;
    }

    internal static int? ReadTrainingSingleLevel(JsonElement root)
    {
        if (root.TryGetProperty("level", out var level) &&
            level.ValueKind == JsonValueKind.Number)
            return level.GetInt32();

        return null;
    }
}
