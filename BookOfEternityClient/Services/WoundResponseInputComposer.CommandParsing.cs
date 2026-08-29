using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record WoundResponseCommandDraft(
    WoundOpportunityAuthority Opportunity,
    JsonElement Decision,
    string? FinalSceneText);

internal sealed class WoundResponseCommandParsingResult
{
    private readonly JsonObject? _commandRoot;
    private readonly WoundResponseCommandDraft[] _commands;
    private readonly ValidationIssue[] _issues;

    internal WoundResponseCommandParsingResult(
        JsonObject? commandRoot,
        IReadOnlyList<WoundResponseCommandDraft> commands,
        IReadOnlyList<ValidationIssue> issues)
    {
        _commandRoot = commandRoot?.DeepClone().AsObject();
        _commands = commands.Select(static value => new WoundResponseCommandDraft(
            WoundAcceptedTurnData.CloneOpportunity(value.Opportunity),
            value.Decision.Clone(),
            value.FinalSceneText)).ToArray();
        _issues = issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray();
    }

    internal JsonObject? CommandRoot => _commandRoot?.DeepClone().AsObject();

    internal IReadOnlyList<WoundResponseCommandDraft> Commands =>
        Array.AsReadOnly(_commands.Select(static value => new WoundResponseCommandDraft(
            WoundAcceptedTurnData.CloneOpportunity(value.Opportunity),
            value.Decision.Clone(),
            value.FinalSceneText)).ToArray());

    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray());

    internal bool Success => _commandRoot is not null && _issues.Length == 0;
}

internal static partial class WoundResponseInputComposer
{
    private const int MaximumAcceptedCommandCount = 32;

    private static readonly IReadOnlySet<string> CommandRootFields = Set(
        "schemaVersion", "sessionId", "requestId", "snapshotToken", "commands");
    private static readonly IReadOnlySet<string> CommandFields = Set(
        "kind", "opportunity", "decision", "finalSceneText");
    private static readonly IReadOnlySet<string> SerializedOpportunityFields = Set(
        "schemaVersion", "sessionId", "requestId", "snapshotToken",
        "opportunityId", "publicRef", "eventRef", "eventKind",
        "eventAuthorityId", "acceptedEventsFingerprint", "owner", "domain", "profileKey", "sourceKind",
        "sourceId", "sourceState", "minimumSeverityRank",
        "maximumSeverityRank", "guaranteedTrigger", "worseningTarget", "safeContext",
        "inputEvidenceFingerprint", "authorityFingerprint");
    private static readonly IReadOnlySet<string> SerializedOwnerFields = Set(
        "realm", "ownerKind", "ownerId", "carrierPath");
    private static readonly IReadOnlySet<string> SerializedGuaranteeFields = Set(
        "triggerId", "sourceKind", "sourceId", "sourceState", "realm",
        "domain", "owner", "requiredSeverityRank", "materializedAtTurn",
        "sourceContractFingerprint", "authorityFingerprint");
    private static readonly IReadOnlySet<string> SerializedSafeContextFields = Set(
        "target", "cause", "allowedLocationKinds");
    private static readonly IReadOnlySet<string> SerializedWorseningTargetFields = Set(
        "causeKind", "expectedBeforeFingerprint", "wound");

