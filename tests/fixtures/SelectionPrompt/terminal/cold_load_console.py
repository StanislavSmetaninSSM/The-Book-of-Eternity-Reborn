"""One cold actual main-menu Load; no bridge/daemon/game action or hosted replacement claim."""
import copy, errno, fcntl, json, os, pathlib, pty, select, struct, subprocess, sys, termios, time
sys.dont_write_bytecode=True
from owned_terminal import OwnedTerminal
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent.parent))
from fullroute_inputs import AcknowledgedTerminalView, fresh_route_key
from client_ui import main_ready, player_ready
from cold_load_navigation import cold_main_ready, clean_cold_load_message, read_cold_archive
from fullroute_inputs import routes
from relay_phase_evidence import ORDINARY_SAVE_PATHS
import datetime, hashlib

from pty_probe import PtyWriter

def main():
    node,prefix,client,base,folder,archive_name,archive_sha=sys.argv[1:]
    session=pathlib.Path(base)/"game_session"
    archive_path=session/"saves/manual_saves"/archive_name
    admitted=read_cold_archive(archive_path,archive_name,archive_sha)
    initial=json.loads((session/"config.json").read_bytes())
    if initial.get("GmBridgeEnabled") is not False or any(initial.get(k) is not False for k in ("MusicEnabled","SoundEnabled","GenerateSceneImages","ShowImagesInConsole")):raise ValueError("Cold bootstrap must be muted and bridge-disabled")
    if {p.name for p in pathlib.Path(base).iterdir()}!={"game_session"}:raise ValueError("Cold base contains prior runtime authority or other assets")
    allowed={"config.json","saves/manual_saves/"+archive_name}
    actual={str(p.relative_to(session)) for p in session.rglob("*") if p.is_file()}
    if actual!=allowed or any(p.is_symlink() for p in session.rglob("*")):raise ValueError("Fresh cold input contains unexpected state")
    root=pathlib.Path(folder);root.mkdir(exist_ok=False)
    start=time.monotonic();deadline=start+45;cleanup_deadline=start+55;master,slave=pty.openpty();original=termios.tcgetattr(slave)
    configured=copy.deepcopy(original);configured[3]&=~(termios.ECHO|termios.ECHONL)
    terminal=None;child=None;failure=None;cleanup=[];eof=False;child_restored=False;owner_restored=False;foreground=None
    writer=PtyWriter(master,master,deadline);raw=open(root/'pty.raw','xb');captured=bytearray();stages=[]
    try:
        termios.tcsetattr(slave,termios.TCSANOW,configured)
        if termios.tcgetattr(slave)!=configured:raise ValueError('Noecho readback')
        fcntl.ioctl(slave,termios.TIOCSWINSZ,struct.pack('HHHH',25,100,0,0));os.set_blocking(master,False)
        terminal=OwnedTerminal(node,prefix,root,deadline,writer,cleanup_deadline=cleanup_deadline)
        env=os.environ.copy();env.update(TERM='xterm-256color',NO_COLOR='1')
        def own():
            os.setsid();fcntl.ioctl(slave,termios.TIOCSCTTY,0);os.tcsetpgrp(slave,os.getpid())
        child=subprocess.Popen(['dotnet',client,base,'--plain-output'],stdin=slave,stdout=slave,stderr=slave,env=env,preexec_fn=own)
        foreground={'Pid':child.pid,'SessionId':os.getsid(child.pid),'ProcessGroupId':os.getpgid(child.pid),'TtyForegroundGroup':os.tcgetpgrp(master)}
        if any(value!=child.pid for value in foreground.values()):raise ValueError('Original foreground controlling PTY identity mismatch')
        view=AcknowledgedTerminalView(terminal,captured)
        def pump():
            if time.monotonic()>=deadline:raise TimeoutError('Original cold-load work deadline')
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
        wait(lambda:cold_main_ready(view,bytes(captured),0,0),'actual cold New Game selected')
        cut=send(b'2','cold select Load',lambda:cold_main_ready(view,bytes(captured),0,0))
        wait(lambda:cold_main_ready(view,bytes(captured),cut,1),'actual cold Load selected')
        prior=cut;cut=send(b'\r','cold Load Enter',lambda:cold_main_ready(view,bytes(captured),prior,1))
        meta=admitted['Metadata']
        date=datetime.datetime.fromisoformat(meta['timestamp'].replace('Z','+00:00')).strftime('%d.%m.%Y %H:%M')
        menu=routes.entry_menu(routes.local_load_menu(meta['currentLocation'],43,admitted['SizeBytes']//1024,date),load_clear=True)
        label=menu['choices'][0]
        def selected():
            frames=routes.selected_frames(bytes(captured),cut,menu)
            return bool(frames and all(row==label for row in frames) and not view.pending and view.cut==len(captured) and any(label in line for line in view.lines))
        wait(selected,'actual sole exact Save43 selected')
        load_cut=send(b'\r','exact Save43 Load',selected)
        height=len(menu['header'].split('\n'))+len(menu['choices'])
        close=b'\r'+b'\x1b[2K\x1b[1A'*height+b'\x1b[2K\x1b[?25h'
        clear=b'\x1b[2J\x1b[3J\x1b[1;1H'
        def loaded():
            tail=bytes(captured)[load_cut:];at=tail.find(close)
            if at<0:return False
            if at:
                prior_frames=routes.selected_frames(tail[:at],0,menu)
                if not prior_frames or any(row!=label for row in prior_frames):return False
            end=tail.find(clear,at+len(close))
            if end<0:return False
            try:message=tail[at+len(close):end].decode('utf-8')
            except UnicodeError:return False
            return clean_cold_load_message(message) and player_ready(view,bytes(captured),load_cut+end)
        wait(loaded,'exact clean cold Load and fresh actual player prompt')
        cut=len(captured)
        # Keep the validated player window, not an empty post-observation cut.
        player_cut=load_cut
        cut=send(b'/options\r','local options command',lambda:player_ready(view,bytes(captured),player_cut))
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
        closing=read_cold_archive(archive_path,archive_name,archive_sha)
        if closing['Members']!=admitted['Members']:raise ValueError('Archive changed during cold Load')
        protected=list(ORDINARY_SAVE_PATHS)+[n for n in admitted['Members'] if n.startswith('stories/')]
        for name in protected:
            path=session/name
            if path.is_symlink() or path.read_bytes()!=admitted['Members'][name]:raise ValueError('Loaded scoped bytes mismatch: '+name)
        if json.loads((session/'config.json').read_bytes())!=admitted['Config']:raise ValueError('Loaded config semantic drift; no repair attempted')
        if (pathlib.Path(base)/'.boe_runtime/gm-runs').exists():raise ValueError('Cold Load unexpectedly created main authority')
        for name in ('input/turn_request.json','ready/turn_complete.json','ready/turn_error.json','game_state/control/gm_bridge_status.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json','game_state/control/pending_turn_snapshot','game_state/control/validation_repair_request.json'):
            if (session/name).exists() or (session/name).is_symlink():raise ValueError('Unexpected active cold runtime artifact: '+name)
        (root/'loaded-proof.json').write_text(json.dumps({'ArchiveName':archive_name,'ArchiveSHA256':archive_sha,'ScopedPaths':protected,'ScopedSHA256':{n:hashlib.sha256((session/n).read_bytes()).hexdigest() for n in protected},'HostedReplacementClaimed':False,'ColdNoActiveSourceBoundary':True},indent=2)+'\n')
        if not child_restored or not eof:raise ValueError('Child baseline/EOF not proved')
    except BaseException as error:failure=repr(error)
    finally:
        deadline=cleanup_deadline;writer.deadline=cleanup_deadline
        if terminal is not None and not terminal.closed:
            try:terminal.enter_cleanup()
            except BaseException as error:cleanup.append('Cleanup admission: '+repr(error))
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
        result={'Success':failure is None and not cleanup and owner_restored,'Failure':failure,'CleanupFailures':cleanup,'EOF':eof,'ChildRestoredConfiguredBaseline':child_restored,'OwnerRestoredOriginal':owner_restored,'TerminalNormalClose':terminal is not None and terminal.closed,'TerminalExitCode':None if terminal is None or terminal.process is None else terminal.process.returncode,'ChildExitCode':None if child is None else child.returncode,'ForegroundIdentity':foreground,'Inputs':writer.audit,'Stages':stages,'NoScriptedInput':True,'GameplayInputSent':False,'HostedReplacementClaimed':False,'OriginalTermios':repr(original),'ConfiguredTermios':repr(configured),'ElapsedSeconds':time.monotonic()-start}
        (root/'result.json').write_text(json.dumps(result,indent=2)+'\n')
    if not result['Success']:raise RuntimeError(result)
if __name__=='__main__':main()
