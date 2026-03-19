using BookOfEternityClient.Core;
using Spectre.Console;

namespace BookOfEternityClient.UI;

internal static class ReputationDisplay
{
    public static ReputationBand GetTier(ReputationScaleKind scale, int value)
        => ReputationScales.Resolve(scale, value);

    public static string BuildPlainValueLabel(int value, ReputationScaleKind scale)
    {
        var tier = GetTier(scale, value);
        return $"{value} ({tier.Label})";
    }

    public static string BuildValueLabelMarkup(int value, ReputationScaleKind scale)
    {
        var tier = GetTier(scale, value);
        return $"[{tier.Color}]{value} — {Markup.Escape(tier.Label)}[/]";
    }

    public static string BuildTierMarkup(int value, ReputationScaleKind scale)
    {
        var tier = GetTier(scale, value);
        return $"[{tier.Color}]{Markup.Escape(tier.Label)}[/]";
    }

    public static string BuildBarMarkup(int value, ReputationScaleKind scale, int width)
    {
        var normalized = Normalize(value, scale, width);
        var tier = GetTier(scale, value);
        return ConsoleLayout.CreateBar(normalized, width, tier.Color);
    }

    public static int Normalize(int value, ReputationScaleKind scale, int width)
    {
        var definition = ReputationScales.Get(scale);
        var clamped = Math.Clamp(value, definition.MinValue, definition.MaxValue);
        var range = Math.Max(1, definition.MaxValue - definition.MinValue);
        return Math.Clamp((clamped - definition.MinValue) * width / range, 0, width);
    }

    public static IReadOnlyList<string> BuildLegendLines(ReputationScaleKind scale, string indent = "")
    {
        var definition = ReputationScales.Get(scale);
        return definition.Bands
            .Select(band => $"{indent}[dim]{band.Min}..{band.Max}[/] [{band.Color}]{Markup.Escape(band.Label)}[/]")
            .ToArray();
    }

    public static bool TryGetNextThreshold(ReputationScaleKind scale, int value, out string label, out int threshold)
    {
        if (ReputationScales.TryGetNextBand(scale, value, out var band))
        {
            label = band.Label;
            threshold = band.Min;
            return true;
        }

        label = string.Empty;
        threshold = 0;
        return false;
    }
}
