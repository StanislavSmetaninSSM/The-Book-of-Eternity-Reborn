using BookOfEternityClient.CommandProtocol;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private Task ShowMathAssistantAsync()
    {
        var commandLine = string.IsNullOrWhiteSpace(_currentCommandRemainder)
            ? "/math"
            : "/math " + _currentCommandRemainder;
        var result = ExplorerMathCommandResultBuilder.Build(commandLine);
        ExplorerCommandResultConsoleRenderer.Render(_console, result);
        return Task.CompletedTask;
    }
}
