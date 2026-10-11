using System.Reflection;
using System.Security.Cryptography;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class OriginalBrowserRollbackHashContractsTests
{
    [Fact]
    public async Task StrictRollbackHash_ActualReaderAndChunkCorePreserveRefusals()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-rollback-hash-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "game_session"));
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            typeof(FileSystemManager).GetField("_browserAdmissionDiagnostic", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(files, null);
            Assert.Null(typeof(FileSystemManager).GetField("_hooks", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(files));
            var read = typeof(FileSystemManager).GetMethod("HashOriginalBrowserRollback", BindingFlags.NonPublic | BindingFlags.Instance)!
                .CreateDelegate<Func<string, TrustedLocalFileScope?, bool, byte[], string?>>(files);
            var bytes = typeof(FileSystemManager).GetMethod("ReadOriginalBrowserBytes", BindingFlags.NonPublic | BindingFlags.Instance,
                null, [typeof(string), typeof(TrustedLocalFileScope), typeof(bool?)], null)!
                .CreateDelegate<Func<string, TrustedLocalFileScope?, bool?, byte[]?>>(files);
            var append = typeof(FileSystemManager).GetMethod("AppendOriginalBrowserInitialBytes", BindingFlags.NonPublic | BindingFlags.Static)!
                .CreateDelegate<Action<Stream, long, byte[], HashAlgorithm>>();
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            Assert.True(lease.IsActive);
            var scope = new TrustedLocalFileScope([files.BasePath]); var scratch = new byte[65536];
            const string relative = "controls/member.rollback.test";
            var path = files.ResolvePath(relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            foreach (var (content, expected) in new (byte[], string)[]
            {
                ([], "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
                ([0, 1, 127, 128, 255, 16, 32], "1804B084980780FD19D518D8BFF867E69CFACC4F14F98D4CD757ADE9ABBFAEB8"),
                ([239, 187, 191, 97, 98], "E54DD095F92262CBAF1EF453DE08896FEE09647D82BE9433CC344752E643E43D"),
                ([240, 40, 140, 188, 192, 175, 0], "B0092EB2C379028748E10D9111849ACEEFE413135CAFADFCB497DE08A96BE290")
            })
            {
                File.WriteAllBytes(path, content);
                Assert.Equal(expected, read(relative, scope, true, scratch));
                Assert.Equal(expected, read(relative, null, false, scratch)); // Unsupplied per-read scope branch; no unheld admission claim.
                Assert.Equal(content, File.ReadAllBytes(path));
            }
            foreach (var length in new[] { 65535, 65536, 65537 })
            {
                var content = Enumerable.Range(0, length).Select(x => (byte)(x * 131 + 17)).ToArray();
                File.WriteAllBytes(path, content);
                Assert.Equal(PendingTurnSnapshotAuthority.ComputeSha256(content), read(relative, scope, true, scratch));
                Assert.Equal(content, File.ReadAllBytes(path));
            }
            Assert.Null(read("controls/missing.rollback.test", scope, true, scratch));
            using (var sparse = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                sparse.SetLength((long)Array.MaxLength + 1);
            Assert.Equal(Assert.Throws<IOException>(() => bytes(relative, scope, true)).Message,
                Assert.Throws<IOException>(() => read(relative, scope, true, scratch)).Message);
            Assert.Throws<InvalidDataException>(() => read("../outside", scope, true, scratch));
            File.Delete(path); Directory.CreateDirectory(path);
            Assert.Throws<InvalidDataException>(() => read(relative, scope, true, scratch));
            Directory.Delete(path);
            var target = Path.Combine(root, "target"); File.WriteAllBytes(target, [1, 2, 3]);
            File.CreateSymbolicLink(path, target);
            Assert.Throws<InvalidDataException>(() => read(relative, scope, true, scratch)); File.Delete(path);
            var block = files.ResolvePath("controls/block"); File.WriteAllBytes(block, [1]);
            Assert.Throws<InvalidDataException>(() => read("controls/block/member.rollback.test", scope, true, scratch));
            File.Delete(block); Directory.CreateSymbolicLink(block, Path.GetDirectoryName(target)!);
            Assert.Throws<InvalidDataException>(() => read("controls/block/target", scope, true, scratch));
            Directory.Delete(block);

            // These exercise the ACTUAL production chunk primitive at a captured
            // initial length, not the frozen source-sequence filter reference.
            foreach (var changedLength in new[] { 2, 4 })
            {
                using var stream = new MemoryStream(new byte[changedLength]);
                using var hash = new FinalizationWitnessHash();
                var failure = Record.Exception(() => append(stream, 3, scratch, hash));
                if (changedLength == 2) Assert.IsType<EndOfStreamException>(failure);
                else Assert.IsType<InvalidDataException>(failure);
                Assert.Equal(0, hash.Finalizations);
            }
            var original = new IOException("original chunk read failure");
            using var broken = new FailingReadStream(original);
            using var brokenHash = new FinalizationWitnessHash();
            Assert.Same(original, Assert.Throws<IOException>(() => append(broken, 3, scratch, brokenHash)));
            Assert.Equal(0, brokenHash.Finalizations);
            Assert.True(lease.IsActive);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class FinalizationWitnessHash : HashAlgorithm
    {
        internal int Finalizations { get; private set; }
        internal FinalizationWitnessHash() { HashSizeValue = 256; }
        public override void Initialize() { }
        protected override void HashCore(byte[] array, int offset, int count) { }
        protected override byte[] HashFinal() { Finalizations++; return new byte[32]; }
    }
    private sealed class FailingReadStream(IOException original) : MemoryStream(new byte[3])
    {
        public override int Read(Span<byte> buffer) => throw original;
        public override int Read(byte[] buffer, int offset, int count) => throw original;
    }
}
