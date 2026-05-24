namespace BookOfEternityClient.Configuration;

public sealed record ClientStartupOptions(
    bool WebMode,
    string BasePath,
    string WebUrl,
    string? E2EScriptPath = null,
    string? E2EArtifactsPath = null,
    bool PlainOutput = false)
{
    public const string DefaultWebUrl = "http://127.0.0.1:8787";

    public static ClientStartupOptions Parse(IReadOnlyList<string> args, string defaultBasePath)
    {
        var webMode = false;
        var basePath = defaultBasePath;
        var webUrl = DefaultWebUrl;
        string? e2eScriptPath = null;
        string? e2eArtifactsPath = null;
        var plainOutput = false;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--web", StringComparison.OrdinalIgnoreCase))
            {
                webMode = true;
                continue;
            }

            if (string.Equals(arg, "--plain-output", StringComparison.OrdinalIgnoreCase))
            {
                plainOutput = true;
                continue;
            }

            if (string.Equals(arg, "--web-url", StringComparison.OrdinalIgnoreCase))
            {
                webUrl = ReadRequiredValue(args, ref index, "--web-url");
                continue;
            }

            if (string.Equals(arg, "--e2e-script", StringComparison.OrdinalIgnoreCase))
            {
                e2eScriptPath = ReadRequiredValue(args, ref index, "--e2e-script");
                continue;
            }

            if (string.Equals(arg, "--e2e-artifacts", StringComparison.OrdinalIgnoreCase))
            {
                e2eArtifactsPath = ReadRequiredValue(args, ref index, "--e2e-artifacts");
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal) && Directory.Exists(arg))
                basePath = arg;
        }

        return new ClientStartupOptions(webMode, basePath, webUrl, e2eScriptPath, e2eArtifactsPath, plainOutput);
    }

    private static string ReadRequiredValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count)
            throw new ArgumentException($"Missing value for {optionName}.", nameof(args));

        var value = args[++index];
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Missing value for {optionName}.", nameof(args));

        return value;
    }
}
