using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class RetiredWorkerRegistrationTests
{
    [Theory]
    [InlineData("active")]
    [InlineData("uncertain")]
    [InlineData("swapped")]
    [InlineData("foreign-root")]
    public void RetiredRegistrationPredicateNeverAcceptsAnotherLifetime(string mode)
    {
        var root = Path.Combine(Path.GetTempPath(), "retired-worker-" + Guid.NewGuid().ToString("N"));
        GmWorkerRootContext? context = null, replacement = null;
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            context = GmWorkerRootContext.Attach(files, true, null);
            context.RequireMainQuiescence();
            if (mode == "active")
            {
                Assert.False(context.IsRetired);
                Assert.Throws<InvalidOperationException>(() => context.RequireRetiredRemoteCandidate(files));
                return;
            }
            context.ReleaseClient();
            Assert.True(context.IsRetired);
            context.RequireRetiredRemoteCandidate(files); // DATA only, no grant
            Assert.Throws<InvalidOperationException>(context.RequireOpen);
            if (mode == "uncertain") context.CloseForUncertainty();
            if (mode == "swapped") replacement = GmWorkerRootContext.Attach(files, true, null);
            if (mode == "foreign-root") files = new FileSystemManager(Path.Combine(root, "other"), NullLogger<FileSystemManager>.Instance);
            Assert.Throws<InvalidOperationException>(() => context.RequireRetiredRemoteCandidate(files));
        }
        finally
        {
            replacement?.ReleaseClient();
            context?.ReleaseClient();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
