using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Holds the shared runtime objects constructed for a lifecycle test engine.
/// </summary>
/// <param name="Engine">
/// The engine using the shared state manager and game loop.
/// </param>
/// <param name="StateManager">
/// The state manager supplied to the engine.
/// </param>
/// <param name="GameLoop">
/// The game loop supplied to the engine.
/// </param>
internal sealed record GameEngineTestRuntime(
    GameEngine Engine,
    StateManager StateManager,
    GameLoop GameLoop);
