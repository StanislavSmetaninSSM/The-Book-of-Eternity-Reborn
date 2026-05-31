namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowAfterlifeChroniclesAsync()
    {
        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Хроники посмертия", "Хроники посмертия доступны только в Море Хаоса и Сияющей Обители.");
            return;
        }

        var result = await ExplorerAfterlifeCombatCommandResultBuilder.TryBuildAsync(
            "/afterlife_chronicles",
            _stateManager,
            _fs);
        if (result == null)
        {
            ShowEmptyPanel("Хроники посмертия", "Команда хроник посмертия недоступна.");
            return;
        }

        Clear();
        ExplorerCommandResultConsoleRenderer.Render(_console, result);
        WaitForKey();
    }
}
