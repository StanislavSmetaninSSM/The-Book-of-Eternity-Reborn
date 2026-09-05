using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class EffectDetachedRollSourceAuthority
{
    private const int MaximumRows = 10_000;

    private readonly ReadOnlyCollection<EffectDetachedRollSourceRow> _rows;

    [System.Text.Json.Serialization.JsonConstructor]
    private EffectDetachedRollSourceAuthority(
        int schemaVersion,
        IReadOnlyList<EffectDetachedRollSourceRow> rows,
        string authorityFingerprint)
    {
        if (rows is null ||
            rows.Count > MaximumRows ||
            rows.Any(static row => row is null))
        {
            throw new System.Text.Json.JsonException(
                "rollSourceAuthority.rows must be a non-null row collection.");
        }

        if (authorityFingerprint is null)
        {
            throw new System.Text.Json.JsonException(
                "rollSourceAuthority.authorityFingerprint is required.");
        }

        SchemaVersion = schemaVersion;
        _rows = new ReadOnlyCollection<EffectDetachedRollSourceRow>(
            rows.Select(static row => row.CloneDetached()).ToArray());
        AuthorityFingerprint = authorityFingerprint;
    }

    public int SchemaVersion { get; }

    public IReadOnlyList<EffectDetachedRollSourceRow> Rows => _rows;

    public string AuthorityFingerprint { get; }

    internal static EffectDetachedRollSourceAuthority Create(
        IReadOnlyList<EffectDetachedRollSourceRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > MaximumRows)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rows),
                "Detached roll source authority exceeds the maximum row count.");
        }

        var detached = rows.Select(static row => row.CloneDetached()).ToArray();
        return new EffectDetachedRollSourceAuthority(
            1,
            detached,
            Fingerprint(1, detached));
    }

    internal bool HasValidSeal(out IReadOnlyList<ValidationIssue> issues)
    {
        if (SchemaVersion != 1 ||
            _rows.Count > MaximumRows ||
            _rows.Any(static row => row is null) ||
            !RowsValid(_rows))
        {
            issues = InvalidIssues("effect_detached_roll_source_invalid");
            return false;
        }

        if (!string.Equals(
                AuthorityFingerprint,
                Fingerprint(SchemaVersion, _rows),
                StringComparison.Ordinal))
        {
            issues = InvalidIssues("effect_detached_roll_source_fingerprint_invalid");
            return false;
        }

        issues = Array.Empty<ValidationIssue>();
        return true;
    }

    internal bool SemanticallyEquals(EffectDetachedRollSourceAuthority other)
    {
        if (other is null ||
            !HasValidSeal(out _) ||
            !other.HasValidSeal(out _) ||
            SchemaVersion != other.SchemaVersion ||
            _rows.Count != other._rows.Count ||
            !string.Equals(
                AuthorityFingerprint,
                other.AuthorityFingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }

        return _rows.Zip(other._rows).All(
            static pair => pair.First.SemanticallyEquals(pair.Second));
    }

    internal EffectDetachedRollSourceAuthority CloneDetached()
    {
        return new EffectDetachedRollSourceAuthority(
            SchemaVersion,
            _rows,
            AuthorityFingerprint);
    }

    private static bool RowsValid(IReadOnlyList<EffectDetachedRollSourceRow> rows)
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!row.IsValid(index))
            {
                return false;
            }

            var exactKey = row.EffectId + "\u001f" + row.ComponentId;
            var confusableKey = ExactIdentifierConfusableKey.Build(row.EffectId) +
                "\u001f" + ExactIdentifierConfusableKey.Build(row.ComponentId);
            if (!exact.Add(exactKey) || !confusable.Add(confusableKey))
            {
                return false;
            }
        }

        return true;
    }

    private static string Fingerprint(
        int schemaVersion,
        IReadOnlyList<EffectDetachedRollSourceRow> rows)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = schemaVersion,
            ["rows"] = new JsonArray(rows.Select(
                static row => (JsonNode?)row.ToJson()).ToArray())
        };
        var canonical = WoundAcceptedTurnFingerprintWriter.CanonicalJson(root) ?? string.Empty;
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static IReadOnlyList<ValidationIssue> InvalidIssues(string code)
    {
        return Array.AsReadOnly(new[]
        {
            new ValidationIssue(
                "treatmentAttempt.procedureCheck.rollSourceAuthority",
                IssueSeverity.Error,
                "Detached roll sources must be a sealed normalized mechanics authority.",
                code: code,
                section: "effect_materialization")
        });
    }
}

internal sealed class EffectDetachedRollSourceRow
{
    private static readonly HashSet<string> RegisteredOperations = new(
        StringComparer.Ordinal)
    {
        "attack_roll",
        "defense_roll",
        "skill_check",
        "saving_throw",
        "damage_roll",
        "initiative_roll"
    };

    private readonly ReadOnlyCollection<string> _operations;