    internal static WoundResponseCommandParsingResult ParseCommandRoot(
        JsonElement root)
    {
        var issues = new List<ValidationIssue>();
        if (!TryReadFields(
                root,
                CommandRootFields,
                CommandRootFields,
                AcceptedMechanicsPlan.WoundCommandPath,
                issues,
                out var fields))
        {
            return ParseFailure(issues);
        }

        if (!TryReadInteger(fields, "schemaVersion", out var schemaVersion) ||
            schemaVersion != 1)
        {
            AddCommandIssue(
                issues,
                AcceptedMechanicsPlan.WoundCommandPath + ".schemaVersion",
                "wound_command_invalid_field",
                "exact integer schemaVersion 1",
                Raw(fields, "schemaVersion"));
        }
        ReadExactIdentifier(
            fields,
            "sessionId",
            AcceptedMechanicsPlan.WoundCommandPath,
            issues);
        ReadExactIdentifier(
            fields,
            "requestId",
            AcceptedMechanicsPlan.WoundCommandPath,
            issues);
        ReadExactIdentifier(
            fields,
            "snapshotToken",
            AcceptedMechanicsPlan.WoundCommandPath,
            issues);

        var commands = new List<WoundResponseCommandDraft>();
        if (!fields.TryGetValue("commands", out var commandArray) ||
            commandArray.ValueKind != JsonValueKind.Array)
        {
            AddCommandIssue(
                issues,
                AcceptedMechanicsPlan.WoundCommandPath + ".commands",
                "wound_command_invalid_field",
                "strict command array",
                Raw(fields, "commands"));
        }
        else if (commandArray.GetArrayLength() > MaximumAcceptedCommandCount)
        {
            AddCommandIssue(
                issues,
                AcceptedMechanicsPlan.WoundCommandPath + ".commands",
                "wound_command_limit_exceeded",
                $"at most {MaximumAcceptedCommandCount} wound decisions",
                commandArray.GetArrayLength().ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            var index = 0;
            foreach (var element in commandArray.EnumerateArray())
            {
                var command = ParseCommand(element, index, issues);
                if (command is not null)
                {
                    commands.Add(command);
                }
                else
                {
                    AddCommandIssue(
                        issues,
                        $"{AcceptedMechanicsPlan.WoundCommandPath}.commands[{index}]",
                        "wound_command_transition_adapter_unavailable",
                        "one complete client-adapted opportunity_decision command",
                        "command could not be adapted to one typed wound transition");
                }
                index++;
            }
        }

        if (commands.Select(static value => value.FinalSceneText)
                .Distinct(StringComparer.Ordinal).Count() > 1)
        {
            AddCommandIssue(
                issues,
                AcceptedMechanicsPlan.WoundCommandPath + ".commands",
                "wound_command_scene_binding_mismatch",
                "one identical finalSceneText for the response command batch",
                "different finalSceneText values");
        }

        if (issues.Count != 0)
            return ParseFailure(issues);
        return new WoundResponseCommandParsingResult(
            JsonNode.Parse(root.GetRawText())!.AsObject(),
            commands,
            Array.Empty<ValidationIssue>());
    }

    internal static WoundResponseInputCompositionResult RecomposeCommandRoot(
        WoundAcceptedTurnBinding binding,
        WoundResponseCommandParsingResult parsed,
        IReadOnlyList<WoundOpportunityDecisionReceipt> priorReceipts)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(priorReceipts);
        if (!parsed.Success || parsed.CommandRoot is null)
            return Failure(parsed.Issues);

        var commands = parsed.Commands;
        var finalSceneText = commands.Count == 0
            ? null
            : commands[0].FinalSceneText;
        var composed = Compose(
            binding,
            commands.Select(static value => value.Opportunity).ToArray(),
            commands.Select(static value => value.Decision).ToArray(),
            finalSceneText,
            priorReceipts);
        if (!composed.Success || composed.CommandRoot is null)
            return composed;
        if (JsonNode.DeepEquals(composed.CommandRoot, parsed.CommandRoot))
            return composed;

        return Failure(new[]
        {
            CommandIssue(
                AcceptedMechanicsPlan.WoundCommandPath,
                "wound_command_recomposition_mismatch",
                "the exact client-composed command root",
                "parsed command changes under typed recomposition")
        });
    }

