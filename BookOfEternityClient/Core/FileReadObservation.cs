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
    /// <summary>Creates only the known diagnostic recheck arm, without adding authority occurrences.</summary>
    internal static FileReadObservation FromNarrativeRecheck(NarrativeRecheckReadObservation value) => new(value);
    /// <summary>Checks the private activity transport token without exporting it.</summary>
    internal static bool IsActivityIssuer(object? issuer) => ReferenceEquals(issuer, ActivityIssuer);

    /// <summary>Composes only known explicit source and passive DATA sinks; inactive/default calls retain their original object.</summary>
    /// <param name="dispatchIssuer">Private actual filesystem reader token.</param>
    /// <param name="files">Actual filesystem performing the read.</param>
    /// <param name="receiver">Existing optional sealed accumulator.</param>
    /// <param name="path">Original source spelling.</param>
    /// <param name="family">Concrete original reader family.</param>
    /// <param name="source">Existing closed N/DFR sink, or null.</param>
    /// <param name="owns">Whether this dispatch must settle its newly issued DATA token.</param>
    /// <returns>The original sink or its closed composition with actual passive observation.</returns>
    internal static FileReadObservation? AttachActivity(object dispatchIssuer, FileSystemManager files, CanonicalReadActivityAccumulator? receiver,
        string path, string family, FileReadObservation? source, out bool owns)
    {
        if (!files.IsCanonicalReadDispatchIssuer(dispatchIssuer))
            throw new InvalidOperationException("Actual filesystem reader dispatch required.");
        owns = false;
        var physicalReader = family is "physical-async" or "physical-sync" or "local-physical-async";
        if ((!physicalReader && source?._activity is not null) || receiver is null) return source;
        CanonicalReadActivityAccumulator.ReadObservation? activity = null;
        try
        {
            var origin = source?._narrative?.Origin ?? source?._recheck?.Origin;
            activity = physicalReader && source?._activity is { } parent
                ? parent.BeginChild(ActivityIssuer, files, path, family, origin)
                : receiver.BeginRead(ActivityIssuer, files, path, family, origin);
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
    /// <summary>Reports whether the actual dispatch has a registered passive read token.</summary>
    internal bool HasActivity => _activity is not null;
    /// <summary>Records an actual dispatch/return event; logical returns do not claim physical data.</summary>
    internal void Activity(CanonicalReadEventKind kind) => _activity?.Record(ActivityIssuer, kind,
        physical: kind is CanonicalReadEventKind.InitialOpened or CanonicalReadEventKind.ReaderReturned);
    /// <summary>Retains the actual error/cancellation before it unwinds the original reader.</summary>
    internal void ActivityFault(Exception error) => _activity?.Record(ActivityIssuer,
        error is OperationCanceledException ? CanonicalReadEventKind.Cancelled : CanonicalReadEventKind.Fault, physical: false, error: error);
    /// <summary>Settles the actual DATA token once, without closing a functional lease.</summary>
    internal void CompleteActivity() => _activity?.Complete(ActivityIssuer);

    internal void Snapshot(byte[]? bytes, bool physical = true)
    {
        _activity?.Record(ActivityIssuer, !physical ? CanonicalReadEventKind.NonPhysicalData :
            bytes is null ? CanonicalReadEventKind.PhysicalAbsence : CanonicalReadEventKind.PhysicalSnapshot, bytes, physical);
        _narrative?.Snapshot(bytes, physical); _debug?.Snapshot(bytes, physical);
    }
    internal void Presence(bool present) { _narrative?.Presence(present); _debug?.Presence(present); }
    internal void Fault(Exception error) { ActivityFault(error); _narrative?.Fault(error); _debug?.Fault(error); }
}
