using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Reverses the reviewed main skill-coupling integration before unchanged historical source guards.
/// </summary>
internal static class SkillCouplingMergeSourceInventory
{
    /// <summary>
    /// Checks the entire supported source and uniquely reverses its reviewed merge fragments.
    /// </summary>
    /// <param name="source">The current implementation or its exact pre-merge source.</param>
    /// <returns>The exact wounds-parent source expected by the existing historical inverses.</returns>
    internal static string ReverseIfPresent(string source)
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,
            "BookOfEternityClient.Tests/TestData/main-skill-coupling-source-inverse.json")))!.AsObject();
        var parentHash = fixture["woundsSourceSha256"]!.GetValue<string>();
        if (Hash(source) == parentHash)
            return source;

        Assert.Equal(fixture["mergedSourceSha256"]!.GetValue<string>(), Hash(source));
        foreach (var fragment in fixture["fragments"]!.AsArray())
        {
            var current = fragment!["current"]!.GetValue<string>();
            var previous = fragment["previous"]!.GetValue<string>();
            Assert.Equal(1, source.Split(current, StringSplitOptions.None).Length - 1);
            source = source.Replace(current, previous, StringComparison.Ordinal);
        }

        Assert.Equal(parentHash, Hash(source));
        return source;
    }

    private static string Hash(string source) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
}