    private static WoundResponseCommandDraft? ParseCommand(
        JsonElement element,
        int index,
        ICollection<ValidationIssue> issues)
    {
        var path = $"{AcceptedMechanicsPlan.WoundCommandPath}.commands[{index}]";
        var start = issues.Count;
        if (!TryReadFields(
                element,
                CommandFields,
                CommandFields,
                path,
                issues,
                out var fields))
        {
            return null;
        }
        if (!TryReadString(fields, "kind", out var kind) ||
            !string.Equals(kind, "opportunity_decision", StringComparison.Ordinal))
        {
            AddCommandIssue(
                issues,
                path + ".kind",
                "wound_command_invalid_field",
                "opportunity_decision",
                Raw(fields, "kind"));
        }

        WoundOpportunityAuthority? opportunity = null;
        if (fields.TryGetValue("opportunity", out var opportunityElement))
            opportunity = ParseOpportunity(opportunityElement, path + ".opportunity", issues);

        JsonElement decision = default;
        if (!fields.TryGetValue("decision", out var decisionElement) ||
            decisionElement.ValueKind != JsonValueKind.Object)
        {
            AddCommandIssue(
                issues,
                path + ".decision",
                "wound_command_invalid_field",
                "strict wound decision object",
                Raw(fields, "decision"));
        }
        else
        {
            decision = decisionElement.Clone();
        }

        string? finalSceneText = null;
        if (!fields.TryGetValue("finalSceneText", out var scene) ||
            scene.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
        {
            AddCommandIssue(
                issues,
                path + ".finalSceneText",
                "wound_command_invalid_field",
                "string or null finalSceneText",
                Raw(fields, "finalSceneText"));
        }
        else if (scene.ValueKind == JsonValueKind.String)
        {
            finalSceneText = scene.GetString();
        }

        return issues.Count == start && opportunity is not null
            ? new WoundResponseCommandDraft(opportunity, decision, finalSceneText)
            : null;
    }

    private static WoundOpportunityAuthority? ParseOpportunity(
        JsonElement element,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var start = issues.Count;
        if (!TryReadFields(
                element,
                SerializedOpportunityFields,
                SerializedOpportunityFields,
                path,
                issues,
                out var fields))
        {
            return null;
        }
        if (!TryReadInteger(fields, "schemaVersion", out var schemaVersion) ||
            schemaVersion != 1)
        {
            AddCommandIssue(
                issues,
                path + ".schemaVersion",
                "wound_command_invalid_field",
                "exact integer schemaVersion 1",
                Raw(fields, "schemaVersion"));
        }

        var sessionId = ReadExactIdentifier(fields, "sessionId", path, issues);
        var requestId = ReadExactIdentifier(fields, "requestId", path, issues);
        var snapshotToken = ReadExactIdentifier(fields, "snapshotToken", path, issues);
        var opportunityId = ReadExactIdentifier(fields, "opportunityId", path, issues);
        var publicRef = ReadStringField(fields, "publicRef", path, issues);
        var eventRef = ReadExactIdentifier(fields, "eventRef", path, issues);
        var eventKind = ReadExactIdentifier(fields, "eventKind", path, issues);
        var eventAuthorityId = ReadExactIdentifier(
            fields,
            "eventAuthorityId",
            path,
            issues);
        var acceptedEventsFingerprint = ReadExactIdentifier(
            fields,
            "acceptedEventsFingerprint",
            path,
            issues);
        var owner = fields.TryGetValue("owner", out var ownerElement)
            ? ParseOwner(ownerElement, path + ".owner", issues)
            : null;
        var domain = ReadExactIdentifier(fields, "domain", path, issues);
        var profileKey = ReadExactIdentifier(fields, "profileKey", path, issues);
        var sourceKind = ReadExactIdentifier(fields, "sourceKind", path, issues);
        var sourceId = ReadExactIdentifier(fields, "sourceId", path, issues);
        var sourceState = ReadExactIdentifier(fields, "sourceState", path, issues);
        int? minimumSeverityRank = null;
        var parsedMinimum = 0;
        if (!fields.TryGetValue("minimumSeverityRank", out var minimum) ||
            minimum.ValueKind is not (JsonValueKind.Null or JsonValueKind.Number) ||
            minimum.ValueKind == JsonValueKind.Number &&
            !minimum.TryGetInt32(out parsedMinimum))
        {
            AddCommandIssue(
                issues,
                path + ".minimumSeverityRank",
                "wound_command_invalid_field",
                "integer severity rank or null",
                Raw(fields, "minimumSeverityRank"));
        }
        else if (minimum.ValueKind == JsonValueKind.Number)
        {
            minimumSeverityRank = parsedMinimum;
        }
        if (!TryReadInteger(fields, "maximumSeverityRank", out var maximumSeverityRank))
        {
            AddCommandIssue(
                issues,
                path + ".maximumSeverityRank",
                "wound_command_invalid_field",
                "integer severity rank",
                Raw(fields, "maximumSeverityRank"));
        }

        WoundGuaranteedTriggerAuthority? guarantee = null;
        if (!fields.TryGetValue("guaranteedTrigger", out var guaranteeElement))
        {
            AddCommandIssue(
                issues,
                path + ".guaranteedTrigger",
                "wound_command_missing_field",
                "guarantee object or null",
                "missing");
        }
        else if (guaranteeElement.ValueKind == JsonValueKind.Object)
        {
            guarantee = ParseGuarantee(
                guaranteeElement,
                path + ".guaranteedTrigger",
                issues);
        }
        else if (guaranteeElement.ValueKind != JsonValueKind.Null)
        {
            AddCommandIssue(
                issues,
                path + ".guaranteedTrigger",
                "wound_command_invalid_field",
                "guarantee object or null",
                guaranteeElement.ValueKind.ToString());
        }

        WoundOpportunityWorseningTargetAuthority? worseningTarget = null;
        if (!fields.TryGetValue("worseningTarget", out var worseningElement))
        {
            AddCommandIssue(
                issues,
                path + ".worseningTarget",
                "wound_command_missing_field",
                "worsening target object or null",
                "missing");
        }
        else if (worseningElement.ValueKind == JsonValueKind.Object)
        {
            worseningTarget = ParseWorseningTarget(
                worseningElement,
                path + ".worseningTarget",
                issues);
        }
        else if (worseningElement.ValueKind != JsonValueKind.Null)
        {
            AddCommandIssue(
                issues,
                path + ".worseningTarget",
                "wound_command_invalid_field",
                "worsening target object or null",
                worseningElement.ValueKind.ToString());
        }

        var safeContext = fields.TryGetValue("safeContext", out var safeElement)
            ? ParseSafeContext(safeElement, path + ".safeContext", issues)
            : null;
        var inputFingerprint = ReadExactIdentifier(
            fields,
            "inputEvidenceFingerprint",
            path,
            issues);
        var authorityFingerprint = ReadExactIdentifier(
            fields,
            "authorityFingerprint",
            path,
            issues);
        if (issues.Count != start || owner is null || safeContext is null)
            return null;

        var opportunity = new WoundOpportunityAuthority(
            sessionId!,
            requestId!,
            snapshotToken!,
            opportunityId!,
            publicRef!,
            eventRef!,
            eventKind!,
            eventAuthorityId!,
            acceptedEventsFingerprint!,
            owner,
            domain!,
            profileKey!,
            sourceKind!,
            sourceId!,
            sourceState!,
            minimumSeverityRank,
            maximumSeverityRank,
            guarantee,
            safeContext,
            inputFingerprint!,
            authorityFingerprint!)
        {
            WorseningTarget = worseningTarget
        };
        if (WoundOpportunityAuthority.HasCompleteShape(opportunity))
            return opportunity;

        AddCommandIssue(
            issues,
            path,
            "wound_command_opportunity_invalid",
            "one complete sealed wound opportunity",
            "shape or authority fingerprint mismatch");
        return null;
    }

    private static WoundOpportunityWorseningTargetAuthority? ParseWorseningTarget(
        JsonElement element,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var start = issues.Count;
        if (!TryReadFields(
                element,
                SerializedWorseningTargetFields,
                SerializedWorseningTargetFields,
                path,
                issues,
                out var fields))
        {
            return null;
        }

        var causeKind = ReadExactIdentifier(fields, "causeKind", path, issues);
        var fingerprint = ReadExactIdentifier(
            fields,
            "expectedBeforeFingerprint",
            path,
            issues);
        WoundMaterializationEnvelope? wound = null;
        if (!fields.TryGetValue("wound", out var woundElement) ||
            woundElement.ValueKind != JsonValueKind.Object)
        {
            AddCommandIssue(
                issues,
                path + ".wound",
                "wound_command_invalid_field",
                "one canonical wound object",
                Raw(fields, "wound"));
        }
        else
        {
            var parsed = WoundMaterializationContract.Parse(
                woundElement.GetRawText(),
                path + ".wound");
            foreach (var issue in parsed.Issues)
                issues.Add(WoundAcceptedTurnData.CloneIssue(issue));
            wound = parsed.Wound;
        }

        return issues.Count == start && wound is not null
            ? new WoundOpportunityWorseningTargetAuthority(
                wound,
                causeKind!,
                fingerprint!)
            : null;
    }

    private static WoundOwnerCoordinate? ParseOwner(
        JsonElement element,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var start = issues.Count;
        if (!TryReadFields(
                element,
                SerializedOwnerFields,
                SerializedOwnerFields,
                path,
                issues,
                out var fields))
        {
            return null;
        }
        var realm = ReadExactIdentifier(fields, "realm", path, issues);
        var ownerKind = ReadExactIdentifier(fields, "ownerKind", path, issues);
        var ownerId = ReadExactIdentifier(fields, "ownerId", path, issues);
        var carrierPath = ReadStringField(fields, "carrierPath", path, issues);
        return issues.Count == start
            ? new WoundOwnerCoordinate(realm!, ownerKind!, ownerId!, carrierPath!)
            : null;
    }

    private static WoundGuaranteedTriggerAuthority? ParseGuarantee(
        JsonElement element,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var start = issues.Count;
        if (!TryReadFields(
                element,
                SerializedGuaranteeFields,
                SerializedGuaranteeFields,
                path,
                issues,
                out var fields))
        {
            return null;
        }
        var triggerId = ReadExactIdentifier(fields, "triggerId", path, issues);
        var sourceKind = ReadExactIdentifier(fields, "sourceKind", path, issues);
        var sourceId = ReadExactIdentifier(fields, "sourceId", path, issues);
        var sourceState = ReadExactIdentifier(fields, "sourceState", path, issues);
        var realm = ReadExactIdentifier(fields, "realm", path, issues);
        var domain = ReadExactIdentifier(fields, "domain", path, issues);
        var owner = fields.TryGetValue("owner", out var ownerElement)
            ? ParseOwner(ownerElement, path + ".owner", issues)
            : null;
        if (!TryReadInteger(fields, "requiredSeverityRank", out var required))
        {
            AddCommandIssue(
                issues,
                path + ".requiredSeverityRank",
                "wound_command_invalid_field",
                "integer severity rank",
                Raw(fields, "requiredSeverityRank"));
        }
        if (!TryReadInteger(fields, "materializedAtTurn", out var materializedAtTurn))
        {
            AddCommandIssue(
                issues,
                path + ".materializedAtTurn",
                "wound_command_invalid_field",
                "positive integer turn",
                Raw(fields, "materializedAtTurn"));
        }
        var sourceContractFingerprint = ReadExactIdentifier(
            fields,
            "sourceContractFingerprint",
            path,
            issues);
        var authorityFingerprint = ReadExactIdentifier(
            fields,
            "authorityFingerprint",
            path,
            issues);
        return issues.Count == start && owner is not null
            ? new WoundGuaranteedTriggerAuthority(
                triggerId!,
                sourceKind!,
                sourceId!,
                sourceState!,
                realm!,
                domain!,
                owner,
                required,
                materializedAtTurn,
                sourceContractFingerprint!,
                authorityFingerprint!)
            : null;
    }

    private static WoundOpportunitySafeContext? ParseSafeContext(
        JsonElement element,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var start = issues.Count;
        if (!TryReadFields(
                element,
                SerializedSafeContextFields,
                SerializedSafeContextFields,
                path,
                issues,
                out var fields))
        {
            return null;
        }
        var target = ReadStringField(fields, "target", path, issues);
        var cause = ReadStringField(fields, "cause", path, issues);
        var locationKinds = new List<string>();
        if (!fields.TryGetValue("allowedLocationKinds", out var allowed) ||
            allowed.ValueKind != JsonValueKind.Array)
        {
            AddCommandIssue(
                issues,
                path + ".allowedLocationKinds",
                "wound_command_invalid_field",
                "string array",
                Raw(fields, "allowedLocationKinds"));
        }
        else
        {
            var index = 0;
            foreach (var value in allowed.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String)
                {
                    AddCommandIssue(
                        issues,
                        $"{path}.allowedLocationKinds[{index}]",
                        "wound_command_invalid_field",
                        "string location kind",
                        value.GetRawText());
                }
                else
                {
                    locationKinds.Add(value.GetString()!);
                }
                index++;
            }
        }
        return issues.Count == start
            ? new WoundOpportunitySafeContext(target!, cause!, locationKinds)
            : null;
    }

