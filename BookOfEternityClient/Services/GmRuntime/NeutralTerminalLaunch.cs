namespace BookOfEternityClient.Services.GmRuntime;

// Internal technical admission. It allocates its own fresh scratch before host
// construction and cannot borrow a game session, executable or argument string.
internal sealed class NeutralTerminalLaunch
{
    internal string Package { get; }
    internal string Scratch { get; }
    private int _consumed;
    private NeutralTerminalLaunch(string package, string scratch) { Package=package; Scratch=scratch; }
    internal static NeutralTerminalLaunch Create(string package, string scratchParent)
    {
        var scratch=Path.Combine(Path.GetFullPath(scratchParent),"neutral-session-"+Guid.NewGuid().ToString("N"));
        scratch=Path.Combine(scratch,"game_session");
        Directory.CreateDirectory(scratch);
        return new(Path.GetFullPath(package),scratch);
    }
    // Internal crash-qualification admission, supplied by the surviving witness.
    // This creates a NEW fixed neutral capability; main.json/old owner/PID are
    // never inputs. The normal factory still validates the fixed package/manifest,
    // and the actual coordinator must admit the root before any launch.
    internal static NeutralTerminalLaunch CreateForFixtureRoot(string package,string fixtureParent,string root)
    {
        package=Path.GetFullPath(package);fixtureParent=Path.GetFullPath(fixtureParent);root=Path.GetFullPath(root);
        var name=Path.GetFileName(root);
        if(package!=fixtureParent || Path.GetDirectoryName(root)!=fixtureParent ||
            !name.StartsWith("neutral-session-",StringComparison.Ordinal) ||
            !Guid.TryParseExact(name["neutral-session-".Length..],"N",out _) ||
            !Directory.Exists(Path.Combine(root,"game_session")))
            throw new InvalidOperationException("Only the witness-owned fixed neutral fixture root is admitted.");
        return new(package,Path.Combine(root,"game_session"));
    }
    internal NeutralTerminalLaunch NextEpoch()=>new(Package,Scratch);
    internal void Consume() { if(Interlocked.Exchange(ref _consumed,1)!=0)throw new InvalidOperationException("Neutral original session admission already consumed."); }
}

internal sealed class OwnedTerminalStartException(IOwnedTerminalSession owner, Exception inner)
    : Exception("Partial terminal startup retains its original owner.", inner)
{
    internal IOwnedTerminalSession Owner { get; } = owner;
}
