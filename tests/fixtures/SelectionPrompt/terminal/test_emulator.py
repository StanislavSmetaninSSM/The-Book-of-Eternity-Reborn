"""Reviewed later: exact package runtime DATA, not PTY/game qualification."""
import pathlib, sys, tempfile, time
from owned_terminal import OwnedTerminal

def main():
    node,prefix=sys.argv[1:]
    with tempfile.TemporaryDirectory(prefix='boe-terminal-data-') as folder:
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
            terminal.feed(b'\x1b[2J\x1b[1;1H'+b'a'*99)
            result=terminal.feed('📦'.encode()+b'\x1b[6n')
            assert result['cursor']=={'x':2,'y':1}
            terminal.feed(b'\x1b[2J\x1b[1;1H'+b'line\r\n'*26)
            result=terminal.feed(b'\x1b[6n');assert result['cursor']=={'x':0,'y':24}
            terminal.close()
        finally:
            # No successful cleanup claim if protocol failed; owner records exact child.
            if not terminal.closed:
                terminal.process.terminate();terminal.process.wait(timeout=2)
                terminal.process.stdin.close();terminal.process.stdout.close();terminal.stderr.close()
    print('terminal-runtime DATA passed')
if __name__=='__main__':main()
