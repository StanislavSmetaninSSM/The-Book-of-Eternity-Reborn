using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record WoundIdentityEntry(
    string WoundId,
    string Realm,
    string OwnerKind,
    string OwnerId,
    string CarrierPath,
    string Domain,
    string Status,
    int CreatedAtTurn,
    string CreatedEventRef,
    int LastTransitionOrdinal,
    string? TerminalTransitionId,
    string SemanticFingerprint);

internal sealed record WoundIdentityParseResult(
    WoundIdentityState? State,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => State is not null && Issues.Count == 0;
}

internal sealed class WoundIdentityState
{
    internal const int SchemaVersion = 1;
    internal const string StatePath = "game_state/wounds/wound_identity_index.json";

    private static readonly IReadOnlySet<string> RootFields = Set("schemaVersion", "entries");
    private static readonly IReadOnlySet<string> EntryFields = Set(
        "woundId", "realm", "ownerKind", "ownerId", "carrierPath", "domain",
        "status", "createdAtTurn", "createdEventRef", "lastTransitionOrdinal",
        "terminalTransitionId", "semanticFingerprint");
    private static readonly IReadOnlySet<string> Realms = Set(
        "mortal_world", "chaos_sea", "shining_abode");
    private static readonly IReadOnlySet<string> OwnerKinds = Set(
        "player", "npc", "combatant", "combatant_member", "player_soul", "guardian",
        "resident", "radiant_actor", "afterlife_actor");
    private static readonly IReadOnlySet<string> Domains = Set("physical", "spiritual");
    private static readonly IReadOnlySet<string> Statuses = Set("active", "healed");

    private readonly ImmutableArray<WoundIdentityEntry> _entries;
    private readonly IReadOnlyDictionary<string, WoundIdentityEntry> _entriesById;

    private WoundIdentityState(IEnumerable<WoundIdentityEntry> entries)
    {
        _entries = entries.ToImmutableArray();
        _entriesById = _entries.ToDictionary(static entry => entry.WoundId, StringComparer.Ordinal);
    }

    internal IReadOnlyList<WoundIdentityEntry> Entries => _entries;

    internal bool TryGetEntry(string woundId, out WoundIdentityEntry entry) =>
        _entriesById.TryGetValue(woundId, out entry!);

    internal static WoundIdentityParseResult Parse(string? json, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return InvalidResult(
                path,
                "wound_identity_invalid_root",
                "non-empty strict JSON object",
                json is null ? "missing" : "empty or whitespace-only input");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return InvalidResult(
                path,
                "wound_identity_invalid_json",
                "well-formed strict JSON object",
                exception.GetType().Name);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return InvalidResult(
                    path,
                    "wound_identity_invalid_root",
                    "strict JSON object",
                    root.ValueKind.ToString());
            }

            var issues = new List<ValidationIssue>();
            FindDuplicateProperties(root, path, issues);
            ValidateClosedObject(root, path, RootFields, issues);
            RequireFields(root, path, RootFields, issues);
            ReadSchemaVersion(root, path, issues);

            if (!root.TryGetProperty("entries", out var entriesElement) ||
                entriesElement.ValueKind != JsonValueKind.Array)
            {
                AddIssue(
                    issues,
                    path + ".entries",
                    "wound_identity_invalid_field",
                    "array",
                    Describe(root, "entries"));
                return new WoundIdentityParseResult(null, issues.ToImmutableArray());
            }

