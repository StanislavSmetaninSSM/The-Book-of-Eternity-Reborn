"""Owner-authorized one genuine turn in an ordinary disposable console game.

Run only beneath the independently compiled guardian --live-turn (300s).
Package is already deployed below fresh base/game_session/_runtime. No scripted
GM, Ready override, query/gate answer, prompt replay, auth/config/history copy.
"""
import errno, fcntl, hashlib, json, os, pty, re, select, socket, struct, subprocess, sys, termios, time, uuid
from pathlib import Path
out = Path(sys.argv[1]).resolve()
base = out / 'play'; session = base / 'game_session'; ship = session / '_runtime'
assert session.is_dir() and ship.is_dir() and not (session / 'config.json').exists()
assert not (session / 'game_state').exists()
assert os.environ.get('TERM') == 'dumb'
binary = Path('/workspace/qualification-1553-opencode-install/package/node_modules/opencode-linux-x64-baseline/bin/opencode')
assert hashlib.sha256(binary.read_bytes()).hexdigest() == '77b2cfe4b97df6f15c3673b22100b9f79c711f25ecb9bf513bb82526b15d24fa'
pipe = 'og-' + uuid.uuid4().hex[:12]
quote = lambda v: "'" + str(v).replace("'", "''") + "'"
command = '& ' + quote(binary) + ' --mini --pure --no-replay -m opencode/ling-3.1-flash-free'
env = {k:v for k,v in os.environ.items() if not k.upper().startswith('OPENCODE_')}
for key, folder in [('XDG_CONFIG_HOME','config'),('XDG_DATA_HOME','data'),('XDG_STATE_HOME','state'),('XDG_CACHE_HOME','cache'),('TMPDIR','tmp')]:
    path=out/folder;path.mkdir();env[key]=str(path)
for key in ['OPENCODE_DISABLE_PROJECT_CONFIG','OPENCODE_DISABLE_AUTOUPDATE','OPENCODE_DISABLE_EXTERNAL_SKILLS','OPENCODE_DISABLE_CLAUDE_CODE']:env[key]='1'
install = binary.parents[4]
env.update(NPM_CONFIG_USERCONFIG=str(install/'empty-user.npmrc'),NPM_CONFIG_GLOBALCONFIG=str(install/'empty-global.npmrc'),NPM_CONFIG_CACHE=str(out/'npm-cache'),NPM_CONFIG_IGNORE_SCRIPTS='true')
(session/'config.json').write_text(json.dumps({
    'Language':'ru','MusicEnabled':False,'SoundEnabled':False,'GenerateSceneImages':False,'ShowImagesInConsole':False,
    'GmBridgeEnabled':True,'GmBridgeBackend':'OwnedTerminal','GmMainOwnerBackend':'NativeLineage','GmBridgeAutoStart':False,
    'GmBridgePipeNameOverride':pipe,'GmCliLaunchCommand':command,'GmBridgeShellWorkingDirectory':str(session),'GmWorkerBridgeProfiles':[],
    'GmCliInputProfile':{'TerminalPresentation':'synchronized-mini-v1','DraftObservation':'external-editor-v1','DraftDirectory':env['TMPDIR'],
        'StartupBannerLines':['','█▀▀█  OpenCode','█  █  '+str(session),'▀▀▀▀',''],'AutomaticSubmissionLimit':1,
        'IdleMarker':' BUILD','WorkingMarker':'interrupt','ObservationTimeoutMilliseconds':15000}
},indent=2,ensure_ascii=False)+'\n')
start=time.monotonic(); peers=[]; journal=[]; original=None; original_record=None; shutdown_attempts=0;cleaning=False;client_exit_attempted=False
result={'AcceptedGameTurns':0,'GenuineActionsSent':0,'GateAnswers':0,'QueryAnswers':0,'ConfiguredModel':'opencode/ling-3.1-flash-free',
        'ConfiguredCommand':command,'ConfiguredCwd':str(session),'NoReadyOverride':True,'OrdinaryNewGame':False,'LogicalLifecycleVerified':False}
