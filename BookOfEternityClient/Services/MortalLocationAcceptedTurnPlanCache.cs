using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class MortalLocationAcceptedTurnPlanCache
{
    private readonly object _gate = new();
    private readonly MortalLocationIdentityFactory _defaultIdentityFactory = new();
    private string? _fingerprint;
    private MortalLocationAcceptedTurnPlanningResult? _result;
    private MortalLocationIdentityFactory? _selectedIdentityFactory;

    /// <summary>
    /// Reuses a plan only for the same complete input and healthy identity factory.
    /// </summary>
    /// <param name="input">
    /// Complete detached accepted-turn location input.
    /// </param>
    /// <param name="identityFactory">
    /// Optional attempt factory; <see langword="null"/> selects the stable ordinary factory.
    /// </param>
    /// <returns>
    /// Accepted plan or its validation issues for the selected factory and input.
    /// </returns>
    internal MortalLocationAcceptedTurnPlanningResult GetOrBuild(
        MortalLocationAcceptedTurnInput input,
        MortalLocationIdentityFactory? identityFactory = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var selectedIdentityFactory = identityFactory ?? _defaultIdentityFactory;
        EnsureHealthy(selectedIdentityFactory);
        var fingerprint = CreateFingerprint(input);
        lock (_gate)
        {
            EnsureHealthy(selectedIdentityFactory);
            if (string.Equals(_fingerprint, fingerprint, StringComparison.Ordinal) &&
                _result != null &&
                ReferenceEquals(_selectedIdentityFactory, selectedIdentityFactory))
            {
                return _result;
            }

            var result = MortalLocationAcceptedTurnPlanner.Build(
                input,
                selectedIdentityFactory);
            EnsureHealthy(selectedIdentityFactory);
            _fingerprint = fingerprint;
            _result = result;
            _selectedIdentityFactory = selectedIdentityFactory;
            return result;
        }
    }

    /// <summary>
    /// Reads an existing successful plan only from its exact healthy original allocation factory.
    /// </summary>
    /// <param name="factory">
    /// Original capture's scoped factory; another factory cannot inherit the cached plan.
    /// </param>
    /// <param name="plan">
    /// Existing completed plan on success; null otherwise. The caller must detach it before transfer.
    /// </param>
    /// <returns>
    /// True only for a successful cached result selected by the supplied live factory.
    /// </returns>
    internal bool TryCaptureCompleted(MortalLocationIdentityFactory factory,
        out MortalLocationAcceptedTurnPlan plan)
    {
        lock (_gate)
        {
            plan = null!;
            if (!factory.IsHealthy || !ReferenceEquals(factory, _selectedIdentityFactory) ||
                _result is not { Success: true, Plan: { } completed })
                return false;
            plan = completed;
            return true;
        }
    }

    /// <summary>
    /// Rejects a revoked factory before either planning or returning cached authority.
    /// </summary>
    /// <param name="identityFactory">
    /// Selected factory whose attempt health is required.
    /// </param>
    private static void EnsureHealthy(MortalLocationIdentityFactory identityFactory)
    {
        if (!identityFactory.IsHealthy)
            throw new InvalidOperationException("The location allocation attempt is revoked.");
    }

    /// <summary>
    /// Hashes all planning inputs, including complete companion authority roots.
    /// </summary>
    /// <param name="input">
    /// Complete detached accepted-turn location input.
    /// </param>
    /// <returns>
    /// Stable comparison fingerprint for this exact planning input.
    /// </returns>
    private static string CreateFingerprint(MortalLocationAcceptedTurnInput input)
    {
        var scope = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["turn"] = input.Turn,
            ["preTurnWorldMap"] = input.PreTurnWorldMap.DeepClone(),
            ["preTurnCurrentLocation"] = input.PreTurnCurrentLocation?.DeepClone(),
            ["preTurnIdentityIndex"] = input.PreTurnIdentityIndex.DeepClone(),
            ["rawCurrentLocationData"] = input.RawCurrentLocationData?.DeepClone(),
            ["rawWorldMapUpdates"] = input.RawWorldMapUpdates?.DeepClone(),
            ["bootstrapScaffold"] = input.BootstrapScaffold?.DeepClone(),
            ["rawNpcCore"] = input.RawNpcCore?.DeepClone(),
            ["rawFactionCore"] = input.RawFactionCore?.DeepClone(),
            ["preTurnStorageContents"] = input.PreTurnStorageContents?.DeepClone(),
            ["rawCurrentItemCarrier"] = input.RawCurrentItemCarrier?.DeepClone(),
            ["companionAuthority"] = input.CompanionAuthority?.Fingerprint
        };
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(scope.ToJsonString())));
    }
}

internal static class MortalLocationAcceptedTurnPlanAuthority
{
    private static readonly ConditionalWeakTable<
        FileSystemManager,
        MortalLocationAcceptedTurnPlanCache> Caches = new();

    /// <summary>
    /// Extracts a completed location plan only through its live original capture and exact scoped factory.
    /// </summary>
    /// <param name="fileSystem">
    /// Exact filesystem retained by the capture and its location cache.
    /// </param>
    /// <param name="lease">
    /// Current canonical lease held by the original capture.
    /// </param>
    /// <param name="capture">
    /// Original owner authorizing the immutable publication transfer.
    /// </param>
    /// <param name="factory">
    /// Exact location allocation factory privately held by the original owner.
    /// </param>
    /// <param name="plan">
    /// Genuine completed location plan on success; null otherwise.
    /// </param>
    /// <returns>
    /// True only for the original owner and its healthy cached plan, without running the planner.
    /// </returns>
    internal static bool TryCaptureSpiritualCompleted(FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease,
        ValidationService.SpiritualOriginalTurnCapture capture, MortalLocationIdentityFactory factory,
        out MortalLocationAcceptedTurnPlan plan)
    {
        plan = null!;
        return capture.AuthorizesC4LocationSnapshot(fileSystem, lease, factory) &&
            Caches.TryGetValue(fileSystem, out var cache) && cache.TryCaptureCompleted(factory, out plan);
    }

    /// <summary>
    /// Uses the file system's shared planning cache for one selected location allocation attempt.
    /// </summary>
    /// <param name="fileSystem">
    /// File system whose accepted-turn cache owns the plan.
    /// </param>
    /// <param name="input">
    /// Complete detached accepted-turn location input.
    /// </param>
    /// <param name="identityFactory">
    /// Optional attempt factory; <see langword="null"/> uses the cache's ordinary factory.
    /// </param>
    /// <returns>
    /// Accepted plan or validation issues for the selected factory and input.
    /// </returns>
    internal static MortalLocationAcceptedTurnPlanningResult GetOrBuild(
        FileSystemManager fileSystem,
        MortalLocationAcceptedTurnInput input,
        MortalLocationIdentityFactory? identityFactory = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return Caches.GetValue(
            fileSystem,
            static _ => new MortalLocationAcceptedTurnPlanCache()).GetOrBuild(input, identityFactory);
    }
}
