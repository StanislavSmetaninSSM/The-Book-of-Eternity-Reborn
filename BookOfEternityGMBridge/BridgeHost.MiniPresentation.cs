using BookOfEternityClient.Configuration;

namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    private const string MiniPlaceholder="Ask anything... \"Fix a TODO in the codebase\"";
    private bool IsMiniEmptyIdle(GmCliInputProfile profile)
    {
        var view=CaptureTerminalView();
        if(!profile.IsSupported || _inputLifetime is not {ManualTakeover:false,MiniPasteAttempted:false} || !view.Reliable || !view.CursorVisible || view.PendingWrap || view.Cells==null || view.Foreground==null || view.CursorColumn!=0)
            return false;
        var rows=view.Cells;var footer=view.CursorRow+2;
        if(footer>=rows.Length || !MiniIdleFooter(profile,rows[footer]) || rows[footer-1].Any(c=>c!=' '))return false;
        if(view.CursorRow!=profile.StartupBannerLines.Length || MiniPlaceholder.Length>view.Columns ||
            rows[view.CursorRow]!=MiniPlaceholder.PadRight(view.Columns) ||
            view.Foreground[view.CursorRow].Take(MiniPlaceholder.Length).Any(c=>c!=0x64748b))return false;
        // Qualification is deliberately one first submission. All current cells
        // outside the composer must match the frozen original startup; keyword
        // absence or an unchanged footer never licenses an unknown panel.
        for(var i=0;i<view.CursorRow;i++)
            if(profile.StartupBannerLines[i].Length>view.Columns || rows[i]!=profile.StartupBannerLines[i].PadRight(view.Columns))return false;
        return rows.Skip(footer+1).All(row=>row.All(c=>c==' '));
    }
    private static bool MiniIdleFooter(GmCliInputProfile p,string row)
    {
        const string suffix="ctrl+p cmd ";
        if(row.Length<40 || p.IdleMarker!=" BUILD" || !row.StartsWith(" BUILD  ",StringComparison.Ordinal) || !row.EndsWith(suffix,StringComparison.Ordinal))return false;
        return row.AsSpan(7,row.Length-7-suffix.Length).IndexOfAnyExcept(' ')<0;
    }
}