action='Я осторожно осматриваю берег Моря Хаоса и спрашиваю моего Хранителя, где я оказался.'
class Terminal:
    def __init__(self,name,args):
        self.name=name;self.master,self.slave=pty.openpty();self.capture=bytearray();self.eof=False
        fcntl.ioctl(self.slave,termios.TIOCSWINSZ,struct.pack('HHHH',25,100,0,0));self.initial=termios.tcgetattr(self.slave)
        def own():
            os.setsid();fcntl.ioctl(self.slave,termios.TIOCSCTTY,0);os.tcsetpgrp(self.slave,os.getpid())
        self.process=subprocess.Popen(args,cwd=ship,env=env,stdin=self.slave,stdout=self.slave,stderr=self.slave,preexec_fn=own)
        peers.append(self);journal.append({'At':elapsed(),'Entrypoint':name,'Argv':args,'OriginalPid':self.process.pid})
    def send(self,data,phase):
        assert elapsed()<(270 if cleaning else 210) and self.process.poll() is None
        journal.append({'At':elapsed(),'Terminal':self.name,'Phase':phase,'Input':data.decode('utf-8')})
        assert os.write(self.master,data)==len(data), 'Ambiguous partial foreground write; never replay'
    def text(self,offset=0):
        return re.sub(r'\x1b\[[0-9;? ]*[A-Za-z~]','',self.capture[offset:].decode('utf-8',errors='replace'))
def elapsed():return round(time.monotonic()-start,3)
def pump(delay=.02):
    fds=[p.master for p in peers if not p.eof]
    readable=select.select(fds,[],[],delay)[0] if fds else []
    for p in peers:
        if p.master not in readable:continue
        try:b=os.read(p.master,65536)
        except OSError as ex:
            if ex.errno!=errno.EIO:raise
            b=b''
        if not b:p.eof=True
        p.capture.extend(b)
        if len(p.capture)>4194304:raise RuntimeError('Bounded foreground capture exceeded4MiB')
    if elapsed()>270:raise TimeoutError('Driver total bound')
def until(test,seconds,label):
    deadline=min(start+(270 if cleaning else 210),time.monotonic()+seconds)
    while not test():
        if time.monotonic()>deadline:raise TimeoutError(label)
        pump()
def fresh(p,marker,offset,seconds=12):
    def observed():
        if marker in p.text(offset):return True
        if p.process.poll() is not None:raise RuntimeError(p.name+' exited before '+marker)
        return False
    until(observed,seconds,p.name+': '+marker)
def answer(p,marker,text,offset):
    fresh(p,marker,offset);offset=len(p.capture);p.send((text+'\r').encode(),'answer '+marker);return offset
def rpc(payload,seconds=2):
    assert payload['command'] in ['status','diagnostics','shutdown']
    item={'At':elapsed(),'RPC':payload,'Attempted':True};journal.append(item)
    with socket.socket(socket.AF_UNIX,socket.SOCK_STREAM) as s:
        s.settimeout(.5);s.connect(str(out/'tmp'/('CoreFxPipe_'+pipe)));s.sendall(json.dumps(payload).encode()+b'\n');s.setblocking(False)
        reply=bytearray();deadline=time.monotonic()+seconds
        while time.monotonic()<deadline:
            pump()
            if not select.select([s],[],[],0)[0]:continue
            b=s.recv(8192)
            if not b:raise EOFError('Original response lost; never retry an operation')
            reply.extend(b)
            if len(reply)>524288:raise RuntimeError('RPC bound')
            if reply.endswith(b'\n'):
                item['Response']=json.loads(reply);return item['Response']
        raise TimeoutError('Original RPC outcome unknown; never replay')
