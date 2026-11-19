"""Existing genuine Continue fixture: actual System input, no provider or scripted input."""
import copy, errno, fcntl, json, os, pathlib, pty, select, struct, subprocess, sys, termios, time
sys.dont_write_bytecode=True
from owned_terminal import OwnedTerminal
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent.parent))
from fullroute_inputs import AcknowledgedTerminalView, fresh_route_key
from client_ui import main_ready, player_ready

from pty_probe import PtyWriter

def main():
    node,prefix,client,base,folder=sys.argv[1:];root=pathlib.Path(folder);root.mkdir(exist_ok=False)
    start=time.monotonic();deadline=start+8;master,slave=pty.openpty();original=termios.tcgetattr(slave)
    configured=copy.deepcopy(original);configured[3]&=~(termios.ECHO|termios.ECHONL)
    terminal=None;child=None;failure=None;cleanup=[];eof=False;child_restored=False;owner_restored=False;foreground=None
    writer=PtyWriter(master,master,deadline);raw=open(root/'pty.raw','xb');captured=bytearray();stages=[]
    try:
        termios.tcsetattr(slave,termios.TCSANOW,configured)
        if termios.tcgetattr(slave)!=configured:raise ValueError('Noecho readback')
        fcntl.ioctl(slave,termios.TIOCSWINSZ,struct.pack('HHHH',25,100,0,0));os.set_blocking(master,False)
        terminal=OwnedTerminal(node,prefix,root,deadline,writer)
        env=os.environ.copy();env.update(TERM='xterm-256color',NO_COLOR='1')
        def own():
            os.setsid();fcntl.ioctl(slave,termios.TIOCSCTTY,0);os.tcsetpgrp(slave,os.getpid())
        child=subprocess.Popen(['dotnet',client,base,'--plain-output'],stdin=slave,stdout=slave,stderr=slave,env=env,preexec_fn=own)
        foreground={'Pid':child.pid,'SessionId':os.getsid(child.pid),'ProcessGroupId':os.getpgid(child.pid),'TtyForegroundGroup':os.tcgetpgrp(master)}
        if any(value!=child.pid for value in foreground.values()):raise ValueError('Original foreground controlling PTY identity mismatch')
        view=AcknowledgedTerminalView(terminal,captured)
        def pump():
            if time.monotonic()>=deadline:raise TimeoutError('Original eight-second navigation deadline')
            if select.select([master],[],[],.01)[0]:
                try:data=os.read(master,65536)
                except BlockingIOError:return
                raw.write(data);raw.flush();captured.extend(data);view.observe(data)
        def wait(predicate,label):
            while True:
                if time.monotonic()>=deadline:raise TimeoutError(label)
                if predicate():stages.append(label);return
                if child.poll() is not None:raise ValueError('Child exited before '+label)
                pump()
        def drain():
            while select.select([master],[],[],0)[0]:pump()
        def send(data,label,predicate):
            fresh_route_key(terminal,view,drain,data,label,predicate,deadline)
            return len(captured)
        wait(lambda:main_ready(view,bytes(captured),0,0),'actual main Continue selected')
        cut=send(b'\r','Continue',lambda:main_ready(view,bytes(captured),0,0))
        wait(lambda:player_ready(view,bytes(captured),cut),'actual cooperative empty player prompt')
        cut=send(b'/options\r','local options command',lambda:player_ready(view,bytes(captured),cut))
        wait(lambda:view.options(cut,'💾 Сохранить',True),'actual Options grid')
        prior=cut;cut=send(b'4','Options select main exit',lambda:view.options(prior,'💾 Сохранить',True))
        wait(lambda:view.options(cut,'🏠 Выйти в главное меню',False),'actual selected main exit')
        prior=cut;cut=send(b'\r','Options Enter',lambda:view.options(prior,'🏠 Выйти в главное меню',False))
        wait(lambda:main_ready(view,bytes(captured),cut,0),'actual returned main menu')
        prior=cut;cut=send(b'8','main select Exit',lambda:main_ready(view,bytes(captured),prior,0))
        wait(lambda:main_ready(view,bytes(captured),cut,7),'actual Exit selected')
        prior=cut;send(b'\r','main Exit Enter',lambda:main_ready(view,bytes(captured),prior,7))
        while child.poll() is None:pump()
        while select.select([master],[],[],0)[0]:pump()
        child_restored=termios.tcgetattr(slave)==configured
        os.close(slave);slave=None
        while time.monotonic()<deadline:
            if not select.select([master],[],[],.01)[0]:continue
            try:data=os.read(master,65536)
            except OSError as error:
                if error.errno==errno.EIO:eof=True;break
                raise
            if not data:eof=True;break
            raw.write(data);raw.flush();captured.extend(data);view.observe(data)
        child.wait(timeout=max(.001,deadline-time.monotonic()))
        if child.returncode!=0:raise ValueError('Actual .NET probe failed')
        terminal.close()
        if not child_restored or not eof:raise ValueError('Child baseline/EOF not proved')
    except BaseException as error:failure=repr(error)
    finally:
        if child is not None and child.poll() is None:
            try:child.terminate();child.wait(timeout=1)
            except subprocess.TimeoutExpired:
                try:child.kill();child.wait(timeout=1)
                except BaseException as error:cleanup.append(repr(error))
            except BaseException as error:cleanup.append(repr(error))
        if terminal is not None and not terminal.closed:
            try:
                proof=terminal.abort()
                if not proof['Joined'] or not proof['EOF'] or proof['Errors']:cleanup.append(proof)
            except BaseException as error:cleanup.append('Terminal abort: '+repr(error))
        try:
            # PTY master retains termios access after slave close; this is owner restoration only.
            termios.tcsetattr(master,termios.TCSANOW,original);owner_restored=termios.tcgetattr(master)==original
        except BaseException as error:cleanup.append(repr(error))
        if slave is not None:os.close(slave)
        os.close(master);raw.close()
        result={'Success':failure is None and not cleanup and owner_restored,'Failure':failure,'CleanupFailures':cleanup,'EOF':eof,'ChildRestoredConfiguredBaseline':child_restored,'OwnerRestoredOriginal':owner_restored,'TerminalNormalClose':terminal is not None and terminal.closed,'TerminalExitCode':None if terminal is None or terminal.process is None else terminal.process.returncode,'ChildExitCode':None if child is None else child.returncode,'ForegroundIdentity':foreground,'Inputs':writer.audit,'Stages':stages,'NoScriptedInput':True,'ProviderCalls':0,'OriginalTermios':repr(original),'ConfiguredTermios':repr(configured),'ElapsedSeconds':time.monotonic()-start}
        (root/'result.json').write_text(json.dumps(result,indent=2)+'\n')
    if not result['Success']:raise RuntimeError(result)
if __name__=='__main__':main()
