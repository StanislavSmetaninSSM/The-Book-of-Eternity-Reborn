using System.Text.RegularExpressions;

namespace BookOfEternityClient.Configuration;

public sealed class GmBridgePasteVisibilityMarker
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "contains";
    public string Pattern { get; set; } = string.Empty;
}

public static class GmBridgePasteVisibilityPolicy
{
    public const string ExactTextOnly = "ExactTextOnly";
    public const string ExactTextOrConfiguredMarker = "ExactTextOrConfiguredMarker";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public static List<GmBridgePasteVisibilityMarker> CreateDefaultMarkers() =>
    [
        new()
        {
            Name = "Codex",
            Kind = "regex",
            Pattern = @"\[Pasted Content \d+ chars\]"
        }
    ];

    public static string NormalizePolicy(string? policy) =>
        string.Equals(policy, ExactTextOnly, StringComparison.OrdinalIgnoreCase)
            ? ExactTextOnly
            : ExactTextOrConfiguredMarker;

    public static List<GmBridgePasteVisibilityMarker> NormalizeMarkers(IEnumerable<GmBridgePasteVisibilityMarker>? markers)
    {
        var normalized = CreateDefaultMarkers();
        var custom = markers?
            .Where(marker => marker != null && !string.IsNullOrWhiteSpace(marker.Pattern))
            .Select(marker => new GmBridgePasteVisibilityMarker
            {
                Name = string.IsNullOrWhiteSpace(marker.Name) ? "custom" : marker.Name.Trim(),
                Kind = NormalizeMarkerKind(marker.Kind),
                Pattern = marker.Pattern.Trim()
            })
            .ToList();

        if (custom == null)
            return normalized;

        foreach (var marker in custom)
        {
            if (normalized.Any(existing =>
                    string.Equals(existing.Kind, marker.Kind, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.Pattern, marker.Pattern, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            normalized.Add(marker);
        }

        return normalized;
    }

    public static bool IsPromptVisible(string prompt, string visibleText, GameSettings settings)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return true;

        var needle = BuildPromptVisibilityNeedle(prompt);
        if (string.IsNullOrWhiteSpace(needle))
            return true;

        var normalizedVisibleText = NormalizeVisibleText(visibleText);
        if (normalizedVisibleText.Contains(needle, StringComparison.Ordinal))
            return true;

        var policy = NormalizePolicy(settings.GmBridgePasteVisibilityPolicy);
        if (string.Equals(policy, ExactTextOnly, StringComparison.Ordinal))
            return false;

        return NormalizeMarkers(settings.GmBridgePasteVisibilityMarkers)
            .Any(marker => MatchesMarker(marker, normalizedVisibleText));
    }

    public static string BuildPromptVisibilityNeedle(string prompt)
    {
        var normalized = NormalizeVisibleText(prompt).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        var compact = Regex.Replace(normalized, @"\s+", " ");
        var length = Math.Min(24, compact.Length);
        return compact[..length];
    }

    public static string NormalizeVisibleText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var withoutAnsi = Regex.Replace(value, @"\x1B\[[0-9;?]*[ -/]*[@-~]", string.Empty);
        return withoutAnsi.Replace("\r", string.Empty);
    }

    private static bool MatchesMarker(GmBridgePasteVisibilityMarker marker, string normalizedVisibleText)
    {
        if (string.IsNullOrWhiteSpace(marker.Pattern))
            return false;

        if (string.Equals(NormalizeMarkerKind(marker.Kind), "regex", StringComparison.Ordinal))
        {
            try
            {
                return Regex.IsMatch(
                    normalizedVisibleText,
                    marker.Pattern,
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                    RegexTimeout);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        return normalizedVisibleText.Contains(marker.Pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMarkerKind(string? kind) =>
        string.Equals(kind, "regex", StringComparison.OrdinalIgnoreCase)
            ? "regex"
            : "contains";
}
