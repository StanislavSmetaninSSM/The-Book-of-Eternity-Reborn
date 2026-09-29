using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

/// <summary>
/// Retains and replays exact allocation values without granting execution or persistence authority.
/// An owning capture must validate causal coordinates and successful execution before committing rows.
/// </summary>
internal sealed partial class SpiritualWoundReplayJournal
{
    private static readonly HashSet<string> IdentityKinds = new(StringComparer.Ordinal)
    {
        "effect", "effect_transition", "effect_resolution", "combatant", "member", "vehicle",
        "resource_definition", "resource_definition_seal", "resource_resolution",
        "resource_operation", "resource_transition", "item", "location", "location_receipt",
        "location_link", "location_link_receipt", "location_transition", "location_threat"
    };

    private readonly JsonArray _rows;
    private readonly HashSet<(string Kind, string Owner, string Coordinate)> _keys = new();
    private readonly HashSet<string> _identities = new(StringComparer.Ordinal);
    private bool _append;
    private readonly bool _requireScope;
    private bool _requesting;
    private int _position;
    private bool _faulted;

    /// <summary>
    /// Gets whether the attempt has avoided a permanent fault; this does not certify replay completion.
    /// </summary>
    internal bool IsHealthy => !_faulted;

    /// <summary>
    /// Validates and owns the complete retained stream before any allocation request is permitted.
    /// </summary>
    /// <param name="json">
    /// Closed row array; missing or malformed JSON is invalid.
    /// </param>
    /// <param name="append">
    /// Whether new values may be generated after the complete retained prefix is consumed.
    /// </param>
    /// <param name="requireScope">
    /// Requires an active allocation scope for every request when <see langword="true"/>.
    /// </param>
    private SpiritualWoundReplayJournal(string json, bool append, bool requireScope)
    {
        try
        {
            var wrapper = Parse("{\"rows\":" + json + "}");
            Closed(wrapper, "rows");
            _rows = wrapper["rows"] is JsonArray rows
                ? rows.DeepClone().AsArray()
                : throw new FormatException("Journal array required.");
            for (var index = 0; index < _rows.Count; index++)
            {
                var row = _rows[index] as JsonObject ?? throw new FormatException("Journal row object required.");
                Closed(row, "ordinal kind owner coordinate value");
                Integer(row["ordinal"], index, index);
                var key = ReadKey(row);
                ValidateKind(key.Kind);
                Require(_keys.Add(key), "Duplicate allocation coordinate.");
                ValidateValue(key.Kind, Text(row["value"]));
            }
            _append = append;
            _requireScope = requireScope;
        }
        catch (JsonException error)
        {
            throw new FormatException("Invalid journal JSON.", error);
        }
    }

    /// <summary>
    /// Creates a stream that must replay its complete retained prefix before recording new values.
    /// </summary>
    /// <param name="json">
    /// Exact retained row array; use an empty array for the first capture.
    /// </param>
    /// <returns>
    /// Detached replay-then-append stream with no file or gameplay authority.
    /// </returns>
    /// <param name="requireScope">
    /// Enables capture transaction enforcement when <see langword="true"/>; defaults to standalone requests.
    /// </param>
    internal static SpiritualWoundReplayJournal CreateAppend(string json, bool requireScope = false) =>
        new(json, true, requireScope);

    /// <summary>
    /// Creates a strict reconstruction stream that never invokes allocation callbacks.
    /// </summary>
    /// <param name="json">
    /// Exact complete retained row array to reproduce.
    /// </param>
    /// <returns>
    /// Detached strict replay stream.
    /// </returns>
    /// <param name="requireScope">
    /// Requires a capture transaction even for retained cursor consumption when <see langword="true"/>.
    /// Defaults to standalone requests.
    /// </param>
    internal static SpiritualWoundReplayJournal CreateReplay(string json, bool requireScope = false) =>
        new(json, false, requireScope);

    /// <summary>
    /// Gets whether a verified capture can append new owner allocations after its retained replay.
    /// </summary>
    internal bool IsAppendMode => _append;

    /// <summary>
    /// Permits fresh allocations only after every retained row has been replayed and committed.
    /// The caller must independently prove all saved gameplay and packet boundaries before this handoff.
    /// </summary>
    internal void EnableAppendAfterReplay()
    {
        EnsureUsable();
        if (_append || _position != _rows.Count || _activeSpeculation is not null || _requesting)
            throw new InvalidOperationException("A complete committed strict replay is required before append.");
        _append = true;
    }

