"""Closed actual-source main/player DATA. No Node, child, provider or world."""
import json,types,unittest
from client_ui import MAIN_CHOICES,MAIN_HEADING,MAIN_FOOTER,main_ready,player_ready

def grid(selected):
    lines=[MAIN_HEADING,'']+[('➤ ' if i==selected else '◆ ')+str(i+1)+'. '+name for i,name in enumerate(MAIN_CHOICES)]+[MAIN_FOOTER]
    return ('\r\n'.join(lines)).encode(),[s for s in lines if s]+['']*15

def view(raw,lines,cursor=None):return types.SimpleNamespace(pending=False,cut=len(raw),lines=lines,cursor=cursor or {'x':0,'y':0})

class ClientUiData(unittest.TestCase):
    def test_exact_compact_main_continue_and_exit(self):
        for selected in (0,7):
            raw,lines=grid(selected);v=view(raw,lines)
            self.assertTrue(main_ready(v,raw,0,selected))
            styled=raw.replace(('➤ '+str(selected+1)+'. '+MAIN_CHOICES[selected]).encode(),('\x1b[1m  ➤ '+str(selected+1)+'. '+MAIN_CHOICES[selected]+'  \x1b[0m').encode())
            self.assertTrue(main_ready(view(styled,lines),styled,0,selected))
            self.assertFalse(main_ready(v,raw,0,7-selected))
    def test_missing_reordered_duplicate_rows_refuse(self):
        raw,lines=grid(0)
        for broken in (raw.replace(MAIN_CHOICES[1].encode(),b''),raw.replace(b'2. ',b'3. ',1),raw.replace(b'3. ',b'2. ',1)):
            self.assertFalse(main_ready(view(broken,lines),broken,0,0))
    def test_partial_newest_grid_never_uses_old_cells(self):
        raw,lines=grid(0)
        for tail in (b'\x1b[0m',b'\x1b[4;1H',raw[:-8],b'\x1b[2K'):
            whole=raw+tail;self.assertFalse(main_ready(view(whole,lines),whole,len(raw),0))
    def test_main_cells_and_pending_must_match(self):
        raw,lines=grid(0);v=view(raw,lines);v.pending=True
        self.assertFalse(main_ready(v,raw,0,0))
        v.pending=False;v.lines=['wrong']*25
        self.assertFalse(main_ready(v,raw,0,0))
    def test_player_complete_system_input_suffix(self):
        raw='── ✦ Ваш ход ✦ ──\r\n  /help\r\n >  \x1b[s'.encode()
        lines=['── ✦ Ваш ход ✦ ──','  /help',' >']+['']*22
        self.assertTrue(player_ready(view(raw,lines,{'x':4,'y':2}),raw,0))
        styled=raw.replace(b' >  ',b'\x1b[1m > \x1b[0m ')
        self.assertTrue(player_ready(view(styled,lines,{'x':4,'y':2}),styled,0))
        for split in range(len(raw)):
            part=raw[:split]
            self.assertFalse(player_ready(view(part,lines,{'x':4,'y':2}),part,0))
    def test_heading_or_prompt_alone_or_reversed_refuses(self):
        for raw in ('✦ Ваш ход ✦',' >  \x1b[s',' >  \x1b[s\n✦ Ваш ход ✦'):
            b=raw.encode();self.assertFalse(player_ready(view(b,[' >']*25,{'x':4,'y':0}),b,0))
    def test_draft_multiline_and_postprompt_bytes_refuse(self):
        raw='✦ Ваш ход ✦\r\n >  \x1b[s'.encode();lines=['✦ Ваш ход ✦',' >']+['']*23
        for b in (raw+b'x',raw+b'\x1b[u',raw+b'\r\n',b'\x1b[2J'+raw+b'partial',('Сохранённый черновик\n').encode()+raw,raw.replace(b' >  ',' │ '.encode())):
            self.assertFalse(player_ready(view(b,lines,{'x':4,'y':1}),b,0))
    def test_wrong_cursor_cells_or_pending_refuse(self):
        raw='✦ Ваш ход ✦\r\n >  \x1b[s'.encode();lines=['✦ Ваш ход ✦',' >']+['']*23
        for cursor in ({'x':3,'y':1},{'x':4,'y':0}):self.assertFalse(player_ready(view(raw,lines,cursor),raw,0))
        v=view(raw,lines,{'x':4,'y':1});v.pending=True
        self.assertFalse(player_ready(v,raw,0))

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(ClientUiData);assert suite.countTestCases()==8
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    print(json.dumps({'Suite':'client-main-player-data','Expected':8,'Executed':result.testsRun,'Success':result.wasSuccessful() and not result.skipped}))
    raise SystemExit(0 if result.wasSuccessful() and result.testsRun==8 and not result.skipped else 1)
