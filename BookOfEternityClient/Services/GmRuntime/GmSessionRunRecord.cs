using System.Text;

namespace BookOfEternityClient.Services.GmRuntime;

internal enum GmSessionRunBackend { Unspecified, WindowsJob, LinuxSupervisor }
internal enum GmSessionRunDisposition { Unspecified, Prepared, Running, Stopping, Uncertain, Stopped }
internal enum GmSessionRunStopKind { Unspecified, OwnedScopeEmpty, VerifiedHostReboot }

/// <summary>Immutable persistent-main identity; it describes one ownership slot, not every writer on the root.</summary>
internal sealed record GmSessionRunIdentity(string RootKey, string RunId, string GenerationId, long Epoch,
    GmSessionRunBackend Backend, string HostInstanceId, string BootId);

/// <summary>A trusted adapter observation; serialization does not authenticate stop or reboot.</summary>
internal sealed record GmSessionRunStopEvidence(GmSessionRunIdentity Identity, GmSessionRunStopKind Kind, string ObservedBootId);

/// <summary>Durable schema only. Persistence and canonical/process consumer integration are separate prerequisites.</summary>
internal sealed record GmSessionRunRecord(int SchemaVersion, GmSessionRunIdentity Identity,
    GmSessionRunDisposition Disposition, GmSessionRunStopEvidence? StopEvidence);

internal static class GmSessionRunValidation
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static InvalidDataException Invalid() => new("Persistent GM run record is invalid.");

    internal static void Validate(GmSessionRunRecord? record)
    {
        if (record is null || record.SchemaVersion != 1 ||
            !Enum.IsDefined(record.Disposition) || record.Disposition == GmSessionRunDisposition.Unspecified)
            throw Invalid();
        ValidateIdentity(record.Identity);
        if (record.Disposition != GmSessionRunDisposition.Stopped)
        {
            if (record.StopEvidence is not null) throw Invalid();
            return;
        }
        var evidence = record.StopEvidence;
        if (evidence is null) throw Invalid();
        ValidateIdentity(evidence.Identity);
        if (!IdentityMatches(record.Identity, evidence.Identity) || !IsBootId(evidence.ObservedBootId)) throw Invalid();
        var sameBoot = string.Equals(record.Identity.BootId, evidence.ObservedBootId, StringComparison.Ordinal);
        if (!(evidence.Kind == GmSessionRunStopKind.OwnedScopeEmpty && sameBoot ||
              evidence.Kind == GmSessionRunStopKind.VerifiedHostReboot && !sameBoot)) throw Invalid();
    }

    internal static void ValidateIdentity(GmSessionRunIdentity? identity)
    {
        if (identity is null || !IsRootKey(identity.RootKey) || !IsId(identity.RunId, allowEmpty: false) ||
            !IsId(identity.GenerationId, allowEmpty: true) || identity.Epoch < 1 || !IsBackend(identity.Backend) ||
            !IsId(identity.HostInstanceId, allowEmpty: false) || !IsBootId(identity.BootId)) throw Invalid();
    }

    internal static bool IsId(string? value, bool allowEmpty) =>
        Guid.TryParseExact(value, "N", out var parsed) && (allowEmpty || parsed != Guid.Empty) &&
        string.Equals(value, parsed.ToString("N"), StringComparison.Ordinal);

    internal static bool IsBackend(GmSessionRunBackend backend) =>
        backend is GmSessionRunBackend.WindowsJob or GmSessionRunBackend.LinuxSupervisor;

    internal static bool IsRootKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl)) return false;
        try { return StrictUtf8.GetByteCount(value) <= 4096; }
        catch (EncoderFallbackException) { return false; }
    }

    private static bool IsBootId(string? value) => value is { Length: >= 1 and <= 128 } &&
        value.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or ':' or '-');

    internal static bool RootMatches(string left, string right, GmSessionRunBackend trustedBackend) =>
        string.Equals(left, right, trustedBackend == GmSessionRunBackend.WindowsJob
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    // Admission compares validated absolute operands using the locally chosen
    // host backend. Stored identity payloads and exact file grants stay unchanged.
    internal static bool AdmissionRootMatches(string left,string right,GmSessionRunBackend trustedBackend)
    {
        try {
            if(!Path.IsPathFullyQualified(left) || !Path.IsPathFullyQualified(right))return false;
            var windows=trustedBackend==GmSessionRunBackend.WindowsJob;
            return RootMatches(Core.CanonicalRootIdentityInterner.NormalizeRootKey(Path.GetFullPath(left),windows),
                Core.CanonicalRootIdentityInterner.NormalizeRootKey(Path.GetFullPath(right),windows),trustedBackend);
        }
        catch(Exception failure) when(failure is ArgumentException or NotSupportedException or PathTooLongException or InvalidDataException)
        { return false; }
    }

    // Both identities are validated first. Never use a stored backend to compare against an unchecked target.
    internal static bool IdentityMatches(GmSessionRunIdentity left, GmSessionRunIdentity right) =>
        left.Backend == right.Backend && RootMatches(left.RootKey, right.RootKey, left.Backend) &&
        left.RunId == right.RunId && left.GenerationId == right.GenerationId && left.Epoch == right.Epoch &&
        left.HostInstanceId == right.HostInstanceId && left.BootId == right.BootId;
}

