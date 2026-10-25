using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Exact inverse of only T587 explicit source plumbing and passive scopes. The frozen ordinary hash stays unchanged.</summary>
internal static class NarrativePolicySourceInventory
{
    /// <summary>Restores the reviewed pre-T587 source before the existing reasoning and policy inverses run.</summary>
    /// <param name="source">The exact current narrative implementation source.</param>
    /// <returns>The pre-T587 source, requiring every changed adapter to occur exactly once.</returns>
    internal static string ReverseIfPresent(string source)
    {
        if (!source.Contains("bool readOnly = false, NarrativeInterfaceSourceSession? source = null", StringComparison.Ordinal)) return source;
        foreach (var (current, previous) in Replacements)
        {
            Assert.Equal(1, source.Split(current, StringSplitOptions.None).Length - 1);
            source = source.Replace(current, previous, StringComparison.Ordinal);
        }
        return source;
    }

    private static readonly (string Current, string Previous)[] Replacements =
    [
        (
"""""""
        }
    }

    private async Task<List<ValidationIssue>> ValidateAcceptedTurnNarrativePayloadInternalAsync(bool readOnly = false, NarrativeInterfaceSourceSession? source = null)
    {
        using var evaluationTiming = _fs.MeasureOperation("NarrativePolicy.Evaluate");
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnTransientOutputFreshnessAsync(
            "output/narrative_response.json",
""""""" + "\n",
"""""""
        }
    }

