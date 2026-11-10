using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundAcquisitionOutputTests
{
    [Theory]
    [InlineData("empty_claim")]
    [InlineData("missing_from_scene")]
    public void Compose_RequiresCompleteAcquisitionNarrationInFinalScene(string mutation)
    {
        var wound = PhysicalWound();
        var claim = Claim(wound);
        var scene = $"Свод обрушивается. {claim.Text} Герой отступает.";
        if (mutation == "empty_claim")
            claim = claim with { Text = string.Empty };
        else
            scene = "Свод обрушивается, но текст о получении раны отсутствует.";

        var result = WoundPlayerNotification.Compose(new WoundAcquisitionOutputRequest(
            "wound_local_test_001",
            wound,
            claim,
            scene));

        Assert.False(result.Success);
        Assert.Null(result.Notification);
        Assert.Null(result.AcquisitionNarration);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_acquisition_narration_missing");
    }

    [Theory]
    [InlineData("wound_ref")]
    [InlineData("event_ref")]
    [InlineData("domain")]
    [InlineData("name")]
    [InlineData("severity")]
    [InlineData("text")]
    public void Compose_RejectsNarrationClaimContradictingAcceptedWound(string mutation)
    {
        var wound = PhysicalWound();
        var claim = Claim(wound);
        claim = mutation switch
        {
            "wound_ref" => claim with { LocalWoundRef = "wound_local_foreign" },
            "event_ref" => claim with { EventRef = "turn_42:foreign_event" },
            "domain" => claim with { Domain = "spiritual" },
            "name" => claim with { WoundName = "Несуществующая рана" },
            "severity" => claim with { SeverityRank = 3 },
            "text" => claim with
            {
                Text = "Удар не причинил никакого вреда и не оставил раны."
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };

        var result = WoundPlayerNotification.Compose(new WoundAcquisitionOutputRequest(
            "wound_local_test_001",
            wound,
            claim,
            $"Сцена. {claim.Text}"));

        Assert.False(result.Success);
        Assert.Null(result.Notification);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_acquisition_narration_contradiction" &&
            issue.Actual!.Contains(mutation, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(
        false,
        "Получена рана: Рваная рана левого бока (II). Подробнее: /раны")]
    [InlineData(
        true,
        "Получена духовная рана: Трещина духовной целостности (II). Подробнее: /раны")]
    public void Compose_ProducesDeterministicRussianNotification(
        bool spiritual,
        string expected)
    {
        var wound = spiritual ? SpiritualWound() : PhysicalWound();
        var claim = Claim(wound);

        var first = WoundPlayerNotification.Compose(new WoundAcquisitionOutputRequest(
            "wound_local_test_001",
            wound,
            claim,
            $"Начало сцены. {claim.Text} Конец сцены."));
        var second = WoundPlayerNotification.Compose(new WoundAcquisitionOutputRequest(
            "wound_local_test_001",
            wound,
            claim,
            $"Начало сцены. {claim.Text} Конец сцены."));

        Assert.True(first.Success);
        Assert.Empty(first.Issues);
        Assert.Equal(expected, first.Notification!.Text.PlainText);
        Assert.Equal("/раны", first.Notification.DetailCommand);
        Assert.Equal(
            first.Notification.Text,
            second.Notification!.Text);
        Assert.Equal(
            wound.Display.AcquisitionNarration,
            first.AcquisitionNarration!.PlainText);
    }

    [Fact]
    public void Compose_EscapesUntrustedConsoleAndBrowserMarkup()
    {
        var baseWound = PhysicalWound();
        var unsafeName = "[red]<script>alert(1)</script>[/]";
        var unsafeNarration =
            "[bold]Осколок[/] оставляет <img src=x onerror=alert(1)> рану.";
        var wound = baseWound with
        {
            Display = baseWound.Display with
            {
                Name = unsafeName,
                AcquisitionNarration = unsafeNarration
            }
        };
        var claim = Claim(wound);

        var result = WoundPlayerNotification.Compose(new WoundAcquisitionOutputRequest(
            "wound_local_test_001",
            wound,
            claim,
            $"Сцена: {unsafeNarration}"));

        Assert.True(result.Success);
        Assert.DoesNotContain(
            "[red]<script>",
            result.Notification!.Text.ConsoleMarkup);
        Assert.Contains("[[red]]", result.Notification.Text.ConsoleMarkup);
        Assert.DoesNotContain("<script>", result.Notification.Text.BrowserText);
        Assert.Contains("&lt;script&gt;", result.Notification.Text.BrowserText);
        Assert.DoesNotContain(
            "[bold]Осколок",
            result.AcquisitionNarration!.ConsoleMarkup);
        Assert.Contains("[[bold]]", result.AcquisitionNarration.ConsoleMarkup);
        Assert.DoesNotContain("<img", result.AcquisitionNarration.BrowserText);
        Assert.Contains("&lt;img", result.AcquisitionNarration.BrowserText);
    }

    [Fact]
    public void Compose_PlayerProjectionContainsNoInternalIdentityOrAuthorityTerms()
    {
        var wound = PhysicalWound();
        var claim = Claim(wound);

        var result = WoundPlayerNotification.Compose(new WoundAcquisitionOutputRequest(
            "wound_local_test_001",
            wound,
            claim,
            $"Сцена: {claim.Text}"));

        Assert.True(result.Success);
        var visible = string.Join('\n', new[]
        {
            result.Notification!.Text.PlainText,
            result.Notification.Text.ConsoleMarkup,
            result.Notification.Text.BrowserText,
            result.Notification.DetailCommand,
            result.AcquisitionNarration!.PlainText,
            result.AcquisitionNarration.ConsoleMarkup,
            result.AcquisitionNarration.BrowserText
        });
        foreach (var forbidden in new[]
        {
            wound.WoundId,
            wound.Origin.OpportunityId,
            wound.Origin.EventRef,
            wound.Origin.SourceId,
            wound.LastTransition.TransitionId,
            "wound_local_test_001",
            "fingerprint",
            "validation",
            "authority"
        })
        {
            Assert.DoesNotContain(forbidden, visible, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Real parsed input and exact raw binding precede the player-only forbidden-control oracle.</summary>
    [Theory]
    [InlineData("name", 27)]
    [InlineData("name", 8)]
    [InlineData("narration", 27)]
    [InlineData("narration", 8)]
    public void Compose_RemovesForbiddenControlsOnlyFromPlayerProjections(string field, int controlCode)
    {
        Assert.True(controlCode is 27 or 8);
        var control = (char)controlCode;
        var root = WoundContractTestData.CreateActiveWound();
        var member = field switch
        {
            "name" => "name",
            "narration" => "acquisitionNarration",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
        root["display"]![member] = field == "name"
            ? "Рваная" + control + " рана"
            : "Осколок" + control + " оставляет заметную рану.";
        var rootBefore = root.ToJsonString();
        // This is the actual contract parser, not a record 'with' that bypasses text admission.
        var wound = Parse(root);
        Assert.Equal(rootBefore, root.ToJsonString());
        var raw = field == "name" ? wound.Display.Name : wound.Display.AcquisitionNarration;
        Assert.True(raw.Contains(control), "The actual parser must retain the supplied control for this causal premise.");
        Assert.True(raw == root["display"]![member]!.GetValue<string>(), "Parsed text must match the exact raw input.");
        var result = ComposeWithoutChangingRawInputs(root, wound);
        var unsafeLabels = ForbiddenControlProjectionLabels(result);
        // The only intended RED: all binding/immutability premises succeeded, but player output retains C0.
        // Labels contain no untrusted payload or control; neither assertions nor case names print C0.
        Assert.True(unsafeLabels.Length == 0,
            "Forbidden control remains in player projection: " + string.Join(", ", unsafeLabels));
    }

    /// <summary>The established LF/tab/Unicode policy is preserved in every player projection.</summary>
    [Fact]
    public void Compose_PreservesAllowedLineBreakTabAndUnicodeInPlayerProjections()
    {
        const string name = "Рана ёж🌙\n\tпечёт";
        const string narration = "Осколок задевает бок.\n\tДуша помнит ёж🌙.";
        const string notification = "Получена рана: Рана ёж🌙\n\tпечёт (II). Подробнее: /раны";
        var root = WoundContractTestData.CreateActiveWound();
        root["display"]!["name"] = name;
        root["display"]!["acquisitionNarration"] = narration;
        var rootBefore = root.ToJsonString();
        var wound = Parse(root);
        Assert.Equal(rootBefore, root.ToJsonString());
        Assert.Equal(name, wound.Display.Name);
        Assert.Equal(narration, wound.Display.AcquisitionNarration);
        Assert.Equal("II", wound.Severity.Value);
        var result = ComposeWithoutChangingRawInputs(root, wound);
        // Fixed expected text; no expected value is computed with the production text policy.
        Assert.Equal(notification, result.Notification!.Text.PlainText);
        Assert.Equal(notification, result.Notification.Text.ConsoleMarkup);
        Assert.Equal(notification, WebUtility.HtmlDecode(result.Notification.Text.BrowserText));
        Assert.Equal(narration, result.AcquisitionNarration!.PlainText);
        Assert.Equal(narration, result.AcquisitionNarration.ConsoleMarkup);
        Assert.Equal(narration, WebUtility.HtmlDecode(result.AcquisitionNarration.BrowserText));
        Assert.Empty(ForbiddenControlProjectionLabels(result));
    }

    /// <summary>Uses the existing exact Claim/Compose path and verifies that no raw binding DATA is rewritten.</summary>
    /// <param name="root">The original genuine JSON input, mutated before the actual parser.</param>
    /// <param name="wound">The actual parsed canonical envelope.</param>
    /// <returns>The genuine successful output after all raw binding and immutability premises.</returns>
    private static WoundAcquisitionOutputResult ComposeWithoutChangingRawInputs(JsonObject root, WoundMaterializationEnvelope wound)
    {
        var rootBefore = root.ToJsonString();
        var woundBefore = WoundMaterializationContract.SerializeCanonical(wound);
        var claim = Claim(wound);
        var claimBefore = JsonSerializer.Serialize(claim);
        Assert.True(claim.WoundName == wound.Display.Name && claim.Text == wound.Display.AcquisitionNarration,
            "Claim must retain exact raw parsed display text before output composition.");
        var result = WoundPlayerNotification.Compose(new WoundAcquisitionOutputRequest(
            "wound_local_test_001", wound, claim, "Сцена: " + claim.Text));
        Assert.True(result.Success);
        Assert.Empty(result.Issues);
        // JSON representations safely escape C0 even if an invariant assertion ever fails.
        Assert.Equal(rootBefore, root.ToJsonString());
        Assert.Equal(woundBefore, WoundMaterializationContract.SerializeCanonical(wound));
        Assert.Equal(claimBefore, JsonSerializer.Serialize(claim));
        return result;
    }

    /// <summary>Reports only safe projection labels; HTML decode prevents entity encoding from hiding C0.</summary>
    /// <param name="result">Actual successful notification and narration projections.</param>
    /// <returns>Safe labels of projections that contain forbidden controls under the already established policy.</returns>
    private static string[] ForbiddenControlProjectionLabels(WoundAcquisitionOutputResult result)
    {
        var notification = result.Notification!.Text;
        var narration = result.AcquisitionNarration!;
        (string Label, string Text)[] projections =
        [
            ("notification/plain", notification.PlainText),
            ("notification/console", notification.ConsoleMarkup),
            ("notification/browser", notification.BrowserText),
            ("notification/browser-decoded", WebUtility.HtmlDecode(notification.BrowserText)),
            ("narration/plain", narration.PlainText),
            ("narration/console", narration.ConsoleMarkup),
            ("narration/browser", narration.BrowserText),
            ("narration/browser-decoded", WebUtility.HtmlDecode(narration.BrowserText))
        ];
        return projections.Where(projection => projection.Text.Any(c => char.IsControl(c) && c is not ('\n' or '\t')))
            .Select(projection => projection.Label).ToArray();
    }

    private static WoundAcquisitionNarrationClaim Claim(
        WoundMaterializationEnvelope wound) => new(
        "wound_local_test_001",
        wound.Origin.EventRef,
        wound.Classification.Domain,
        wound.Display.Name,
        wound.Severity.Rank,
        wound.Display.AcquisitionNarration);

    private static WoundMaterializationEnvelope PhysicalWound() =>
        Parse(WoundContractTestData.CreateActiveWound());

    private static WoundMaterializationEnvelope SpiritualWound() =>
        Parse(WoundContractTestData.CreateSpiritualActiveWound());

    private static WoundMaterializationEnvelope Parse(
        System.Text.Json.Nodes.JsonObject root)
    {
        var result = WoundMaterializationContract.Parse(
            root.ToJsonString(),
            "woundAcquisition");
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        return result.Wound!;
    }
}
