using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class WoundMenuCallbackReadTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("skill_name")]
    [InlineData("invalid_effect")]
    public async Task MenuRetainedDataHonorsChangesFromAnArbitraryMidReadCallback(string fault)
    {
        const string activePath = "game_state/player/skills_active.json";
        const string passivePath = "game_state/player/skills_passive.json";
        var changedPath = fault == "skill_name" ? activePath : EffectCarrierCatalog.PlayerPath;
        var changedBytes = Encoding.UTF8.GetBytes(fault == "skill_name"
            ? "{\"activeSkillChanges\":[{\"skillId\":\"callback_skill\",\"skillName\":\"После callback\",\"lifecycle\":\"active\",\"active\":true}]}"
            : "{\"activeEffects\":");
        var armed = false; var changes = 0;
        FileSystemManager? files = null;
        var wound = WoundContractTestData.CreateSpiritualActiveWound("wound_callback_menu", "chaos_sea",
            ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: "chaos_sea",
            suppliedInput: WoundPlayerProjectionTestData.InputInRealm("chaos_sea", wound),
            beforeRead: path =>
            {
                // Both sources have already been read by the menu at this point.
                // This is an arbitrary existing FS callback, not accepted passive capture.
                if (armed && path == passivePath)
                {
                    armed = false; changes++;
                    File.WriteAllBytes(files!.ResolvePath(changedPath), changedBytes);
                }
                return Task.CompletedTask;
            });
        files = fixture.Files;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            await files.WriteFileAtomicAsync(lease, activePath,
                "{\"activeSkillChanges\":[{\"skillId\":\"callback_skill\",\"skillName\":\"До callback\",\"lifecycle\":\"active\",\"active\":true}]}");
            await files.WriteFileAtomicAsync(lease, passivePath, "{}");
            await files.WriteFileAtomicAsync(lease, EffectCarrierCatalog.PlayerPath, "{\"schemaVersion\":1,\"activeEffects\":[]}");
            var original = await EffectMechanicsSnapshot.LoadAsync(files, lease);
            Assert.True(original.IsAccepted);
            Assert.Contains("До callback", original.SkillScopeAuthority.CreateGmCatalog().ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
        // The existing full-root snapshot includes the private lock file. Read
        // it only after releasing this fixture's lease; never omit that image.
        var before = fixture.Snapshot(); var mutations = fixture.MutationCalls;
        armed = true;
        WoundPlayerReadContext? actual;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            actual = await WoundPlayerReadService.LoadContextAsync(files, lease, retainMenuInputs: true);
        Assert.Equal(1, changes); Assert.False(armed);
        Assert.Equal(changedBytes, File.ReadAllBytes(files.ResolvePath(changedPath)));
        var expectedPrefix = Path.GetRelativePath(fixture.Root, files.ResolvePath(changedPath)).Replace('\\', '/') + ":";
        var after = fixture.Snapshot();
        Assert.Equal(before.Where(row => !row.StartsWith(expectedPrefix, StringComparison.Ordinal)),
            after.Where(row => !row.StartsWith(expectedPrefix, StringComparison.Ordinal)));
        Assert.NotEqual(Assert.Single(before, row => row.StartsWith(expectedPrefix, StringComparison.Ordinal)),
            Assert.Single(after, row => row.StartsWith(expectedPrefix, StringComparison.Ordinal)));
        Assert.Equal(mutations, fixture.MutationCalls);

        // The unchanged pre-shortcut reader is the independent current-source oracle.
        EffectMechanicsSnapshot fresh;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            fresh = await EffectMechanicsSnapshot.LoadAsync(files, lease);
        Assert.Equal(after, fixture.Snapshot());
        if (fault == "skill_name")
        {
            Assert.True(fresh.IsAccepted);
            var expected = fresh.SkillScopeAuthority.CreateGmCatalog();
            Assert.Contains("После callback", expected.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            Assert.DoesNotContain("До callback", expected.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            Assert.True(actual is not null && actual.Input.Effects.IsAccepted &&
                JsonNode.DeepEquals(expected, actual.Input.Effects.SkillScopeAuthority.CreateGmCatalog()),
                "CALLBACK_CAUSAL: menu retained stale skill DATA or became unavailable after a valid mid-read source change.");
        }
        else
        {
            Assert.False(fresh.IsAccepted);
            Assert.Contains(fresh.Issues, issue => issue.FilePath == changedPath);
            Assert.True(actual is null,
                "CALLBACK_CAUSAL: menu accepted a retained effect source that the unchanged fresh reader rejects after the callback.");
        }
    }
}
