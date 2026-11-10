using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

internal static class ExplorerElyaraCommandResultBuilder
{
    internal static ExplorerCommandResult Build(ElyaraVisitPlan plan) => new()
    {
        Command = ElyaraVisitService.Command,
        State = plan.Kind == ElyaraVisitKind.ChaosTravel ? CommandExecutionState.RequiresInput : CommandExecutionState.Completed,
        Blocks = [new UiPanelBlock
        {
            Title = ElyaraVisitService.Label,
            Blocks = new List<UiBlock> { new UiTextBlock { Text = plan.Message } }.Concat(
                string.IsNullOrWhiteSpace(plan.LocationName) ? Array.Empty<UiBlock>() :
                [new UiTextBlock { Text = "Текущее место: " + plan.LocationName }]).ToList()
        }],
        Prompts = plan.Kind != ElyaraVisitKind.ChaosTravel ? [] :
            [new UiConfirmationPrompt { Id = "confirm_elyara_visit", Required = true,
                Prompt = "Подтвердить переход к Элиаре и один цикл мира без лечения и оплаты?" }],
        ElyaraVisitCommandState = plan.State
    };
}
