namespace BookOfEternityClient.Configuration;

/// <summary>Controlled input/observation contract, not a qualified live CLI profile or terminal gesture.</summary>
public sealed class GmCliInputProfile
{
    public string TerminalPresentation { get; set; } = "";
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
    public bool IsSupported => !string.IsNullOrEmpty(IdleMarker) && !string.IsNullOrEmpty(PromptPrefix) &&
        !string.IsNullOrEmpty(WorkingMarker) && !string.IsNullOrEmpty(PasteStart) && !string.IsNullOrEmpty(PasteEnd) &&
        !string.IsNullOrEmpty(NewlineSequence) && !string.IsNullOrEmpty(SubmitSequence) &&
        BlockedMarkers.Length > 0 && BlockedMarkers.All(m => !string.IsNullOrWhiteSpace(m));
    public GmCliInputProfile Snapshot() => new()
    {
        TerminalPresentation = TerminalPresentation ?? "",
        IdleMarker = IdleMarker ?? "", PromptPrefix = PromptPrefix ?? "", WorkingMarker = WorkingMarker ?? "",
        PasteStart = PasteStart ?? "", PasteEnd = PasteEnd ?? "", NewlineSequence = NewlineSequence ?? "",
        SubmitSequence = SubmitSequence ?? "", InterruptSequence = InterruptSequence ?? "", ExitSequence = ExitSequence ?? "",
        BlockedMarkers = (BlockedMarkers ?? []).ToArray(), ObservationTimeoutMilliseconds = Math.Clamp(ObservationTimeoutMilliseconds, 100, 60000)
    };
}
