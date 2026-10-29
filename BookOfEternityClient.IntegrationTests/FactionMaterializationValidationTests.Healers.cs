using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class FactionMaterializationValidationTests
{
    [Fact]
    public async Task CurrentMaterializedFactionWithoutHealerIsRejectedWithoutChangingAuthoredState()
    {
        await WriteGenericStoryFactionOutcomeAsync(BuildCompleteGenericStoryShiningFaction());
        var before = new[] { ShiningPath, ResidentPath, AfterlifeProfilesPath, SarefStoryPath }
            .ToDictionary(path => path, path => File.Exists(_fs.ResolvePath(path))
                ? File.ReadAllBytes(_fs.ResolvePath(path)) : null, StringComparer.Ordinal);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();
        foreach (var (path, bytes) in before)
        {
            var after = File.Exists(_fs.ResolvePath(path)) ? File.ReadAllBytes(_fs.ResolvePath(path)) : null;
            Assert.True(bytes is null ? after is null : after is not null && bytes.AsSpan().SequenceEqual(after), path);
        }
        var factionErrors = issues.Where(i => i.Severity == IssueSeverity.Error &&
            i.Actor == "shining_faction:shine_faction_dawn_archive").ToArray();
        Assert.DoesNotContain(factionErrors, i => i.Code != "faction_materialization_shining_healer_missing");
        Assert.Contains(factionErrors, i => i.Code == "faction_materialization_shining_healer_missing");
    }
}
