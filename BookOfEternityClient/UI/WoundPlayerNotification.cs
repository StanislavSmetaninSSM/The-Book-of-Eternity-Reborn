using System.Text.Encodings.Web;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

internal sealed record WoundPlayerTextProjection(
    string PlainText,
    string ConsoleMarkup,
    string BrowserText);

internal sealed record WoundAcquisitionNarrationClaim(
    string LocalWoundRef,
    string EventRef,
    string Domain,
    string WoundName,
    int SeverityRank,
    string Text);

internal sealed record WoundAcquisitionOutputRequest(
    string LocalWoundRef,
    WoundMaterializationEnvelope Wound,
    WoundAcquisitionNarrationClaim Claim,
    string FinalSceneText);

internal sealed record WoundWorseningOutputRequest(
    string LocalWoundRef,
    string EventRef,
    WoundMaterializationEnvelope Wound,
    WoundAcquisitionNarrationClaim Claim,
    string FinalSceneText);

internal sealed record WoundAcquisitionOutputResult(
    WoundPlayerNotification? Notification,
    WoundPlayerTextProjection? AcquisitionNarration,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success =>
        Notification is not null &&
        AcquisitionNarration is not null &&
        Issues.Count == 0;
}

internal sealed record WoundPlayerNotification(
    WoundPlayerTextProjection Text,
    string DetailCommand)
{
    private const string Command = "/раны";

    internal static WoundAcquisitionOutputResult Compose(
        WoundAcquisitionOutputRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Wound);
        return ComposeCore(
            request.LocalWoundRef,
            request.Wound,
            request.Claim,
            request.Wound.Origin.EventRef,
            request.FinalSceneText,
            worsening: false);
    }

    internal static WoundAcquisitionOutputResult ComposeWorsening(
        WoundWorseningOutputRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ComposeCore(
            request.LocalWoundRef,
            request.Wound,
            request.Claim,
            request.EventRef,
            request.FinalSceneText,
            worsening: true);
    }

    private static WoundAcquisitionOutputResult ComposeCore(
        string localWoundRef,
        WoundMaterializationEnvelope wound,
        WoundAcquisitionNarrationClaim claim,
        string expectedEventRef,
        string finalSceneText,
        bool worsening)
    {
        ArgumentNullException.ThrowIfNull(wound);
        ArgumentNullException.ThrowIfNull(claim);
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(claim.Text) ||
            string.IsNullOrWhiteSpace(finalSceneText) ||
            !finalSceneText.Contains(
                claim.Text,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundAcquisition.narration",
                "wound_acquisition_narration_missing",
                "the exact non-empty acquisition narration inside the final scene",
                string.IsNullOrWhiteSpace(claim.Text)
                    ? "empty claim"
                    : "claim absent from final scene");
            return new WoundAcquisitionOutputResult(null, null, issues);
        }

        var contradictions = new List<string>();
        if (!string.Equals(
                localWoundRef,
                claim.LocalWoundRef,
                StringComparison.Ordinal))
        {
            contradictions.Add("wound_ref");
        }
        if (!string.Equals(
                expectedEventRef,
                claim.EventRef,
                StringComparison.Ordinal))
        {
            contradictions.Add("event_ref");
        }
        if (!string.Equals(
                wound.Classification.Domain,
                claim.Domain,
                StringComparison.Ordinal))
        {
            contradictions.Add("domain");
        }
        if (!string.Equals(
                wound.Display.Name,
                claim.WoundName,
                StringComparison.Ordinal))
        {
            contradictions.Add("name");
        }
        if (wound.Severity.Rank != claim.SeverityRank)
            contradictions.Add("severity");
        if (!string.Equals(
                wound.Display.AcquisitionNarration,
                claim.Text,
                StringComparison.Ordinal))
        {
            contradictions.Add("text");
        }
        if (contradictions.Count != 0)
        {
            Add(
                issues,
                "woundAcquisition.narration",
                "wound_acquisition_narration_contradiction",
                "exact local wound, event, domain, name, severity, and narration",
                string.Join(",", contradictions));
            return new WoundAcquisitionOutputResult(null, null, issues);
        }

        var spiritual = string.Equals(
            wound.Classification.Domain,
            "spiritual",
            StringComparison.Ordinal);
        var prefix = worsening
            ? spiritual
                ? "Духовная рана ухудшилась"
                : "Рана ухудшилась"
            : spiritual
                ? "Получена духовная рана"
                : "Получена рана";
        var plain =
            $"{prefix}: {wound.Display.Name} ({wound.Severity.Value}). Подробнее: {Command}";
        return new WoundAcquisitionOutputResult(
            new WoundPlayerNotification(Project(plain), Command),
            Project(claim.Text),
            Array.Empty<ValidationIssue>());
    }

    private static WoundPlayerTextProjection Project(string value) => new(
        value,
        Markup.Escape(value),
        HtmlEncoder.Default.Encode(value));

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "Wound acquisition output does not match the accepted wound.",
        code: code,
        section: "wound_acquisition_output",
        expected: expected,
        actual: actual,
        repairHint:
            "Keep the accepted wound unchanged and correct only its acquisition narration."));
}
