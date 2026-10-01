using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Failure must escape the bound initializer, not only return from its lambda.
    private async Task RequireInitialSettingsReadyAsync()
    {
        if (await WriteGameSettingsForGm()) return;
        _logger.LogWarning("Initial turn dispatch stopped because settings synchronization requires follow-up.");
        // WriteGameSettingsForGm has already reported the specific persisted,
        // rolled-back or uncertain outcome. This failure does not undo it.
        throw new InvalidDataException(
            "Настройки ещё не готовы для первого хода. Сначала завершите их проверку, затем повторите запуск.");
    }
}
