using System.Text;

namespace BookOfEternityGMBridge;

internal sealed record TerminalViewObservation(string BindingId, long Revision, string Text, bool Reliable);

// Controlled neutral-v1 view only. UTF8, CR/LF/BS, CSI 2J and cursor home.
// Unknown VT/width/alternate-screen behavior cannot certify an idle composer.
internal sealed class TerminalScreen(string bindingId)
{
    private readonly List<StringBuilder> _lines = [new()];
    private int _row, _column, _scalarBytes, _scalarValue, _scalarMinimum;
    private string? _escape;
    private bool _invalid = true, _ended, _awaitingHome;
    private long _revision;
    internal TerminalViewObservation Capture() => new(bindingId, _revision,
        string.Join("\n", _lines.Select(l => l.ToString())).TrimEnd('\n'), !_invalid && !_ended && !_awaitingHome && _scalarBytes == 0 && _escape == null);
    internal void Fault() { _invalid = true; _ended = true; }
    internal void Feed(ReadOnlySpan<byte> bytes, ReadOnlySpan<char> text)
    {
        foreach (var b in bytes)
        {
            if (_scalarBytes != 0)
            {
                if ((b & 0xc0) != 0x80) { _invalid = true; _scalarBytes = 0; }
                else {
                    _scalarValue = (_scalarValue << 6) | (b & 63);
                    if (--_scalarBytes == 0 && (_scalarValue < _scalarMinimum || _scalarValue > 0x10ffff || _scalarValue is >= 0xd800 and <= 0xdfff)) _invalid = true;
                    continue;
                }
            }
            if (b < 128) continue;
            if (b is >= 0xc2 and <= 0xdf) { _scalarBytes=1; _scalarValue=b&31; _scalarMinimum=128; }
            else if (b is >= 0xe0 and <= 0xef) { _scalarBytes=2; _scalarValue=b&15; _scalarMinimum=2048; }
            else if (b is >= 0xf0 and <= 0xf4) { _scalarBytes=3; _scalarValue=b&7; _scalarMinimum=65536; }
            else _invalid=true;
        }
        foreach (var c in text)
        {
            if (_escape != null)
            {
                _escape += c;
                if (_escape.Length == 2 && c != '[') { _invalid=true; _escape=null; continue; }
                if (_escape.Length <= 2) continue;
                if (char.IsAsciiLetter(c) || c == '~')
                {
                    if (_escape == "\u001b[2J") { _lines.Clear(); _lines.Add(new()); _invalid=false; _awaitingHome=true; }
                    else if (_escape is "\u001b[H" or "\u001b[1;1H") { _row=_column=0; _awaitingHome=false; }
                    else _invalid=true;
                    _escape=null;
                }
                else if (_escape.Length > 16 || !(char.IsAsciiDigit(c) || c == ';')) { _invalid=true; _escape=null; }
                continue;
            }
            if (c == '\u001b') { _escape="\u001b"; continue; }
            if (c == '\r') { _column=0; continue; }
            if (c == '\n') { if (++_row >= 128) { _invalid=true; _row=127; } while (_lines.Count<=_row) _lines.Add(new()); continue; }
            if (c == '\b') { _column=Math.Max(0,_column-1); continue; }
            if (c < ' ' || c is '\u007f' or '\ufffd') { _invalid=true; continue; }
            if (_column >= 4096) { _invalid=true; continue; }
            var line=_lines[_row]; while(line.Length<_column)line.Append(' ');
            if (_column<line.Length)line[_column]=c;else line.Append(c); _column++;
        }
        if (text.Length>0 && !_invalid && !_awaitingHome && _scalarBytes==0 && _escape==null) _revision++;
    }
}
