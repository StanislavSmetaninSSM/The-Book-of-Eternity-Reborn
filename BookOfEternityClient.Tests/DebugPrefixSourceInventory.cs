using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Reverses only the reviewed DFR extraction before all unchanged historical source guards.</summary>
internal static class DebugPrefixSourceInventory
{
    internal static string ReverseIfPresent(string source)
    {
        source = SpiritualHealingInterfaceSourceInventory.ReverseIfPresent(source);
        if (source.Contains("var prefix = await EvaluateAcceptedReasoningPrefixAsync(null);", StringComparison.Ordinal))
        {
            AssertSharedPrefix();
            foreach (var (current, previous) in Replacements)
            {
                Assert.Equal(1, source.Split(current, StringSplitOptions.None).Length - 1);
                source = source.Replace(current, previous, StringComparison.Ordinal);
            }
        }
        Assert.Equal("4a6318c866ebd7d60855a3a96f9b1b3ba8464879dbf68c69e593f1d797b5cb8f",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant());
        return source;
    }

    private static void AssertSharedPrefix()
    {
        var source = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,
            "BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnReasoningPrefix.cs"));
        var first = source.IndexOf("        var issues = new List<ValidationIssue>();", StringComparison.Ordinal);
        var last = source.IndexOf("        source?.Policy(\"output/debug_logs.json\", \"scope-result\", JsonSerializer.Serialize(scope));", StringComparison.Ordinal);
        Assert.True(first >= 0 && last > first);
        var body = source[first..last];
        Assert.Equal(HelperHeader + body + HelperSuffix, source);
        foreach (var (current, previous, count) in PrefixAdapters)
        {
            Assert.Equal(count, body.Split(current, StringSplitOptions.None).Length - 1);
            body = body.Replace(current, previous, StringComparison.Ordinal);
        }
        Assert.Equal(FrozenPrefix, OrdinaryHeader + body);
    }

    private static readonly string HelperHeader =
"""""""
using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private sealed record ReasoningPrefixEvaluation(List<ValidationIssue> Issues, bool Terminal,
        string? NormalizedThoughts, ReasoningScopeManifest? Scope, ReasoningScopeMode ScopeMode, string ActorType);

    private async Task<ReasoningPrefixEvaluation> EvaluateAcceptedReasoningPrefixAsync(DebugPrefixSourceSession? source)
    {
        using var evaluationTiming = _fs.MeasureOperation("ReasoningPrefix.Evaluate");
""""""" + "\n";

    private static readonly string HelperSuffix =
"""""""
        source?.Policy("output/debug_logs.json", "scope-result", JsonSerializer.Serialize(scope));
        return new(issues, false, normalizedThoughts, scope, scopeMode, actorType);
    }
}
""""""" + "\n";

    private static readonly string OrdinaryHeader =
"""""""
    private async Task<List<ValidationIssue>> ValidateAcceptedTurnReasoningInternalAsync(
        AcceptedTurnReasoningValidationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
""""""" + "\n";

    private static readonly string FrozenPrefix =
"""""""
    private async Task<List<ValidationIssue>> ValidateAcceptedTurnReasoningInternalAsync(
        AcceptedTurnReasoningValidationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnTransientOutputFreshnessAsync(
            "output/debug_logs.json",
            "gm_thoughts_markdown",
            "accepted_turn_stale_debug_logs",
            "output/debug_logs.json must be freshly rewritten for the current accepted turn",
            "Не переиспользуй старый output/debug_logs.json. Перезапиши debug_logs.json заново для текущего accepted turn вместе с актуальным gm_thoughts_markdown.",
            issues);
        var debugJson = await _fs.ReadFileAsync("output/debug_logs.json");
        if (string.IsNullOrWhiteSpace(debugJson))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "Отсутствует gm_thoughts_markdown при обязательной проверке actor reasoning scope",
                code: "missing_gm_thoughts",
                section: "gm_thoughts_markdown",
                expected: "gm_thoughts_markdown with NPC scope declaration",
                actual: "missing or empty",
                repairHint: "Добавь debug_logs.json.gm_thoughts_markdown с секцией 'Охват NPC-анализа' и reasoning blocks."));
            return issues;
        }

        string gmThoughts;
        try
        {
            using var doc = JsonDocument.Parse(debugJson);
            if (!EvaluateAcceptedReasoningDocumentPrefix(doc.RootElement, issues, out gmThoughts))
                return issues;

            ValidateRequiredIsoTimestampField(
                doc.RootElement,
                "output/debug_logs.json",
                issues,
                "timestamp",
                "gm_thoughts_markdown",
                "debug_logs_missing_timestamp",
                "debug_logs_invalid_timestamp",
                "Добавь в output/debug_logs.json поле timestamp в ISO 8601 формате вместе с актуальным gm_thoughts_markdown.");

            AppendAcceptedReasoningUnknownFields(doc.RootElement, issues);
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                $"output/debug_logs.json должен быть валидным JSON: {ex.Message}",
                code: "invalid_debug_logs_json",
                section: "gm_thoughts_markdown",
                expected: "valid JSON object with gm_thoughts_markdown and timestamp",
                actual: "invalid JSON",
                repairHint: "Исправь output/debug_logs.json и оставь в нём валидный JSON-объект с полями gm_thoughts_markdown и timestamp."));
            return issues;
        }

        if (!EvaluateAcceptedReasoningScopePrefix(gmThoughts, issues, out var normalizedThoughts,
                out var scope, out var scopeMode, out var actorType))
            return issues;

