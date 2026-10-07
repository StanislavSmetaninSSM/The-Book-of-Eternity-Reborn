using BookOfEternityClient.Configuration;

namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    private const string MiniPlaceholder="Ask anything... \"Fix a TODO in the codebase\"";
    private bool IsMiniEmptyIdle(GmCliInputProfile profile)
    {
        var view=CaptureTerminalView();
        return _inputLifetime is {ManualTakeover:false,MiniPasteAttempted:false} && IsMiniInitialFrame(profile,view);
    }
    private static bool IsMiniInitialFrame(GmCliInputProfile profile,TerminalViewObservation view)
    {
        if(!profile.IsSupported || !view.Reliable || !view.CursorVisible || view.PendingWrap || view.Cells==null || view.Foreground==null || view.CursorColumn!=0)
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

    // These raw spans prove focused, responsive visible edges only. Full contents
    // come from the independently revalidated file; no screen Composer is fabricated.
    private bool MiniDraftRegion(PromptOperation op,out TerminalViewObservation v,out int start,out int last)
    {
        var p=op.Snapshot.Profile;v=CaptureTerminalView();start=p.StartupBannerLines.Length;last=-1;
        if(!v.Reliable || !v.CursorVisible || v.PendingWrap || v.Cells==null || v.Foreground==null ||
            v.CursorColumn<0 || v.CursorColumn>=v.Columns || v.CursorRow<start)return false;
        for(var i=0;i<start;i++)if(p.StartupBannerLines[i].Length>v.Columns || v.Cells[i]!=p.StartupBannerLines[i].PadRight(v.Columns))return false;
        var cells=v.Cells;var footers=Enumerable.Range(start+2,Math.Max(0,Math.Min(6,cells.Length-start-2))).Where(i=>MiniIdleFooter(p,cells[i])).ToArray();
        if(footers.Length!=1)return false;var footer=footers[0];last=footer-2;
        if(last-start is <0 or >5 || v.CursorRow>last || v.Cells[footer-1].Any(c=>c!=' ') ||
            v.Cells.Skip(footer+1).Any(r=>r.Any(c=>c!=' ')))return false;
        for(var r=start;r<=last;r++)for(var col=0;col<v.Columns;col++)
            // SGR39 in the pinned completed paste uses the parser's default
            // sentinel; this makes no promise about the terminal's actual RGB.
            if(v.Cells[r][col]!=' ' && v.Foreground[r][col] is not (-1 or 0xe2e8f0))return false;
        return true;
    }
    private bool IsMiniEdge(PromptOperation op,bool home)
    {
        if(!MiniDraftRegion(op,out var v,out var start,out var last))return false;
        var edge=home?op.FirstEdge:op.LastEdge;
        if(edge.Length==0 || edge.Length>v.Columns-2)return false;
        if(home)return v.CursorRow==start && v.CursorColumn==0 && v.Cells[start].AsSpan(0,edge.Length).SequenceEqual(edge);
        return v.CursorRow==last && v.CursorColumn>=edge.Length &&
            v.Cells[last].AsSpan(v.CursorColumn-edge.Length,edge.Length).SequenceEqual(edge) &&
            v.Cells[last].AsSpan(v.CursorColumn).IndexOfAnyExcept(' ')<0;
    }

    // Exact deduplicated width=8, blocks, trailSteps=6 frames from pinned
    // OpenCode53d1eabb packages/tui/src/ui/spinner.ts; not a general spinner/TUI.
    private static readonly HashSet<string> MiniBusyFrames=new(StringComparer.Ordinal) {
        "■⬝⬝⬝⬝⬝⬝⬝",
        "■■⬝⬝⬝⬝⬝⬝",
        "■■■⬝⬝⬝⬝⬝",
        "■■■■⬝⬝⬝⬝",
        "■■■■■⬝⬝⬝",
        "■■■■■■⬝⬝",
        "⬝■■■■■■⬝",
        "⬝⬝■■■■■■",
        "⬝⬝⬝⬝⬝⬝■■",
        "⬝⬝⬝⬝⬝⬝⬝■",
        "⬝⬝⬝⬝⬝■■■",
        "⬝⬝⬝⬝■■■■",
        "⬝⬝⬝■■■■■",
        "⬝⬝⬝⬝⬝⬝⬝⬝",
    };

    private bool IsMiniWorking(GmCliInputProfile p)
    {
        var v=CaptureTerminalView();
        var start=p.StartupBannerLines.Length;var footer=start+2;
        if(!v.Reliable || v.PendingWrap || v.Cells==null || footer>=v.Cells.Length || v.CursorRow!=start || v.CursorColumn!=0)return false;
        for(var i=0;i<start;i++)if(p.StartupBannerLines[i].Length>v.Columns || v.Cells[i]!=p.StartupBannerLines[i].PadRight(v.Columns))return false;
        if(v.Cells[start].Any(c=>c!=' ') || v.Cells[start+1].Any(c=>c!=' ') || v.Cells.Skip(footer+1).Any(row=>row.Any(c=>c!=' ')))return false;
        var row=v.Cells[footer];const string suffix="ctrl+p cmd ";const string marker=" esc interrupt";
        return row.Length>=16+marker.Length+suffix.Length && row.StartsWith(" BUILD  ",StringComparison.Ordinal) && MiniBusyFrames.Contains(row.Substring(8,8)) &&
            row.AsSpan(16,marker.Length).SequenceEqual(marker) && row.EndsWith(suffix,StringComparison.Ordinal) &&
            row.AsSpan(16+marker.Length,row.Length-16-marker.Length-suffix.Length).IndexOfAnyExcept(' ')<0;
    }
}
