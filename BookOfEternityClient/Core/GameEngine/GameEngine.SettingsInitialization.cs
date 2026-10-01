namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Failure must escape the bound initializer, not only return from its lambda.
    private Task RequireInitialSettingsReadyAsync() => throw new NotImplementedException();
}