/// <summary>Pure transition plans. The caller must durably publish a plan under the future fence before acting on it.</summary>
internal static class GmSessionRunTransitions
{
    /// <summary>Plans Running after separately authenticated ownership attachment; this is not permission to release a writer.</summary>
    internal static GmSessionRunRecord MarkRunning(GmSessionRunRecord record, GmSessionRunIdentity owner)
    {
        RequireOwner(record, owner);
        if (record.Disposition != GmSessionRunDisposition.Prepared) throw IllegalTransition();
        return record with { Disposition = GmSessionRunDisposition.Running };
    }

    internal static GmSessionRunRecord MarkStopping(GmSessionRunRecord record, GmSessionRunIdentity owner)
    {
        RequireOwner(record, owner);
        if (record.Disposition is not (GmSessionRunDisposition.Prepared or GmSessionRunDisposition.Running))
            throw IllegalTransition();
        return record with { Disposition = GmSessionRunDisposition.Stopping };
    }

    internal static GmSessionRunRecord MarkUncertain(GmSessionRunRecord record)
    {
        GmSessionRunValidation.Validate(record);
        if (record.Disposition == GmSessionRunDisposition.Stopped) throw IllegalTransition();
        return record with { Disposition = GmSessionRunDisposition.Uncertain };
    }

    /// <summary>Plans a terminal record from exact trusted evidence, retaining its identity and observed boot.</summary>
    internal static GmSessionRunRecord ConfirmStopped(GmSessionRunRecord record, GmSessionRunStopEvidence evidence)
    {
        GmSessionRunValidation.Validate(record);
        var stopped = record with { Disposition = GmSessionRunDisposition.Stopped, StopEvidence = evidence };
        GmSessionRunValidation.Validate(stopped);
        if (record.Disposition == GmSessionRunDisposition.Stopped && record.StopEvidence != evidence) throw IllegalTransition();
        return stopped;
    }

    /// <summary>Cold decoding establishes no live owner; every nonterminal observation remains uncertain without writing the file.</summary>
    internal static GmSessionRunRecord InterpretCold(GmSessionRunRecord record)
    {
        GmSessionRunValidation.Validate(record);
        return record.Disposition == GmSessionRunDisposition.Stopped ? record : MarkUncertain(record);
    }

    private static void RequireOwner(GmSessionRunRecord record, GmSessionRunIdentity owner)
    {
        GmSessionRunValidation.Validate(record);
        GmSessionRunValidation.ValidateIdentity(owner);
        if (!GmSessionRunValidation.IdentityMatches(record.Identity, owner)) throw GmSessionRunValidation.Invalid();
    }

    private static InvalidOperationException IllegalTransition() => new("Persistent GM run transition is not allowed.");
}
