using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Runtime.CompilerServices;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal delegate AcceptedMechanicsPlanningResult AcceptedMechanicsPlanFactory(
    AcceptedMechanicsInput input,
    string inputFingerprint);

internal sealed class AcceptedMechanicsPlanCache
{
    private readonly object _gate = new();
    private readonly AcceptedMechanicsPlanFactory _planner;
    private string? _inputFingerprint;
    private AcceptedMechanicsPlanningResult? _planningResult;
    private string? _validatedBindingFingerprint;
    private AcceptedMechanicsPlanBinding? _validatedBinding;
    private AcceptedMechanicsPlanningResult? _validatedResult;

    internal AcceptedMechanicsPlanCache(AcceptedMechanicsPlanFactory planner) =>
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));

    internal bool HasValidated
    {
        get
        {
            lock (_gate)
                return _validatedResult != null;
        }
    }

    internal AcceptedMechanicsPlanningResult GetOrBuildValidated(
        AcceptedMechanicsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock (_gate)
        {
            InvalidateValidatedCore();
            if (input.ValidationIssues.Count > 0)
            {
                return new AcceptedMechanicsPlanningResult(
                    null,
                    input.ValidationIssues.ToArray());
            }

            var binding = input.CreateBinding();
            var fingerprint = CreateFingerprint(binding);
            AcceptedMechanicsPlanningResult result;
            if (_planningResult != null &&
                string.Equals(_inputFingerprint, fingerprint, StringComparison.Ordinal))
            {
                result = _planningResult;
            }
            else
            {
                result = _planner(input, fingerprint) ??
                    throw new InvalidOperationException("Accepted mechanics planner returned null.");
                result = ValidatePlannerResult(input, fingerprint, result);
                _inputFingerprint = fingerprint;
                _planningResult = result;
            }

            if (!result.Success)
            {
                InvalidateValidatedCore();
                return result;
            }

            _validatedBindingFingerprint = fingerprint;
            _validatedBinding = input.CreateBinding();
            _validatedResult = result;
            return result;
        }
    }

    internal bool TryTakeValidated(
        AcceptedMechanicsPlanBinding liveBinding,
        out AcceptedMechanicsPlanningResult result)
    {
        ArgumentNullException.ThrowIfNull(liveBinding);
        var fingerprint = CreateFingerprint(liveBinding);
        lock (_gate)
        {
            if (_validatedResult != null &&
                string.Equals(
                    _validatedBindingFingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
            {
                result = _validatedResult;
                InvalidateValidatedCore();
                return true;
            }
            InvalidateValidatedCore();
        }

        result = null!;
        return false;
    }

    internal void InvalidateValidated()
    {
        lock (_gate)
            InvalidateValidatedCore();
    }

    internal bool TryPeekValidated(
        out AcceptedMechanicsPlanBinding binding,
        out AcceptedMechanicsPlanningResult result)
    {
        lock (_gate)
        {
            if (_validatedBinding != null && _validatedResult != null)
            {
                binding = new AcceptedMechanicsPlanBinding(
                    _validatedBinding.SessionId,
                    _validatedBinding.RequestId,
                    _validatedBinding.SnapshotToken,
                    _validatedBinding.Realm,
                    _validatedBinding.Turn,
                    _validatedBinding.AcceptedEvents,
                    _validatedBinding.ResourceCommands,
                    _validatedBinding.EffectCommands,
                    _validatedBinding.PendingInput,
                    _validatedBinding.InternalInputs,
                    _validatedBinding.AuthorityFingerprints,
                    _validatedBinding.BeforeImages);
                result = _validatedResult;
                return true;
            }
        }
        binding = null!;
        result = null!;
        return false;
    }

    private void InvalidateValidatedCore()
    {
        _validatedBindingFingerprint = null;
        _validatedBinding = null;
        _validatedResult = null;
    }

    private static AcceptedMechanicsPlanningResult ValidatePlannerResult(
        AcceptedMechanicsInput input,
        string fingerprint,
        AcceptedMechanicsPlanningResult result)
    {
        if (!result.Success)
        {
            if (result.Plan != null)
                return Failed("accepted_mechanics_partial_plan", "failed plan with no after-images");
            return result.Issues.Count == 0
                ? Failed("accepted_mechanics_plan_missing", "complete plan or bounded validation issues")
                : result;
        }

        var plan = result.Plan!;
        if (!string.Equals(plan.InputFingerprint, fingerprint, StringComparison.Ordinal))
            return Failed("accepted_mechanics_plan_fingerprint_mismatch", fingerprint);
        if (plan.AuthorityFingerprints != input.AuthorityFingerprints)
            return Failed("accepted_mechanics_authority_fingerprint_mismatch", "exact input authority fingerprints");
        if (!BeforeImagesEqual(plan.BeforeImages, input.BeforeImages))
            return Failed("accepted_mechanics_before_image_mismatch", "exact input before-images");
        return result;
    }

    private static bool BeforeImagesEqual(
        IReadOnlyDictionary<string, CanonicalBeforeImage> left,
        IReadOnlyDictionary<string, CanonicalBeforeImage> right)
    {
        if (left.Count != right.Count)
            return false;
        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) ||
                pair.Value.Existed != value.Existed ||
                !BytesEqual(pair.Value.Bytes, value.Bytes))
            {
                return false;
            }
        }
        return true;
    }

    private static bool BytesEqual(byte[]? left, byte[]? right) =>
        left == null || right == null
            ? left == null && right == null
            : left.AsSpan().SequenceEqual(right);

    private static AcceptedMechanicsPlanningResult Failed(string code, string expected) =>
        new(
            null,
            new[]
            {
                new ValidationIssue(
                    "acceptedMechanicsPlan",
                    IssueSeverity.Error,
                    "Accepted mechanics planner returned an invalid publication plan.",
                    code: code,
                    section: "accepted_mechanics",
                    expected: expected,
                    actual: "planner result",
                    repairHint: "Discard the plan and rebuild once from the complete validated accepted-turn input.")
            });

    private static string CreateFingerprint(AcceptedMechanicsPlanBinding binding)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["sessionId"] = binding.SessionId,
            ["requestId"] = binding.RequestId,
            ["snapshotToken"] = binding.SnapshotToken,
            ["realm"] = binding.Realm,
            ["turn"] = binding.Turn,
            ["acceptedEvents"] = Canonicalize(binding.AcceptedEvents),
            ["resourceCommands"] = Canonicalize(binding.ResourceCommands),
            ["effectCommands"] = Canonicalize(binding.EffectCommands),
            ["pendingInput"] = Canonicalize(binding.PendingInput),
            ["internalInputs"] = Canonicalize(binding.InternalInputs),
            ["authorityFingerprints"] = new JsonObject(
                binding.AuthorityFingerprints.Enumerate()
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(static pair =>
                        KeyValuePair.Create<string, JsonNode?>(pair.Key, pair.Value))),
            ["beforeImages"] = new JsonArray(binding.BeforeImages
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => (JsonNode)new JsonObject
                {
                    ["path"] = pair.Key,
                    ["existed"] = pair.Value.Existed,
                    ["bytes"] = pair.Value.Bytes == null
                        ? null
                        : Convert.ToBase64String(pair.Value.Bytes)
                }).ToArray())
        };
        return "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())))
            .ToLowerInvariant();
    }

    private static JsonNode? Canonicalize(JsonNode? node)
    {
        if (node is JsonObject valueObject)
        {
            var result = new JsonObject();
            foreach (var pair in valueObject.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                result.Add(pair.Key, Canonicalize(pair.Value));
            return result;
        }
        if (node is JsonArray valueArray)
            return new JsonArray(valueArray.Select(Canonicalize).ToArray());
        return node?.DeepClone();
    }
}

