using BookOfEternityClient.Services;

namespace BookOfEternityClient.CommandProtocol;

internal static class ExplorerRealmTradeCommandResolver
{
    public static ExplorerRealmTradeCommandResolution Resolve(string? currentRealm, string? arguments)
    {
        if (!RealmSemantics.HasResolvedRealm(currentRealm))
        {
            return new ExplorerRealmTradeCommandResolution(
                Success: false,
                Command: string.Empty,
                ErrorMessage: "Торговлю нельзя открыть, пока текущая реальность не определена.");
        }

        var command = RealmSemantics.IsChaosSea(currentRealm)
            ? "/guardian_trade"
            : RealmSemantics.IsShiningRealm(currentRealm)
                ? "/shining_trade"
                : "/npc_trade";
        var trimmedArguments = arguments?.Trim() ?? string.Empty;

        return new ExplorerRealmTradeCommandResolution(
            Success: true,
            Command: string.IsNullOrWhiteSpace(trimmedArguments)
                ? command
                : $"{command} {trimmedArguments}",
            ErrorMessage: string.Empty);
    }
}

internal sealed record ExplorerRealmTradeCommandResolution(
    bool Success,
    string Command,
    string ErrorMessage);