    private async Task<List<ValidationIssue>> ValidateAcceptedTurnNarrativePayloadInternalAsync(bool readOnly = false)
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnTransientOutputFreshnessAsync(
            "output/narrative_response.json",
""""""" + "\n"
        ),
        (
"""""""
            "accepted_turn_stale_narrative_response",
            "output/narrative_response.json must be freshly rewritten for the current accepted turn",
            "Не переиспользуй старый output/narrative_response.json. Перезапиши narrative_response.json свежим ответом именно для текущего accepted turn.",
            issues, source);
        var json = await ReadNarrativeSourceTextAsync("output/narrative_response.json", source);
        if (string.IsNullOrWhiteSpace(json))
        {
            source?.Policy("output/narrative_response.json", "document-kind", "NotReachedMissingOrBlank");
            issues.Add(new ValidationIssue(
                "output/narrative_response.json",
                IssueSeverity.Error,
""""""" + "\n",
"""""""
            "accepted_turn_stale_narrative_response",
            "output/narrative_response.json must be freshly rewritten for the current accepted turn",
            "Не переиспользуй старый output/narrative_response.json. Перезапиши narrative_response.json свежим ответом именно для текущего accepted turn.",
            issues);
        var json = await _fs.ReadFileAsync("output/narrative_response.json");
        if (string.IsNullOrWhiteSpace(json))
        {
            issues.Add(new ValidationIssue(
                "output/narrative_response.json",
                IssueSeverity.Error,
""""""" + "\n"
        ),
        (
"""""""
                    code: "mortal_joint_sealed_narrative_requires_normalization"));
        }
        else
            json = await NormalizeAcceptedTurnNarrativeLineBreakArtifactsAsync("output/narrative_response.json", json, source);

        var documentParsed = false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            documentParsed = true;
            source?.Policy("output/narrative_response.json", "document-kind", doc.RootElement.ValueKind.ToString());
            if (!EvaluateAcceptedNarrativePrefix(doc.RootElement, issues, out var responseText))
                return issues;

            var timestampIssueStart = issues.Count;
            source?.BeginTimestampPolicy();
            ValidateRequiredIsoTimestampField(
                doc.RootElement,
                "output/narrative_response.json",
""""""" + "\n",
"""""""
                    code: "mortal_joint_sealed_narrative_requires_normalization"));
        }
        else
            json = await NormalizeAcceptedTurnNarrativeLineBreakArtifactsAsync("output/narrative_response.json", json);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!EvaluateAcceptedNarrativePrefix(doc.RootElement, issues, out var responseText))
                return issues;

            ValidateRequiredIsoTimestampField(
                doc.RootElement,
                "output/narrative_response.json",
""""""" + "\n"
        ),
        (
"""""""
                "narrative_response_invalid_timestamp",
                "Добавь в output/narrative_response.json поле timestamp в ISO 8601 формате вместе со свежим response для текущего accepted turn.");

            source?.ReturnedTimestampPolicy(issues, timestampIssueStart);
            EvaluateAcceptedNarrativeTail(doc.RootElement, responseText, issues);

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                await ValidateAchievementUnlockNarrativeMarkersAsync(responseText, issues, source);
                await ValidateAcceptedTurnProseStateDeltasAsync(responseText, issues, source);
            }
        }
        catch (JsonException)
        {
            source?.Policy("output/narrative_response.json", "narrative-domain-catch",
                documentParsed ? "DownstreamJsonException" : "InvalidJson");
            issues.Add(new ValidationIssue(
                "output/narrative_response.json",
                IssueSeverity.Error,
""""""" + "\n",
"""""""
                "narrative_response_invalid_timestamp",
                "Добавь в output/narrative_response.json поле timestamp в ISO 8601 формате вместе со свежим response для текущего accepted turn.");

            EvaluateAcceptedNarrativeTail(doc.RootElement, responseText, issues);

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                await ValidateAchievementUnlockNarrativeMarkersAsync(responseText, issues);
                await ValidateAcceptedTurnProseStateDeltasAsync(responseText, issues);
            }
        }
        catch (JsonException)
        {
            issues.Add(new ValidationIssue(
                "output/narrative_response.json",
                IssueSeverity.Error,
""""""" + "\n"
        ),
        (
"""""""
    /// <returns>
    /// The normalized JSON when changed; otherwise the exact original text.
    /// </returns>
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    private async Task<string> NormalizeAcceptedTurnNarrativeLineBreakArtifactsAsync(
        string narrativePath,
        string json, NarrativeInterfaceSourceSession? source = null)
    {
        using var projectionTiming = _fs.MeasureOperation("NarrativePolicy.ProjectNarrative");
        var projected = AcceptedTurnOutputProjector.ProjectNarrative(json);
        projectionTiming?.Dispose();
        source?.Projection(projected.Json, projected.Changed);
        if (projected.Changed)
        {
            var writer = source?.BeginWrite(narrativePath);
            try
            {
                if (writer is null) await _fs.WriteFileAtomicAsync(narrativePath, projected.Json);
                else await _fs.WriteFileAtomicAsync(narrativePath, projected.Json, writer);
            }
            catch (Exception error) { writer?.Fault(error); throw; }
        }
        return projected.Json;
    }

""""""" + "\n",
"""""""
    /// <returns>
    /// The normalized JSON when changed; otherwise the exact original text.
    /// </returns>
    private async Task<string> NormalizeAcceptedTurnNarrativeLineBreakArtifactsAsync(
        string narrativePath,
        string json)
    {
        var projected = AcceptedTurnOutputProjector.ProjectNarrative(json);
        if (projected.Changed)
            await _fs.WriteFileAtomicAsync(narrativePath, projected.Json);
        return projected.Json;
    }

""""""" + "\n"
        ),
        (
"""""""
        return false;
    }

    private async Task ValidateAcceptedTurnProseStateDeltasAsync(string narrativeText, List<ValidationIssue> issues, NarrativeInterfaceSourceSession? source = null)
    {
        var playerFacingText = await BuildAcceptedTurnPlayerFacingTextForDeltaAuditAsync(narrativeText, source);
        if (string.IsNullOrWhiteSpace(playerFacingText))
            return;

        var filesModified = await ReadAcceptedTurnFilesModifiedAsync(source);
        var noProgressRationaleText = await ReadAcceptedTurnNoProgressRationaleTextAsync(source);

        var knownSkillNames = await ReadCurrentKnownPlayerSkillNamesAsync(source);
        foreach (var skillName in knownSkillNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (!ContainsSkillUseClaim(playerFacingText, skillName))
                continue;

            var hasDelta = await HasAcceptedTurnStateDeltaAsync(
                filesModified, source,
                "game_state/player/skill_mastery.json",
                "game_state/player/skills_active.json",
                "game_state/player/skills_passive.json");
""""""" + "\n",
"""""""
        return false;
    }

    private async Task ValidateAcceptedTurnProseStateDeltasAsync(string narrativeText, List<ValidationIssue> issues)
    {
        var playerFacingText = await BuildAcceptedTurnPlayerFacingTextForDeltaAuditAsync(narrativeText);
        if (string.IsNullOrWhiteSpace(playerFacingText))
            return;

        var filesModified = await ReadAcceptedTurnFilesModifiedAsync();
        var noProgressRationaleText = await ReadAcceptedTurnNoProgressRationaleTextAsync();

        var knownSkillNames = await ReadCurrentKnownPlayerSkillNamesAsync();
        foreach (var skillName in knownSkillNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (!ContainsSkillUseClaim(playerFacingText, skillName))
                continue;

            var hasDelta = await HasAcceptedTurnStateDeltaAsync(
                filesModified,
                "game_state/player/skill_mastery.json",
                "game_state/player/skills_active.json",
                "game_state/player/skills_passive.json");
""""""" + "\n"
        ),
        (
"""""""
                repairHint: $"Если '{skillName}' реально помог, добавь узкое изменение mastery/progression в game_state/player/skill_mastery.json или соответствующий skill state delta. Если прогресса быть не должно, добавь в output/debug_logs.json.gm_thoughts_markdown секцию 'Prose State Delta Rationale' с именем навыка и причиной отсутствия прогресса."));
        }

        var activeQuests = await ReadCurrentActiveRegularQuestIdentitiesAsync(source);
        foreach (var quest in activeQuests.OrderBy(q => q.Title, StringComparer.OrdinalIgnoreCase))
        {
            if (!ContainsQuestClueClaim(playerFacingText, quest.Title))
                continue;

            var hasDelta = await HasAcceptedTurnStateDeltaAsync(
                filesModified, source,
                "game_state/quests/regular_quests.json",
                "game_state/quests/quest_history.json",
                "game_state/quests/plot_outline.json");
""""""" + "\n",
"""""""
                repairHint: $"Если '{skillName}' реально помог, добавь узкое изменение mastery/progression в game_state/player/skill_mastery.json или соответствующий skill state delta. Если прогресса быть не должно, добавь в output/debug_logs.json.gm_thoughts_markdown секцию 'Prose State Delta Rationale' с именем навыка и причиной отсутствия прогресса."));
        }

        var activeQuests = await ReadCurrentActiveRegularQuestIdentitiesAsync();
        foreach (var quest in activeQuests.OrderBy(q => q.Title, StringComparer.OrdinalIgnoreCase))
        {
            if (!ContainsQuestClueClaim(playerFacingText, quest.Title))
                continue;

            var hasDelta = await HasAcceptedTurnStateDeltaAsync(
                filesModified,
                "game_state/quests/regular_quests.json",
                "game_state/quests/quest_history.json",
                "game_state/quests/plot_outline.json");
""""""" + "\n"
        ),
        (
"""""""
        }
    }

    private async Task<string> BuildAcceptedTurnPlayerFacingTextForDeltaAuditAsync(string narrativeText, NarrativeInterfaceSourceSession? source = null)
    {
        var builder = new StringBuilder(narrativeText ?? string.Empty);
        var interfaceJson = await ReadNarrativeSourceTextAsync("output/interface_updates.json", source);
        if (string.IsNullOrWhiteSpace(interfaceJson))
            return builder.ToString();

        try
        {
            using var doc = JsonDocument.Parse(interfaceJson);
            source?.Policy("output/interface_updates.json", "document-kind", doc.RootElement.ValueKind.ToString());
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return builder.ToString();

""""""" + "\n",
"""""""
        }
    }

    private async Task<string> BuildAcceptedTurnPlayerFacingTextForDeltaAuditAsync(string narrativeText)
    {
        var builder = new StringBuilder(narrativeText ?? string.Empty);
        var interfaceJson = await _fs.ReadFileAsync("output/interface_updates.json");
        if (string.IsNullOrWhiteSpace(interfaceJson))
            return builder.ToString();

        try
        {
            using var doc = JsonDocument.Parse(interfaceJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return builder.ToString();

""""""" + "\n"
        ),
        (
"""""""
                }
            }
        }
        catch (JsonException error)
        {
            source?.Policy("output/interface_updates.json", "domain-catch", error.GetType().Name);
            // Interface validation reports malformed JSON separately.
        }

        return builder.ToString();
    }

    private async Task<HashSet<string>> ReadCurrentKnownPlayerSkillNamesAsync(NarrativeInterfaceSourceSession? source = null)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        names.UnionWith(ParsePlayerSkillNames(await ReadNarrativeSourceTextAsync("game_state/player/skills_active.json", source), "activeSkillChanges"));
        names.UnionWith(ParsePlayerSkillNames(await ReadNarrativeSourceTextAsync("game_state/player/skills_passive.json", source), "passiveSkillChanges"));
        return names.Where(name => !string.IsNullOrWhiteSpace(name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<ProseQuestIdentity>> ReadCurrentActiveRegularQuestIdentitiesAsync(NarrativeInterfaceSourceSession? source = null)
    {
        var result = new List<ProseQuestIdentity>();
        var json = await ReadNarrativeSourceTextAsync("game_state/quests/regular_quests.json", source);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            source?.Policy("game_state/quests/regular_quests.json", "document-kind", doc.RootElement.ValueKind.ToString());
            foreach (var propName in new[] { "quests", "UpdateQuests" })
            {
                if (!doc.RootElement.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
""""""" + "\n",
"""""""
                }
            }
        }
        catch (JsonException)
        {
            // Interface validation reports malformed JSON separately.
        }

        return builder.ToString();
    }

    private async Task<HashSet<string>> ReadCurrentKnownPlayerSkillNamesAsync()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        names.UnionWith(ParsePlayerSkillNames(await _fs.ReadFileAsync("game_state/player/skills_active.json"), "activeSkillChanges"));
        names.UnionWith(ParsePlayerSkillNames(await _fs.ReadFileAsync("game_state/player/skills_passive.json"), "passiveSkillChanges"));
        return names.Where(name => !string.IsNullOrWhiteSpace(name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<ProseQuestIdentity>> ReadCurrentActiveRegularQuestIdentitiesAsync()
    {
        var result = new List<ProseQuestIdentity>();
        var json = await _fs.ReadFileAsync("game_state/quests/regular_quests.json");
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var propName in new[] { "quests", "UpdateQuests" })
            {
                if (!doc.RootElement.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
""""""" + "\n"
        ),
        (
"""""""
                }
            }
        }
        catch (JsonException error)
        {
            source?.Policy("game_state/quests/regular_quests.json", "domain-catch", error.GetType().Name);
            // Generic quest state validation reports malformed JSON separately.
        }

        return result;
    }

    private Task<bool> HasAcceptedTurnStateDeltaAsync(IReadOnlyCollection<string> filesModified, params string[] paths) =>
        HasAcceptedTurnStateDeltaAsync(filesModified, null, paths);

    private async Task<bool> HasAcceptedTurnStateDeltaAsync(IReadOnlyCollection<string> filesModified, NarrativeInterfaceSourceSession? source, params string[] paths)
    {
        var hasPreTurnSnapshotEvidence = false;

        foreach (var path in paths)
        {
            var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(path, source);
            if (preTurnJson == null)
                continue;

            hasPreTurnSnapshotEvidence = true;
            var currentJson = await ReadNarrativeSourceTextAsync(path, source) ?? string.Empty;
            if (!string.Equals(currentJson, preTurnJson, StringComparison.Ordinal))
                return true;
        }
""""""" + "\n",
"""""""
                }
            }
        }
        catch (JsonException)
        {
            // Generic quest state validation reports malformed JSON separately.
        }

        return result;
    }

    private async Task<bool> HasAcceptedTurnStateDeltaAsync(IReadOnlyCollection<string> filesModified, params string[] paths)
    {
        var hasPreTurnSnapshotEvidence = false;

        foreach (var path in paths)
        {
            var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(path);
            if (preTurnJson == null)
                continue;

            hasPreTurnSnapshotEvidence = true;
            var currentJson = await _fs.ReadFileAsync(path) ?? string.Empty;
            if (!string.Equals(currentJson, preTurnJson, StringComparison.Ordinal))
                return true;
        }
""""""" + "\n"
        ),
        (
"""""""
        return false;
    }

    private async Task<string> ReadAcceptedTurnNoProgressRationaleTextAsync(NarrativeInterfaceSourceSession? source = null)
    {
        var debugJson = await ReadNarrativeSourceTextAsync("output/debug_logs.json", source);
        if (string.IsNullOrWhiteSpace(debugJson))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(debugJson);
            source?.Policy("output/debug_logs.json", "document-kind", doc.RootElement.ValueKind.ToString());
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("gm_thoughts_markdown", out var gmThoughts) &&
                gmThoughts.ValueKind == JsonValueKind.String)