internal static class AcceptedMechanicsPlanAuthority
{
    private static readonly ConditionalWeakTable<FileSystemManager, AcceptedMechanicsPlanCache>
        Caches = new();

    internal static AcceptedMechanicsPlanningResult GetOrBuildValidated(
        FileSystemManager fileSystem,
        AcceptedMechanicsInput input)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(input);
        return Cache(fileSystem).GetOrBuildValidated(input);
    }

    internal static bool TryTakeValidated(
        FileSystemManager fileSystem,
        AcceptedMechanicsPlanBinding liveBinding,
        out AcceptedMechanicsPlanningResult result)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(liveBinding);
        return Cache(fileSystem).TryTakeValidated(liveBinding, out result);
    }

    internal static void InvalidateValidated(FileSystemManager fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        Cache(fileSystem).InvalidateValidated();
    }

    internal static bool HasValidated(FileSystemManager fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return Cache(fileSystem).HasValidated;
    }

    internal static bool TryPeekValidated(
        FileSystemManager fileSystem,
        out AcceptedMechanicsPlanBinding binding,
        out AcceptedMechanicsPlanningResult result)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return Cache(fileSystem).TryPeekValidated(out binding, out result);
    }

    private static AcceptedMechanicsPlanCache Cache(FileSystemManager fileSystem) =>
        Caches.GetValue(
            fileSystem,
            static _ => new AcceptedMechanicsPlanCache(
                AcceptedMechanicsPlanner.BuildAcceptedPlan));
}
