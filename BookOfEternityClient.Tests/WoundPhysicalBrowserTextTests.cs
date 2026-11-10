using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Typed physical choices must not inherit spiritual-cycle or diagnosis-only promises.</summary>
public sealed class WoundPhysicalBrowserTextTests
{
    private static readonly string Fingerprint = "sha256:" + new string('a', 64);

    [Theory]
    [InlineData("procedure")]
    [InlineData("guaranteed")]
    [InlineData("course")]
    public void PhysicalOnlyMenuConfirmsTreatmentWithoutInventingCycle(string mode)
    {
        var choice = Physical(mode);
        var before = choice.Selection.ToJson().ToJsonString();
        var result = Build([choice]);
        var prompt = Assert.Single(result.Prompts.OfType<UiConfirmationPrompt>());
        Assert.Contains("лечение физической раны", prompt.Prompt);
        Assert.Contains("услов", prompt.Prompt);
        Assert.Contains("физическую рану", Text(result.Blocks));
        Assert.Contains("одну попытку или текущий этап курса", Text(result.Blocks));
        AssertNoSpiritualCycle(Text(result.Blocks) + prompt.Prompt);
        AssertOptionsUnchanged(result, [choice]);
        Assert.Equal(before, choice.Selection.ToJson().ToJsonString());
    }

    [Theory]
    [InlineData("procedure", false)]
    [InlineData("guaranteed", false)]
    [InlineData("course", false)]
    [InlineData("procedure", true)]
    [InlineData("guaranteed", true)]
    [InlineData("course", true)]
    public void MixedMenuDescribesBothActionsRegardlessOfOrder(string mode, bool reverse)
    {
        WoundTreatmentChoice[] choices = reverse ? [Diagnosis(), Physical(mode)] : [Physical(mode), Diagnosis()];
        var result = Build(choices);
        var prompt = Assert.Single(result.Prompts.OfType<UiConfirmationPrompt>());
        Assert.Contains("лечение или осмотр физической раны", prompt.Prompt);
        Assert.DoesNotContain("выбранный осмотр", prompt.Prompt);
        AssertNoSpiritualCycle(Text(result.Blocks) + prompt.Prompt);
        AssertOptionsUnchanged(result, choices);
    }