""""""" + "\n",
"""""""
        return false;
    }

    private async Task<string> ReadAcceptedTurnNoProgressRationaleTextAsync()
    {
        var debugJson = await _fs.ReadFileAsync("output/debug_logs.json");
        if (string.IsNullOrWhiteSpace(debugJson))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(debugJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("gm_thoughts_markdown", out var gmThoughts) &&
                gmThoughts.ValueKind == JsonValueKind.String)
""""""" + "\n"
        ),
        (
"""""""
                return gmThoughts.GetString() ?? string.Empty;
            }
        }
        catch (JsonException error)
        {
            source?.Policy("output/debug_logs.json", "domain-catch", error.GetType().Name);
            // Debug logs validation reports malformed JSON separately.
        }

""""""" + "\n",
"""""""
                return gmThoughts.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // Debug logs validation reports malformed JSON separately.
        }

""""""" + "\n"
        ),
        (
"""""""

    private async Task<List<ValidationIssue>> ValidateAcceptedTurnInterfacePayloadInternalAsync()
    {
        using var evaluationTiming = _fs.MeasureOperation("NarrativePolicy.EvaluateInterface");
        var issues = new List<ValidationIssue>();
        var interfacePath = "output/interface_updates.json";
        await ValidateAcceptedTurnTransientOutputFreshnessAsync(
""""""" + "\n",
"""""""

    private async Task<List<ValidationIssue>> ValidateAcceptedTurnInterfacePayloadInternalAsync()
    {
        var issues = new List<ValidationIssue>();
        var interfacePath = "output/interface_updates.json";
        await ValidateAcceptedTurnTransientOutputFreshnessAsync(
""""""" + "\n"
        ),
        (
"""""""
               text.Contains("combat", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<string>> ReadAcceptedTurnFilesModifiedAsync(NarrativeInterfaceSourceSession? source = null)
    {
        var result = new List<string>();
        var json = await ReadNarrativeSourceTextAsync("ready/turn_complete.json", source);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            source?.Policy("ready/turn_complete.json", "document-kind", doc.RootElement.ValueKind.ToString());
            if (!doc.RootElement.TryGetProperty("filesModified", out var files) ||
                files.ValueKind != JsonValueKind.Array)
            {
""""""" + "\n",
"""""""
               text.Contains("combat", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<string>> ReadAcceptedTurnFilesModifiedAsync()
    {
        var result = new List<string>();
        var json = await _fs.ReadFileAsync("ready/turn_complete.json");
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("filesModified", out var files) ||
                files.ValueKind != JsonValueKind.Array)
            {
""""""" + "\n"
        ),
        (
"""""""
                }
            }
        }
        catch (JsonException error)
        {
            source?.Policy("ready/turn_complete.json", "domain-catch", error.GetType().Name);
            return result;
        }

""""""" + "\n",
"""""""
                }
            }
        }
        catch (JsonException)
        {
            return result;
        }

""""""" + "\n"
        ),
        (
"""""""
    }

    private async Task<ValidationPendingTurnSnapshotManifest?> LoadValidationPendingTurnSnapshotManifestAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null, NarrativeInterfaceSourceSession? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        var json = await ReadAcceptedTurnValidationFileAsync(
            PendingTurnSnapshotManifestPath,
            writeLease, source, narrativeRolePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

""""""" + "\n",
"""""""
    }

    private async Task<ValidationPendingTurnSnapshotManifest?> LoadValidationPendingTurnSnapshotManifestAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        var json = await ReadAcceptedTurnValidationFileAsync(
            PendingTurnSnapshotManifestPath,
            writeLease);
        if (string.IsNullOrWhiteSpace(json))
            return null;

""""""" + "\n"
        ),
        (
"""""""
        {
            return JsonSerializer.Deserialize<ValidationPendingTurnSnapshotManifest>(json, ManifestJsonOpts);
        }
        catch (Exception error)
        {
            source?.Policy(narrativeRolePath, "original-manifest-parse", error.GetType().Name);
            return null;
        }
    }
""""""" + "\n",
"""""""
        {
            return JsonSerializer.Deserialize<ValidationPendingTurnSnapshotManifest>(json, ManifestJsonOpts);
        }
        catch
        {
            return null;
        }
    }
""""""" + "\n"
        ),
        (
"""""""
        return PendingTurnSnapshotAuthority.ComputeSha256(content);
    }

    private async Task<string?> ReadPreTurnTrackedFileAsync(string relativePath, NarrativeInterfaceSourceSession? source = null)
    {
        return await ReadValidatedCurrentPreTurnTrackedFileAsync(relativePath, source);
    }

    private static bool HasValidatedTrackedSnapshotRegistration(
""""""" + "\n",
"""""""
        return PendingTurnSnapshotAuthority.ComputeSha256(content);
    }

    private async Task<string?> ReadPreTurnTrackedFileAsync(string relativePath)
    {
        return await ReadValidatedCurrentPreTurnTrackedFileAsync(relativePath);
    }

    private static bool HasValidatedTrackedSnapshotRegistration(
""""""" + "\n"
        ),
        (
"""""""
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

""""""" + "\n",
"""""""
        string code,
        string expected,
        string repairHint,
        List<ValidationIssue> issues)
    {
        var preTurnContent = await ReadValidatedCurrentPreTurnTrackedFileAsync(relativePath);
        if (preTurnContent == null)
            return;

        var currentContent = await _fs.ReadFileAsync(relativePath) ?? string.Empty;
        if (!string.Equals(currentContent, preTurnContent, StringComparison.Ordinal))
            return;

""""""" + "\n"
        ),
        (
"""""""
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

    private string? ReadValidatedCurrentPreTurnTrackedFileSync(string relativePath)
""""""" + "\n",
"""""""
        return ReadValidatedCurrentPreTurnTrackedFileSync(relativePath);
    }

    private async Task<string?> ReadValidatedCurrentPreTurnTrackedFileAsync(string relativePath)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        return lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable && lookup.Manifest != null
            ? await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, relativePath)
            : null;
    }

    private string? ReadValidatedCurrentPreTurnTrackedFileSync(string relativePath)
