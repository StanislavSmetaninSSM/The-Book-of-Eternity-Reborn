using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class MortalWoundTreatmentAcceptedStateRegistryTests
{
    [Fact]
    public void Bind_ReleasedOldLeaseWithSameManagerNewLease_RejectsStaleCandidateWithoutCachePoisoning()
    {
        using var fixture = ProductionAcceptedStateFixture.Create();
        var staleCandidate = fixture.ExportCurrentCore();
        AssertValid(staleCandidate);

        fixture.ReplaceLeaseWithoutGenerationRotation(replaceFileSystemManager: false);

        var rejected = BindWithoutThrow(
            fixture.FileSystem,
            fixture.Lease,
            staleCandidate);
        AssertRejectedFrozen(rejected);
        AssertFreshCandidateCanBind(fixture, fixture.FileSystem, fixture.Lease);
    }

    [Fact]
    public void Bind_SameRootForeignManager_RejectsStaleCandidateWithoutCachePoisoning()
    {
        using var fixture = ProductionAcceptedStateFixture.Create();
        var staleCandidate = fixture.ExportCurrentCore();
        AssertValid(staleCandidate);

        fixture.ReplaceLeaseWithoutGenerationRotation(replaceFileSystemManager: true);

        var rejected = BindWithoutThrow(
            fixture.FileSystem,
            fixture.Lease,
            staleCandidate);
        AssertRejectedFrozen(rejected);
        AssertFreshCandidateCanBind(fixture, fixture.FileSystem, fixture.Lease);
    }

    [Fact]
    public async Task Bind_RotatedGeneration_RejectsStaleCandidateWithoutCachePoisoning()
    {
        using var fixture = ProductionAcceptedStateFixture.Create();
        fixture.ReleaseLease();

        await using var lifecycle =
            await fixture.FileSystem.AcquireSessionLifecycleLeaseAsync();
        await using var replacement =
            await fixture.FileSystem.AcquireSessionReplacementWriteLeaseAsync(lifecycle);
        var staleCandidate = fixture.ExportCurrentCore(fixture.FileSystem, replacement);
        AssertValid(staleCandidate);
        fixture.FileSystem.RotateSessionGeneration(replacement);

        var rejected = BindWithoutThrow(
            fixture.FileSystem,
            replacement,
            staleCandidate);
        AssertRejectedFrozen(rejected);
        AssertFreshCandidateCanBind(fixture, fixture.FileSystem, replacement);
    }

    private static MortalWoundTreatmentAcceptedStateAuthorityResult BindWithoutThrow(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease,
        MortalWoundTreatmentAcceptedStateAuthorityResult candidate)
    {
        MortalWoundTreatmentAcceptedStateAuthorityResult? result = null;
        var exception = Record.Exception(() =>
            result = AcceptedTurnAuthorityRegistry.BindMortalWoundTreatmentAcceptedState(
                fileSystem,
                lease,
                candidate));

        Assert.Null(exception);
        return Assert.IsType<MortalWoundTreatmentAcceptedStateAuthorityResult>(result);
    }

    private static void AssertFreshCandidateCanBind(
        ProductionAcceptedStateFixture fixture,
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease)
    {
        var freshCandidate = fixture.ExportCurrentCore(fileSystem, lease);
        AssertValid(freshCandidate);

        var rebound = BindWithoutThrow(fileSystem, lease, freshCandidate);

        AssertValid(rebound);
        Assert.Same(freshCandidate.Authority, rebound.Authority);
    }

    private static void AssertValid(
        MortalWoundTreatmentAcceptedStateAuthorityResult result)
    {
        Assert.True(result.IsValid, Describe(result.Issues));
        Assert.Empty(result.Issues);
        Assert.NotNull(result.Authority);
    }

    private static void AssertRejectedFrozen(
        MortalWoundTreatmentAcceptedStateAuthorityResult result)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.Authority);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(
            "mortal_wound_treatment_accepted_state_registry_candidate_stale",
            issue.Code);

        var mutableView = Assert.IsAssignableFrom<IList<ValidationIssue>>(result.Issues);
        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(issue));
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(issue =>
                $"{issue.FilePath}: {issue.Code}: {issue.Expected}; actual={issue.Actual}"));

    private sealed class ProductionAcceptedStateFixture : IDisposable
    {
        private static readonly Type FixtureType =
            typeof(MortalWoundTreatmentResolverTests).GetNestedType(
                "AcceptedStateFixture",
                BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Accepted-state fixture type was not found.");

        private static readonly MethodInfo ExportCurrentCoreMethod =
            typeof(MortalWoundTreatmentAcceptedStateAuthority).GetMethod(
                "ExportCurrentCore",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types:
                [
                    typeof(FileSystemManager),
                    typeof(FileSystemManager.CanonicalWriteLease),
                    typeof(MortalWoundTreatmentAuthority.Context),
                    typeof(string)
                ],
                modifiers: null)
            ?? throw new InvalidOperationException("Accepted-state core exporter was not found.");

        private readonly object _fixture;

        private ProductionAcceptedStateFixture(object fixture) => _fixture = fixture;

        internal FileSystemManager FileSystem =>
            ReadProperty<FileSystemManager>("FileSystem");

        internal FileSystemManager.CanonicalWriteLease Lease =>
            ReadProperty<FileSystemManager.CanonicalWriteLease>("Lease");

        private MortalWoundTreatmentAuthority.Context Context =>
            ReadProperty<MortalWoundTreatmentAuthority.Context>("TreatmentContext");

        private string WoundId => ReadProperty<string>("WoundId");

        internal static ProductionAcceptedStateFixture Create()
        {
            var scenarioFactory = typeof(MortalWoundTreatmentResolverTests).GetMethod(
                "CreateScenario",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(string), typeof(string)],
                modifiers: null)
                ?? throw new InvalidOperationException("Resolver scenario factory was not found.");
            var scenario = Invoke(
                scenarioFactory,
                instance: null,
                ["procedure_normal_uses_lowest_free_die", "procedure"]);
            var fixtureFactory = FixtureType.GetMethods(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method =>
                    string.Equals(method.Name, "Create", StringComparison.Ordinal) &&
                    method.GetParameters().Length == 2);
            return new ProductionAcceptedStateFixture(
                Invoke(fixtureFactory, instance: null, [scenario, null]));
        }

        internal MortalWoundTreatmentAcceptedStateAuthorityResult ExportCurrentCore() =>
            ExportCurrentCore(FileSystem, Lease);

        internal MortalWoundTreatmentAcceptedStateAuthorityResult ExportCurrentCore(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease) =>
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthorityResult>(Invoke(
                ExportCurrentCoreMethod,
                instance: null,
                [fileSystem, lease, Context, WoundId]));

        internal void ReplaceLeaseWithoutGenerationRotation(
            bool replaceFileSystemManager) =>
            InvokeInstance(
                "ReplaceLeaseWithoutGenerationRotation",
                [replaceFileSystemManager]);

        internal void ReleaseLease() =>
            InvokeInstance("ReleaseLeaseForExternalDistribution", []);

        public void Dispose() => InvokeInstance("Dispose", []);

        private T ReadProperty<T>(string name)
        {
            var property = FixtureType.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Fixture property '{name}' was not found.");
            return Assert.IsType<T>(property.GetValue(_fixture));
        }

        private void InvokeInstance(string name, object?[] arguments)
        {
            var method = FixtureType.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(candidate =>
                    string.Equals(candidate.Name, name, StringComparison.Ordinal) &&
                    candidate.GetParameters().Length == arguments.Length);
            try
            {
                method.Invoke(_fixture, arguments);
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }

        private static object Invoke(
            MethodInfo method,
            object? instance,
            object?[] arguments)
        {
            try
            {
                return method.Invoke(instance, arguments)
                    ?? throw new InvalidOperationException(
                        $"Reflection call '{method.Name}' returned null.");
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }
    }
}
