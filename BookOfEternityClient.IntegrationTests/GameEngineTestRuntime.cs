using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

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
/// <param name="AudioService">
/// The actual audio service supplied to the engine and QTE service; its owner disposes it after use.
/// </param>
/// <param name="ValidationService">
/// The actual validation service supplied to the engine and QTE service.
/// </param>
/// <param name="QteSceneService">
/// The actual QTE service supplied to the engine, including its retained constructor input.
/// </param>
/// <param name="FinalizationHooks">
/// The hooks configured during construction, or <see langword="null"/> when no hooks were requested.
/// </param>
internal sealed record GameEngineTestRuntime(
    GameEngine Engine,
    StateManager StateManager,
    GameLoop GameLoop,
    AudioService AudioService,
    ValidationService ValidationService,
    QteSceneService QteSceneService,
    GameEngineSessionFinalizationHooks? FinalizationHooks);
