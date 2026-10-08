namespace BookOfEternityClient.Configuration;

/// <summary>Controlled input/observation contract, not a qualified live CLI profile or terminal gesture.</summary>
public sealed class GmCliInputProfile
{
    public string TerminalPresentation { get; set; } = "";
    public string DraftObservation { get; set; } = "";
    public string DraftDirectory { get; set; } = "";
    public string[] StartupBannerLines { get; set; } = [];
    public int AutomaticSubmissionLimit { get; set; }
    public string IdleMarker { get; set; } = "";
    public string PromptPrefix { get; set; } = "";
    public string WorkingMarker { get; set; } = "";
    public string PasteStart { get; set; } = "\u001b[200~";
    public string PasteEnd { get; set; } = "\u001b[201~";
    public string NewlineSequence { get; set; } = "\n";
    public string SubmitSequence { get; set; } = "\r";
    public string InterruptSequence { get; set; } = "\u0003";
    public string ExitSequence { get; set; } = "\u0004";
    public string[] BlockedMarkers { get; set; } = ["trust", "update", "authentication", "sign in"];
    public int ObservationTimeoutMilliseconds { get; set; } = 15000;
    public bool IsMini => TerminalPresentation == "synchronized-mini-v1" && DraftObservation == "external-editor-v1" &&
        !string.IsNullOrEmpty(DraftDirectory) && Path.IsPathFullyQualified(DraftDirectory) && AutomaticSubmissionLimit == 1 &&
        StartupBannerLines.Length is >0 and <=16 && StartupBannerLines.All(s=>s!=null && !s.Any(c=>c<' ')) &&
        PasteStart=="\u001b[200~" && PasteEnd=="\u001b[201~" && NewlineSequence=="\n" && SubmitSequence=="\r";
    public bool IsSupported => !string.IsNullOrEmpty(IdleMarker) && (IsMini || (TerminalPresentation=="" && !string.IsNullOrEmpty(PromptPrefix))) &&
        !string.IsNullOrEmpty(WorkingMarker) && !string.IsNullOrEmpty(PasteStart) && !string.IsNullOrEmpty(PasteEnd) &&
        !string.IsNullOrEmpty(NewlineSequence) && !string.IsNullOrEmpty(SubmitSequence) &&
        BlockedMarkers.Length > 0 && BlockedMarkers.All(m => !string.IsNullOrWhiteSpace(m));
    public GmCliInputProfile Snapshot() => new()
    {
        TerminalPresentation = TerminalPresentation ?? "",
        DraftObservation = DraftObservation ?? "", DraftDirectory = DraftDirectory ?? "",
        StartupBannerLines=(StartupBannerLines ?? []).ToArray(), AutomaticSubmissionLimit=AutomaticSubmissionLimit,
        IdleMarker = IdleMarker ?? "", PromptPrefix = PromptPrefix ?? "", WorkingMarker = WorkingMarker ?? "",
        PasteStart = PasteStart ?? "", PasteEnd = PasteEnd ?? "", NewlineSequence = NewlineSequence ?? "",
        SubmitSequence = SubmitSequence ?? "", InterruptSequence = InterruptSequence ?? "", ExitSequence = ExitSequence ?? "",
        BlockedMarkers = (BlockedMarkers ?? []).ToArray(), ObservationTimeoutMilliseconds = Math.Clamp(ObservationTimeoutMilliseconds, 100, 60000)
    };
}
