using System.Reflection;
using Xunit;

namespace BookOfEternityClient.Tests;

// Real path resolution using inert files. No executable or native Windows owner runs.
public sealed class GmWindowsShellResolutionTests
{
    [Theory]
    [InlineData("preferred")]
    [InlineData("fallback")]
    [InlineData("missing")]
    public void ResolvesExactConfigurationWithoutLaunchingAProbe(string mode)
    {
        var root=Path.Combine(Path.GetTempPath(),"boe-shell-resolution-"+Guid.NewGuid().ToString("N"));
        var preferred=Path.Combine(root,"preferred");var system=Path.Combine(root,"system");
        var pwsh=Path.Combine(preferred,"pwsh.exe");
        var fallback=Path.Combine(system,"WindowsPowerShell","v1.0","powershell.exe");
        Directory.CreateDirectory(preferred);Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
        try {
            if(mode=="preferred")File.WriteAllText(pwsh,"inert file; never execute");
            if(mode!="missing")File.WriteAllText(fallback,"inert fallback; never execute");
            var bridge=LoadBridge();
            var resolver=bridge.GetType("BookOfEternityGMBridge.BridgeHost",true)!.GetMethod("ResolveWindowsShellExecutable",BindingFlags.Static|BindingFlags.NonPublic)!;
            var search=string.Join(Path.PathSeparator,"", ".", "relative-path",preferred);
            if(mode=="missing") {
                var failure=Assert.Throws<TargetInvocationException>(()=>resolver.Invoke(null,[search,system]));
                Assert.IsType<FileNotFoundException>(failure.InnerException);
            } else {
                var result=Assert.IsType<string>(resolver.Invoke(null,[search,system]));
                Assert.Equal(mode=="preferred"?pwsh:fallback,result);
                Assert.True(Path.IsPathFullyQualified(result));
                Assert.Contains("never execute",File.ReadAllText(result));
            }
        } finally {Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("configured")]
    [InlineData("default")]
    public void WorkingDirectoryResolutionDoesNotCreateCanonicalStateBeforeAdmission(string mode)
    {
        var root=Path.Combine(Path.GetTempPath(),"boe-cwd-resolution-"+Guid.NewGuid().ToString("N"));
        var session=Path.Combine(root,"game_session");Directory.CreateDirectory(session);
        var relative="game_state/control/new-cwd";var target=Path.Combine(session,"game_state","control","new-cwd");
        if(mode=="configured")Directory.CreateDirectory(target);
        var before=Directory.GetFileSystemEntries(root,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var type=LoadBridge().GetType("BookOfEternityGMBridge.BridgeHost",true)!;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var host=Activator.CreateInstance(type,flags,null,[session,"boe-cwd-"+Guid.NewGuid().ToString("N")],null)!;
        try {
            string? actual=null;Exception? failure=null;
            try {actual=(string)type.GetMethod("ResolveGmBridgeShellWorkingDirectory",flags)!.Invoke(host,[mode=="default"?null:relative])!;}
            catch(TargetInvocationException error){failure=error.InnerException;}
            Console.WriteLine($"mode={mode};returned={actual};targetCreated={Directory.Exists(target)};failure={failure}");
            Assert.Null(type.GetField("_mainRun",flags)!.GetValue(host));
            Assert.Null(type.GetField("_pty",flags)!.GetValue(host));
            if(mode=="missing") {
                Assert.False(Directory.Exists(target),"Resolution created canonical child before original admission.");
                Assert.IsType<DirectoryNotFoundException>(failure);
            } else {
                Assert.Null(failure);Assert.Equal(mode=="default"?session:target,actual);
            }
            Assert.Equal(before,Directory.GetFileSystemEntries(root,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray());
        } finally {((IDisposable)host).Dispose();Directory.Delete(root,true);}
    }
    internal static Assembly LoadBridge()=>Assembly.LoadFrom(Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityGMBridge/bin",
        new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,"net8.0/BookOfEternityGMBridge.dll"));
}
