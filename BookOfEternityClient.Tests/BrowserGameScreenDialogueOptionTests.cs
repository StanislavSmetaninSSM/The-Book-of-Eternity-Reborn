using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BrowserGameScreenDialogueOptionTests
{
    [Fact]
    public void ReadDialogueOptions_HidesControlTagButKeepsSubmittedValue()
    {
        var root = JsonNode.Parse("""
        {
          "dialogueOptions": [
            {
              "text": "[AFTERLIFE_SPIRITUAL_ACTION: afterlife_conflict_myriel_ash_ward_trial_turn_7] Держу первый круг Оберега.",
              "category": "action"
            }
          ]
        }
        """)!.AsObject();

        var options = InvokeReadDialogueOptions(root);
        var option = Assert.Single(options);

        Assert.Equal("Держу первый круг Оберега.", option.Text);
        Assert.Equal(
            "[AFTERLIFE_SPIRITUAL_ACTION: afterlife_conflict_myriel_ash_ward_trial_turn_7] Держу первый круг Оберега.",
            option.InputValue);
        Assert.DoesNotContain("AFTERLIFE_SPIRITUAL_ACTION", option.Text, StringComparison.Ordinal);
    }

    private static IReadOnlyList<BrowserGameScreenDialogueOptionDto> InvokeReadDialogueOptions(JsonObject root)
    {
        var method = typeof(BrowserGameScreenService).GetMethod(
            "ReadDialogueOptions",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<IReadOnlyList<BrowserGameScreenDialogueOptionDto>>(method.Invoke(null, [root]));
    }
}
