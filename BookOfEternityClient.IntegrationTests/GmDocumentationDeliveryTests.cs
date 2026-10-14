using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class GmDocumentationDeliveryTests
{
    private readonly ITestOutputHelper _output;
    public GmDocumentationDeliveryTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task PackagedProductionWriterPublishesExactRequiredReadingClosure()
    {
        var result = await GmDocumentationDeliveryScenario.RunAsync(TestRepoPaths.RepoRoot, publish: true);
        _output.WriteLine("GM1557_DELIVERY " + result.Evidence.ToJsonString());
        // Real writer/close/ownership must succeed before the intended missing-new-module oracle.
        Assert.Null(result.Failure);
        Assert.True(result.Evidence["exitConfirmed"]!.GetValue<bool>());
        Assert.True(result.Evidence["rootsRemoved"]!.GetValue<bool>());
        Assert.True(result.Ast!["runtimeExecuted"]!.GetValue<bool>());
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task PackagedDaemonAstCutsOnlyCompleteTwoCommandStartup()
    {
        var result = await GmDocumentationDeliveryScenario.RunAsync(TestRepoPaths.RepoRoot, publish: false);
        _output.WriteLine("GM1557_AST " + result.Evidence.ToJsonString());
        Assert.Null(result.Failure);
        Assert.Empty(result.Issues);
        Assert.True(result.Evidence["exitConfirmed"]!.GetValue<bool>());
        Assert.True(result.Evidence["rootsRemoved"]!.GetValue<bool>());
        Assert.False(result.Ast!["runtimeExecuted"]!.GetValue<bool>());
        Assert.Equal("Write-GmContextPack", result.Ast["predecessor"]!.GetValue<string>());
        Assert.Equal(3, result.Ast["bomLength"]!.GetValue<int>());
        Assert.True(result.Ast["prefixLength"]!.GetValue<int>() > result.Ast["endOffsetUtf16"]!.GetValue<int>());
        // Dynamic complete-source/prefix pins, never historical offsets substituted for final AST proof.
        Assert.Equal(result.Evidence["packagedDaemonHash"]!.GetValue<string>(), result.Ast["sourceHash"]!.GetValue<string>());
    }

    [Fact]
    public async Task FinalDaemonSourceKeepsScopedDirectionsAtRealDispatch()
    {
        var result = await GmDocumentationDeliveryScenario.RunAsync(TestRepoPaths.RepoRoot, publish: false);
        _output.WriteLine("GM1557_FINAL_SOURCE " + result.Evidence.ToJsonString());
        Assert.Null(result.Failure);
        Assert.Empty(result.Issues);
        Assert.True(result.Evidence["rootsRemoved"]!.GetValue<bool>());
        // Separate full-source AST/dataflow proof: prefix publication alone proves no dispatch.
        Assert.Empty(GmDocumentationDeliveryRules.VerifyFullSource(result.Ast!));
    }

    [Theory]
    [InlineData("repair_ban")]
    [InlineData("late_override")]
    [InlineData("late_empty_reset")]
    [InlineData("receipt_post_builder")]
    [InlineData("read_all")]
    [InlineData("receipt_ordinary")]
    [InlineData("wrong_boundary")]
    public async Task SourceGuardMutationsFailSpecificFinalOrBoundaryGuards(string mutation)
    {
        var changed = await GmDocumentationDeliveryScenario.RunAsync(TestRepoPaths.RepoRoot, publish: false,
            sourceMutation: bytes => MutateSource(bytes, mutation), sourceSeedMutation: bytes => MutateSource(bytes, null));
        _output.WriteLine("GM1557_SOURCE_NEGATIVE " + changed.Evidence.ToJsonString());
        var seed = changed.Ast!["seed"]!.AsObject();
        Assert.True(seed["completed"]!.GetValue<bool>());
        Assert.Empty(GmDocumentationDeliveryRules.VerifyFullSource(seed));
        Assert.True(changed.Evidence["rootsRemoved"]!.GetValue<bool>());
        if (mutation == "wrong_boundary") Assert.Contains("ast.startup_boundary", changed.Failure, StringComparison.Ordinal);
        else
        {
            Assert.Null(changed.Failure); Assert.Empty(changed.Issues);
            var expected = mutation switch { "repair_ban" => "source.repair_ban_missing", "late_override" => "source.late_source_override",
                "late_empty_reset" => "source.late_source_override", "read_all" => "source.universal_read", _ => "source.receipt_ordinary_injected" };
            Assert.Contains(expected, GmDocumentationDeliveryRules.VerifyFullSource(changed.Ast!));
        }
    }

    [Theory]
    [InlineData("missing_copy")]
    [InlineData("broken_anchor")]
    [InlineData("escape")]
    [InlineData("fenced_anchor")]
    public void MissingCopyAndBrokenLinkFixturesFailSpecificClosureGuards(string mutation)
    {
        var docs = ClosureSeed();
        Assert.Empty(GmDocumentationDeliveryRules.VerifyClosure(docs));
        string expected;
        if (mutation == "fenced_anchor")
        {
            docs["OtherGuides/Afterlife_Lifecycle.md"] = Encoding.UTF8.GetBytes(
                "# Anchor\n# Anchor\n<a id='real-alias'></a>\n```markdown\n# Only fenced\n```\n~~~html\n<a id='fenced-alias'></a>\n~~~\n");
            docs["OtherGuides/GM_Common_Core.md"] = Encoding.UTF8.GetBytes(
                "# Anchor\n[real](Afterlife_Lifecycle.md#anchor)\n[duplicate](Afterlife_Lifecycle.md#anchor-1)\n[alias](Afterlife_Lifecycle.md#real-alias)\n");
            Assert.Empty(GmDocumentationDeliveryRules.VerifyClosure(docs));
            docs["OtherGuides/GM_Common_Core.md"] = docs["OtherGuides/GM_Common_Core.md"].Concat(Encoding.UTF8.GetBytes(
                "[fenced heading](Afterlife_Lifecycle.md#only-fenced)\n[fenced alias](Afterlife_Lifecycle.md#fenced-alias)\n")).ToArray();
            var issues = GmDocumentationDeliveryRules.VerifyClosure(docs);
            Assert.Equal(2, issues.Count);
            Assert.Contains("delivery.anchor_missing:Afterlife_Lifecycle.md#only-fenced", issues);
            Assert.Contains("delivery.anchor_missing:Afterlife_Lifecycle.md#fenced-alias", issues);
            return;
        }
        if (mutation == "missing_copy") { docs.Remove("OtherGuides/Afterlife_Lifecycle.md"); expected = "delivery.copy_missing:"; }
        else
        {
            var target = mutation == "escape" ? "../../outside.md" : "Afterlife_Lifecycle.md#missing";
            docs["OtherGuides/GM_Common_Core.md"] = Encoding.UTF8.GetBytes("# Anchor\n[required](" + target + ")\n");
            expected = mutation == "escape" ? "delivery.link_escape:" : "delivery.anchor_missing:";
        }
        Assert.Contains(GmDocumentationDeliveryRules.VerifyClosure(docs), issue => issue.StartsWith(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void PhysicalLinkVerifierRefusesSymlinkEscape()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm1557-link-" + Guid.NewGuid().ToString("N"));
        try
        {
            var docs = ClosureSeed();
            foreach (var pair in docs)
            {
                var path = Path.Combine(root, "pack", pair.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, pair.Value);
            }
            var outside = Path.Combine(root, "outside.md"); File.WriteAllText(outside, "# Anchor\n");
            var link = Path.Combine(root, "pack/OtherGuides/Afterlife_Lifecycle.md"); File.Delete(link);
            File.CreateSymbolicLink(link, outside);
            Assert.Empty(GmDocumentationDeliveryRules.VerifyClosure(docs));
            Assert.Contains(GmDocumentationDeliveryRules.VerifyClosure(docs, Path.Combine(root, "pack")),
                issue => issue.StartsWith("delivery.link_symlink:", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            _output.WriteLine(JsonSerializer.Serialize(new { CleanupOwnedRoot = root, Removed = !Directory.Exists(root) }));
        }
        Assert.False(Directory.Exists(root));
    }

    private static Dictionary<string, byte[]> ClosureSeed()
    {
        // Owned memory verifier fixture, never masquerades as production writer or delivered bytes.
        var docs = GmDocumentationDeliveryScenario.Expected.ToDictionary(doc => doc.Path, _ => Encoding.UTF8.GetBytes("# Anchor\n"), StringComparer.Ordinal);
        docs["CLI_Rules_Index.md"] = Encoding.UTF8.GetBytes("# Anchor\n" + string.Join('\n', GmDocumentationDeliveryScenario.Expected
            .Where(doc => doc.Role.StartsWith("afterlife_rules_", StringComparison.Ordinal) || doc.Role.StartsWith("afterlife_examples_", StringComparison.Ordinal))
            .Select(doc => "`" + doc.Path + "`")));
        docs["OtherGuides/GM_Common_Core.md"] = Encoding.UTF8.GetBytes("# Anchor\n[required](Afterlife_Lifecycle.md#anchor)\n");
        return docs;
    }
    private static byte[] MutateSource(byte[] bytes, string? mutation)
    {
        var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        // Exact source-only seed clarification: current missing-router/old bootstrap-exception directions are not executed.
        const string docStart = "$script:GmDocPathDirective = \" GM documentation paths are session-local and authoritative:";
        Assert.Equal(1, text.Split(docStart, StringSplitOptions.None).Length - 1);
        text = text.Replace(docStart, docStart + " Conditional situation router CLI_Rules_Index.md selects only applicable topic/dependency union.", StringComparison.Ordinal)
            .Replace("or repository source unless validation later names an exact surface", "or repository source", StringComparison.Ordinal);
        if (mutation == "repair_ban")
        {
            var start = text.IndexOf("$message = \"REPAIR MODE", StringComparison.Ordinal);
            var end = text.IndexOf('\n', start); var line = text[start..end];
            Assert.Contains("$($script:GmSourceFallbackDirective)", line, StringComparison.Ordinal);
            text = text[..start] + line.Replace("$($script:GmSourceFallbackDirective)", string.Empty, StringComparison.Ordinal) + text[end..];
        }
        if (mutation == "late_override") text += "\n$script:GmSourceFallbackDirective = 'Read implementation code during repair.'\n";
        if (mutation == "late_empty_reset") text += "\n$script:GmSourceFallbackDirective = \"\"\n";
        if (mutation == "receipt_post_builder")
        {
            const string receiptAssignment = "$message = Build-QteEffectResolutionDispatchMessage -Request $request";
            Assert.Equal(1, text.Split(receiptAssignment, StringSplitOptions.None).Length - 1);
            text = text.Replace(receiptAssignment, receiptAssignment + "\n        $message += $script:GmDocPathDirective", StringComparison.Ordinal);
        }
        if (mutation == "read_all") text = text.Replace("Conditional situation router CLI_Rules_Index.md selects only applicable topic/dependency union.",
            "Read all modules every turn; CLI_Rules_Index.md.", StringComparison.Ordinal);
        if (mutation == "receipt_ordinary") text = text.Replace("Use only safePacket.requests from that exact request.",
            "Read OtherGuides/GM_Common_Core.md and input\\turn_request.json.\nUse only safePacket.requests from that exact request.", StringComparison.Ordinal);
        if (mutation == "wrong_boundary") text = text.Replace("    Write-GmTurnHelperBootstrap\n    Write-GmContextPack\n}",
            "    Write-GmTurnHelperBootstrap\n    Write-GmContextPack\n    Write-GmContextPack\n}", StringComparison.Ordinal);
        return Encoding.UTF8.GetPreamble().Concat(new UTF8Encoding(false, true).GetBytes(text)).ToArray();
    }
}
