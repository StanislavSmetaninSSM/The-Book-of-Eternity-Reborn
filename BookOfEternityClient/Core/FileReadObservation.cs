using NarrativeReadObservation = BookOfEternityClient.Services.ValidationService.NarrativeInterfaceSourceSession.NarrativeReadObservation;
using DebugPrefixReadObservation = BookOfEternityClient.Services.ValidationService.DebugPrefixSourceSession.DebugPrefixReadObservation;

namespace BookOfEternityClient.Core;

/// <summary>Closed physical-event transport; family roles and authority remain in their sealed owners.</summary>
internal sealed class FileReadObservation
{
    private readonly NarrativeReadObservation? _narrative;
    private readonly DebugPrefixReadObservation? _debug;
    private FileReadObservation(NarrativeReadObservation value) => _narrative = value;
    private FileReadObservation(DebugPrefixReadObservation value) => _debug = value;
    public static implicit operator FileReadObservation?(NarrativeReadObservation? value) => value is null ? null : new(value);
    public static implicit operator FileReadObservation?(DebugPrefixReadObservation? value) => value is null ? null : new(value);
    internal static FileReadObservation FromNarrative(NarrativeReadObservation value) => new(value);
    internal static FileReadObservation FromDebugPrefix(DebugPrefixReadObservation value) => new(value);
    internal void Snapshot(byte[]? bytes, bool physical = true)
    {
        _narrative?.Snapshot(bytes, physical); _debug?.Snapshot(bytes, physical);
    }
    internal void Presence(bool present) { _narrative?.Presence(present); _debug?.Presence(present); }
    internal void Fault(Exception error) { _narrative?.Fault(error); _debug?.Fault(error); }
}
