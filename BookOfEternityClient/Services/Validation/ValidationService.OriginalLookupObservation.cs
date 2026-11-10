using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Closed transport for the existing nullable-original algorithm, not a path
    // registry, reader replacement or a source of original/publication authority.
    private sealed class OriginalLookupObservation
    {
        private readonly NarrativeInterfaceSourceSession? _narrative;
        private readonly DebugPrefixSourceSession? _debug;
        private OriginalLookupObservation(NarrativeInterfaceSourceSession value) => _narrative = value;
        private OriginalLookupObservation(DebugPrefixSourceSession value) => _debug = value;
        public static implicit operator OriginalLookupObservation?(NarrativeInterfaceSourceSession? value) => value is null ? null : new(value);
        public static implicit operator OriginalLookupObservation?(DebugPrefixSourceSession? value) => value is null ? null : new(value);
        internal FileSystemManager.CanonicalWriteLease? Lease => _debug?.Lease;
        internal FileReadObservation BeginRead(string role, string path, string route) => _narrative is { } narrative
            ? FileReadObservation.FromNarrative(narrative.BeginRead(role, path, route)) : FileReadObservation.FromDebugPrefix(_debug!.BeginRead(role, path, route));
        internal Task<string?> ReadTextAsync(string role, string path, string route, FileSystemManager.CanonicalWriteLease? lease) =>
            _narrative is { } narrative ? narrative.ReadTextAsync(role, path, route, lease) : _debug!.ReadAuxiliaryTextAsync(role, path, route, lease);
        internal Task<byte[]?> ReadBytesAsync(string role, string path, string route, FileSystemManager.CanonicalWriteLease? lease) =>
            _narrative is { } narrative ? narrative.ReadBytesAsync(role, path, route, lease) : _debug!.ReadAuxiliaryBytesAsync(role, path, route, lease);
        internal string? ReadTextSync(string role, string path, string route) =>
            _narrative is { } narrative ? narrative.ReadTextSync(role, path, route) : throw new InvalidDataException("debug_prefix_requires_supplied_lease");
        internal byte[]? ReadBytesSync(string role, string path, string route) =>
            _narrative is { } narrative ? narrative.ReadBytesSync(role, path, route) : throw new InvalidDataException("debug_prefix_requires_supplied_lease");
        internal Task<string?> ReadOriginalTextAsync(ValidationService validator, string role) => _narrative is not null
            ? validator.ReadValidatedCurrentPreTurnTrackedFileCoreAsync(role, null, this) : _debug!.ReadOriginalDebugAsync();
        internal Task<string?> ReadCurrentTextAsync(string role) => _narrative is { } narrative
            ? narrative.ReadTextAsync(role, role, "public-current") : _debug!.ReadDebugForFreshnessAsync();
        internal void Policy(string role, string route, string state)
        {
            if (_narrative is { } narrative) narrative.Policy(role, route, state); else _debug!.Policy(role, route, state);
        }
        internal void OriginalContext(string session, string request, string token, int turn, string provenance, string data)
        {
            if (_narrative is { } narrative) narrative.OriginalContext(session, request, token, turn, provenance, data);
            else _debug!.OriginalContext(session, request, token, turn, provenance, data);
        }
        internal void ScopedLookup()
        {
            _narrative?.Unavailable("narrative_policy_scoped_lookup_provenance_unavailable");
            _debug?.RequireOriginalJoin();
        }
        internal void Unavailable(string reason) { _narrative?.Unavailable(reason); _debug?.Unavailable(reason); }
        internal void Fault(Exception error) { _narrative?.Fault(error); _debug?.Fault(error); }
    }
}
