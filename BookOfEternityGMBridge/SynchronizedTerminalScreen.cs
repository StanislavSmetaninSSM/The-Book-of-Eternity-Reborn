using System.Text;
using System.Text.RegularExpressions;

namespace BookOfEternityGMBridge;

// Pinned, bounded one-cell presentation. This is not a general terminal emulator.
// Queries are observed, never answered. Unsupported output poisons this original
// view; later known repaint cannot certify an unsupported terminal behavior.
internal sealed class SynchronizedTerminalScreen
{
    private readonly string _binding;
    private readonly int _columns, _rows;
    private readonly char[][] _cells;
    private readonly int[][] _foregroundCells;
    private int _foreground = -1;
    private int _row, _column, _savedRow, _savedColumn, _scalarBytes, _scalarValue, _scalarMinimum;
    private bool _saved, _visible, _inFrame, _committed, _dirty = true, _unsupported, _ended, _hasPainted;
    private int _spaceProbePhase;
    private bool _probeCursorUnknown;
    private long _revision;
    private int _frameCharacters;
    private string? _escape;

    internal SynchronizedTerminalScreen(string binding, int columns, int rows)
    {
        _binding = binding;
        _columns = Math.Clamp(columns, 1, 512); _rows = Math.Clamp(rows, 1, 128);
        _unsupported = columns != _columns || rows != _rows;
        _cells = Enumerable.Range(0, _rows).Select(_ => Enumerable.Repeat(' ', _columns).ToArray()).ToArray();
        _foregroundCells = Enumerable.Range(0, _rows).Select(_ => Enumerable.Repeat(-1, _columns).ToArray()).ToArray();
    }

    internal TerminalViewObservation Capture()
    {
        var cells = _cells.Select(row => new string(row)).ToArray();
        return new(_binding, _revision, string.Join("\n", cells.Select(row => row.TrimEnd(' '))).TrimEnd('\n'),
            _committed && !_unsupported && !_ended && !_dirty && !_inFrame && !_probeCursorUnknown && _escape == null && _scalarBytes == 0,
            cells, _row, Math.Min(_column, _columns-1), _visible, _columns, _rows, _column == _columns,
            _foregroundCells.Select(row => row.ToArray()).ToArray());
    }
    internal void Fault() => _ended = true;
    internal void Resize(int columns, int rows) { if (columns != _columns || rows != _rows) Fault(); }

    internal void Feed(ReadOnlySpan<byte> bytes, ReadOnlySpan<char> text)
    {
        ValidateUtf8(bytes);
        foreach (var c in text)
        {
            if (_inFrame && ++_frameCharacters > 1048576) _unsupported = true;
            if (_escape != null) { FeedEscape(c); continue; }
            if (c == '\u001b') { _escape = "\u001b"; continue; }
            if (c == '\r') { if (_probeCursorUnknown) _unsupported = true; _column = 0; _dirty = true; continue; }
            if (c == '\n') { if (_probeCursorUnknown || _column == _columns) _unsupported = true; else Move(_row + 1, _column); continue; }
            if (c == '\b') { if (_probeCursorUnknown || _column == _columns) _unsupported = true; else Move(_row, Math.Max(0, _column - 1)); continue; }
            if (!SupportedCell(c)) { _unsupported = true; continue; }
            Paint(c);
        }
    }

    internal static bool SupportedCell(char c) => c is >= ' ' and <= '~' or >= '\u00a0' and <= '\u024f'
        or >= '\u0370' and <= '\u052f' or >= '\u2500' and <= '\u259f' or >= '\u2800' and <= '\u28ff'
        && c != '\u00ad' && !char.IsControl(c) && char.GetUnicodeCategory(c) is not
            (System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.SpacingCombiningMark
            or System.Globalization.UnicodeCategory.EnclosingMark or System.Globalization.UnicodeCategory.Format);