            var entries = ImmutableArray.CreateBuilder<WoundIdentityEntry>();
            var exactIds = new HashSet<string>(StringComparer.Ordinal);
            var confusableIds = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var element in entriesElement.EnumerateArray())
            {
                var entryPath = $"{path}.entries[{index++}]";
                var entry = ParseEntry(
                    element,
                    entryPath,
                    exactIds,
                    confusableIds,
                    issues);
                if (entry is not null)
                    entries.Add(entry);
            }

            return issues.Count == 0
                ? new WoundIdentityParseResult(
                    new WoundIdentityState(entries),
                    ImmutableArray<ValidationIssue>.Empty)
                : new WoundIdentityParseResult(null, issues.ToImmutableArray());
        }
    }

    internal static string SerializeCanonical(WoundIdentityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(
                   buffer,
                   new JsonWriterOptions
                   {
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                       Indented = false
                   }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WritePropertyName("entries");
            writer.WriteStartArray();
            foreach (var entry in state._entries.OrderBy(
                         static entry => entry.WoundId,
                         StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("woundId", entry.WoundId);
                writer.WriteString("realm", entry.Realm);
                writer.WriteString("ownerKind", entry.OwnerKind);
                writer.WriteString("ownerId", entry.OwnerId);
                writer.WriteString("carrierPath", entry.CarrierPath);
                writer.WriteString("domain", entry.Domain);
                writer.WriteString("status", entry.Status);
                writer.WriteNumber("createdAtTurn", entry.CreatedAtTurn);
                writer.WriteString("createdEventRef", entry.CreatedEventRef);
                writer.WriteNumber("lastTransitionOrdinal", entry.LastTransitionOrdinal);
                if (entry.TerminalTransitionId is null)
                    writer.WriteNull("terminalTransitionId");
                else
                    writer.WriteString("terminalTransitionId", entry.TerminalTransitionId);
                writer.WriteString("semanticFingerprint", entry.SemanticFingerprint);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    internal static string ComputeSemanticFingerprint(WoundMaterializationEnvelope wound)
    {
        ArgumentNullException.ThrowIfNull(wound);
        var canonical = WoundMaterializationContract.SerializeCanonical(wound);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return "sha256:" + Convert.ToHexString(digest).ToLowerInvariant();
    }

    internal static IReadOnlyList<ValidationIssue> ValidateActiveAgreement(
        WoundIdentityEntry entry,
        WoundMaterializationEnvelope wound,
        string path)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(wound);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var issues = new List<ValidationIssue>();
        AddAgreementIssueIfDifferent(
            issues, path + ".woundId", wound.WoundId, entry.WoundId);
        if (!string.Equals(entry.Status, "active", StringComparison.Ordinal) ||
            !string.Equals(wound.Lifecycle, "active", StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                path + ".status",
                "wound_identity_active_agreement_mismatch",
                "active index status and active wound lifecycle",
                $"index={entry.Status}; wound={wound.Lifecycle}");
        }
        AddAgreementIssueIfDifferent(
            issues, path + ".realm", wound.Owner.Realm, entry.Realm);
        AddAgreementIssueIfDifferent(
            issues, path + ".ownerKind", wound.Owner.OwnerKind, entry.OwnerKind);
        AddAgreementIssueIfDifferent(
            issues, path + ".ownerId", wound.Owner.OwnerId, entry.OwnerId);
        AddAgreementIssueIfDifferent(
            issues, path + ".carrierPath", wound.Owner.CarrierPath, entry.CarrierPath);
        AddAgreementIssueIfDifferent(
            issues, path + ".domain", wound.Classification.Domain, entry.Domain);
        AddAgreementIssueIfDifferent(
            issues,
            path + ".createdAtTurn",
            wound.Origin.CreatedAtTurn.ToString(CultureInfo.InvariantCulture),
            entry.CreatedAtTurn.ToString(CultureInfo.InvariantCulture));
        AddAgreementIssueIfDifferent(
            issues, path + ".createdEventRef", wound.Origin.EventRef, entry.CreatedEventRef);
        AddAgreementIssueIfDifferent(
            issues,
            path + ".lastTransitionOrdinal",
            wound.LastTransition.Ordinal.ToString(CultureInfo.InvariantCulture),
            entry.LastTransitionOrdinal.ToString(CultureInfo.InvariantCulture));
        AddAgreementIssueIfDifferent(
            issues,
            path + ".semanticFingerprint",
            ComputeSemanticFingerprint(wound),
            entry.SemanticFingerprint);
        return issues.ToImmutableArray();
    }

    private static WoundIdentityEntry? ParseEntry(
        JsonElement element,
        string path,
        HashSet<string> exactIds,
        HashSet<string> confusableIds,
        List<ValidationIssue> issues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            AddIssue(
                issues,
                path,
                "wound_identity_invalid_field",
                "closed identity entry object",
                element.ValueKind.ToString());
            return null;
        }

        var issueCount = issues.Count;
        ValidateClosedObject(element, path, EntryFields, issues);
        RequireFields(element, path, EntryFields, issues);
        var woundId = ReadExactIdentifier(element, "woundId", path, issues);
        if (woundId.Length != 0)
        {
            if (!exactIds.Add(woundId))
            {
                AddIssue(
                    issues,
                    path + ".woundId",
                    "wound_identity_duplicate_id",
                    "globally unique exact wound identity",
                    woundId);
            }
            else if (!confusableIds.Add(MortalLocationIdentityState.BuildConfusableKey(woundId)))
            {
                AddIssue(
                    issues,
                    path + ".woundId",
                    "wound_identity_confusable_id",
                    "globally unique exact/confusable wound identity",
                    woundId);
            }
        }

        var realm = ReadClosedString(element, "realm", path, Realms, issues);
        var ownerKind = ReadClosedString(element, "ownerKind", path, OwnerKinds, issues);
        var ownerId = ReadExactIdentifier(element, "ownerId", path, issues);
        var carrierPath = ReadExactIdentifier(element, "carrierPath", path, issues);
        var domain = ReadClosedString(element, "domain", path, Domains, issues);
        var status = ReadClosedString(element, "status", path, Statuses, issues);
        var createdAtTurn = ReadInt32(element, "createdAtTurn", path, 0, int.MaxValue, issues);
        var createdEventRef = ReadExactIdentifier(element, "createdEventRef", path, issues);
        var lastTransitionOrdinal = ReadInt32(
            element,
            "lastTransitionOrdinal",
            path,
            1,
            int.MaxValue,
            issues);
        var terminalTransitionId = ReadNullableExactIdentifier(
            element,
            "terminalTransitionId",
            path,
            issues);
        var semanticFingerprint = ReadFingerprint(
            element,
            "semanticFingerprint",
            path,
            issues);

        if (status.Length != 0)
        {
            var terminalEvidenceIsValid = status switch
            {
                "active" => terminalTransitionId is null,
                "healed" => terminalTransitionId is not null,
                _ => true
            };
            if (!terminalEvidenceIsValid)
            {
                AddIssue(
                    issues,
                    path + ".terminalTransitionId",
                    "wound_identity_terminal_evidence_mismatch",
                    status == "active"
                        ? "null terminalTransitionId for active identity"
                        : "one exact non-null terminalTransitionId for healed identity",
                    terminalTransitionId ?? "null");
            }
        }

        return issues.Count == issueCount
            ? new WoundIdentityEntry(
                woundId,
                realm,
                ownerKind,
                ownerId,
                carrierPath,
                domain,
                status,
                createdAtTurn,
                createdEventRef,
                lastTransitionOrdinal,
                terminalTransitionId,
                semanticFingerprint)
            : null;
    }

    private static int ReadSchemaVersion(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("schemaVersion", out var value))
            return 0;
        if (value.ValueKind == JsonValueKind.Number &&
            string.Equals(
                value.GetRawText(),
                SchemaVersion.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            return SchemaVersion;
        }
        AddIssue(
            issues,
            path + ".schemaVersion",
            "wound_identity_invalid_field",
            "exact integer 1",
            value.GetRawText());
        return 0;
    }

    private static string ReadExactIdentifier(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (parent.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            ResourceMaterializationContract.IsExactIdentifier(value.GetString()))
        {
            return value.GetString()!;
        }
        AddIssue(
            issues,
            path + "." + field,
            "wound_identity_invalid_identifier",
            "non-empty trimmed NFKC exact identifier without control or separator characters",
            Describe(parent, field));
        return string.Empty;
    }

    private static string? ReadNullableExactIdentifier(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.String &&
            ResourceMaterializationContract.IsExactIdentifier(value.GetString()))
        {
            return value.GetString();
        }
        AddIssue(
            issues,
            path + "." + field,
            "wound_identity_invalid_identifier",
            "null or non-empty trimmed NFKC exact identifier without control or separator characters",
            value.GetRawText());
        return null;
    }

    private static string ReadFingerprint(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (parent.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            ResourceMaterializationContract.IsAuthorityFingerprint(value.GetString()))
        {
            return value.GetString()!;
        }
        AddIssue(
            issues,
            path + "." + field,
            "wound_identity_invalid_fingerprint",
            "exact lowercase sha256: fingerprint with 64 hexadecimal digits",
            Describe(parent, field));
        return string.Empty;
    }

    private static string ReadClosedString(
        JsonElement parent,
        string field,
        string path,
        IEnumerable<string> allowed,
        List<ValidationIssue> issues)
    {
        var value = parent.TryGetProperty(field, out var candidate) &&
                    candidate.ValueKind == JsonValueKind.String
            ? candidate.GetString() ?? string.Empty
            : string.Empty;
        if (allowed.Contains(value, StringComparer.Ordinal))
            return value;
        AddIssue(
            issues,
            path + "." + field,
            "wound_identity_invalid_field",
            "one ordinal current-schema value: " + string.Join(", ", allowed),
            Describe(parent, field));
        return string.Empty;
    }

    private static int ReadInt32(
        JsonElement parent,
        string field,
        string path,
        int minimum,
        int maximum,
        List<ValidationIssue> issues)
    {
        if (parent.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var result) &&
            IsIntegerLexeme(value.GetRawText()) &&
            result >= minimum && result <= maximum)
        {
            return result;
        }
        AddIssue(
            issues,
            path + "." + field,
            "wound_identity_invalid_field",
            $"exact integer {minimum}..{maximum}",
            Describe(parent, field));
        return 0;
    }

    private static bool IsIntegerLexeme(string raw) =>
        raw.IndexOfAny(new[] { '.', 'e', 'E' }) < 0;

    private static void ValidateClosedObject(
        JsonElement value,
        string path,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return;
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                AddIssue(
                    issues,
                    path + "." + property.Name,
                    "wound_identity_unknown_field",
                    "registered current-schema identity field",
                    property.Name);
            }
        }
    }

    private static void RequireFields(
        JsonElement value,
        string path,
        IReadOnlySet<string> required,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return;
        foreach (var field in required)
        {
            if (!value.TryGetProperty(field, out _))
            {
                AddIssue(
                    issues,
                    path + "." + field,
                    "wound_identity_missing_field",
                    "required final version-1 identity field",
                    "missing");
            }
        }
    }

    private static void FindDuplicateProperties(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    var propertyPath = path + "." + property.Name;
                    if (!names.Add(property.Name))
                    {
                        AddIssue(
                            issues,
                            propertyPath,
                            "wound_identity_duplicate_property",
                            "one occurrence of each exact property",
                            property.Name);
                    }
                    FindDuplicateProperties(property.Value, propertyPath, issues);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                    FindDuplicateProperties(item, $"{path}[{index++}]", issues);
                break;
        }
    }

    private static void AddAgreementIssueIfDifferent(
        ICollection<ValidationIssue> issues,
        string path,
        string expected,
        string actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
            return;
        AddIssue(
            issues,
            path,
            "wound_identity_active_agreement_mismatch",
            expected,
            actual);
    }

    private static WoundIdentityParseResult InvalidResult(
        string path,
        string code,
        string expected,
        string actual)
    {
        var issues = ImmutableArray.CreateBuilder<ValidationIssue>();
        AddIssue(issues, path, code, expected, actual);
        return new WoundIdentityParseResult(null, issues.ToImmutable());
    }

    private static void AddIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Wound identity index violates client-owned identity authority.",
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Restore one strict client-owned wound identity entry; do not author aliases, carrier discovery, or terminal history evidence here."));

    private static string Describe(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) ? value.GetRawText() : "missing";

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
