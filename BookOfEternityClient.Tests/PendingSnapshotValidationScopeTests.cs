using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises physical pending-snapshot authentication through the real validator lookups.
/// </summary>
/// <param name="output">
/// Destination for physical read counts in the test report.
/// </param>
public sealed class PendingSnapshotValidationScopeTests(ITestOutputHelper output)
{
    /// <summary>
    /// Repeated sync and async consumers must not multiply full rollback authentication.
    /// </summary>
    /// <param name="consumers">
    /// Number of alternating synchronous and asynchronous lookup consumers.
    /// </param>
    /// <param name="cold">
    /// Whether admission must authenticate the console evidence cohort for the first time.
    /// </param>
    [Theory]
    [InlineData(1, false)] [InlineData(8, false)] [InlineData(32, false)]
    [InlineData(1, true)] [InlineData(8, true)] [InlineData(32, true)]
    public async Task Scope_ReadBudgetIsIndependentOfConsumers(int consumers, bool cold)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(cold);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var issues = await fixture.RunAsync(async () =>
        {
            for (var i = 0; i < consumers; i++)
                Assert.Equal("Usable", Status(await fixture.LookupAsync(i % 2 != 0)));
            return [new("first", IssueSeverity.Error, "one"), new("second", IssueSeverity.Error, "two")];
        });
        output.WriteLine($"N={consumers}; cold={cold}; ordinary={fixture.Ordinary}; cohort={fixture.Cohort}; total={fixture.Ordinary + fixture.Cohort}");
        Assert.Equal(new[] { "first", "second" }, issues.Select(i => i.FilePath));
        Assert.InRange(fixture.Ordinary, 0, cold ? 9 : 6);
        Assert.InRange(fixture.Ordinary + fixture.Cohort, 1, cold ? 12 : 9);
    }

    /// <summary>
    /// Durable changes to entry dependencies cannot publish a previously valid result.
    /// </summary>
    /// <param name="change">
    /// The exact dependency change performed after a usable lookup.
    /// </param>
    [Theory]
    [InlineData("overwrite")] [InlineData("delete")] [InlineData("bom")]
    [InlineData("same-mtime")] [InlineData("manifest")] [InlineData("authority")]
    [InlineData("request")] [InlineData("session")] [InlineData("turn")]
    [InlineData("alternative")] [InlineData("generation")] [InlineData("valid-pair")]
    public async Task Scope_DependencyChangeBeforeFinalProofRefuses(string change)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var mutated = false;
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.RunAsync(async () =>
        {
            Assert.Equal("Usable", Status(await fixture.LookupAsync(false)));
            await fixture.ChangeAsync(change);
            if (change == "valid-pair")
                Assert.Equal("Usable", Status(await fixture.LookupAsync(false,
                    new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance))));
            mutated = true;
            return [];
        }));
        Assert.True(mutated, "The test must reach the dependency change rather than pass on fixture admission failure.");
    }

    /// <summary>
    /// Consumer mutation of a returned DTO cannot change subsequent lookup evidence.
    /// </summary>
    [Fact]
    public async Task Scope_LookupsReturnIndependentManifestCopies()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await fixture.RunAsync(async () =>
        {
            var first = Manifest(await fixture.LookupAsync(false));
            first.GetType().GetProperty("SessionId")!.SetValue(first, "changed");
            var second = Manifest(await fixture.LookupAsync(true));
            Assert.NotSame(first, second);
            Assert.Equal("session", second.GetType().GetProperty("SessionId")!.GetValue(second));
            return [];
        });
    }

    /// <summary>
    /// Reads the status from the actual private lookup result without duplicating its algorithm.
    /// </summary>
    /// <param name="lookup">
    /// Result returned by the validator.
    /// </param>
    /// <returns>
    /// The validator's status name.
    /// </returns>
    private static string Status(object lookup) => lookup.GetType().GetProperty("Status")!.GetValue(lookup)!.ToString()!;

    /// <summary>
    /// Obtains the manifest owned by one lookup result.
    /// </summary>
    /// <param name="lookup">
    /// A usable lookup result.
    /// </param>
    /// <returns>
    /// The lookup's mutable consumer DTO.
    /// </returns>
    private static object Manifest(object lookup) => lookup.GetType().GetProperty("Manifest")!.GetValue(lookup)!;

    /// <summary>
    /// Nested writes and concurrent validator entry must refuse while evidence is reserved.
    /// </summary>
    [Fact]
    public async Task Scope_NestedMutationAndConcurrentEntryRefuse()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await fixture.RunAsync(async () =>
        {
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.Files.WriteFileAtomicAsync("game_state/world/scope0.json", "{}"));
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.RunAsync(() => Task.FromResult(new List<ValidationIssue>())));
            return [];
        });
        // Successful teardown makes the original owner's ordinary writer available again.
        await fixture.Files.WriteFileAtomicAsync("game_state/world/scope0.json", "{}");
    }

    /// <summary>
    /// A copied asynchronous context cannot reuse evidence after the invocation has closed.
    /// </summary>
    [Fact]
    public async Task Scope_CopiedContextAfterCloseRefuses()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<object>? copied = null;
        await fixture.RunAsync(() =>
        {
            copied = Task.Run(async () => { await release.Task; return await fixture.LookupAsync(true); });
            return Task.FromResult(new List<ValidationIssue>());
        });
        release.SetResult();
        await Assert.ThrowsAnyAsync<Exception>(async () => { await copied!; });
    }

    /// <summary>
    /// Missing reads finish under caller ambient ownership and current files remain physical.
    /// </summary>
    [Fact]
    public async Task Scope_MissingAndCurrentReadsStayPhysical()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await fixture.RunAsync(async () =>
        {
            Assert.Null(await fixture.Files.ReadFileAsync("game_state/world/scope-missing.json").WaitAsync(TimeSpan.FromSeconds(3)));
            const string path = "game_state/world/scope0.json";
            var before = await fixture.Files.ReadFileAsync(path);
            File.WriteAllText(fixture.Files.ResolvePath(path), "{\"value\":2}");
            Assert.NotEqual(before, await fixture.Files.ReadFileAsync(path));
            return [];
        });
    }

    /// <summary>
    /// A consumer exception keeps its identity and releases ownership for a later operation.
    /// </summary>
    [Fact]
    public async Task Scope_PrimaryFailureReleasesInvocation()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var primary = new OperationCanceledException("consumer cancellation");
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => fixture.RunAsync(() => Task.FromException<List<ValidationIssue>>(primary)));
        Assert.Same(primary, failure is TargetInvocationException wrapper ? wrapper.InnerException : failure);
        await fixture.RunAsync(() => Task.FromResult(new List<ValidationIssue>()));
    }

    /// <summary>
    /// Same-lease mutation, override replacement and owning lifecycle close cannot extend read authority.
    /// </summary>
    /// <param name="disposeOwner">
    /// True attempts direct owner disposal; false starts the ordinary lifecycle close.
    /// </param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Scope_SameLeaseOverrideAndLifecycleMutationRefuse(bool disposeOwner)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await fixture.RunAsync(async () =>
        {
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.Files.WriteFileAtomicAsync(fixture.CurrentLease, "game_state/world/scope0.json", "{}"));
            Assert.ThrowsAny<Exception>(() => fixture.Validator.UsePrevalidatedPendingTurnSnapshotScope(null));
            if (disposeOwner) Assert.ThrowsAny<Exception>(() => owner.Dispose());
            else Assert.ThrowsAny<Exception>(() => fixture.Files.BeginMainOperationClosing());
            return [];
        });
    }

    /// <summary>
    /// A second manager and a paused acquisition cannot borrow mutation authority from a reserved owner.
    /// </summary>
    [Fact]
    public async Task Scope_PausedBorrowAndSecondManagerCannotMutate()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.OnBorrow = () => { reached.TrySetResult(); return release.Task; };
        var validation = fixture.RunAsync(() => Task.FromResult(new List<ValidationIssue>()));
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var other = new FileSystemManager(fixture.Files.BasePath, NullLogger<FileSystemManager>.Instance);
            await Assert.ThrowsAnyAsync<Exception>(() => other.WriteFileAtomicAsync("game_state/world/scope0.json", "{}"));
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.Validator.ValidateGameStateAsync());
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            Task contender;
            using (ExecutionContext.SuppressFlow())
                contender = Task.Run(async () => { await using var lease = await other.AcquireCanonicalWriteLeaseAsync(cancellationToken: cancellation.Token); });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => contender);
        }
        finally { release.TrySetResult(); fixture.OnBorrow = null; }
        await validation;
    }

    /// <summary>
    /// A final physical read fault and lease-close failure never become a successful issue result.
    /// </summary>
    /// <param name="failRead">
    /// True injects the primary error in final authentication; false throws it from the consumer.
    /// </param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Scope_FinalReadAndCloseFailurePreservePrimary(bool failRead)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var primary = new IOException("primary validation failure");
        var close = new IOException("owned close failure");
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => fixture.RunAsync(() =>
        {
            fixture.CurrentLease.ExternalPublicationContext = new FailingClose(close);
            if (!failRead) return Task.FromException<List<ValidationIssue>>(primary);
            fixture.OnRead = _ => throw primary;
            return Task.FromResult(new List<ValidationIssue>());
        }));
        Assert.Same(primary, failure);
        Assert.Same(close, failure.Data["PendingSnapshotValidation.CloseFailure"]);
        fixture.OnRead = null;
        await fixture.RunAsync(() => Task.FromResult(new List<ValidationIssue>()));
    }

    /// <summary>
    /// Cancellation during acquisition releases only the attempted read reservation.
    /// </summary>
    [Fact]
    public async Task Scope_OpeningCancellationReleasesReservation()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var scope = fixture.Files.BeginPendingSnapshotValidationRead(cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.OpenAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.DisposeAsync().AsTask());
        await fixture.RunAsync(() => Task.FromResult(new List<ValidationIssue>()));
    }

    /// <summary>
    /// Supplies a deterministic close failure through the existing canonical lease teardown contract.
    /// </summary>
    /// <param name="failure">
    /// Exception thrown by disposal, retained as secondary when validation already failed.
    /// </param>
    private sealed class FailingClose(Exception failure) : IDisposable
    {
        /// <summary>
        /// Throws the fixture's predetermined close failure.
        /// </summary>
        public void Dispose() => throw failure;
    }

    /// <summary>
    /// Missing and unusable sources retain their existing statuses instead of becoming cached usable authority.
    /// </summary>
    /// <param name="missing">
    /// True removes the manifest; false leaves a well-formed manifest with an invalid payload hash.
    /// </param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Scope_MissingAndUnusableRemainDistinct(bool missing)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        var path = fixture.Files.ResolvePath("game_state/control/pending_turn_snapshot.json");
        if (missing) File.Delete(path);
        else
        {
            var invalid = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            invalid["manifestPayloadHash"] = new string('0', 64);
            File.WriteAllText(path, invalid.ToJsonString());
        }
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var expected = Status(await fixture.LookupAsync(false));
        Assert.Equal(missing ? "Missing" : "Unusable", expected);
        await fixture.RunAsync(async () =>
        {
            Assert.Equal(expected, Status(await fixture.LookupAsync(false)));
            Assert.Equal(expected, Status(await fixture.LookupAsync(true)));
            return [];
        });
    }

    /// <summary>
    /// An unusable manifest still binds its declared rollback dependencies for the whole invocation.
    /// </summary>
    [Fact]
    public async Task Scope_UnusableSourceStillBindsRollbackDependencies()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        var path = fixture.Files.ResolvePath("game_state/control/pending_turn_snapshot.json");
        var invalid = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        invalid["manifestPayloadHash"] = new string('0', 64);
        File.WriteAllText(path, invalid.ToJsonString());
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var changed = false;
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.RunAsync(async () =>
        {
            Assert.Equal("Unusable", Status(await fixture.LookupAsync(false)));
            await fixture.ChangeAsync("overwrite");
            changed = true;
            return [];
        }));
        Assert.True(changed);
    }

    /// <summary>
    /// Actual validator issues retain their order and content across scalar and reserved execution.
    /// </summary>
    [Fact]
    public async Task Scope_ActualValidationPreservesOrderedIssues()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var scalar = await fixture.Validator.ValidateGameStateAsync(GameStateValidationPhase.RequiredFiles);
        Assert.NotEmpty(scalar);
        var scoped = await fixture.RunAsync(() => (Task<List<ValidationIssue>>)typeof(ValidationService)
            .GetMethod("ValidateGameStateCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(fixture.Validator, [new GameStateValidationSelection(GameStateValidationPhase.RequiredFiles), false, null, null])!);
        Assert.Equal(JsonSerializer.Serialize(scalar), JsonSerializer.Serialize(scoped));
    }

    /// <summary>
    /// Every retained original admission remains protected, including ancestors of the reserving frame.
    /// </summary>
    [Fact]
    public async Task Scope_OuterOriginalAdmissionCannotClose()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var outer = fixture.Files.BeginMainAdmission();
        await outer.AcquireAsync();
        using var inner = fixture.Files.BeginMainAdmission();
        await inner.AcquireAsync();
        await fixture.RunAsync(() =>
        {
            Assert.Throws<InvalidOperationException>(() => outer.Dispose());
            return Task.FromResult(new List<ValidationIssue>());
        });
        Assert.False(outer.Closed);
        Assert.False(inner.Closed);
        inner.Dispose();
        outer.Dispose();
        Assert.True(outer.Closed);
    }

    /// <summary>
    /// An explicit lease from another root cannot borrow the current invocation's evidence.
    /// </summary>
    [Fact]
    public async Task Scope_ExplicitForeignLeaseRefuses()
    {
        using var foreign = new Fixture();
        await foreign.PrepareAsync(false);
        await using var foreignLease = await foreign.Files.AcquireCanonicalWriteLeaseAsync();
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        var foreignRefused = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync(async () =>
        {
            Assert.NotSame(foreignLease, fixture.CurrentLease);
            Assert.Equal("Usable", Status(await fixture.LookupAsync(true, writeLease: fixture.CurrentLease)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.LookupAsync(true, writeLease: foreignLease));
            foreignRefused = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.LookupAsync(true, writeLease: fixture.CurrentLease));
            return [];
        }));
        Assert.True(foreignRefused);
        await fixture.RunAsync(() => Task.FromResult(new List<ValidationIssue>()));
    }

    /// <summary>
    /// A real source-consuming phase reports a current-state deletion with the original issue and ordering.
    /// </summary>
    [Fact]
    public async Task Scope_CurrentStateMutationPreservesContinuityIssue()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync(false, flagBaseline: true);
        using var owner = fixture.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        const GameStateValidationPhase phase = GameStateValidationPhase.AfterlifeGlobalFlagPreTurnContinuity;
        Assert.Empty(await fixture.Validator.ValidateGameStateAsync(phase));
        File.WriteAllText(fixture.Files.ResolvePath(AfterlifeGlobalFlagState.StatePath), "{\"schemaVersion\":1,\"flags\":[]}");
        var scalar = await fixture.Validator.ValidateGameStateAsync(phase);
        Assert.Equal("afterlife_global_flag_removed_without_obsolete_marker", Assert.Single(scalar).Code);
        var scoped = await fixture.RunAsync(() => (Task<List<ValidationIssue>>)typeof(ValidationService)
            .GetMethod("ValidateGameStateCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(fixture.Validator, [new GameStateValidationSelection(phase), false, null, null])!);
        Assert.Equal(JsonSerializer.Serialize(scalar), JsonSerializer.Serialize(scoped));
    }

    /// <summary>
    /// Owns an isolated real filesystem and a three-file signed rollback cohort.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private const string ManifestPath = "game_state/control/pending_turn_snapshot.json";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-validation-scope-" + Guid.NewGuid().ToString("N"));
        private readonly HashSet<string> _rollback = new(StringComparer.Ordinal);
        private readonly ValidationService _validator;
        internal readonly FileSystemManager Files;
        internal int Ordinary, Cohort;
        internal Func<Task>? OnBorrow;
        internal Func<string, Task>? OnRead;

        /// <summary>
        /// The actual validator used by this independent fixture.
        /// </summary>
        internal ValidationService Validator => _validator;

        /// <summary>
        /// The active runner's original lease, used only for adversarial same-lease and close tests.
        /// </summary>
        internal FileSystemManager.CanonicalWriteLease CurrentLease
        {
            get
            {
                var invocation = typeof(ValidationService).GetField("_pendingSnapshotReservation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_validator)!;
                var scope = invocation.GetType().GetField("Physical", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(invocation)!;
                return (FileSystemManager.CanonicalWriteLease)scope.GetType().GetProperty("Lease", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scope)!;
            }
        }

        /// <summary>
        /// Installs counting observers without adding physical reads.
        /// </summary>
        internal Fixture()
        {
            Directory.CreateDirectory(_root);
            Files = new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks
                {
                    BeforeMainBorrowRetainAsync = () => OnBorrow?.Invoke() ?? Task.CompletedTask,
                    AfterCanonicalReadInitialValidationAsync = path => { if (_rollback.Contains(path)) Ordinary++; return OnRead?.Invoke(path) ?? Task.CompletedTask; },
                    AfterSnapshotEvidenceRead = path => { if (_rollback.Contains(path)) Cohort++; }
                });
            Files.EnsureDirectoryStructure();
            _validator = new(Files, NullLogger<ValidationService>.Instance);
        }

        /// <summary>
        /// Creates physical evidence using the existing detached-authority implementation.
        /// </summary>
        /// <param name="cold">
        /// True leaves the signed cohort unadmitted; false primes its existing storage owner.
        /// </param>
        /// <param name="flagBaseline">
        /// True replaces the first fixture file with a canonical flag for the real continuity phase.
        /// </param>
        /// <returns>
        /// Completion after the independent fixture is ready.
        /// </returns>
        internal async Task PrepareAsync(bool cold, bool flagBaseline = false)
        {
            string generation;
            await using (var lease = await Files.AcquireCanonicalWriteLeaseAsync()) generation = Files.GetOrCreateSessionGeneration(lease);
            var cohort = ConsoleLocalTurnRollbackArtifacts.CreateRoot();
            var marker = cohort + "/" + ConsoleLocalTurnRollbackArtifacts.MarkerName;
            Raw(marker, ConsoleLocalTurnRollbackArtifacts.CreateMarker(cohort, generation));
            var files = new JsonObject { [marker] = marker };
            var hashes = new JsonObject { [marker] = PendingTurnSnapshotAuthority.ComputeSha256(File.ReadAllBytes(Files.ResolvePath(marker))) };
            var backups = new JsonObject();
            var baseline = new JsonArray();
            for (var i = 0; i < 3; i++)
            {
                var original = flagBaseline && i == 0 ? AfterlifeGlobalFlagState.StatePath : $"game_state/world/scope{i}.json";
                var snapshot = $"game_state/control/pending_turn_snapshot/scope{i}.json";
                var rollback = $"{cohort}/scope{i}.rollback.json";
                var bytes = Encoding.UTF8.GetBytes(flagBaseline && i == 0
                    ? "{\"schemaVersion\":1,\"flags\":[{\"flagId\":\"retained-flag\",\"state\":\"active\"}]}"
                    : "{\"value\":1}");
                Raw(original, bytes); Raw(snapshot, bytes); Raw(rollback, bytes);
                _rollback.Add(rollback); files[original] = snapshot;
                hashes[original] = PendingTurnSnapshotAuthority.ComputeSha256(bytes);
                backups[original] = rollback; baseline.Add(original);
            }
            var manifest = new JsonObject
            {
                ["sessionId"] = "session", ["requestId"] = "request", ["turnNumber"] = 42,
                ["requestTimestamp"] = "time", ["playerAction"] = "action", ["files"] = files,
                ["snapshotFileHashes"] = hashes, ["clientOwnedValidationHashes"] = new JsonObject(),
                ["rollbackBackups"] = backups, ["rollbackBaselineFiles"] = baseline,
                ["sourceLabel"] = "scope fixture", ["manifestPayloadHash"] = ""
            };
            // Round-trip the production DTO so its canonical hash contains exactly its serialized fields.
            var type = typeof(ValidationService).GetNestedType("ValidationPendingTurnSnapshotManifest", BindingFlags.NonPublic)!;
            var opts = (JsonSerializerOptions)typeof(ValidationService).GetField("ManifestHashJsonOpts", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var dto = JsonSerializer.Deserialize(manifest.ToJsonString(), type, opts)!;
            manifest = JsonSerializer.SerializeToNode(dto, type, opts)!.AsObject();
            manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
            Raw(ManifestPath, Encoding.UTF8.GetBytes(manifest.ToJsonString()));
            Raw("input/turn_request.json", Encoding.UTF8.GetBytes("{\"sessionId\":\"session\",\"requestId\":\"request\",\"turnNumber\":42,\"timestamp\":\"time\",\"playerAction\":\"action\"}"));
            // Authority creation reads before-images by their actual paths, so use its pure method instead of a writer admission.
            var authority = Sign(manifest, opts);
            Raw(PendingTurnSnapshotAuthority.AuthorityPath, Encoding.UTF8.GetBytes(authority));
            if (!cold) { await using var lease = await Files.AcquireCanonicalWriteLeaseAsync(); }
            Ordinary = Cohort = 0;
        }

        /// <summary>
        /// Builds a valid detached authority with the unchanged production builder over this fixture's bytes.
        /// </summary>
        /// <param name="manifest">
        /// Manifest with its actual payload hash already computed.
        /// </param>
        /// <param name="opts">
        /// Production canonical serialization options.
        /// </param>
        /// <returns>
        /// Valid authority text for the supplied source.
        /// </returns>
        private string Sign(JsonObject manifest, JsonSerializerOptions opts) =>
            PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(manifest, opts,
                m => m["manifestPayloadHash"]!.GetValue<string>(), (m, h) => m["manifestPayloadHash"] = h,
                m => m["sessionId"]!.GetValue<string>(), m => m["requestId"]!.GetValue<string>(), m => m["turnNumber"]!.GetValue<int>(),
                m => Map(m, "files"), m => Map(m, "snapshotFileHashes"), m => Map(m, "clientOwnedValidationHashes"),
                m => m["rollbackBaselineFiles"]!.AsArray().Select(n => n!.GetValue<string>()).ToList(), m => m["sourceLabel"]!.GetValue<string>(),
                m => Map(m, "rollbackBackups"), p => File.ReadAllBytes(Files.ResolvePath(p)), hashSnapshotBytesExactly: true);

        /// <summary>
        /// Runs a consumer through the same lifetime runner as the production entrypoint.
        /// </summary>
        /// <param name="body">
        /// Real lookup consumers to run inside the invocation.
        /// </param>
        /// <returns>
        /// Issues only after the runner has completed.
        /// </returns>
        internal Task<List<ValidationIssue>> RunAsync(Func<Task<List<ValidationIssue>>> body) =>
            (Task<List<ValidationIssue>>)typeof(ValidationService).GetMethod("RunPendingSnapshotValidationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_validator, [body])!;

        /// <summary>
        /// Calls one of the unchanged real lookup contracts.
        /// </summary>
        /// <param name="asyncRead">
        /// True selects the asynchronous consumer; false selects the synchronous consumer.
        /// </param>
        /// <param name="validator">
        /// Optional independent validator for a physical replacement-proof control; null uses the fixture instance.
        /// </param>
        /// <param name="writeLease">
        /// Explicit asynchronous lookup lease; null uses the invocation's ordinary lookup contract.
        /// </param>
        /// <returns>
        /// The actual lookup result.
        /// </returns>
        internal async Task<object> LookupAsync(bool asyncRead, ValidationService? validator = null,
            FileSystemManager.CanonicalWriteLease? writeLease = null)
        {
            try
            {
                var method = typeof(ValidationService).GetMethod(asyncRead ? "LoadValidatedPendingTurnSnapshotLookupAsync" : "LoadValidatedPendingTurnSnapshotLookupSync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var result = method.Invoke(validator ?? _validator, asyncRead ? new object?[] { writeLease } : null)!;
                if (result is not Task task) return result;
                await task; return task.GetType().GetProperty("Result")!.GetValue(task)!;
            }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException!).Throw(); throw; }
        }

        /// <summary>
        /// Applies a durable raw test-hook change without claiming participating writer authority.
        /// </summary>
        /// <param name="change">
        /// Mutation scenario to apply.
        /// </param>
        /// <returns>
        /// Completion once changed bytes are physically present.
        /// </returns>
        internal Task ChangeAsync(string change)
        {
            var path = Files.ResolvePath(_rollback.First());
            var oldTime = File.GetLastWriteTimeUtc(path);
            switch (change)
            {
                case "delete": File.Delete(path); break;
                case "bom": File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, .. File.ReadAllBytes(path)]); break;
                case "overwrite": case "same-mtime": File.WriteAllText(path, "{\"value\":2}"); File.SetLastWriteTimeUtc(path, oldTime); break;
                case "manifest": File.AppendAllText(Files.ResolvePath(ManifestPath), " "); break;
                case "authority": File.AppendAllText(Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath), " "); break;
                case "alternative": Raw("ready/turn_complete.json", "{}"u8.ToArray()); break;
                case "generation": File.WriteAllText(Files.SessionGenerationPath, "{}"); break;
                case "valid-pair":
                    var replacement = JsonNode.Parse(File.ReadAllText(Files.ResolvePath(ManifestPath)))!.AsObject();
                    replacement["sessionId"] = "replacement-session";
                    replacement["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(replacement);
                    var opts = (JsonSerializerOptions)typeof(ValidationService).GetField("ManifestHashJsonOpts", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                    Raw(PendingTurnSnapshotAuthority.AuthorityPath, Encoding.UTF8.GetBytes(Sign(replacement, opts)));
                    Raw(ManifestPath, Encoding.UTF8.GetBytes(replacement.ToJsonString()));
                    var matchingRequest = JsonNode.Parse(File.ReadAllText(Files.ResolvePath("input/turn_request.json")))!.AsObject();
                    matchingRequest["sessionId"] = "replacement-session";
                    Raw("input/turn_request.json", Encoding.UTF8.GetBytes(matchingRequest.ToJsonString()));
                    break;
                default:
                    var requestPath = Files.ResolvePath("input/turn_request.json");
                    var request = JsonNode.Parse(File.ReadAllText(requestPath))!.AsObject();
                    if (change == "turn") request["turnNumber"] = 43;
                    else request[change == "session" ? "sessionId" : "requestId"] = "different";
                    File.WriteAllText(requestPath, request.ToJsonString()); break;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Writes fixture bytes directly before admission or as an explicit adversarial test hook.
        /// </summary>
        /// <param name="path">
        /// Root-relative fixture path.
        /// </param>
        /// <param name="bytes">
        /// Exact bytes to publish.
        /// </param>
        private void Raw(string path, byte[] bytes) { var full = Files.ResolvePath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllBytes(full, bytes); }

        /// <summary>
        /// Converts a manifest dictionary for the existing authority builder.
        /// </summary>
        /// <param name="manifest">
        /// Manifest being signed.
        /// </param>
        /// <param name="key">
        /// Dictionary property name.
        /// </param>
        /// <returns>
        /// Independent string dictionary.
        /// </returns>
        private static Dictionary<string, string> Map(JsonObject manifest, string key) => manifest[key]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<string>());

        /// <summary>
        /// Removes only this fixture's unique owned temporary directory.
        /// </summary>
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