""""""" + "\n"
        ),
        (
"""""""
    private async Task<string?> ReadValidatedPendingTurnSnapshotFileAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        string relativePath,
        FileSystemManager.CanonicalWriteLease? writeLease = null, NarrativeInterfaceSourceSession? source = null)
    {
        if (manifest.Files == null ||
            !manifest.Files.TryGetValue(relativePath, out var snapshotPath))
        {
            source?.Policy(relativePath, "original", manifest.OriginalPathPresenceV1?.TryGetValue(relativePath, out var existed) == true && !existed
                ? "ProvedAbsent" : "Uncovered");
            return null;
        }

        if (string.IsNullOrWhiteSpace(snapshotPath) || !PendingTurnSnapshotAuthority.IsSafeRelativePath(snapshotPath))
        {
            source?.Policy(relativePath, "original", "InvalidDeclaredArtifact");
            source?.Unavailable("narrative_policy_original_artifact_invalid");
            return null;
        }

""""""" + "\n",
"""""""
    private async Task<string?> ReadValidatedPendingTurnSnapshotFileAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        string relativePath,
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        if (manifest.Files == null ||
            !manifest.Files.TryGetValue(relativePath, out var snapshotPath) ||
            string.IsNullOrWhiteSpace(snapshotPath) ||
            !PendingTurnSnapshotAuthority.IsSafeRelativePath(snapshotPath))
        {
            return null;
        }

""""""" + "\n"
        ),
        (
"""""""
            !manifest.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
            string.IsNullOrWhiteSpace(expectedSnapshotHash))
        {
            source?.Policy(relativePath, "original", "MissingDeclaredHash");
            source?.Unavailable("narrative_policy_original_hash_missing");
            return null;
        }

        var snapshotBytes = source is null
            ? writeLease == null
                ? CurrentWorldReadPhase() is { } phase ? await phase.ReadBytesAsync(snapshotPath) : await _fs.ReadFileBytesAsync(snapshotPath)
                : await _fs.ReadFileBytesAsync(writeLease, snapshotPath)
            : await ReadNarrativeOriginalBytesAsync(relativePath, snapshotPath, writeLease, source);
        if (snapshotBytes == null)
        {
            source?.Policy(relativePath, "original", "MissingDeclaredArtifact");
            source?.Unavailable("narrative_policy_original_artifact_missing");
            return null;
        }

        var authorityPayload = await LoadCurrentDetachedPendingTurnSnapshotAuthorityPayloadAsync(
            writeLease, source, relativePath);
        if (authorityPayload == null &&
            !ReferenceEquals(manifest, _prevalidatedPendingTurnSnapshotOverride))
        {
            source?.Policy(relativePath, "original", "Unauthenticated");
            source?.Unavailable("narrative_policy_original_unauthenticated");
            return null;
        }

