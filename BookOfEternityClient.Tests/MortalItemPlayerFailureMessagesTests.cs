using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalItemPlayerFailureMessagesTests
{
    [Theory]
    [InlineData("Receipt предмета itm_private расходится с индексом идентичности.")]
    [InlineData("game_state/inventory/items.json.equippedItems.MainHand не разрешается в receipt-bearing itemId itm_private.")]
    [InlineData("materialization carrier transitionId=tr_private отклонён.")]
    [InlineData("Не удалось записать E:\\Games\\session\\game_state\\inventory\\items.json: доступ запрещён.")]
    public void Sanitize_InternalMortalItemDiagnosticsReturnsPlayerSafeStateMessage(string diagnostic)
    {
        var message = MortalItemPlayerFailureMessages.Sanitize(diagnostic);

        Assert.Equal(MortalItemPlayerFailureMessages.StateRequiresRepair, message);
        Assert.DoesNotContain("private", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_PlayerFacingFailureIsPreserved()
    {
        const string message = "Экипированный предмет нельзя продать из этой панели.";

        Assert.Equal(message, MortalItemPlayerFailureMessages.Sanitize(message));
    }
}
