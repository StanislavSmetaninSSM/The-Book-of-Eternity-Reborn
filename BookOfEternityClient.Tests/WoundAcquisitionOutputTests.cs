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