""""""" + "\n",
"""""""
            !manifest.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
            string.IsNullOrWhiteSpace(expectedSnapshotHash))
        {
            return null;
        }

        var snapshotBytes = writeLease == null
            ? CurrentWorldReadPhase() is { } phase ? await phase.ReadBytesAsync(snapshotPath) : await _fs.ReadFileBytesAsync(snapshotPath)
            : await _fs.ReadFileBytesAsync(writeLease, snapshotPath);
        if (snapshotBytes == null)
            return null;

        var authorityPayload = await LoadCurrentDetachedPendingTurnSnapshotAuthorityPayloadAsync(
            writeLease);
        if (authorityPayload == null &&
            !ReferenceEquals(manifest, _prevalidatedPendingTurnSnapshotOverride))
        {
            return null;
        }

""""""" + "\n"
        ),
        (
"""""""
            ? ComputeSha256(DecodePendingSnapshotText(snapshotBytes))
            : PendingTurnSnapshotAuthority.ComputeSnapshotFileHash(authorityPayload, snapshotBytes);
        if (!string.Equals(actualSnapshotHash, expectedSnapshotHash, StringComparison.OrdinalIgnoreCase))
        {
            source?.Policy(relativePath, "original", "HashInvalid");
            source?.Unavailable("narrative_policy_original_hash_invalid");
            return null;
        }

        var snapshotJson = DecodePendingSnapshotText(snapshotBytes);
        source?.Policy(relativePath, "original", string.IsNullOrWhiteSpace(snapshotJson) ? "EmptyOrWhitespace" : "PresentText");
        return string.IsNullOrWhiteSpace(snapshotJson) ? null : snapshotJson;
    }