    [Theory]
    [InlineData("procedure", false)]
    [InlineData("guaranteed", false)]
    [InlineData("course", false)]
    [InlineData("procedure", true)]
    [InlineData("guaranteed", true)]
    [InlineData("course", true)]
    public void DisabledPhysicalChoicesKeepPhysicalLabelsForSelfAndNpc(string mode, bool npc)
    {
        var choice = Physical(mode, available: false, npc: npc);
        var state = new WoundCommandState("generation", [choice]);
        if (npc)
            state = state.ForTarget(new("target", "Пациент рядом", "Текущая локация", [],
                choice.Selection.MortalTreatment!.Target, null, Fingerprint));
        var result = ExplorerWoundCommandResultBuilder.Build("/лечить", Projection(), state, treatment: true);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Empty(result.Prompts);
        var panel = Assert.Single(result.Blocks.OfType<UiPanelBlock>(), value => value.Title.Contains(choice.Name));
        Assert.StartsWith("Лечение физической раны", panel.Title);
        Assert.Equal(choice.Reason, Assert.IsType<UiTextBlock>(Assert.Single(panel.Blocks)).Text);
        var text = Text(result.Blocks);
        Assert.DoesNotContain("духовн", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Исцеление", text);
        Assert.DoesNotContain("согласие обитателя", text);
        Assert.Throws<InvalidDataException>(() => state.Resolve(choice.Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OneDisabledPhysicalActionDoesNotChangeTheOtherActionIntoDiagnosis(bool treatmentAvailable)
    {
        WoundTreatmentChoice[] choices = [Physical("procedure", treatmentAvailable), Diagnosis(!treatmentAvailable)];
        var result = Build(choices);
        Assert.Contains("лечение или осмотр физической раны", Assert.Single(result.Prompts.OfType<UiConfirmationPrompt>()).Prompt);
        AssertOptionsUnchanged(result, choices);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosisOnlyRetainsItsOwnConditions(bool available)
    {
        var result = Build([Diagnosis(available)]);
        if (available)
        {
            Assert.Contains("выбранный осмотр", Assert.Single(result.Prompts.OfType<UiConfirmationPrompt>()).Prompt);
            Assert.Contains("Сам осмотр не лечит", Text(result.Blocks));
        }
        else
        {
            Assert.Empty(result.Prompts);
            Assert.Contains("физического осмотра", Text(result.Blocks));
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("false")]
    [InlineData("wrong-type")]
    public async Task EarlyConfirmationRefusalDoesNotPromiseOrStartACycle(string answer)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-wound-prompt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            var coordinator = new BrowserLocalWriteCoordinator(files, new LocalUiSessionLockService(files));
            var service = new BrowserWoundWriteService(files, coordinator);
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(root, path).Replace('\\', '/').StartsWith(".boe_runtime/", StringComparison.Ordinal))
                .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
            var answers = new Dictionary<string, JsonNode?> { ["wound_selection"] = JsonValue.Create("physical") };
            if (answer != "missing") answers["confirm_wound_treatment"] = answer == "false" ? JsonValue.Create(false) : JsonValue.Create("yes");
            var result = await service.ApplyWithinTransactionAsync(lease,
                new("generation", [Physical("procedure")]), answers,
                new("owner", "browser", "Локальный игрок", TimeSpan.FromMinutes(2)),
                () => throw new InvalidOperationException("Unconfirmed action must not enter current-prompt execution."));
            Assert.False(result.Success);
            Assert.True(result.KeepSessionOpen);
            Assert.Equal(CommandExecutionState.RequiresInput, result.State);
            Assert.Contains("услов", result.Message);
            AssertNoSpiritualCycle(result.Message);
            Assert.Equal(before.Keys.Order(), Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(root, path).Replace('\\', '/').StartsWith(".boe_runtime/", StringComparison.Ordinal)).Order());
            foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static WoundTreatmentChoice Physical(string mode, bool available = true, bool npc = false)
    {
        var owner = new WoundOwnerCoordinate("mortal_world", npc ? "npc" : "player",
            npc ? "patient_01" : "player_current", npc ? WoundCarrierCatalog.NpcPath : WoundCarrierCatalog.PlayerPath);
        var selection = new SpiritualSelfTreatmentSelection("wound_physical_test", Fingerprint)
        {
            MortalTreatment = new(owner, owner, "route_physical_test", mode, mode == "course" ? 2 : null,
                Fingerprint, Fingerprint)
        };
        selection = SpiritualSelfTreatmentSelection.Parse(selection.ToJson());
        return new("physical", selection, "Перевязка [раны]", "II", null, available,
            "Общие условия: чистая повязка. Расходы: одна повязка. " + (available ? "Доступно." : "Не хватает материала."));
    }

    private static WoundTreatmentChoice Diagnosis(bool available = true)
    {
        var owner = new WoundOwnerCoordinate("mortal_world", "player", "player_current", WoundCarrierCatalog.PlayerPath);
        var selection = new SpiritualSelfTreatmentSelection("wound_physical_test", Fingerprint)
        { MortalDiagnosis = new(owner, owner, "diagnosis_test", Fingerprint, Fingerprint) };
        selection = SpiritualSelfTreatmentSelection.Parse(selection.ToJson());
        return new("diagnosis", selection, "Осмотр раны", "II", null, available,
            "Осмотр: 30 минут, заявленная проверка. Сам осмотр не лечит.");
    }

    private static ExplorerCommandResult Build(WoundTreatmentChoice[] choices) =>
        ExplorerWoundCommandResultBuilder.Build("/лечить", Projection(), new("generation", choices), treatment: true);

    private static WoundPlayerProjectionResult Projection() => new(true, "", [], []);

    private static void AssertOptionsUnchanged(ExplorerCommandResult result, WoundTreatmentChoice[] choices)
    {
        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        var selection = Assert.Single(result.Prompts.OfType<UiSelectionPrompt>());
        Assert.True(selection.Required);
        Assert.True(Assert.Single(result.Prompts.OfType<UiConfirmationPrompt>()).Required);
        var options = selection.Options;
        Assert.Equal(choices.Length, options.Count);
        for (var i = 0; i < choices.Length; i++)
        {
            Assert.Equal(choices[i].Value, options[i].Value);
            Assert.Equal(choices[i].Name + " — " + choices[i].Severity, options[i].Label);
            Assert.Equal(choices[i].Reason, options[i].Description);
            Assert.Equal(!choices[i].Available, options[i].Disabled);
        }
    }

    private static void AssertNoSpiritualCycle(string text)
    {
        Assert.DoesNotContain("цикл", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ОД", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Бросок сохраняется", text);
    }

    private static string Text(IEnumerable<UiBlock> blocks) => string.Join("\n", blocks.Select(block => block switch
    {
        UiTextBlock text => text.Text,
        UiPanelBlock panel => panel.Title + "\n" + Text(panel.Blocks),
        _ => ""
    }));
}
