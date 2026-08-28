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
        ArgumentNullException.ThrowIfNull(request.Claim);
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(request.Claim.Text) ||
            string.IsNullOrWhiteSpace(request.FinalSceneText) ||
            !request.FinalSceneText.Contains(
                request.Claim.Text,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundAcquisition.narration",
                "wound_acquisition_narration_missing",
                "the exact non-empty acquisition narration inside the final scene",
                string.IsNullOrWhiteSpace(request.Claim.Text)
                    ? "empty claim"
                    : "claim absent from final scene");
            return new WoundAcquisitionOutputResult(null, null, issues);
        }

        var contradictions = new List<string>();
        var wound = request.Wound;
        var claim = request.Claim;
        if (!string.Equals(
                request.LocalWoundRef,
                claim.LocalWoundRef,
                StringComparison.Ordinal))
        {
            contradictions.Add("wound_ref");
        }
        if (!string.Equals(
                wound.Origin.EventRef,
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

        var prefix = string.Equals(
            wound.Classification.Domain,
            "spiritual",
            StringComparison.Ordinal)
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