def read_json(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def fresh_main(client,offset):
    fresh(client,'Продолжить',offset)
    until(lambda:re.search(r'8\.\s*[^\r\n]*Выход',client.text(offset)) is not None,8,'fresh eight-item main menu')
def fresh_player(client,offset,seconds=12):
    fresh(client,'Ваш ход',offset,seconds);fresh(client,'🌊 > ',offset,seconds)
def exit_client(client):
    global client_exit_attempted
    assert not client_exit_attempted, 'Do not replay an ambiguous UI close sequence'
    client_exit_attempted=True
    offset=len(client.capture);client.send(b'/options\r','current prompt: game menu');fresh(client,'Игровое меню',offset)
    offset=len(client.capture);client.send(b'4\r','observed game menu: exit to menu');fresh_main(client,offset)
    client.send(b'8\r','observed eight-item main menu: exit');until(lambda:client.process.poll() is not None,8,'client normal exit')
    assert client.process.returncode==0
client=bridge=daemon=None;client_wait=False;client_prompt=False
try:
    client=Terminal('client',['dotnet',str(ship/'BookOfEternityClient/BookOfEternityClient.dll'),str(base),'--plain-output'])
    fresh(client,'Тренировка QTE',0);offset=len(client.capture);client.send(b'\r','observed NewGame selected')
    offset=answer(client,'Введите имя вашей души:','Пробная Душа',offset)
    offset=answer(client,'Опишите форму вашей души:','Человеческий силуэт мягкого синего света.',offset)
    offset=answer(client,'Выберите способ создания Хранителя:','1',offset)
    offset=answer(client,'Опишите вашего хранителя:','Спокойный Хранитель маяка, который встречает душу на берегу Моря Хаоса.',offset)
    until(lambda:(session/'input/turn_request.json').exists(),10,'ordinary initial request');client_wait=True
    result['InitialBootstrapRequest']=read_json(session/'input/turn_request.json');result['OrdinaryNewGame']=True
    # Observe the actual waiting UI, allow its cancellation listener to start.
    fresh(client,'Мастер игры размышляет...',offset,8);pump(.3)
    offset=len(client.capture);client.send(b'\x1b','cancel initial wait before CLI exists')
    fresh(client,'Переходный ход отменён.',offset,10);fresh_main(client,offset);client_wait=False
    assert not (session/'input/turn_request.json').exists()
    result['BootstrapCancelledBeforeCli']=True
    launcher=str(ship/'BookOfEternityClient/Launcher/bookofeternity.ps1')
    bridge=Terminal('bridge',['pwsh','-NoLogo','-NoProfile','-File',launcher,'start-bridge','visible','-SessionPath',str(session)])
    record_path=base/'.boe_runtime/gm-runs/main.json';socket_path=out/'tmp'/('CoreFxPipe_'+pipe)
    until(lambda:record_path.exists() and socket_path.exists() and read_json(record_path)['Disposition']=='Running',10,'original Running')
    record=read_json(record_path);status=rpc({'command':'status'})
    assert status['ok'] and status['status']['terminalOwnerRetained'] and status['status']['terminalRunId']==record['Identity']['RunId']
    original_record=record['Identity'];original={k[0].lower()+k[1:]:v for k,v in original_record.items()};original['backend']=2
    result['OriginalRunningRecord']=record;result['OriginalRunningStatus']=status
    assert status['status']['cliLaunchCommand']==command and status['status']['shellWorkingDirectory']==str(session)
    def ready():
        status=rpc({'command':'status'})['status'];result['LastStartupStatus']=status
        if status['terminalUncertain'] or not status['terminalOwnerRetained']:raise RuntimeError('Original owner lost')
        return status['ready']
    until(ready,15,'original derived Ready; no gate/query answers')
    result['ReadinessQualified']=True
    daemon=Terminal('daemon',['pwsh','-NoLogo','-NoProfile','-File',launcher,'start-daemon','visible','--timeout','180','--log',str(out/'daemon.log'),'-SessionPath',str(session)])
    fresh(daemon,'Waiting for turns...',0,15)
    offset=len(client.capture);client.send(b'\r','observed Continue selected');fresh_player(client,offset,15);client_prompt=True
    offset=len(client.capture);client.send((action+'\r').encode(),'one genuine game action');result['GenuineActionsSent']=1;client_prompt=False;client_wait=True
    until(lambda:(session/'input/turn_request.json').exists(),8,'genuine game request')
    request=read_json(session/'input/turn_request.json');result['ActualRequest']=request;result['ActualRequestSHA256']=hashlib.sha256((session/'input/turn_request.json').read_bytes()).hexdigest()
    assert request['playerAction']==action and request['turnNumber']==1
    assert request['sessionId']==result['InitialBootstrapRequest']['sessionId'] and request['requestId']!=result['InitialBootstrapRequest']['requestId']
    (out/'actual-turn-request.json').write_bytes((session/'input/turn_request.json').read_bytes())
    story=session/'stories/chaos_sea.jsonl';provider_deadline=min(start+210,time.monotonic()+170)
    while time.monotonic()<provider_deadline:
        pump()
        current=rpc({'command':'status'})['status'];result['LastBridgeStatus']=current
        if current['terminalUncertain'] or not current['terminalOwnerRetained']:raise RuntimeError('Original owner uncertainty; acceptance remains blocked')
        refused=current.get('promptDelivery')
        if refused and (refused.get('disposition')=='draft-uncertain' or
            (refused.get('disposition')=='not-written' and refused.get('reason')=='unsupported-draft-shape')):
            result['OriginalPromptDelivery']=refused
            raise RuntimeError('Actual original automatic input refused; no manual reconstruction or replay')
        if story.exists():
            entries=[json.loads(line) for line in story.read_text().splitlines() if line.strip()]
            accepted=[e for e in entries if e.get('turn')==1 and e.get('player')==action and e.get('narrative','').strip()]
            if accepted:
                fresh_player(client,offset,10)
                assert not (session/'input/turn_request.json').exists()
                assert not (session/'game_state/control/pending_turn_snapshot.json').exists()
                assert not (session/'game_state/control/pending_turn_snapshot.authority.json').exists()
                assert not (session/'ready/turn_complete.json').exists() and not (session/'ready/turn_error.json').exists()
                result['AcceptedGameTurns']=1;result['AcceptedStory']=accepted[0];client_wait=False;client_prompt=True
                delivery=current.get('promptDelivery');result['OriginalPromptDelivery']=delivery
                result['InputQualified']=bool(delivery and delivery.get('disposition')=='submission-observed' and
                    delivery.get('inputBindingId')==result['OriginalRunningStatus']['status']['inputBindingId'] and
                    delivery.get('operationKind')=='turn' and delivery.get('operationRevision')=='live')
                break
        if any(marker in bridge.text()[-4000:].lower() for marker in ['permission required','allow this','trust this','sign in','authorize access']):
            raise RuntimeError('Unexpected CLI access/auth gate; no answer supplied')
        if (session/'ready/turn_error.json').exists():
            result['ActualTerminalError']=read_json(session/'ready/turn_error.json');raise RuntimeError('Actual game terminal error; no automatic replay')
    if result['AcceptedGameTurns']!=1:raise TimeoutError('One-turn phase ended without actual game acceptance')
    cleaning=True;exit_client(client);client_prompt=False
except Exception as ex:
    result['Failure']=type(ex).__name__+': '+str(ex)
finally:
    cleaning=True
    # Settle participating client wait before original owner stop; never replay its action.
    if client is not None and client.process.poll() is None:
        try:
            if client_wait:
                offset=len(client.capture);client.send(b'\x1b','failure: cancel original wait once')
                def settled():
                    text=client.text(offset)
                    return not (session/'input/turn_request.json').exists() and ('Продолжить' in text or ('Ваш ход' in text and '🌊 > ' in text))
                until(settled,12,'completed original cancellation/rollback and fresh menu/player screen');client_wait=False
                client_prompt='Ваш ход' in client.text(offset) and '🌊 > ' in client.text(offset)
            if client_prompt and not client_exit_attempted:exit_client(client)
            elif client.process.poll() is None:
                client.send(b'\x03','failure: stop original client foreground');until(lambda:client.process.poll() is not None,6,'client foreground stop')
        except Exception as ex:result['ClientCleanupFailure']=type(ex).__name__+': '+str(ex)
    if daemon is not None and daemon.process.poll() is None:
        try:
            daemon.send(b'\x03','stop original daemon foreground');until(lambda:daemon.process.poll() is not None,8,'daemon original stop')
        except Exception as ex:result['DaemonCleanupFailure']=type(ex).__name__+': '+str(ex)
    if original is not None:
        try:
            shutdown_attempts+=1
            stop=rpc({'command':'shutdown','rootKey':original['rootKey'],'expectedMainIdentity':original},12)
            result['ShutdownReceipt']=stop;assert stop['ok'];proof=stop['status']['terminalStop']
            assert proof['identity']['runId']==original['runId'] and proof['state']=='stopped-within-scope' and proof['cleanupComplete'] and not proof['authorityRetained']
            until(lambda:bridge.process.poll() is not None,8,'original bridge exit');assert bridge.process.returncode==0
            settled=read_json(record_path);result['StoppedRecord']=settled
            assert settled['Disposition']=='Stopped' and settled['Identity']==original_record
            result['LogicalLifecycleVerified']=True
        except Exception as ex:result['CleanupFailure']=type(ex).__name__+': '+str(ex)
    elif bridge is not None:result['CleanupFailure']='Original Running identity unavailable; guardian cannot logical-success'
    result['ShutdownAttempts']=shutdown_attempts
    for p in peers:
        if p.process.poll() is not None and p.slave>=0:os.close(p.slave);p.slave=-1
    try:until(lambda:all(p.eof for p in peers),5,'actual foreground EOF')
    except Exception as ex:result['IoCleanupFailure']=type(ex).__name__+': '+str(ex)
    result['Terminals']={}
    for p in peers:
        try:restored=termios.tcgetattr(p.master)==p.initial
        except OSError:restored=False
        result['Terminals'][p.name]={'ExitCode':p.process.poll(),'EOF':p.eof,'TermiosRestored':restored,'CapturedBytes':len(p.capture)}
        (out/(p.name+'.raw')).write_bytes(p.capture)
        for fd in [p.master,p.slave]:
            if fd>=0:os.close(fd)
    result['ElapsedSeconds']=elapsed();result['Success']=result['AcceptedGameTurns']==1 and result.get('InputQualified',False) and result['LogicalLifecycleVerified'] and not any(k.endswith('Failure') for k in result) and all(v['EOF'] and v['TermiosRestored'] for v in result['Terminals'].values())
    (out/'rpc-journal.json').write_text(json.dumps(journal,indent=2,ensure_ascii=False)+'\n')
    (out/'probe-result.json').write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n')
    print(json.dumps({k:result.get(k) for k in ['Success','AcceptedGameTurns','GenuineActionsSent','Failure','CleanupFailure','ElapsedSeconds']}))
sys.exit(0 if result['Success'] else 1)