    private static bool TryReadFields(
        JsonElement element,
        IReadOnlySet<string> allowed,
        IReadOnlySet<string> required,
        string path,
        ICollection<ValidationIssue> issues,
        out Dictionary<string, JsonElement> fields)
    {
        fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (element.ValueKind != JsonValueKind.Object)
        {
            AddCommandIssue(
                issues,
                path,
                "wound_command_invalid_field",
                "strict JSON object",
                element.ValueKind.ToString());
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            var propertyPath = path + "." + property.Name;
            if (!fields.TryAdd(property.Name, property.Value.Clone()))
            {
                AddCommandIssue(
                    issues,
                    propertyPath,
                    "wound_command_duplicate_field",
                    "one exact occurrence",
                    property.Name);
            }
            if (!allowed.Contains(property.Name))
            {
                AddCommandIssue(
                    issues,
                    propertyPath,
                    "wound_command_unknown_field",
                    string.Join(", ", allowed.OrderBy(
                        static value => value,
                        StringComparer.Ordinal)),
                    property.Name);
            }
        }
        foreach (var field in required)
        {
            if (fields.ContainsKey(field))
                continue;
            AddCommandIssue(
                issues,
                path + "." + field,
                "wound_command_missing_field",
                "required strict command field",
                "missing");
        }
        return true;
    }