    private void ValidateUtf8(ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            if (_scalarBytes != 0)
            {
                if ((b & 0xc0) != 0x80) { _unsupported = true; _scalarBytes = 0; }
                else
                {
                    _scalarValue = (_scalarValue << 6) | (b & 63);
                    if (--_scalarBytes == 0 && (_scalarValue < _scalarMinimum || _scalarValue > 0x10ffff || _scalarValue is >= 0xd800 and <= 0xdfff)) _unsupported = true;
                    continue;
                }
            }
            if (b < 128) continue;
            if (b is >= 0xc2 and <= 0xdf) { _scalarBytes = 1; _scalarValue = b & 31; _scalarMinimum = 128; }
            else if (b is >= 0xe0 and <= 0xef) { _scalarBytes = 2; _scalarValue = b & 15; _scalarMinimum = 2048; }
            else if (b is >= 0xf0 and <= 0xf4) { _scalarBytes = 3; _scalarValue = b & 7; _scalarMinimum = 65536; }
            else _unsupported = true;
        }
    }

    private void FeedEscape(char c)
    {
        _escape += c;
        if (_escape.Length > 512) { _unsupported = true; _escape = null; return; }
        if (_escape.Length == 2)
        {
            if (c is not ('[' or ']' or 'P')) { _unsupported = true; _escape = null; }
            return;
        }
        if (_escape[1] == '[')
        {
            if (c is >= '\u0040' and <= '\u007e')
            {
                Csi(_escape[2..]); _escape = null;
            }
            else if (!(char.IsAsciiDigit(c) || c is ';' or '?' or '>' or '$' or ' ')) { _unsupported = true; _escape = null; }
        }
        else if (c == '\u0007' || _escape.EndsWith("\u001b\\", StringComparison.Ordinal))
        {
            if (_escape[1] == 'P' && c == '\u0007') { _unsupported = true; _escape = null; return; }
            var payload = _escape[2..^(c == '\u0007' ? 1 : 2)];
            StringControl(_escape[1], payload); _escape = null;
        }
    }

    private void Csi(string code)
    {
        switch (code)
        {
            case "?2026h":
                if (_inFrame) _unsupported = true;
                _inFrame = true; _frameCharacters = 0; return;
            case "?2026l":
                if (!_inFrame || _probeCursorUnknown) { _unsupported = true; return; }
                _inFrame = false; _dirty = false; _committed = true; _revision++; return;
            case "?25h": _visible = true; _dirty = true; return;
            case "?25l": _visible = false; _dirty = true; return;
            case "s": if (_probeCursorUnknown || _column == _columns) { _unsupported = true; return; } _savedRow = _row; _savedColumn = _column; _saved = true; return;
            case "u": if (!_saved) _unsupported = true; else Realign(_savedRow, _savedColumn); return;
            case "H": Realign(0, 0); return;
            case "K": if (_column == _columns) { _unsupported = true; return; } EraseRow(_row, _column); return;
            case "2K": EraseRow(_row, 0); return;
            case "J": if (_column == _columns) { _unsupported = true; return; } EraseRow(_row, _column); for (var row = _row + 1; row < _rows; row++) EraseRow(row, 0); return;
            case "2J": for (var row = 0; row < _rows; row++) EraseRow(row, 0); return;
            // Exact observed cursor shape/input-only modes and unanswered queries.
            case "0 q": case "1 q": case "6n": case "14t": case ">0q": case "?u":
            case ">4;0m": case ">4;1m": case "?2004h": case "?2004l": case "?2027h": case "?2031h": case "?2031l":
            case "?1004$p": case "?1016$p": case "?2004$p": case "?2026$p": case "?2027$p": case "?2031$p": return;
        }
        if (code.EndsWith('H'))
        {
            var fields = code[..^1].Split(';');
            if (fields.Length == 2 && fields.All(DecimalField) && int.TryParse(fields[0], out var row) && int.TryParse(fields[1], out var column) && row > 0 && row <= _rows && column > 0 && column <= _columns)
            { Realign(row - 1, column - 1); return; }
        }
        if (code.EndsWith('m') && Sgr(code[..^1])) return;
        _unsupported = true;
    }

    private bool Sgr(string code)
    {
        if (code is "0" or "39") { _foreground = -1; return true; }
        if (code is "1" or "49") return true;
        var p = code.Split(';');
        if (p.Length != 5 || p[0] is not ("38" or "48") || p[1] != "2" || !p.Skip(2).All(v => DecimalField(v) && int.TryParse(v, out var n) && n is >= 0 and <= 255)) return false;
        if (p[0] == "38") _foreground = (int.Parse(p[2]) << 16) | (int.Parse(p[3]) << 8) | int.Parse(p[4]);
        return true;
    }
    private static bool DecimalField(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);

    private void StringControl(char kind, string payload)
    {
        if (kind == 'P') { if (payload != "+q4d73") _unsupported = true; return; }
        if (payload is "10;?" or "11;?" or "99;i=opentui-notifications:p=?;" or "1337;Capabilities"
            or "22;" or "0;" or "12;default" or "112") return;
        if (Regex.IsMatch(payload, @"\A4;[0-9]{1,3};\?\z", RegexOptions.CultureInvariant)
            && int.Parse(payload.Split(';')[1]) <= 255) return;
        if (Regex.IsMatch(payload, @"\A12;#[0-9a-fA-F]{6}\z", RegexOptions.CultureInvariant)) return;
        // The observed initial space-size probes are not composer content and
        // cannot certify a frame. No response/capability is generated. They may
        // only precede the first real synchronized repaint and remain untrusted.
        if (_spaceProbePhase < 2 && !_probeCursorUnknown && !_committed && !_inFrame && !_hasPainted && _row == 0 && _column == 0 && _saved && _savedRow == 0 && _savedColumn == 0 &&
            payload == (_spaceProbePhase == 0 ? "66;w=1; " : "66;s=2; "))
        { _spaceProbePhase++; _probeCursorUnknown = true; _dirty = true; return; }
        _unsupported = true;
    }

    private void Realign(int row, int column)
    {
        Move(row, column);
        if (!_unsupported) _probeCursorUnknown = false;
    }
    private void Move(int row, int column)
    {
        _dirty = true;
        if (row < 0 || row >= _rows || column < 0 || column >= _columns) { _unsupported = true; return; }
        _row = row; _column = column;
    }
    private void Paint(char c)
    {
        if (_probeCursorUnknown || _column == _columns) { _unsupported = true; return; } // No implicit-wrap model in this pinned subset.
        if (_row >= _rows || _column >= _columns) { _unsupported = true; return; }
        _foregroundCells[_row][_column] = _foreground; _cells[_row][_column++] = c; _dirty = true; _hasPainted = true;
    }
    private void EraseRow(int row, int start)
    {
        if (_probeCursorUnknown || _column == _columns) { _unsupported = true; return; }
        if (row < 0 || row >= _rows || start < 0 || start > _columns) { _unsupported = true; return; }
        Array.Fill(_cells[row], ' ', start, _columns - start); _dirty = true;
        Array.Fill(_foregroundCells[row], _foreground, start, _columns - start);
    }
}
