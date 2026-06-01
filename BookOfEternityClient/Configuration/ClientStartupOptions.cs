using System.Net;

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
    public const string DefaultAgentConsoleUrl = "http://127.0.0.1:8790";

    public bool AgentConsoleMode { get; init; }

    public string AgentConsoleUrl { get; init; } = DefaultAgentConsoleUrl;

    public string? AgentConsoleToken { get; init; }

    public static ClientStartupOptions Parse(IReadOnlyList<string> args, string defaultBasePath)
    {
        var webMode = false;
        var basePath = defaultBasePath;
        var webUrl = DefaultWebUrl;
        string? e2eScriptPath = null;
        string? e2eArtifactsPath = null;
        var plainOutput = false;
        var agentConsoleMode = false;
        var agentConsoleUrl = DefaultAgentConsoleUrl;
        var agentConsoleUrlProvided = false;
        string? agentConsoleToken = null;

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

            if (string.Equals(arg, "--agent-console", StringComparison.OrdinalIgnoreCase))
            {
                agentConsoleMode = true;
                continue;
            }

            if (string.Equals(arg, "--agent-url", StringComparison.OrdinalIgnoreCase))
            {
                agentConsoleUrl = ReadRequiredValue(args, ref index, "--agent-url");
                agentConsoleUrlProvided = true;
                continue;
            }

            if (string.Equals(arg, "--agent-token", StringComparison.OrdinalIgnoreCase))
            {
                agentConsoleToken = ReadRequiredValue(args, ref index, "--agent-token");
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal) && Directory.Exists(arg))
                basePath = arg;
        }

        if (agentConsoleMode && string.IsNullOrWhiteSpace(agentConsoleToken))
            throw new ArgumentException("Missing value for --agent-token when --agent-console is enabled.", nameof(args));

        if (agentConsoleMode && webMode)
            throw new ArgumentException("--agent-console cannot be combined with --web.", nameof(args));

        if (agentConsoleMode && !string.IsNullOrWhiteSpace(e2eScriptPath))
            throw new ArgumentException("--agent-console cannot be combined with --e2e-script.", nameof(args));

        if ((agentConsoleMode || agentConsoleUrlProvided) && !IsLoopbackHttpUrl(agentConsoleUrl))
            throw new ArgumentException("--agent-url must be an absolute HTTP(S) loopback URL.", nameof(args));

        return new ClientStartupOptions(webMode, basePath, webUrl, e2eScriptPath, e2eArtifactsPath, plainOutput)
        {
            AgentConsoleMode = agentConsoleMode,
            AgentConsoleUrl = agentConsoleUrl,
            AgentConsoleToken = agentConsoleToken
        };
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

    private static bool IsLoopbackHttpUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }
}
