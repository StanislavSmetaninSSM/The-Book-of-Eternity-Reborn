using System.Reflection;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmMainCrashFixtureAdmissionTests
{
    private static NeutralTerminalLaunch Admit(string package,string fixture,string root)
    {
        var method=typeof(NeutralTerminalLaunch).GetMethod("CreateForFixtureRoot",BindingFlags.Static|BindingFlags.NonPublic);
        Assert.NotNull(method); // causal RED for the new fixture seam, not a build failure
        try{return (NeutralTerminalLaunch)method.Invoke(null,[package,fixture,root])!;}
        catch(TargetInvocationException failure){throw failure.InnerException!;}
    }

    [Fact]
    public void FreshFixtureAdmission_IsSingleUseAndDoesNotDecodeOldRecord()
    {
        var fixture=Path.Combine(Path.GetTempPath(),"boe-f3-admission-"+Guid.NewGuid().ToString("N"));
        try {
            var initial=NeutralTerminalLaunch.Create(fixture,fixture);
            var root=Directory.GetParent(initial.Scratch)!.FullName;
            Directory.CreateDirectory(Path.Combine(root,".boe_runtime/gm-runs"));
            File.WriteAllText(Path.Combine(root,".boe_runtime/gm-runs/main.json"),"invalid old record is never authority");
            var fresh=Admit(fixture,fixture,root);
            Assert.NotSame(initial,fresh);Assert.Equal(initial.Scratch,fresh.Scratch);Assert.Equal(initial.Package,fresh.Package);
            fresh.Consume();Assert.Throws<InvalidOperationException>(fresh.Consume);
            Assert.Equal("invalid old record is never authority",File.ReadAllText(Path.Combine(root,".boe_runtime/gm-runs/main.json")));
        } finally {if(Directory.Exists(fixture))Directory.Delete(fixture,true);}
    }

    [Fact]
    public void FixtureAdmission_RejectsOutsideRootAndNonFixedPackageBeforeEffects()
    {
        var fixture=Path.Combine(Path.GetTempPath(),"boe-f3-admission-"+Guid.NewGuid().ToString("N"));
        try {
            var initial=NeutralTerminalLaunch.Create(fixture,fixture);var root=Directory.GetParent(initial.Scratch)!.FullName;
            var count=Directory.GetFileSystemEntries(fixture).Length;
            Assert.Throws<InvalidOperationException>(()=>Admit(fixture,fixture,fixture));
            Assert.Throws<InvalidOperationException>(()=>Admit(fixture,fixture,Path.Combine(fixture,"other-root")));
            Assert.Throws<InvalidOperationException>(()=>Admit(fixture+"-other",fixture,root));
            Assert.Equal(count,Directory.GetFileSystemEntries(fixture).Length);
        } finally {if(Directory.Exists(fixture))Directory.Delete(fixture,true);}
    }
}
