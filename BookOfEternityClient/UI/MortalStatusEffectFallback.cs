using BookOfEternityClient.Models.GameState;

namespace BookOfEternityClient.UI;

internal static class MortalStatusEffectFallback
{
    public const string Message =
        "Подробная запись эффекта ещё не заведена. Ниже показаны состояния, которые уже видны вам по самочувствию.";

    public static List<MortalStatusEffectFallbackRow> BuildRows(PlayerStatusState status)
    {
        var rows = new List<MortalStatusEffectFallbackRow>();
        if (IsInformativeCondition(status.CurrentCondition))
            rows.Add(new MortalStatusEffectFallbackRow("Текущее состояние", status.CurrentCondition.Trim()));

        if (!string.IsNullOrWhiteSpace(status.CurrentConditionDescription))
            rows.Add(new MortalStatusEffectFallbackRow("Описание", status.CurrentConditionDescription.Trim()));

        foreach (var condition in status.ActiveConditions)
        {
            if (!string.IsNullOrWhiteSpace(condition))
                rows.Add(new MortalStatusEffectFallbackRow("Активное состояние", condition.Trim()));
        }

        return rows;
    }

    private static bool IsInformativeCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return false;

        var normalized = condition.Trim();
        return !new[] { "Здоров", "Здорова", "Норма", "Нормально", "Healthy", "Normal", "None", "Нет", "-", "—" }
            .Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }
}

internal sealed record MortalStatusEffectFallbackRow(string Label, string Details);