""""""" + "\n";

    private static readonly (string Current, string Previous, int Count)[] PrefixAdapters =
    [
        ("            issues, source);\n", "            issues);\n", 1),
        ("var debugJson = source is null ? await _fs.ReadFileAsync(\"output/debug_logs.json\") : await source.ReadDebugForParseAsync();", "var debugJson = await _fs.ReadFileAsync(\"output/debug_logs.json\");", 1),
        ("            source?.Policy(\"output/debug_logs.json\", \"document-kind\", \"MissingOrBlank\");\n", "", 1),
        ("            source?.Policy(\"output/debug_logs.json\", \"document-kind\", doc.RootElement.ValueKind.ToString());\n", "", 1),
        ("            var timestampIssueStart = issues.Count;\n            source?.BeginTimestampPolicy(doc.RootElement);\n", "", 1),
        ("            source?.EndTimestampPolicy(issues.Skip(timestampIssueStart));\n", "", 1),
        ("            source?.Policy(\"output/debug_logs.json\", \"domain-catch\", ex.GetType().Name);\n", "", 1),
        ("return new(issues, true, null, null, default, string.Empty);", "return issues;", 4),
    ];

    private static readonly (string Current, string Previous)[] Replacements =
    [
        (
"""""""
            : string.Empty;
    }

    private async Task<ValidationPendingTurnSnapshotManifest?> LoadValidationPendingTurnSnapshotManifestAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null, OriginalLookupObservation? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        var json = await ReadAcceptedTurnValidationFileAsync(
            PendingTurnSnapshotManifestPath,
""""""" + "\n",
"""""""
            : string.Empty;
    }

    private async Task<ValidationPendingTurnSnapshotManifest?> LoadValidationPendingTurnSnapshotManifestAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null, NarrativeInterfaceSourceSession? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        var json = await ReadAcceptedTurnValidationFileAsync(
            PendingTurnSnapshotManifestPath,
""""""" + "\n"
        ),
        (
"""""""
        string section,
        string code,
        string expected,
        string repairHint,
        List<ValidationIssue> issues, OriginalLookupObservation? source = null)
    {
        var preTurnContent = source is null && relativePath == "output/debug_logs.json"
            ? await ReadAcceptedTurnReasoningOriginalDebugAsync()
            : source is null ? await ReadValidatedCurrentPreTurnTrackedFileCoreAsync(relativePath, null, null) : await source.ReadOriginalTextAsync(this, relativePath);
        if (preTurnContent == null)
            return;

        var currentContent = (source is null ? await _fs.ReadFileAsync(relativePath) : await source.ReadCurrentTextAsync(relativePath)) ?? string.Empty;
        if (!string.Equals(currentContent, preTurnContent, StringComparison.Ordinal))
            return;

        issues.Add(new ValidationIssue(
""""""" + "\n",
"""""""
        string section,
        string code,
        string expected,
        string repairHint,
        List<ValidationIssue> issues, NarrativeInterfaceSourceSession? source = null)
    {
        var preTurnContent = await ReadValidatedCurrentPreTurnTrackedFileAsync(relativePath, source);
        if (preTurnContent == null)
            return;

        var currentContent = await ReadNarrativeSourceTextAsync(relativePath, source) ?? string.Empty;
        if (!string.Equals(currentContent, preTurnContent, StringComparison.Ordinal))
            return;

        issues.Add(new ValidationIssue(
""""""" + "\n"
        ),
        (
"""""""
    {
        return ReadValidatedCurrentPreTurnTrackedFileSync(relativePath);
    }

    private Task<string?> ReadValidatedCurrentPreTurnTrackedFileAsync(string relativePath, NarrativeInterfaceSourceSession? source = null) =>
        ReadValidatedCurrentPreTurnTrackedFileCoreAsync(relativePath, null, source);

    private async Task<string?> ReadValidatedCurrentPreTurnTrackedFileCoreAsync(string relativePath,
        FileSystemManager.CanonicalWriteLease? writeLease, OriginalLookupObservation? source)
    {
        try
        {
            var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync(writeLease, source, narrativeRolePath: relativePath);
            source?.Policy(relativePath, "original-lookup", lookup.Status.ToString());
            if (lookup.Status == ValidatedPendingTurnSnapshotStatus.Unusable) source?.Unavailable("narrative_policy_original_lookup_unusable");
            return lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable && lookup.Manifest != null
                ? await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, relativePath, writeLease, source)
                : null;
        }
        catch (Exception error) { source?.Fault(error); throw; }
    }
""""""" + "\n",
"""""""
    {
        return ReadValidatedCurrentPreTurnTrackedFileSync(relativePath);
    }

    private async Task<string?> ReadValidatedCurrentPreTurnTrackedFileAsync(string relativePath, NarrativeInterfaceSourceSession? source = null)
    {
        try
        {
            var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync(source: source, narrativeRolePath: relativePath);
            source?.Policy(relativePath, "original-lookup", lookup.Status.ToString());
            if (lookup.Status == ValidatedPendingTurnSnapshotStatus.Unusable) source?.Unavailable("narrative_policy_original_lookup_unusable");
            return lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable && lookup.Manifest != null
                ? await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, relativePath, source: source)
                : null;
        }
        catch (Exception error) { source?.Fault(error); throw; }
    }
""""""" + "\n"
        ),
        (
"""""""

    private async Task<string?> ReadValidatedPendingTurnSnapshotFileAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        string relativePath,
        FileSystemManager.CanonicalWriteLease? writeLease = null, OriginalLookupObservation? source = null)
    {
        if (manifest.Files == null ||
            !manifest.Files.TryGetValue(relativePath, out var snapshotPath))
        {
""""""" + "\n",
"""""""

    private async Task<string?> ReadValidatedPendingTurnSnapshotFileAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        string relativePath,
        FileSystemManager.CanonicalWriteLease? writeLease = null, NarrativeInterfaceSourceSession? source = null)
    {
        if (manifest.Files == null ||
            !manifest.Files.TryGetValue(relativePath, out var snapshotPath))
        {
""""""" + "\n"
        ),
        (
"""""""
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    /// <param name="narrativeRolePath">The originating closed narrative logical role for nested authority reads.</param>
    private PendingTurnRequestValidationContext? LoadPendingTurnRequestValidationContextSync(
        string requestPath,
        FileSystemManager.CanonicalWriteLease? writeLease = null, OriginalLookupObservation? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        var json = source is not null
            ? ReadNarrativeAuxiliaryTextSync(narrativeRolePath, requestPath, writeLease, source)
""""""" + "\n",
"""""""
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    /// <param name="narrativeRolePath">The originating closed narrative logical role for nested authority reads.</param>
    private PendingTurnRequestValidationContext? LoadPendingTurnRequestValidationContextSync(
        string requestPath,
        FileSystemManager.CanonicalWriteLease? writeLease = null, NarrativeInterfaceSourceSession? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        var json = source is not null
            ? ReadNarrativeAuxiliaryTextSync(narrativeRolePath, requestPath, writeLease, source)
""""""" + "\n"
        ),
        (
"""""""
    /// </returns>
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    /// <param name="narrativeRolePath">The originating closed narrative logical role for nested authority reads.</param>
    private async Task<ValidatedPendingTurnSnapshotLookup> LoadValidatedPendingTurnSnapshotLookupAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null, OriginalLookupObservation? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        if (CurrentPendingSnapshotLookup(writeLease) is { } scoped)
        {
            source?.ScopedLookup();
            if (scoped.Manifest is { } supplied) RecordNarrativeOriginalContext(source, supplied, "ScopedData");
            return scoped;
        }
        if (_prevalidatedPendingTurnSnapshotOverride != null)
""""""" + "\n",
"""""""
    /// </returns>
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    /// <param name="narrativeRolePath">The originating closed narrative logical role for nested authority reads.</param>
    private async Task<ValidatedPendingTurnSnapshotLookup> LoadValidatedPendingTurnSnapshotLookupAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null, NarrativeInterfaceSourceSession? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        if (CurrentPendingSnapshotLookup(writeLease) is { } scoped)
        {
            source?.Unavailable("narrative_policy_scoped_lookup_provenance_unavailable");
            if (scoped.Manifest is { } supplied) RecordNarrativeOriginalContext(source, supplied, "ScopedData");
            return scoped;
        }
        if (_prevalidatedPendingTurnSnapshotOverride != null)
""""""" + "\n"
        ),
        (
"""""""
            RecordNarrativeOriginalContext(source, _prevalidatedPendingTurnSnapshotOverride, "SuppliedPrevalidatedData");
            return new ValidatedPendingTurnSnapshotLookup(ValidatedPendingTurnSnapshotStatus.Usable, _prevalidatedPendingTurnSnapshotOverride);
        }

        var presence = source?.BeginRead(narrativeRolePath, PendingTurnSnapshotManifestPath, "lookup-presence");
        bool manifestExists;
        try
        {
            manifestExists = writeLease == null
                ? _fs.FileExists(PendingTurnSnapshotManifestPath)
                : _fs.FileExists(writeLease, PendingTurnSnapshotManifestPath);
            presence?.Presence(manifestExists);
        }
        catch (Exception error) { presence?.Fault(error); throw; }
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync(writeLease, source, narrativeRolePath);
        var authorityJson = await ReadAcceptedTurnValidationFileAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
            writeLease, source, narrativeRolePath);
""""""" + "\n",
"""""""
            RecordNarrativeOriginalContext(source, _prevalidatedPendingTurnSnapshotOverride, "SuppliedPrevalidatedData");
            return new ValidatedPendingTurnSnapshotLookup(ValidatedPendingTurnSnapshotStatus.Usable, _prevalidatedPendingTurnSnapshotOverride);
        }

        var manifestExists = writeLease == null
            ? _fs.FileExists(PendingTurnSnapshotManifestPath)
            : _fs.FileExists(writeLease, PendingTurnSnapshotManifestPath);
        source?.BeginRead(narrativeRolePath, PendingTurnSnapshotManifestPath, "lookup-presence").Presence(manifestExists);
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync(writeLease, source, narrativeRolePath);
        var authorityJson = await ReadAcceptedTurnValidationFileAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
            writeLease, source, narrativeRolePath);
""""""" + "\n"
        ),
        (
"""""""
            manifest);
    }

    private async Task<PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload?> LoadCurrentDetachedPendingTurnSnapshotAuthorityPayloadAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null, OriginalLookupObservation? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        var authorityJson = await ReadAcceptedTurnValidationFileAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
""""""" + "\n",
"""""""
            manifest);
    }

    private async Task<PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload?> LoadCurrentDetachedPendingTurnSnapshotAuthorityPayloadAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null, NarrativeInterfaceSourceSession? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        var authorityJson = await ReadAcceptedTurnValidationFileAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
