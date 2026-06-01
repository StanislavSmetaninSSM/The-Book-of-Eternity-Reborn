using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private void RecordConsoleObservation(
        ConsoleE2EInputMode inputMode,
        string screenTitle,
        string playerFacingText,
        IReadOnlyList<string> options,
        string? selectedOption,
        string slug,
        string? logPath = null)
    {
        if (_inputSource is ConsoleE2EScriptedInputSource scriptedInput)
        {
            scriptedInput.WriteObservation(
                inputMode,
                screenTitle,
                playerFacingText,
                options,
                selectedOption,
                slug,
                logPath);
        }

        if (_inputSource is not AgentConsoleLiveInputSource liveInput)
            return;

        var observation = new ConsoleE2EObservationSnapshot(
            RunId: "agent-console",
            StepIndex: 0,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            InputMode: inputMode,
            ScreenTitle: screenTitle,
            PlayerFacingText: playerFacingText,
            Options: options,
            SelectedOption: selectedOption,
            ArtifactRoot: string.Empty,
            LogPath: null);
        var snapshot = AgentConsoleE2EObservationMapper.ToAgentConsoleSnapshot(observation, screenId: slug);
        liveInput.PublishSnapshot(snapshot, $"Rendered {slug}.");
    }
}
