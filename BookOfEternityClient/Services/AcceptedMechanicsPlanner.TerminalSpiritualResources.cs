namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    internal sealed partial class ResourceExecutionSession
    {
        /// <summary>
        /// Gets the capture-issued preparation installed before this executor begins.
        /// Every use verifies the exact retained capture and executor identity.
        /// </summary>
        internal ValidationService.SpiritualOriginalTurnCapture.PreparedTerminal? TerminalPreparation { get; }
    }
}
