using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeDocumentationCoverageTests
{
    [Fact]
    public void GuardianMusingsWorkedExampleKeepsSingleMemorySurfaceAndOriginalClientBinding()
    {
        var text = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var section = text.Split("guardian_musings_original_bound_v1", StringSplitOptions.None)[1]
            .Split("The broader fragment below", StringSplitOptions.None)[0];
        Assert.Contains("validated pre-turn snapshot", section, StringComparison.Ordinal);
        Assert.Contains("`activeGuardian.guardianId` is `guard_social_azalia_001`", section, StringComparison.Ordinal);
        Assert.Contains("without a matching", section, StringComparison.Ordinal);
        Assert.Contains("without appending again", section, StringComparison.Ordinal);
        Assert.Contains("only the current listed outputs", section, StringComparison.Ordinal);
        Assert.Contains("before one fresh validation iteration", section, StringComparison.Ordinal);
        Assert.Contains("cannot borrow that completion", section, StringComparison.Ordinal);
        Assert.Contains("afterlife-only command", section, StringComparison.Ordinal);
        Assert.Contains("no internal proof or handoff fields", section, StringComparison.Ordinal);
        var snippet = Assert.Single(ExampleSnippetExtractor.ExtractAll(), candidate =>
            candidate.File == "E_CLI_Afterlife_Turns.txt" &&
            candidate.RawText.Contains("Я запомню, что душа попросила память", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(snippet.RawText);
        var property = Assert.Single(document.RootElement.EnumerateObject());
        Assert.Equal("UpdateGuardians", property.Name);
        var command = Assert.Single(property.Value.EnumerateArray());
        Assert.Equal("addMusings", command.GetProperty("command").GetString());
        Assert.Equal("guard_social_azalia_001", command.GetProperty("guardianId").GetString());
        var musing = Assert.Single(command.GetProperty("musings").EnumerateArray());
        Assert.Equal(326, musing.GetProperty("turn").GetInt32());
        Assert.Equal("soul_assessment", musing.GetProperty("topic").GetString());
        Assert.Equal("hopeful", musing.GetProperty("mood").GetString());
        Assert.False(string.IsNullOrWhiteSpace(musing.GetProperty("text").GetString()));
    }
}
