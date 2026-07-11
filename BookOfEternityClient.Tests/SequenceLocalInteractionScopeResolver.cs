using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal sealed class SequenceLocalInteractionScopeResolver : ILocalInteractionScopeResolver
{
    private readonly IReadOnlyList<LocalInteractionScope> _scopes;
    private readonly Func<int, Task>? _beforeResolve;
    private int _nextIndex;

    public SequenceLocalInteractionScopeResolver(params LocalInteractionScope[] scopes)
    {
        if (scopes.Length == 0)
            throw new ArgumentException("At least one scope is required.", nameof(scopes));

        _scopes = scopes;
    }

    public SequenceLocalInteractionScopeResolver(
        Func<int, Task> beforeResolve,
        params LocalInteractionScope[] scopes)
        : this(scopes)
    {
        _beforeResolve = beforeResolve;
    }

    public int ResolveCallCount { get; private set; }

    public async Task<LocalInteractionScope> ResolveAsync(string? currentRealm = null)
    {
        ResolveCallCount += 1;
        if (_beforeResolve != null)
            await _beforeResolve(ResolveCallCount);

        var index = Math.Min(_nextIndex, _scopes.Count - 1);
        _nextIndex += 1;
        return _scopes[index];
    }
}
