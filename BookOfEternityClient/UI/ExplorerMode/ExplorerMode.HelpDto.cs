using BookOfEternityClient.CommandProtocol;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private Task ShowHelpDto()
    {
        var result = ExplorerHelpCommandResultBuilder.Build(new ExplorerHelpCommandContext
        {
            Command = "/help",
            Title = _loc.T("help"),
            IsChaosSea = _stateManager.CurrentState.IsInChaosSea,
            IsShiningAbode = _stateManager.CurrentState.IsInShiningAbode,
            IsPendingShiningAbodeBootstrap = _stateManager.CurrentState.IsInShiningAbodePendingBootstrap,
            CanReenterShiningAbode = _stateManager.CurrentState.CanReenterShiningAbode
        });

        ExplorerCommandResultConsoleRenderer.Render(_console, result);
        WaitForKey();
        return Task.CompletedTask;
    }
}
