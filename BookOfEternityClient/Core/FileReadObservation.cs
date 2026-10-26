using NarrativeReadObservation = BookOfEternityClient.Services.ValidationService.NarrativeInterfaceSourceSession.NarrativeReadObservation;
using NarrativeRecheckReadObservation = BookOfEternityClient.Services.ValidationService.NarrativeInterfaceSourceSession.RecheckReadObservation;
using DebugPrefixReadObservation = BookOfEternityClient.Services.ValidationService.DebugPrefixSourceSession.DebugPrefixReadObservation;

namespace BookOfEternityClient.Core;

/// <summary>Closed physical-event transport; family roles and authority remain in their sealed owners.</summary>
internal sealed class FileReadObservation
{
    private static readonly object ActivityIssuer = new();
    private readonly CanonicalReadActivityAccumulator.ReadObservation? _activity;
    private readonly NarrativeRecheckReadObservation? _recheck;
    private readonly NarrativeReadObservation? _narrative;
    private readonly DebugPrefixReadObservation? _debug;
    private FileReadObservation(NarrativeReadObservation value) => _narrative = value;
    private FileReadObservation(DebugPrefixReadObservation value) => _debug = value;
    public static implicit operator FileReadObservation?(NarrativeReadObservation? value) => value is null ? null : new(value);
    public static implicit operator FileReadObservation?(DebugPrefixReadObservation? value) => value is null ? null : new(value);
    internal static FileReadObservation FromNarrative(NarrativeReadObservation value) => new(value);
    internal static FileReadObservation FromDebugPrefix(DebugPrefixReadObservation value) => new(value);
    private FileReadObservation(NarrativeRecheckReadObservation value) => _recheck = value;
    private FileReadObservation(FileReadObservation? source, CanonicalReadActivityAccumulator.ReadObservation activity)
    { _narrative = source?._narrative; _debug = source?._debug; _recheck = source?._recheck; _activity = activity; }
    internal static FileReadObservation FromNarrativeRecheck(NarrativeRecheckReadObservation value) => new(value);
    internal static bool IsActivityIssuer(object? issuer) => ReferenceEquals(issuer, ActivityIssuer);

    /// <summary>Composes only known explicit source and passive DATA sinks; inactive/default calls retain their original object.</summary>
    internal static FileReadObservation? AttachActivity(object dispatchIssuer, FileSystemManager files, CanonicalReadActivityAccumulator? receiver,
        string path, string family, FileReadObservation? source, out bool owns)
    {
        if (!files.IsCanonicalReadDispatchIssuer(dispatchIssuer))
            throw new InvalidOperationException("Actual filesystem reader dispatch required.");
        owns = false;
        if (source?._activity is not null || receiver is null) return source;
        CanonicalReadActivityAccumulator.ReadObservation? activity = null;
        try
        {
            activity = receiver.BeginRead(ActivityIssuer, files, path, family, source?._narrative?.Origin ?? source?._recheck?.Origin);
            if (activity is null) return source;
            var attached = new FileReadObservation(source, activity);
            owns = true;
            return attached;
        }
        catch (Exception)
        {
            receiver.FailObservation();
            activity?.Complete(ActivityIssuer);
            return source;
        }
    }
    internal void Activity(CanonicalReadEventKind kind) => _activity?.Record(ActivityIssuer, kind);
    internal void ActivityFault(Exception error) => _activity?.Record(ActivityIssuer,
        error is OperationCanceledException ? CanonicalReadEventKind.Cancelled : CanonicalReadEventKind.Fault, error: error);
    internal void CompleteActivity() => _activity?.Complete(ActivityIssuer);

    internal void Snapshot(byte[]? bytes, bool physical = true)
    {
        _narrative?.Snapshot(bytes, physical); _debug?.Snapshot(bytes, physical);
        _activity?.Record(ActivityIssuer, !physical ? CanonicalReadEventKind.CachedData :
            bytes is null ? CanonicalReadEventKind.PhysicalAbsence : CanonicalReadEventKind.PhysicalSnapshot, bytes, physical);
    }
    internal void Presence(bool present) { _narrative?.Presence(present); _debug?.Presence(present); }
    internal void Fault(Exception error) { _narrative?.Fault(error); _debug?.Fault(error); ActivityFault(error); }
}