""""""" + "\n"
        ),
        (
"""""""
    }

    private bool IsCurrentDetachedPendingTurnSnapshotAuthorityPayload(
        PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload payload,
        FileSystemManager.CanonicalWriteLease? writeLease = null, OriginalLookupObservation? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        const string repairRequestPath = "game_state/control/validation_repair_request.json";
        var repairContext = LoadPendingTurnRequestValidationContextSync(
""""""" + "\n",
"""""""
    }

    private bool IsCurrentDetachedPendingTurnSnapshotAuthorityPayload(
        PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload payload,
        FileSystemManager.CanonicalWriteLease? writeLease = null, NarrativeInterfaceSourceSession? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        const string repairRequestPath = "game_state/control/validation_repair_request.json";
        var repairContext = LoadPendingTurnRequestValidationContextSync(
""""""" + "\n"
        ),
        (
"""""""
    }

    private Task<string?> ReadAcceptedTurnValidationFileAsync(
        string path,
        FileSystemManager.CanonicalWriteLease? writeLease, OriginalLookupObservation? source = null,
        string narrativeRolePath = "output/narrative_response.json") =>
        source is not null ? ReadNarrativeAuxiliaryTextAsync(narrativeRolePath, path, writeLease, source) :
        writeLease == null
            ? CurrentWorldReadPhase() is { } phase ? phase.ReadTextAsync(path) : _fs.ReadFileAsync(path)
""""""" + "\n",
"""""""
    }

    private Task<string?> ReadAcceptedTurnValidationFileAsync(
        string path,
        FileSystemManager.CanonicalWriteLease? writeLease, NarrativeInterfaceSourceSession? source = null,
        string narrativeRolePath = "output/narrative_response.json") =>
        source is not null ? ReadNarrativeAuxiliaryTextAsync(narrativeRolePath, path, writeLease, source) :
        writeLease == null
            ? CurrentWorldReadPhase() is { } phase ? phase.ReadTextAsync(path) : _fs.ReadFileAsync(path)
""""""" + "\n"
        ),
        (
"""""""
    private async Task<List<ValidationIssue>> ValidateAcceptedTurnReasoningInternalAsync(
        AcceptedTurnReasoningValidationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var prefix = await EvaluateAcceptedReasoningPrefixAsync(null);
        var issues = prefix.Issues;
        if (prefix.Terminal) return issues;
        var normalizedThoughts = prefix.NormalizedThoughts!;
        var scope = prefix.Scope!;
        var scopeMode = prefix.ScopeMode;
        var actorType = prefix.ActorType;

        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        var guardianIdentityContext = BuildGuardianReasoningIdentityContext(guardianPolicyContext);
        CoalesceCanonicalGuardianAliasesInScope(scope, guardianIdentityContext);
""""""" + "\n",
"""""""
    private async Task<List<ValidationIssue>> ValidateAcceptedTurnReasoningInternalAsync(
        AcceptedTurnReasoningValidationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnTransientOutputFreshnessAsync(
            "output/debug_logs.json",
            "gm_thoughts_markdown",
            "accepted_turn_stale_debug_logs",
            "output/debug_logs.json must be freshly rewritten for the current accepted turn",
            "Не переиспользуй старый output/debug_logs.json. Перезапиши debug_logs.json заново для текущего accepted turn вместе с актуальным gm_thoughts_markdown.",
            issues);
        var debugJson = await _fs.ReadFileAsync("output/debug_logs.json");
        if (string.IsNullOrWhiteSpace(debugJson))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "Отсутствует gm_thoughts_markdown при обязательной проверке actor reasoning scope",
                code: "missing_gm_thoughts",
                section: "gm_thoughts_markdown",
                expected: "gm_thoughts_markdown with NPC scope declaration",
                actual: "missing or empty",
                repairHint: "Добавь debug_logs.json.gm_thoughts_markdown с секцией 'Охват NPC-анализа' и reasoning blocks."));
            return issues;
        }

        string gmThoughts;
        try
        {
            using var doc = JsonDocument.Parse(debugJson);
            if (!EvaluateAcceptedReasoningDocumentPrefix(doc.RootElement, issues, out gmThoughts))
                return issues;

            ValidateRequiredIsoTimestampField(
                doc.RootElement,
                "output/debug_logs.json",
                issues,
                "timestamp",
                "gm_thoughts_markdown",
                "debug_logs_missing_timestamp",
                "debug_logs_invalid_timestamp",
                "Добавь в output/debug_logs.json поле timestamp в ISO 8601 формате вместе с актуальным gm_thoughts_markdown.");

            AppendAcceptedReasoningUnknownFields(doc.RootElement, issues);
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                $"output/debug_logs.json должен быть валидным JSON: {ex.Message}",
                code: "invalid_debug_logs_json",
                section: "gm_thoughts_markdown",
                expected: "valid JSON object with gm_thoughts_markdown and timestamp",
                actual: "invalid JSON",
                repairHint: "Исправь output/debug_logs.json и оставь в нём валидный JSON-объект с полями gm_thoughts_markdown и timestamp."));
            return issues;
        }

        if (!EvaluateAcceptedReasoningScopePrefix(gmThoughts, issues, out var normalizedThoughts,
                out var scope, out var scopeMode, out var actorType))
            return issues;

        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        var guardianIdentityContext = BuildGuardianReasoningIdentityContext(guardianPolicyContext);
        CoalesceCanonicalGuardianAliasesInScope(scope, guardianIdentityContext);
""""""" + "\n"
        ),
    ];
}
