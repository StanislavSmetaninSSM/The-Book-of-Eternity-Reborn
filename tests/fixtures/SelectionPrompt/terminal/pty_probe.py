"""One inert actual .NET CursorTop under owned PTY + pinned terminal sidecar."""
import copy, errno, fcntl, json, os, pathlib, pty, select, struct, subprocess, sys, termios, time
from owned_terminal import OwnedTerminal

class PtyWriter:
    def __init__(self, master, slave, deadline):
        self.master=master;self.slave=slave;self.deadline=deadline;self.audit=[]
    def __call__(self,data,kind,seq):
        if time.monotonic()>=self.deadline:raise TimeoutError('Original PTY input deadline')
        if struct.unpack('HHHH',fcntl.ioctl(self.slave,termios.TIOCGWINSZ,b'\0'*8))[:2]!=(25,100):raise ValueError('PTY geometry drift')
        if termios.tcgetattr(self.slave)[3]&(termios.ECHO|termios.ECHONL):raise ValueError('PTY echo enabled')
        # Snapshot fence only: runtime could subsequently toggle flags. Raw admission still applies.
        pending=memoryview(data)
        while pending:
            if time.monotonic()>=self.deadline:raise TimeoutError('Original PTY write deadline')
            if not select.select([], [self.master], [], .02)[1]:continue
            try:count=os.write(self.master,pending)
            except BlockingIOError:continue
            pending=pending[count:]
        self.audit.append({'kind':kind,'seq':seq,'hex':data.hex()})

def main():
    node,prefix,helper,folder=sys.argv[1:];root=pathlib.Path(folder);root.mkdir(exist_ok=False)
    start=time.monotonic();deadline=start+12;master,slave=pty.openpty();original=termios.tcgetattr(slave)
    configured=copy.deepcopy(original);configured[3]&=~(termios.ECHO|termios.ECHONL)
    terminal=None;child=None;failure=None;cleanup=[];eof=False;child_restored=False;owner_restored=False
    writer=PtyWriter(master,slave,deadline);raw=open(root/'pty.raw','xb')
    try:
        termios.tcsetattr(slave,termios.TCSANOW,configured)
        if termios.tcgetattr(slave)!=configured:raise ValueError('Noecho readback')
        fcntl.ioctl(slave,termios.TIOCSWINSZ,struct.pack('HHHH',25,100,0,0));os.set_blocking(master,False)
        terminal=OwnedTerminal(node,prefix,root,deadline,writer)
        env=os.environ.copy();env.update(TERM='xterm-256color',NO_COLOR='1')
        child=subprocess.Popen(['dotnet',helper,'terminal-cursor-probe',str(root/'managed.json')],stdin=slave,stdout=slave,stderr=slave,env=env)
        while True:
            if time.monotonic()>=deadline:raise TimeoutError('Original inert probe deadline')
            if select.select([master],[],[],.02)[0]:
                try:data=os.read(master,65536)
                except OSError as error:
                    if error.errno==errno.EIO:eof=True;break
                    raise
                if not data:eof=True;break
                raw.write(data);raw.flush();terminal.feed(data)
            if child.poll() is not None and slave is not None:
                # Drain all currently readable bytes before checking child baseline.
                if select.select([master],[],[],0)[0]:continue
                child_restored=termios.tcgetattr(slave)==configured
                os.close(slave);slave=None
        child.wait(timeout=max(.001,deadline-time.monotonic()))
        if child.returncode!=0:raise ValueError('Actual .NET probe failed')
        actual=json.loads((root/'managed.json').read_text())
        if actual!={'CursorTop':4,'WindowWidth':100,'WindowHeight':25,'IsInputRedirected':False,'IsOutputRedirected':False,'IsErrorRedirected':False}:raise ValueError('Actual cursor/geometry mismatch')
        replies=[x for x in writer.audit if x['kind']=='terminal-reply']
        if len(replies)!=1 or bytes.fromhex(replies[0]['hex'])!=b'\x1b[5;1R':raise ValueError('Actual query/reply mismatch')
        terminal.close()
        if not child_restored or not eof:raise ValueError('Child baseline/EOF not proved')
    except BaseException as error:failure=repr(error)
    finally:
        if child is not None and child.poll() is None:
            try:child.terminate();child.wait(timeout=1)
            except subprocess.TimeoutExpired:child.kill();child.wait(timeout=1)
            except BaseException as error:cleanup.append(repr(error))
        if terminal is not None and not terminal.closed:
            proof=terminal.abort()
            if not proof['Joined'] or not proof['EOF'] or proof['Errors']:cleanup.append(proof)
        try:
            # PTY master retains termios access after slave close; this is owner restoration only.
            termios.tcsetattr(master,termios.TCSANOW,original);owner_restored=termios.tcgetattr(master)==original
        except BaseException as error:cleanup.append(repr(error))
        if slave is not None:os.close(slave)
        os.close(master);raw.close()
        result={'Success':failure is None and not cleanup and owner_restored,'Failure':failure,'CleanupFailures':cleanup,'EOF':eof,'ChildRestoredConfiguredBaseline':child_restored,'OwnerRestoredOriginal':owner_restored,'ChildExitCode':None if child is None else child.returncode,'Inputs':writer.audit,'OriginalTermios':repr(original),'ConfiguredTermios':repr(configured),'ElapsedSeconds':time.monotonic()-start}
        (root/'result.json').write_text(json.dumps(result,indent=2)+'\n')
    if not result['Success']:raise RuntimeError(result)
if __name__=='__main__':main()
