"""Reviewed later: exact package runtime DATA, not PTY/game qualification."""
import pathlib, sys, time
from owned_terminal import OwnedTerminal

def main():
    node,prefix,folder=sys.argv[1:]
    pathlib.Path(folder).mkdir(exist_ok=False)
    if True:
        emitted=[]
        terminal=OwnedTerminal(node,prefix,folder,time.monotonic()+10,
                               lambda data,kind,seq:emitted.append((data,kind,seq)))
        try:
            # Query-instant state differs from final write snapshot intentionally.
            result=terminal.feed(b'abc\x1b[6nXYZ')
            assert emitted[-1][0]==b'\x1b[1;4R'
            assert result['replies'][0]['state']=={'x':3,'y':0}
            assert result['cursor']=={'x':6,'y':0}
            for text,width in [('⚙️',1),('⚔️',1),('📦',2),('é',1),('中文',4)]:
                terminal.feed(b'\x1b[2J\x1b[1;1H')
                raw=text.encode()
                for byte in raw:terminal.feed(bytes([byte]))
                result=terminal.feed(b'\x1b[6n')
                assert result['cursor']=={'x':width,'y':0},(text,result)
            # Authored visible-line goldens, not derived from terminal snapshots.
            scenario=['⚙️ Игровое меню','⚔️ Древний меч [Оружие]','📦 Запертая кладовая разметочных материалов','Хозяйственный двор у нижней лестницы','é 中文']
            terminal.feed(b'\x1b[2J\x1b[1;1H'+('\r\n'.join(scenario)).encode())
            snapshot=terminal.snapshot()
            assert snapshot['lines']==scenario+['']*20,snapshot
            for separator,expected in [(b'\n','   Z'),(b'\r\n','Z')]:
                terminal.feed(b'\x1b[2J\x1b[1;1Habc'+separator+b'Z')
                snapshot=terminal.snapshot()
                assert snapshot['lines']==['abc',expected]+['']*23,snapshot
            terminal.feed(b'\x1b[2J\x1b[1;1H'+b'a'*99)
            result=terminal.feed('📦'.encode()+b'\x1b[6n')
            assert result['cursor']=={'x':2,'y':1}
            terminal.feed(b'\x1b[2J\x1b[1;1H'+b'line\r\n'*26)
            result=terminal.feed(b'\x1b[6n');assert result['cursor']=={'x':0,'y':24}
            # Exact cooperative-input CSI save/restore, with authored cells/cursor.
            terminal.feed(b'\x1b[2J\x1b[1;1H >  \x1b[s\x1b[3;8Haway\x1b[u')
            snapshot=terminal.snapshot()
            assert snapshot['cursor']=={'x':4,'y':0},snapshot
            assert snapshot['lines']==[' >','','       away']+['']*22,snapshot
            terminal.feed(b'\x1b[J/options\x1b[u')
            snapshot=terminal.snapshot()
            assert snapshot['cursor']=={'x':4,'y':0},snapshot
            assert snapshot['lines']==[' >  /options']+['']*24,snapshot
            terminal.close()
        finally:
            # No successful cleanup claim if protocol failed; owner records exact child.
            if not terminal.closed:
                terminal.abort()
    print('terminal-runtime DATA passed')
if __name__=='__main__':main()
