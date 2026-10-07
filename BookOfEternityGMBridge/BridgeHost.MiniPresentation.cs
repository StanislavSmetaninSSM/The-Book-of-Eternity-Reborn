using BookOfEternityClient.Configuration;

namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    private const string MiniPlaceholder="Ask anything... \"Fix a TODO in the codebase\"";
    private bool IsMiniEmptyIdle(GmCliInputProfile profile)
    {
        var view=CaptureTerminalView();
        if(!profile.IsSupported || !view.Reliable || !view.CursorVisible || view.PendingWrap || view.Cells==null || view.Foreground==null || view.CursorColumn!=0)
            return false;
        var rows=view.Cells;var footer=view.CursorRow+2;
        if(footer>=rows.Length || !MiniIdleFooter(profile,rows[footer]) || rows[footer-1].Any(c=>c!=' '))return false;
        var draft=rows[view.CursorRow];
        if(draft.Any(c=>c!=' ')) {
            if(draft!=MiniPlaceholder.PadRight(view.Columns) || view.Foreground[view.CursorRow].Take(MiniPlaceholder.Length).Any(c=>c!=0x64748b))return false;
        }
        // Gate/panel evidence never borrows an identical placeholder line.
        return !profile.BlockedMarkers.Any(marker=>rows.Where((_,i)=>i!=view.CursorRow).Any(row=>row.Contains(marker,StringComparison.OrdinalIgnoreCase)));
    }
    private static bool MiniIdleFooter(GmCliInputProfile p,string row)
    {
        const string suffix="ctrl+p cmd ";
        if(row.Length<40 || p.IdleMarker!=" BUILD" || !row.StartsWith(" BUILD  ",StringComparison.Ordinal) || !row.EndsWith(suffix,StringComparison.Ordinal))return false;
        return row.AsSpan(7,row.Length-7-suffix.Length).IndexOfAnyExcept(' ')<0;
    }
}