""""""" + "\n",
"""""""
            ? ComputeSha256(DecodePendingSnapshotText(snapshotBytes))
            : PendingTurnSnapshotAuthority.ComputeSnapshotFileHash(authorityPayload, snapshotBytes);
        if (!string.Equals(actualSnapshotHash, expectedSnapshotHash, StringComparison.OrdinalIgnoreCase))
            return null;

        var snapshotJson = DecodePendingSnapshotText(snapshotBytes);
        return string.IsNullOrWhiteSpace(snapshotJson) ? null : snapshotJson;
    }

""""""" + "\n"
        ),
        (
"""""""
    /// <returns>
    /// Request identity, or null when missing or malformed.
    /// </returns>
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    /// <param name="narrativeRolePath">The originating closed narrative logical role for nested authority reads.</param>
    private PendingTurnRequestValidationContext? LoadPendingTurnRequestValidationContextSync(
        string requestPath,
        FileSystemManager.CanonicalWriteLease? writeLease = null, NarrativeInterfaceSourceSession? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        var json = source is not null
            ? ReadNarrativeAuxiliaryTextSync(narrativeRolePath, requestPath, writeLease, source)
            : writeLease == null
            ? CurrentWorldReadPhase() is { } phase ? phase.ReadTextSync(requestPath) : _fs.ReadFileSync(requestPath)
            : _fs.ReadFileAsync(writeLease, requestPath)
                .GetAwaiter()
""""""" + "\n",
"""""""
    /// <returns>
    /// Request identity, or null when missing or malformed.
    /// </returns>
    private PendingTurnRequestValidationContext? LoadPendingTurnRequestValidationContextSync(
        string requestPath,
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        var json = writeLease == null
            ? CurrentWorldReadPhase() is { } phase ? phase.ReadTextSync(requestPath) : _fs.ReadFileSync(requestPath)
            : _fs.ReadFileAsync(writeLease, requestPath)
                .GetAwaiter()
""""""" + "\n"
        ),
        (
"""""""
    /// <returns>
    /// Actual source status and an independent manifest copy when invocation evidence is active.
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
        {
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
        if (manifest == null)
        {
            return new ValidatedPendingTurnSnapshotLookup(
""""""" + "\n",
"""""""
    /// <returns>
    /// Actual source status and an independent manifest copy when invocation evidence is active.
    /// </returns>
    private async Task<ValidatedPendingTurnSnapshotLookup> LoadValidatedPendingTurnSnapshotLookupAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        if (CurrentPendingSnapshotLookup(writeLease) is { } scoped) return scoped;
        if (_prevalidatedPendingTurnSnapshotOverride != null)
            return new ValidatedPendingTurnSnapshotLookup(ValidatedPendingTurnSnapshotStatus.Usable, _prevalidatedPendingTurnSnapshotOverride);

        var manifestExists = writeLease == null
            ? _fs.FileExists(PendingTurnSnapshotManifestPath)
            : _fs.FileExists(writeLease, PendingTurnSnapshotManifestPath);
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync(writeLease);
        var authorityJson = await ReadAcceptedTurnValidationFileAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
            writeLease);
        if (manifest == null)
        {
            return new ValidatedPendingTurnSnapshotLookup(
""""""" + "\n"
        ),
        (
"""""""
                null);
        }

        RecordNarrativeOriginalContext(source, manifest, "PhysicalLookupData");
        return new ValidatedPendingTurnSnapshotLookup(
            IsValidatedPendingTurnSnapshotManifestUsable(
                manifest,
                authorityJson,
                writeLease,
                source is null ? null : path => ReadNarrativeAuxiliaryBytesSync(narrativeRolePath, path, writeLease, source),
                source is null ? null : path => ReadNarrativeAuxiliaryTextSync(narrativeRolePath, path, writeLease, source))
                ? ValidatedPendingTurnSnapshotStatus.Usable
                : ValidatedPendingTurnSnapshotStatus.Unusable,
            manifest);
    }

    private async Task<PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload?> LoadCurrentDetachedPendingTurnSnapshotAuthorityPayloadAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null, NarrativeInterfaceSourceSession? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        var authorityJson = await ReadAcceptedTurnValidationFileAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
            writeLease, source, narrativeRolePath);
        if (!PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(authorityJson, out var payload) || payload == null)
        {
            source?.Policy(narrativeRolePath, "original-authority", "MissingOrInvalid");
            source?.Unavailable("narrative_policy_original_authority_unavailable");
            return null;
        }

        var current = IsCurrentDetachedPendingTurnSnapshotAuthorityPayload(
                payload,
                writeLease, source, narrativeRolePath);
        if (!current)
        {
            source?.Policy(narrativeRolePath, "original-authority", "WrongContext");
            source?.Unavailable("narrative_policy_original_authority_context_changed");
        }
        return current ? payload : null;
    }

    /// <summary>
""""""" + "\n",
"""""""
                null);
        }

        return new ValidatedPendingTurnSnapshotLookup(
            IsValidatedPendingTurnSnapshotManifestUsable(
                manifest,
                authorityJson,
                writeLease)
                ? ValidatedPendingTurnSnapshotStatus.Usable
                : ValidatedPendingTurnSnapshotStatus.Unusable,
            manifest);
    }

    private async Task<PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload?> LoadCurrentDetachedPendingTurnSnapshotAuthorityPayloadAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        var authorityJson = await ReadAcceptedTurnValidationFileAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
            writeLease);
        if (!PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(authorityJson, out var payload) || payload == null)
            return null;

        return IsCurrentDetachedPendingTurnSnapshotAuthorityPayload(
                payload,
                writeLease)
            ? payload
            : null;
    }

    /// <summary>
