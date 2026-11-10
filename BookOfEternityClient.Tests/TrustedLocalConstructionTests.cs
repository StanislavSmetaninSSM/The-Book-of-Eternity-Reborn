using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises constructor observation order with detached kinds and errors, without issuing file authority.
/// </summary>
public sealed class TrustedLocalConstructionTests
{
    private static readonly string Volume = Path.GetPathRoot(Path.GetTempPath())!;
    private static readonly string Parent = Path.Combine(Volume, "boe-construction-parent");
    private static readonly string Root = Path.Combine(Parent, "grant");

    /// <summary>
    /// Visits each required ancestor once and then the root entry once.
    /// </summary>
    [Fact]
    public void ExistingRoot_ObservesOneParentChainAndOneRootEntry()
    {
        var seen = new List<string>();
        TrustedLocalFileScope.ValidateConstructionRoot(Root, path =>
        {
            seen.Add(path);
            return TrustedLocalFileScope.EntryKind.Directory;
        });
        Assert.Equal(new[] { Volume, Parent, Root }, seen);
    }

    /// <summary>
    /// Accepts an absent root after checking every required parent exactly once.
    /// </summary>
    [Fact]
    public void MissingRoot_ObservesRequiredParentsAndAcceptsRootAbsence()
    {
        var seen = new List<string>();
        TrustedLocalFileScope.ValidateConstructionRoot(Root, path =>
        {
            seen.Add(path);
            return path == Root ? TrustedLocalFileScope.EntryKind.Missing : TrustedLocalFileScope.EntryKind.Directory;
        });
        Assert.Equal(new[] { Volume, Parent, Root }, seen);
    }

    /// <summary>
    /// Refuses a missing required parent before observing the optional root.
    /// </summary>
    [Fact]
    public void MissingParent_RefusesBeforeRootObservation()
    {
        var seen = new List<string>();
        var error = Assert.Throws<InvalidDataException>(() => TrustedLocalFileScope.ValidateConstructionRoot(Root, path =>
        {
            seen.Add(path);
            return path == Parent ? TrustedLocalFileScope.EntryKind.Missing : TrustedLocalFileScope.EntryKind.Directory;
        }));
        Assert.Equal(new[] { Volume, Parent }, seen);
        Assert.Equal("A local storage ancestor is missing, linked or not a directory: " + Parent, error.Message);
    }

    /// <summary>
    /// Refuses a file, link or special root with the existing ancestor failure and path.
    /// </summary>
    /// <param name="rootKind">
    /// The detached regular-file, link or unsupported entry kind supplied for the root.
    /// </param>
    [Theory]
    [InlineData("RegularFile")]
    [InlineData("Link")]
    [InlineData("Other")]
    public void NonDirectoryRoot_RefusesWithExactPath(string rootKind)
    {
        var seen = new List<string>();
        var kind = Enum.Parse<TrustedLocalFileScope.EntryKind>(rootKind);
        var error = Assert.Throws<InvalidDataException>(() => TrustedLocalFileScope.ValidateConstructionRoot(Root, path =>
        {
            seen.Add(path);
            return path == Root ? kind : TrustedLocalFileScope.EntryKind.Directory;
        }));
        Assert.Equal(new[] { Volume, Parent, Root }, seen);
        Assert.Equal("A local storage ancestor is missing, linked or not a directory: " + Root, error.Message);
    }

    /// <summary>
    /// Refuses a non-directory required parent before any root observation.
    /// </summary>
    /// <param name="parentKind">
    /// The detached regular-file, link or unsupported entry kind supplied for the required parent.
    /// </param>
    [Theory]
    [InlineData("RegularFile")]
    [InlineData("Link")]
    [InlineData("Other")]
    public void NonDirectoryParent_RefusesBeforeRoot(string parentKind)
    {
        var seen = new List<string>();
        var kind = Enum.Parse<TrustedLocalFileScope.EntryKind>(parentKind);
        var error = Assert.Throws<InvalidDataException>(() => TrustedLocalFileScope.ValidateConstructionRoot(Root, path =>
        {
            seen.Add(path);
            return path == Parent ? kind : TrustedLocalFileScope.EntryKind.Directory;
        }));
        Assert.Equal(new[] { Volume, Parent }, seen);
        Assert.Equal("A local storage ancestor is missing, linked or not a directory: " + Parent, error.Message);
    }

    /// <summary>
    /// Propagates the exact observation exception without converting access or I/O failure into absence.
    /// </summary>
    /// <param name="accessFailure">
    /// Whether the detached observer throws an access exception instead of an ordinary I/O exception.
    /// This controls propagation only and does not simulate native permission enforcement.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObservationError_PropagatesExactInstance(bool accessFailure)
    {
        var seen = new List<string>();
        Exception expected = accessFailure ? new UnauthorizedAccessException("detached access failure") : new IOException("detached read failure");
        var actual = Record.Exception(() => TrustedLocalFileScope.ValidateConstructionRoot(Root, path =>
        {
            seen.Add(path);
            if (path == Parent) throw expected;
            return TrustedLocalFileScope.EntryKind.Directory;
        }));
        Assert.Same(expected, actual);
        Assert.Equal(new[] { Volume, Parent }, seen);
    }

    /// <summary>
    /// Re-observes the required parent on the next invocation and refuses its changed kind.
    /// </summary>
    [Fact]
    public void SubsequentInvocation_UsesFreshParentObservations()
    {
        var seen = new List<string>();
        var changed = false;
        TrustedLocalFileScope.EntryKind Observe(string path)
        {
            seen.Add(path);
            return changed && path == Parent ? TrustedLocalFileScope.EntryKind.Link : TrustedLocalFileScope.EntryKind.Directory;
        }
        TrustedLocalFileScope.ValidateConstructionRoot(Root, Observe);
        changed = true;
        Assert.Throws<InvalidDataException>(() => TrustedLocalFileScope.ValidateConstructionRoot(Root, Observe));
        Assert.Equal(new[] { Volume, Parent, Root, Volume, Parent }, seen);
    }

    /// <summary>
    /// Keeps observations local to each root rather than sharing a parent result between grants.
    /// </summary>
    [Fact]
    public void SeparateRoots_ObserveTheirParentsIndependently()
    {
        var otherRoot = Path.Combine(Parent, "other-grant");
        var seen = new List<string>();
        foreach (var root in new[] { Root, otherRoot })
            TrustedLocalFileScope.ValidateConstructionRoot(root, path =>
            {
                seen.Add(path);
                return TrustedLocalFileScope.EntryKind.Directory;
            });
        Assert.Equal(new[] { Volume, Parent, Root, Volume, Parent, otherRoot }, seen);
    }

    /// <summary>
    /// Refuses a filesystem-volume root before requesting an entry observation.
    /// </summary>
    [Fact]
    public void VolumeRoot_RefusesWithoutObservation()
    {
        var observations = 0;
        var error = Assert.Throws<InvalidDataException>(() => TrustedLocalFileScope.ValidateConstructionRoot(Volume, _ =>
        {
            observations++;
            return TrustedLocalFileScope.EntryKind.Directory;
        }));
        Assert.Equal("A filesystem root is not a local storage scope.", error.Message);
        Assert.Equal(0, observations);
    }

    /// <summary>
    /// Rejects a missing invocation dependency before beginning namespace validation.
    /// </summary>
    [Fact]
    public void MissingObserver_RejectsNullDependency()
    {
        var error = Assert.Throws<ArgumentNullException>(() => TrustedLocalFileScope.ValidateConstructionRoot(Root, null!));
        Assert.Equal("observeEntry", error.ParamName);
    }
}