    [System.Text.Json.Serialization.JsonConstructor]
    private EffectDetachedRollSourceRow(
        int ordinal,
        string effectId,
        string componentId,
        string realm,
        string targetKind,
        string targetId,
        IReadOnlyList<string> operations,
        string contribution,
        string scopeKind,
        string? scopeSkillId)
    {
        if (effectId is null ||
            componentId is null ||
            realm is null ||
            targetKind is null ||
            targetId is null ||
            operations is null ||
            operations.Count > 6 ||
            contribution is null ||
            scopeKind is null)
        {
            throw new System.Text.Json.JsonException(
                "rollSourceAuthority rows must contain non-null mechanical fields.");
        }

        Ordinal = ordinal;
        EffectId = effectId;
        ComponentId = componentId;
        Realm = realm;
        TargetKind = targetKind;
        TargetId = targetId;
        _operations = new ReadOnlyCollection<string>(operations.ToArray());
        Contribution = contribution;
        ScopeKind = scopeKind;
        ScopeSkillId = scopeSkillId;
    }

    public int Ordinal { get; }

    public string EffectId { get; }

    public string ComponentId { get; }

    public string Realm { get; }

    public string TargetKind { get; }

    public string TargetId { get; }

    public IReadOnlyList<string> Operations => _operations;

    public string Contribution { get; }

    public string ScopeKind { get; }

    public string? ScopeSkillId { get; }

    internal static EffectDetachedRollSourceRow Create(
        int ordinal,
        string effectId,
        string componentId,
        string realm,
        string targetKind,
        string targetId,
        IReadOnlyList<string> operations,
        string contribution,
        string scopeKind,
        string? scopeSkillId)
    {
        return new EffectDetachedRollSourceRow(
            ordinal,
            effectId,
            componentId,
            realm,
            targetKind,
            targetId,
            operations,
            contribution,
            scopeKind,
            scopeSkillId);
    }

    internal EffectDetachedRollSourceRow CloneDetached()
    {
        return new EffectDetachedRollSourceRow(
            Ordinal,
            EffectId,
            ComponentId,
            Realm,
            TargetKind,
            TargetId,
            _operations,
            Contribution,
            ScopeKind,
            ScopeSkillId);
    }

    internal bool SemanticallyEquals(EffectDetachedRollSourceRow other)
    {
        return other is not null &&
            Ordinal == other.Ordinal &&
            string.Equals(EffectId, other.EffectId, StringComparison.Ordinal) &&
            string.Equals(ComponentId, other.ComponentId, StringComparison.Ordinal) &&
            string.Equals(Realm, other.Realm, StringComparison.Ordinal) &&
            string.Equals(TargetKind, other.TargetKind, StringComparison.Ordinal) &&
            string.Equals(TargetId, other.TargetId, StringComparison.Ordinal) &&
            _operations.SequenceEqual(other._operations, StringComparer.Ordinal) &&
            string.Equals(Contribution, other.Contribution, StringComparison.Ordinal) &&
            string.Equals(ScopeKind, other.ScopeKind, StringComparison.Ordinal) &&
            string.Equals(ScopeSkillId, other.ScopeSkillId, StringComparison.Ordinal);
    }

    internal bool IsValid(int expectedOrdinal)
    {
        return Ordinal == expectedOrdinal &&
            ResourceMaterializationContract.IsExactIdentifier(EffectId) &&
            ResourceMaterializationContract.IsExactIdentifier(ComponentId) &&
            ResourceMaterializationContract.IsExactIdentifier(Realm) &&
            ResourceMaterializationContract.IsExactIdentifier(TargetKind) &&
            ResourceMaterializationContract.IsExactIdentifier(TargetId) &&
            _operations.Count is >= 1 and <= 6 &&
            _operations.All(RegisteredOperations.Contains) &&
            _operations.Distinct(StringComparer.Ordinal).Count() == _operations.Count &&
            Contribution is "advantage" or "disadvantage" &&
            ((ScopeKind == "all" && ScopeSkillId is null) ||
             (ScopeKind == "skill" &&
              ResourceMaterializationContract.IsExactIdentifier(ScopeSkillId) &&
              _operations.Count == 1 &&
              string.Equals(
                  _operations[0],
                  "skill_check",
                  StringComparison.Ordinal)));
    }

    internal JsonObject ToJson()
    {
        return new JsonObject
        {
            ["ordinal"] = Ordinal,
            ["effectId"] = EffectId,
            ["componentId"] = ComponentId,
            ["realm"] = Realm,
            ["targetKind"] = TargetKind,
            ["targetId"] = TargetId,
            ["operations"] = new JsonArray(_operations.Select(
                static value => JsonValue.Create(value)).ToArray()),
            ["contribution"] = Contribution,
            ["scopeKind"] = ScopeKind,
            ["scopeSkillId"] = ScopeSkillId
        };
    }
}

internal sealed record EffectRollSkillUsabilityProof(
    string Realm,
    string ActorKind,
    string ActorId,
    string SkillId);

internal sealed record EffectRollSourceCaptureResult(
    bool IsValid,
    EffectDetachedRollSourceAuthority? Authority,
    IReadOnlyList<ValidationIssue> Issues);
