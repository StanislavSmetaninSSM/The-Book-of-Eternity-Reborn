using System.Security.Cryptography;

namespace BookOfEternityClient.AgentConsole;

public sealed record AgentConsoleAccessToken(string Value, bool WasGenerated)
{
    private const int GeneratedTokenByteCount = 32;

    public static AgentConsoleAccessToken Resolve(string tokenOption)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenOption);

        return string.Equals(tokenOption, "auto", StringComparison.OrdinalIgnoreCase)
            ? new AgentConsoleAccessToken(GenerateToken(), WasGenerated: true)
            : new AgentConsoleAccessToken(tokenOption, WasGenerated: false);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(GeneratedTokenByteCount);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