""""""" + "\n"
        ),
        (
"""""""

    private bool IsCurrentDetachedPendingTurnSnapshotAuthorityPayload(
        PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload payload,
        FileSystemManager.CanonicalWriteLease? writeLease = null, NarrativeInterfaceSourceSession? source = null,
        string narrativeRolePath = "output/narrative_response.json")
    {
        const string repairRequestPath = "game_state/control/validation_repair_request.json";
        var repairContext = LoadPendingTurnRequestValidationContextSync(
            repairRequestPath,
            writeLease, source, narrativeRolePath);
        if (DoesPendingTurnRequestValidationContextMatchAuthorityPayload(payload, repairContext))
            return true;

        var turnContext = LoadPendingTurnRequestValidationContextSync(
            "input/turn_request.json",
            writeLease, source, narrativeRolePath);
        if (DoesPendingTurnRequestValidationContextMatchAuthorityPayload(payload, turnContext))
            return true;

        var completionContext = LoadPendingTurnRequestValidationContextSync(
            "ready/turn_complete.json",
            writeLease, source, narrativeRolePath);
        return DoesPendingTurnRequestValidationContextMatchAuthorityPayload(payload, completionContext);
    }

""""""" + "\n",
"""""""

    private bool IsCurrentDetachedPendingTurnSnapshotAuthorityPayload(
        PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload payload,
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        const string repairRequestPath = "game_state/control/validation_repair_request.json";
        var repairContext = LoadPendingTurnRequestValidationContextSync(
            repairRequestPath,
            writeLease);
        if (DoesPendingTurnRequestValidationContextMatchAuthorityPayload(payload, repairContext))
            return true;

        var turnContext = LoadPendingTurnRequestValidationContextSync(
            "input/turn_request.json",
            writeLease);
        if (DoesPendingTurnRequestValidationContextMatchAuthorityPayload(payload, turnContext))
            return true;

        var completionContext = LoadPendingTurnRequestValidationContextSync(
            "ready/turn_complete.json",
            writeLease);
        return DoesPendingTurnRequestValidationContextMatchAuthorityPayload(payload, completionContext);
    }

""""""" + "\n"
        ),
        (
"""""""

    private Task<string?> ReadAcceptedTurnValidationFileAsync(
        string path,
        FileSystemManager.CanonicalWriteLease? writeLease, NarrativeInterfaceSourceSession? source = null,
        string narrativeRolePath = "output/narrative_response.json") =>
        source is not null ? ReadNarrativeAuxiliaryTextAsync(narrativeRolePath, path, writeLease, source) :
        writeLease == null
            ? CurrentWorldReadPhase() is { } phase ? phase.ReadTextAsync(path) : _fs.ReadFileAsync(path)
            : _fs.ReadFileAsync(writeLease, path);
""""""" + "\n",
"""""""

    private Task<string?> ReadAcceptedTurnValidationFileAsync(
        string path,
        FileSystemManager.CanonicalWriteLease? writeLease) =>
        writeLease == null
            ? CurrentWorldReadPhase() is { } phase ? phase.ReadTextAsync(path) : _fs.ReadFileAsync(path)
            : _fs.ReadFileAsync(writeLease, path);
""""""" + "\n"
        )
    ];
}