    private static string? ReadExactIdentifier(
        IReadOnlyDictionary<string, JsonElement> fields,
        string name,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (TryReadString(fields, name, out var value) &&
            ResourceMaterializationContract.IsExactIdentifier(value))
        {
            return value;
        }
        AddCommandIssue(
            issues,
            path + "." + name,
            "wound_command_invalid_field",
            "exact non-empty identifier",
            Raw(fields, name));
        return null;
    }

    private static string? ReadStringField(
        IReadOnlyDictionary<string, JsonElement> fields,
        string name,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (TryReadString(fields, name, out var value))
            return value;
        AddCommandIssue(
            issues,
            path + "." + name,
            "wound_command_invalid_field",
            "string",
            Raw(fields, name));
        return null;
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, JsonElement> fields,
        string name,
        out string value)
    {
        value = string.Empty;
        return fields.TryGetValue(name, out var element) &&
               element.ValueKind == JsonValueKind.String &&
               (value = element.GetString()!) is not null;
    }

    private static bool TryReadInteger(
        IReadOnlyDictionary<string, JsonElement> fields,
        string name,
        out int value)
    {
        value = 0;
        return fields.TryGetValue(name, out var element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt32(out value);
    }

    private static string Raw(
        IReadOnlyDictionary<string, JsonElement> fields,
        string name) => fields.TryGetValue(name, out var value)
        ? value.GetRawText()
        : "missing";

    private static WoundResponseCommandParsingResult ParseFailure(
        IReadOnlyList<ValidationIssue> issues) => new(
        null,
        Array.Empty<WoundResponseCommandDraft>(),
        issues);

    private static void AddCommandIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(CommandIssue(path, code, expected, actual));

    private static ValidationIssue CommandIssue(
        string path,
        string code,
        string expected,
        string actual) => new(
        path,
        IssueSeverity.Error,
        "Accepted wound command violates the sealed response contract.",
        code: code,
        section: "wound_command",
        expected: expected,
        actual: actual,
        repairHint:
            "Recreate the wound command from the exact current opportunity and GM decision.");
}
