using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Publishes an actual signed C2 wound through the common C4 transaction for response-delivery boundary tests.
    /// </summary>
    /// <param name="hooks">
    /// Optional physical read hook used after publication; <see langword="null"/> preserves ordinary reads.
    /// </param>
    /// <returns>
    /// The disposable real session and its genuine detached published output, without an executable owner.
    /// </returns>
    internal static async Task<(ResourceMaterializationTestContext Context, SpiritualWoundPublishedOutput Output)>
        CreateSpiritualPublishedResponseFixtureAsync(FileSystemManagerHooks? hooks = null)
    {
        var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        try
        {
            await context.WriteExactJsonAsync("output/interface_updates.json",
                "{\"dialogueOptions\":[{\"text\":\"Принятый выбор\",\"category\":\"neutral\"}],\"image_prompt\":\"accepted-image\"}");
            await context.WriteExactJsonAsync("output/debug_logs.json",
                "{\"gm_thoughts_markdown\":\"Принятое объяснение.\"}");
            await PrepareSpiritualC4PublicationAsync(context);
            var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
            AssertNoConflictFrameErrors(published.Issues);
            Assert.NotNull(published.MechanicsPlan);
            var output = Assert.IsType<SpiritualWoundPublishedOutput>(published.SpiritualWoundOutput);
            Assert.Empty(output.Issues);
            Assert.Single(output.Notifications);
            return (context, output);
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }
}

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Rejects physical output drift before response construction and clears both pending delivery fields on that failure.
    /// </summary>
    /// <param name="changedPath">
    /// One member of the accepted narrative, interface and debug output cohort changed immediately before delivery.
    /// </param>
    /// <returns>
    /// A task completing after rejection and a subsequent ordinary response prove that no retained notice survives.
    /// </returns>
    [Theory]
    [InlineData("output/narrative_response.json")]
    [InlineData("output/interface_updates.json")]
    [InlineData("output/debug_logs.json")]
    public async Task BuildGameResponse_SpiritualPublishedOutputRejectsDriftAndClearsNotices(string changedPath)
    {
        var fixture = await AfterlifeResourceCutoverTests.CreateSpiritualPublishedResponseFixtureAsync();
        await using var context = fixture.Context;
        var engine = CreateGameEngine(fileSystem: context.FileSystem);
        RetainGenuineSpiritualResponseOutput(engine, fixture.Output);
        var changedJson = changedPath switch
        {
            "output/narrative_response.json" => "{\"response\":\"Изменённая сцена.\"}",
            "output/interface_updates.json" => "{\"dialogueOptions\":[{\"text\":\"Изменённый выбор\",\"category\":\"neutral\"}]}",
            _ => "{\"gm_thoughts_markdown\":\"Изменённое объяснение.\"}"
        };
        await context.WriteExactJsonAsync(changedPath, changedJson);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            InvokePrivateTaskResultAsync(engine, "BuildGameResponseFromFiles"));
        Assert.Contains(changedPath, error.Message, StringComparison.Ordinal);
        AssertSpiritualResponseDeliveryCleared(engine);
        Assert.Equal(Encoding.UTF8.GetBytes(changedJson), await context.FileSystem.ReadFileBytesAsync(changedPath));

        var later = Assert.IsType<GameResponse>(await InvokePrivateTaskResultAsync(engine, "BuildGameResponseFromFiles"));
        Assert.True(later.WoundNotifications is null or { Length: 0 });
        if (changedPath == "output/narrative_response.json")
            Assert.Equal("Изменённая сцена.", later.Response);
        else if (changedPath == "output/interface_updates.json")
            Assert.Equal("Изменённый выбор", Assert.Single(later.DialogueOptions!).Text);
        else
            Assert.Equal("Изменённое объяснение.", later.GmThoughtsMarkdown);
    }

    /// <summary>
    /// Uses immutable accepted texts with the genuine notices when an external writer changes already-checked files during witness reads.
    /// The physical read hook models that bounded race; it does not create or replace publication authority.
    /// </summary>
    /// <returns>
    /// A task completing after the accepted response and the next changed response prove that their texts and notices never mix.
    /// </returns>
    [Fact]
    public async Task BuildGameResponse_SpiritualPublishedOutputDoesNotMixRetainedNoticesWithChangedText()
    {
        var armed = false;
        var injected = false;
        string? narrativePath = null;
        string? interfacePath = null;
        const string changedNarrative = "{\"response\":\"Поздняя чужая сцена.\"}";
        const string changedInterface = "{\"dialogueOptions\":[{\"text\":\"Поздний чужой выбор\",\"category\":\"neutral\"}],\"image_prompt\":\"changed-image\"}";
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = async path =>
            {
                if (!armed || path != "output/debug_logs.json") return;
                armed = false;
                injected = true;
                await File.WriteAllBytesAsync(narrativePath!, Encoding.UTF8.GetBytes(changedNarrative));
                await File.WriteAllBytesAsync(interfacePath!, Encoding.UTF8.GetBytes(changedInterface));
            }
        };
        var fixture = await AfterlifeResourceCutoverTests.CreateSpiritualPublishedResponseFixtureAsync(hooks);
        await using var context = fixture.Context;
        narrativePath = context.FileSystem.ResolvePath("output/narrative_response.json");
        interfacePath = context.FileSystem.ResolvePath("output/interface_updates.json");
        Assert.Equal(new[] { "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" },
            WoundAcceptedTurnSnapshotContract.OutputPaths);
        var engine = CreateGameEngine(fileSystem: context.FileSystem);
        RetainGenuineSpiritualResponseOutput(engine, fixture.Output);
        var expectedNotice = Assert.Single(fixture.Output.Notifications).Text.PlainText;
        armed = true;
        var accepted = Assert.IsType<GameResponse>(await InvokePrivateTaskResultAsync(engine, "BuildGameResponseFromFiles"));
        Assert.True(injected);
        Assert.Equal("Чужое давление надломило волю хранителя.", accepted.Response);
        Assert.Equal("Принятый выбор", Assert.Single(accepted.DialogueOptions!).Text);
        Assert.Equal("accepted-image", accepted.ImagePrompt);
        Assert.Equal("Принятое объяснение.", accepted.GmThoughtsMarkdown);
        Assert.Equal(expectedNotice, Assert.Single(accepted.WoundNotifications!));
        AssertSpiritualResponseDeliveryCleared(engine);
        Assert.Equal(Encoding.UTF8.GetBytes(changedNarrative), await File.ReadAllBytesAsync(narrativePath));
        Assert.Equal(Encoding.UTF8.GetBytes(changedInterface), await File.ReadAllBytesAsync(interfacePath));
        var later = Assert.IsType<GameResponse>(await InvokePrivateTaskResultAsync(engine, "BuildGameResponseFromFiles"));
        Assert.Equal("Поздняя чужая сцена.", later.Response);
        Assert.Equal("Поздний чужой выбор", Assert.Single(later.DialogueOptions!).Text);
        Assert.Equal("changed-image", later.ImagePrompt);
        Assert.True(later.WoundNotifications is null or { Length: 0 });
    }

    /// <summary>
    /// Places only genuine completed publication results into the existing response-delivery fields.
    /// This bounded builder seam does not simulate execution, a private receipt or the full accepted caller.
    /// </summary>
    /// <param name="engine">
    /// Engine using the exact filesystem that performed the real publication.
    /// </param>
    /// <param name="output">
    /// Detached result returned by the successful common publication transaction.
    /// </param>
    private static void RetainGenuineSpiritualResponseOutput(GameEngine engine, SpiritualWoundPublishedOutput output)
    {
        SetPrivateField(engine, "_acceptedTurnSpiritualOutput", output);
        SetPrivateField(engine, "_acceptedTurnWoundNotifications", output.Notifications.ToArray());
    }

    /// <summary>
    /// Checks that neither the immutable output handoff nor its notices remain available after one delivery attempt.
    /// </summary>
    /// <param name="engine">
    /// Engine whose response builder has completed or rejected the guarded output.
    /// </param>
    private static void AssertSpiritualResponseDeliveryCleared(GameEngine engine)
    {
        Assert.Null(typeof(GameEngine).GetField("_acceptedTurnSpiritualOutput", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(engine));
        Assert.Empty(GetPrivateField<WoundPlayerNotification[]>(engine, "_acceptedTurnWoundNotifications"));
    }
}
