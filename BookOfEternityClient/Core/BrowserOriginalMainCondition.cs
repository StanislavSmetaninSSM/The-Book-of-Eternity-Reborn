using System.Text.Json.Serialization;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Core;

// An immutable refusal witness. Only the existing guard/pin grants admission.
internal sealed record BrowserOriginalMainCondition(string Kind, string RootKey, string Generation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GmSessionRunIdentity? ActiveIdentity = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GmSessionRunRecord? StoppedRecord = null)
{
    internal void Validate(string root, string generation)
    {
        var backend = OperatingSystem.IsWindows() ? GmSessionRunBackend.WindowsJob : GmSessionRunBackend.LinuxSupervisor;
        if(!GmSessionRunValidation.IsId(Generation, false) || Generation != generation ||
            !GmSessionRunValidation.AdmissionRootMatches(RootKey, root, backend)) throw Invalid();
        switch(Kind)
        {
            case "active":
                GmSessionRunValidation.ValidateIdentity(ActiveIdentity);
                if(StoppedRecord != null || ActiveIdentity!.Backend != backend || ActiveIdentity.GenerationId != Generation ||
                    !GmSessionRunValidation.AdmissionRootMatches(ActiveIdentity.RootKey, root, backend)) throw Invalid();
                break;
            case "quiescentAbsent":
                if(ActiveIdentity != null || StoppedRecord != null) throw Invalid();
                break;
            case "quiescentStopped":
                GmSessionRunValidation.Validate(StoppedRecord);
                if(ActiveIdentity != null || StoppedRecord!.Disposition != GmSessionRunDisposition.Stopped ||
                    StoppedRecord.Identity.Backend != backend ||
                    !GmSessionRunValidation.AdmissionRootMatches(StoppedRecord.Identity.RootKey, root, backend)) throw Invalid();
                break;
            default: throw Invalid();
        }
    }

    internal void RequireCurrent(FileSystemManager files)
    {
        Validate(files.BasePath, files.ObserveExistingHelperGeneration());
        var bytes = GmSessionRunPersistence.Read(files.BasePath);
        var current = bytes == null ? null : GmSessionRunRecordCodec.Decode(bytes);
        if(Kind == "active")
        {
            if(current?.Disposition != GmSessionRunDisposition.Running ||
                !GmSessionRunValidation.IdentityMatches(ActiveIdentity!, current.Identity)) throw Invalid();
        }
        else if(Kind == "quiescentAbsent" ? current != null : current != StoppedRecord) throw Invalid();
    }

    internal static InvalidDataException Invalid() => new("Original browser action, run or snapshot evidence changed; continuation refused.");
}
