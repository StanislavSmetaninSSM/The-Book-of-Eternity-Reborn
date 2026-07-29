using BookOfEternityClient.Core;

namespace BookOfEternityClient.WebUi;

internal sealed class QteInteractionTokenAuthority
{
    internal const string SessionReplacedCode = "SessionReplaced";
    internal const string StaleInteractionCode = "StaleInteraction";

    private const int RetainedTokenLimit = 256;

    private readonly FileSystemManager _fs;
    private readonly Dictionary<string, Binding> _bindingsByToken =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Binding> _currentByKind =
        new(StringComparer.Ordinal);
    private readonly Queue<string> _issuedTokens = new();

    internal QteInteractionTokenAuthority(FileSystemManager fs)
    {
        _fs = fs;
    }

    internal string Publish(
        FileSystemManager.CanonicalWriteLease writeLease,
        string kind,
        string identity,
        string stateFingerprint)
    {
        var generation = _fs.GetOrCreateSessionGeneration(writeLease);
        if (_currentByKind.TryGetValue(kind, out var current) &&
            string.Equals(current.SessionGeneration, generation, StringComparison.Ordinal) &&
            string.Equals(current.Identity, identity, StringComparison.Ordinal) &&
            string.Equals(current.StateFingerprint, stateFingerprint, StringComparison.Ordinal))
        {
            return current.Token;
        }

        var revision = current != null &&
                       string.Equals(current.SessionGeneration, generation, StringComparison.Ordinal) &&
                       string.Equals(current.Identity, identity, StringComparison.Ordinal)
            ? checked(current.Revision + 1)
            : 1;
        var binding = new Binding(
            Guid.NewGuid().ToString("N"),
            generation,
            kind,
            identity,
            revision,
            stateFingerprint);
        _bindingsByToken[binding.Token] = binding;
        _currentByKind[kind] = binding;
        _issuedTokens.Enqueue(binding.Token);
        TrimRetainedTokens();
        return binding.Token;
    }

    internal Validation ValidatePresented(
        FileSystemManager.CanonicalWriteLease writeLease,
        string kind,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(token) ||
            !_bindingsByToken.TryGetValue(token.Trim(), out var binding) ||
            !string.Equals(binding.Kind, kind, StringComparison.Ordinal))
        {
            return Validation.Stale(
                "Страница взаимодействия устарела. Обновите её и повторите действие.");
        }

        var currentGeneration = _fs.GetOrCreateSessionGeneration(writeLease);
        if (!string.Equals(
                binding.SessionGeneration,
                currentGeneration,
                StringComparison.Ordinal))
        {
            return Validation.SessionReplaced(
                "Игровая сессия была заменена. Обновите страницу перед новым действием.");
        }

        if (!_currentByKind.TryGetValue(kind, out var current) ||
            !string.Equals(current.Token, binding.Token, StringComparison.Ordinal))
        {
            return Validation.Stale(
                "Состояние взаимодействия уже изменилось. Обновите страницу и повторите действие.");
        }

        return Validation.Valid(binding);
    }

    internal static Validation ValidateCurrentState(
        Validation presented,
        string identity,
        string stateFingerprint)
    {
        if (!presented.IsValid)
            return presented;

        var binding = presented.Binding!;
        return string.Equals(binding.Identity, identity, StringComparison.Ordinal) &&
               string.Equals(
                   binding.StateFingerprint,
                   stateFingerprint,
                   StringComparison.Ordinal)
            ? presented
            : Validation.Stale(
                "Состояние взаимодействия уже изменилось. Обновите страницу и повторите действие.");
    }

    private void TrimRetainedTokens()
    {
        while (_issuedTokens.Count > RetainedTokenLimit)
        {
            var token = _issuedTokens.Dequeue();
            if (_bindingsByToken.TryGetValue(token, out var binding) &&
                (!_currentByKind.TryGetValue(binding.Kind, out var current) ||
                 !string.Equals(current.Token, token, StringComparison.Ordinal)))
            {
                _bindingsByToken.Remove(token);
            }
        }
    }

    internal sealed record Binding(
        string Token,
        string SessionGeneration,
        string Kind,
        string Identity,
        long Revision,
        string StateFingerprint);

    internal sealed record Validation(
        bool IsValid,
        string? ErrorCode,
        string? Error,
        Binding? Binding)
    {
        internal static Validation Valid(Binding binding) =>
            new(true, null, null, binding);

        internal static Validation SessionReplaced(string error) =>
            new(false, SessionReplacedCode, error, null);

        internal static Validation Stale(string error) =>
            new(false, StaleInteractionCode, error, null);
    }
}