    /// <summary>
    /// Replays the next exact causal request or records one new owner-generated value.
    /// Any mismatch or callback failure permanently faults this attempt.
    /// </summary>
    /// <param name="kind">
    /// Closed allocation family or utc_time.
    /// </param>
    /// <param name="owner">
    /// Exact subsystem coordinate supplied by the real allocation owner.
    /// </param>
    /// <param name="coordinate">
    /// Stable typed causal operation and slot; a display label or call count alone is insufficient.
    /// </param>
    /// <param name="allocate">
    /// Ordinary random or clock callback used only for a new append-mode row.
    /// </param>
    /// <returns>
    /// Exact retained or newly generated value for this request.
    /// </returns>
    internal string Request(string kind, string owner, string coordinate, Func<string> allocate)
    {
        EnsureUsable();
        if (_requireScope && _requesting)
        {
            Invalidate();
            throw new InvalidOperationException("An allocation callback cannot reenter its journal.");
        }
        var requestScope = _activeSpeculation;
        if (_requireScope) _requesting = true;
        try
        {
            if (_requireScope && requestScope is null)
                throw new InvalidOperationException("A capture allocation requires an active scope.");
            ArgumentNullException.ThrowIfNull(allocate);
            var key = (Kind: Text(JsonValue.Create(kind)), Owner: Text(JsonValue.Create(owner)),
                Coordinate: Text(JsonValue.Create(coordinate)));
            ValidateKind(key.Kind);
            if (_position < _rows.Count)
            {
                var retained = _rows[_position]!.AsObject();
                if (ReadKey(retained) != key)
                    throw new InvalidOperationException("Allocation request differs from retained causal order.");
                _position++;
                return Text(retained["value"]);
            }
            if (!_append)
                throw new InvalidOperationException("Replay requested an allocation beyond the retained stream.");
            if (!_keys.Add(key))
                throw new InvalidOperationException("Allocation coordinate was already consumed.");
            var value = allocate();
            if (_requireScope)
            {
                EnsureUsable();
                if (!ReferenceEquals(requestScope, _activeSpeculation))
                    throw new InvalidOperationException("The allocation scope changed during its callback.");
            }
            ValidateValue(kind, value);
            _rows.Add(new JsonObject
            {
                ["ordinal"] = _rows.Count, ["kind"] = kind, ["owner"] = owner,
                ["coordinate"] = coordinate, ["value"] = value
            });
            _position++;
            return value;
        }
        catch
        {
            _faulted = true;
            throw;
        }
        finally
        {
            if (_requireScope) _requesting = false;
        }
    }

    /// <summary>
    /// Reads the committed allocation position even while retained future rows remain.
    /// This count is comparison evidence, not permission to persist or continue gameplay.
    /// </summary>
    /// <returns>
    /// Number of exact allocation requests committed by this healthy journal.
    /// </returns>
    internal int ReadCursor()
    {
        EnsureUsable();
        if (_activeSpeculation is not null || _requesting)
            throw new InvalidOperationException("Speculative allocations have no committed cursor.");
        return _position;
    }

    /// <summary>
    /// Exports detached comparison rows only after the full retained prefix has been consumed.
    /// This operation does not certify gameplay validity or commit a checkpoint.
    /// </summary>
    /// <returns>
    /// A detached array, provided the stream is neither faulted nor partially replayed.
    /// </returns>
    internal JsonArray Export()
    {
        EnsureUsable();
        if (_activeSpeculation is not null)
            throw new InvalidOperationException("Speculative allocations cannot be exported.");
        if (_position != _rows.Count)
            throw new InvalidOperationException("Retained allocation prefix has not been fully replayed.");
        return _rows.DeepClone().AsArray();
    }

    /// <summary>
    /// Reads only committed rows through the current cursor while later saved rows remain replay-only.
    /// A prefix export is comparison evidence and never permits allocation or persistence.
    /// </summary>
    /// <returns>
    /// Detached committed allocation rows in their original order.
    /// </returns>
    internal JsonArray ExportConsumedPrefix()
    {
        var count = ReadCursor();
        return new JsonArray(_rows.Take(count).Select(row => row?.DeepClone()).ToArray());
    }

    /// <summary>
    /// Revokes this attempt when its owning execution fails after an allocation has succeeded.
    /// Repeated invalidation is harmless; no further request or export is permitted.
    /// </summary>
    internal void Invalidate() => _faulted = true;

    /// <summary>
    /// Reads the ordinally compared typed request key.
    /// </summary>
    /// <param name="row">
    /// Closed retained row whose key fields must be exact identifiers.
    /// </param>
    /// <returns>
    /// Allocation family, owner and causal coordinate.
    /// </returns>
    private static (string Kind, string Owner, string Coordinate) ReadKey(JsonObject row) =>
        (Text(row["kind"]), Text(row["owner"]), Text(row["coordinate"]));

    /// <summary>
    /// Rejects unsupported allocation families.
    /// </summary>
    /// <param name="kind">
    /// Exact requested or retained family.
    /// </param>
    private static void ValidateKind(string kind) =>
        Require(kind == "utc_time" || IdentityKinds.Contains(kind), "Unknown allocation family.");

    /// <summary>
    /// Validates the existing owner value format and rejects repeated permanent identities.
    /// </summary>
    /// <param name="kind">
    /// Previously checked allocation family.
    /// </param>
    /// <param name="value">
    /// Actual owner-generated or retained value; <see langword="null"/>, empty or noncanonical text is invalid.
    /// </param>
    private void ValidateValue(string kind, string value)
    {
        Text(JsonValue.Create(value));
        if (kind == "utc_time")
        {
            Require(DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time) && time.Offset == TimeSpan.Zero &&
                time.ToString("O", CultureInfo.InvariantCulture) == value, "Exact UTC timestamp required.");
            return;
        }
        var prefix = kind switch
        {
            "item" => "itm_",
            "location" => "loc_",
            "location_receipt" => "mlocrec_",
            "location_link" => "lnk_",
            "location_link_receipt" => "mlinkrec_",
            "location_transition" => "mltrn_",
            "location_threat" => "threat_",
            _ => kind + "_"
        };
        Require(value.StartsWith(prefix, StringComparison.Ordinal) && value.Length == prefix.Length + 32 &&
            value[prefix.Length..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'),
            "Exact generated identifier required.");
        Require(_identities.Add(value), "Generated identity was already allocated.");
    }

    /// <summary>
    /// Rejects all further use of a failed attempt.
    /// </summary>
    internal void EnsureUsable()
    {
        if (_faulted) throw new InvalidOperationException("Allocation attempt is faulted.");
    }
}
