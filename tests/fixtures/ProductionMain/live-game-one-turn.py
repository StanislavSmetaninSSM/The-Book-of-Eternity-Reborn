"""Owner-authorized one genuine turn in an ordinary disposable console game.

Run only beneath the independently compiled guardian: --live-turn (300s),
or --relay-turn (750s) for the explicit maintained developer relay.
Package is already deployed below fresh base/game_session/_runtime. No scripted
GM, Ready override, query/gate answer, prompt replay, auth/config/history copy.
"""
import errno, fcntl, hashlib, json, os, pty, re, select, struct, subprocess, sys, termios, time, uuid
from pathlib import Path

class IncrementalEvidence:
    """Each observed chunk survives loss of the driver before finally.

    Flush/close gives process-loss persistence, not a power-loss durability claim.
    No diagnostic quota or final rewrite. Protocol reply bounds stay separate.
    """
    def __init__(self, root): self.root=Path(root)
    def raw(self, name, data):
        with (self.root/(name+'.raw')).open('ab') as stream:
            stream.write(data);stream.flush()
    def event(self, entry):
        with (self.root/'rpc-journal.jsonl').open('a',encoding='utf-8') as stream:
            stream.write(json.dumps(entry,ensure_ascii=False)+'\n');stream.flush()

class EvidenceJournal(list):
    def __init__(self, evidence): super().__init__();self.evidence=evidence
    def append(self, entry):
        self.evidence.event(entry);super().append(entry)

def observed_rpc(journal, payload, invoke, now):
    identity=uuid.uuid4().hex
    entry={'At':now(),'RPC':payload,'Attempted':True,'RequestId':identity};journal.append(entry)
    try:response=invoke(identity)
    except Exception as failure:
        journal.append({'At':now(),'RequestId':identity,'RPCOutcome':'failed-or-unknown',
            'Failure':type(failure).__name__+': '+str(failure)})
        raise
    journal.append({'At':now(),'RequestId':identity,'RPCOutcome':'completed','Response':response})
    return response

class OwnedProcessSnapshots:
    """Read-only sample, never exit/cleanup authority; no argv or environment.

    Roots are Popen identities; descendants must have a freshly checked parent.
    A racing observation is explicitly incomplete, not an exit.
    """
    def __init__(self, proc_root=Path('/proc')): self.proc=Path(proc_root)
    def _read(self, pid):
        text=(self.proc/str(pid)/'stat').read_text()
        fields=text[text.rindex(')')+2:].split()
        if int(text.split(' ',1)[0])!=pid:raise ValueError('PID changed')
        return {'Pid':pid,'ParentPid':int(fields[1]),'StartTicks':int(fields[19]),'State':fields[0]}
    def register(self, pid):
        try:return {'Pid':pid,'StartTicks':self._read(pid)['StartTicks']}
        except (OSError,ValueError,IndexError):return {'Pid':pid,'StartTicks':None}
    def collect(self, roots):
        result={'Processes':[],'Unavailable':[],'Incomplete':False}
        pending=list(roots);seen=set()
        def unavailable(identity,reason):
            result['Incomplete']=True;result['Unavailable'].append(dict(identity,Reason=reason))
        while pending:
            identity=pending.pop(0);pid=identity['Pid']
            if pid in seen:continue
            seen.add(pid)
            try:
                current=self._read(pid)
                if identity['StartTicks'] is None:
                    unavailable(identity,'unavailable');continue
                if current['StartTicks']!=identity['StartTicks']:
                    unavailable(identity,'identity-changed');continue
                if 'ExpectedParent' in identity and (current['ParentPid']!=identity['ExpectedParent'] or
                        self._read(identity['ExpectedParent'])['StartTicks']!=identity['ExpectedParentStartTicks']):
                    unavailable(identity,'parent-changed');continue
                try:current['WaitChannel']=(self.proc/str(pid)/'wchan').read_text().strip()
                except OSError:current['WaitChannel']=None
                children=set();children_errors=[]
                try:tasks=sorted((self.proc/str(pid)/'task').iterdir())
                except OSError as error:
                    tasks=[];children_errors.append(dict(identity,Stage='task-enumeration',Errno=error.errno))
                for task in tasks:
                    if not task.name.isdecimal():continue
                    try:children.update(int(c) for c in (task/'children').read_text().split())
                    except (OSError,ValueError) as error:
                        children_errors.append(dict(identity,Stage='task-children',Tid=int(task.name),Errno=getattr(error,'errno',None)))
                final=self._read(pid)
                if final['StartTicks']!=identity['StartTicks']:
                    unavailable(identity,'identity-changed');continue
                if final['ParentPid']!=current['ParentPid'] or ('ExpectedParent' in identity and
                        self._read(identity['ExpectedParent'])['StartTicks']!=identity['ExpectedParentStartTicks']):
                    unavailable(identity,'parent-changed');continue
                result['Processes'].append(current)
                for error in children_errors:unavailable(error,'children-unavailable')
                for child in sorted(children):
                    candidate=self.register(child)
                    candidate['ExpectedParent']=pid;candidate['ExpectedParentStartTicks']=current['StartTicks'];pending.append(candidate)
            except (OSError,ValueError,IndexError):unavailable(identity,'unavailable')
        return result

if len(sys.argv)==2 and sys.argv[1] in ('--check-incremental-evidence','--check-owned-process-snapshots'):
    import tempfile
    with tempfile.TemporaryDirectory(prefix='boe-driver-inert-') as directory:
        root=Path(directory)
        if sys.argv[1]=='--check-incremental-evidence':
            evidence=IncrementalEvidence(root);events=EvidenceJournal(evidence)
            evidence.raw('client',b'first\x00');events.append({'Phase':'first'})
            assert (root/'client.raw').exists(), 'Raw evidence must exist before final cleanup'
            assert (root/'client.raw').read_bytes()==b'first\x00'
            assert json.loads((root/'rpc-journal.jsonl').read_text())=={'Phase':'first'}
            evidence.raw('client',b'next');evidence.raw('client-echo',b'echo')
            events.append({'Phase':'next'})
            assert len(events)==2
            assert (root/'client.raw').read_bytes()==b'first\x00next'
            assert (root/'client-echo.raw').read_bytes()==b'echo'
            assert [json.loads(x)['Phase'] for x in (root/'rpc-journal.jsonl').read_text().splitlines()]==['first','next']
        else:
            def proc(pid,ppid,start,children=''):
                target=root/str(pid);(target/'task'/str(pid)).mkdir(parents=True,exist_ok=True)
                fields=['S',str(ppid)]+['0']*17+[str(start)]
                (target/'stat').write_text(str(pid)+' (name with ) paren) '+' '.join(fields))
                (target/'wchan').write_text('pipe_read')
                (target/'task'/str(pid)/'children').write_text(children)
            proc(10,1,100,'11 12');proc(11,10,110,'13');proc(12,99,120);proc(13,11,130)
            snapshot=OwnedProcessSnapshots(root);identity=snapshot.register(10)
            observed=snapshot.collect([identity])
            assert [r['Pid'] for r in observed['Processes']]==[10,11,13], 'Only original roots and verified descendants'
            assert observed['Processes'][0]['StartTicks']==100
            assert observed['Processes'][0]['WaitChannel']=='pipe_read'
            (root/'10'/'task'/'99').mkdir()
            siblings=snapshot.collect([identity])
            assert [r['Pid'] for r in siblings['Processes']]==[10,11,13]
            assert siblings['Incomplete'] and siblings['Unavailable'][0]['Tid']==99
            assert siblings['Unavailable'][0]['Stage']=='task-children' and siblings['Unavailable'][0]['Errno']==2
            (root/'10'/'task'/'99').rmdir()
            (root/'10'/'task'/'10'/'children').unlink()
            limited=snapshot.collect([identity])
            assert [r['Pid'] for r in limited['Processes']]==[10], 'Missing children interface must not erase verified root observation'
            assert limited['Incomplete'] and limited['Unavailable'][0]['Reason']=='children-unavailable'
            proc(10,1,999,'11')
            reused=snapshot.collect([identity])
            assert not reused['Processes'] and reused['Incomplete']
            assert reused['Unavailable'][0]['Reason']=='identity-changed'
            (root/'10'/'stat').unlink()
            missing=snapshot.collect([identity])
            assert not missing['Processes'] and missing['Unavailable'][0]['Reason']=='unavailable'
    print(json.dumps({'InertEvidenceChecks':'passed','ProcessesStarted':0,'RuntimeTestsExecuted':0}));sys.exit(0)

if len(sys.argv)==2 and sys.argv[1]=='--check-incremental-rpc':
    import tempfile
    with tempfile.TemporaryDirectory(prefix='boe-rpc-evidence-') as directory:
        events=EvidenceJournal(IncrementalEvidence(directory));seen=[]
        def success(identity):seen.append(identity);return {'ok':True}
        assert observed_rpc(events,{'command':'status'},success,lambda:1)=={'ok':True}
        rows=[json.loads(x) for x in (Path(directory)/'rpc-journal.jsonl').read_text().splitlines()]
        assert len(rows)==2, 'Completed RPC must persist separately before final cleanup'
        assert rows[0]['RequestId']==rows[1]['RequestId']==seen[0]
        assert rows[1]['RPCOutcome']=='completed' and rows[1]['Response']=={'ok':True}
        def failure(identity):seen.append(identity);raise TimeoutError('original reply unknown')
        try:observed_rpc(events,{'command':'shutdown'},failure,lambda:2)
        except TimeoutError as error:assert str(error)=='original reply unknown'
        else:raise AssertionError('Lost response must remain a failure')
        rows=[json.loads(x) for x in (Path(directory)/'rpc-journal.jsonl').read_text().splitlines()]
        assert len(rows)==4 and len(seen)==2, 'No retry or missing failure evidence'
        assert rows[2]['RequestId']==rows[3]['RequestId']==seen[1] and seen[0]!=seen[1]
        assert rows[3]['RPCOutcome']=='failed-or-unknown' and 'Response' not in rows[3]
        assert 'Response' not in rows[0], 'Attempt evidence stays immutable'
    print(json.dumps({'InertRpcEvidenceChecks':'passed','ProcessesStarted':0,'RuntimeTestsExecuted':0}));sys.exit(0)

if len(sys.argv)==2 and sys.argv[1]=='--check-driver-evidence-integration':
    # Execute the real module assignment, real provider artifact loop and real
    # pump in ONE namespace. Actual pipe bytes, no package/game/subprocess.
    import ast, tempfile, types
    tree=ast.parse(Path(__file__).read_text())
    setup=next(n for n in tree.body if isinstance(n,ast.Assign) and isinstance(n.value,ast.Call)
               and isinstance(n.value.func,ast.Name) and n.value.func.id=='IncrementalEvidence')
    artifact_loop=next(n for n in ast.walk(tree) if isinstance(n,ast.For) and isinstance(n.iter,ast.List)
                      and n.iter.elts and isinstance(n.iter.elts[0],ast.Constant) and n.iter.elts[0].value=='error_log.txt')
    actual_pump=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='pump')
    with tempfile.TemporaryDirectory(prefix='boe-driver-integration-') as directory:
        root=Path(directory);read_fd,write_fd=os.pipe()
        try:
            peer=types.SimpleNamespace(master=read_fd,streams={read_fd:'output'},closed_streams=set(),
                name='actual-pump',capture=bytearray(),echo=bytearray(),eof=False)
            scope=dict(globals(),out=root,session=root,peers=[peer],elapsed=lambda:0,last_process_sample=0,
                total_seconds=100,journal=[])
            exec(compile(ast.Module(body=[setup,artifact_loop,actual_pump],type_ignores=[]),__file__,'exec'),scope)
            os.write(write_fd,b'actual pump after artifact loop')
            try:scope['pump'](0)
            except AttributeError as failure:raise AssertionError('Artifact loop must not replace the live evidence sink') from failure
            assert (root/'actual-pump.raw').read_bytes()==b'actual pump after artifact loop'
            assert peer.capture==b'actual pump after artifact loop'
            assert scope['journal'][0]['ByteEnd']==len(peer.capture)
        finally:os.close(read_fd);os.close(write_fd)
    print(json.dumps({'InertActualPumpChecks':'passed','ProcessesStarted':0,'RuntimeTestsExecuted':0}));sys.exit(0)

def rpc_helper_arguments(script, shipped, current_session):
    # All code locations derive from the source fixture and explicit shipped package.
    return ['pwsh','-NoLogo','-NoProfile','-File',str(Path(script).with_name('bridge-request.ps1')),
            '-ClientAssembly',str(Path(shipped)/'BookOfEternityClient/BookOfEternityClient.dll'),
            '-StatusPath',str(Path(current_session)/'game_state/control/gm_bridge_status.json')]

if len(sys.argv)==2 and sys.argv[1]=='--check-shipped-rpc-arguments':
    args=rpc_helper_arguments('/source/with space/live-game-one-turn.py','/shipped/with space','/own/game_session')
    assert args[:4]==['pwsh','-NoLogo','-NoProfile','-File']
    assert args[4]=='/source/with space/bridge-request.ps1'
    assert args[5:7]==['-ClientAssembly','/shipped/with space/BookOfEternityClient/BookOfEternityClient.dll']
    assert args[7:]==['-StatusPath','/own/game_session/game_state/control/gm_bridge_status.json']
    print(json.dumps({'InertRpcArgumentChecks':4,'Passed':4,'ProcessesStarted':0,'RuntimeTestsExecuted':0}));sys.exit(0)

def rpc_helper_closed(result):
    proof=result.get('RpcHelperClosure')
    return isinstance(proof,dict) and proof.get('ExitCode')==0 and proof.get('StdoutEof') is True and proof.get('StderrEof') is True and 'RpcHelperCleanupFailure' not in result

if len(sys.argv)==2 and sys.argv[1]=='--check-shipped-rpc-closure':
    valid={'ExitCode':0,'StdoutEof':True,'StderrEof':True}
    assert rpc_helper_closed({'RpcHelperClosure':valid})
    assert not rpc_helper_closed({'RpcHelperClosure':dict(valid,ExitCode=1)})
    assert not rpc_helper_closed({'RpcHelperClosure':dict(valid,StderrEof=False)})
    assert not rpc_helper_closed({'RpcHelperClosure':valid,'RpcHelperCleanupFailure':'join unknown'})
    print(json.dumps({'InertRpcClosureChecks':4,'Passed':4,'ProcessesStarted':0,'RuntimeTestsExecuted':0}));sys.exit(0)

# Oracle consumes Peer.text() with ANSI CSI already removed.
# BrowserPlayerInput.cs PromptMarkup + TextComposer.cs appended space.
PLAYER_PROMPT_MARKER = '\n >  '
def current_player_prompt_at(text):
    heading=text.rfind('Ваш ход')
    marker=text.rfind(PLAYER_PROMPT_MARKER)
    return marker if heading>=0 and marker>heading else -1

if len(sys.argv)==2 and sys.argv[1]=='--check-player-prompt':
    assert current_player_prompt_at('✦ Ваш ход ✦\nhints\n >  ')>=0, 'Current cooperative prompt must be recognized'
    assert current_player_prompt_at('✦ Ваш ход ✦\r\nhints\r\n >  ')>=0
    assert current_player_prompt_at('✦ Ваш ход ✦\nhints')==-1
    assert current_player_prompt_at('menu\n >  ')==-1
    assert current_player_prompt_at('✦ Ваш ход ✦\n🌊 > ')==-1
    assert current_player_prompt_at('\n >  \n✦ Ваш ход ✦\nhints')==-1
    assert current_player_prompt_at('✦ Ваш ход ✦\nquoted >  text')==-1
    print(json.dumps({'InertPlayerPromptChecks':7,'Passed':7,'ProcessesStarted':0,'RuntimeTestsExecuted':0}));sys.exit(0)

ELYARA_PRESET_NAME = 'Элиара Последней Раны'
def observed_elyara_choice(text):
    # MainMenu PromptGuardianPresetChoice renders numbered exact names with
    # optional parenthesized details, then the ordinary "> " menu prompt.
    heading=text.rfind('Выберите извечного Хранителя:')
    if heading<0:return None
    menu=text[heading:]
    prompt=re.search(r'(?m)^> ',menu)
    if prompt is None:return None
    choices=re.findall(r'(?m)^([1-9][0-9]*)\. '+re.escape(ELYARA_PRESET_NAME)+r'(?: \([^\r\n]*)?\r?$',menu[:prompt.start()])
    if len(choices)>1:raise ValueError('Ambiguous exact Elyara preset in rendered menu')
    return choices[0] if choices else None

if len(sys.argv)==2 and sys.argv[1]=='--check-guardian-preset':
    menu='Выберите извечного Хранителя:\n1. Другой\n3. '+ELYARA_PRESET_NAME+' (домен • тон)\n4. Назад\n/tmp/presets\n> '
    assert observed_elyara_choice(menu)=='3'
    assert observed_elyara_choice(menu.replace('3. ', '12. '))=='12'
    assert observed_elyara_choice(menu.replace('\n> ','')) is None
    assert observed_elyara_choice('3. '+ELYARA_PRESET_NAME+'\n> ') is None
    assert observed_elyara_choice(menu.replace(ELYARA_PRESET_NAME,ELYARA_PRESET_NAME+' чужая')) is None
    try:observed_elyara_choice(menu.replace('4. Назад','4. '+ELYARA_PRESET_NAME))
    except ValueError:pass
    else:raise AssertionError('Ambiguous exact names must be rejected')
    print(json.dumps({'InertGuardianPresetChecks':6,'Passed':6,'ProcessesStarted':0,'RuntimeTestsExecuted':0}));sys.exit(0)

def current_client_failure(text):
    return any(marker in text for marker in ['Мир не смог безопасно завершить действие.', 'Ход прервался', 'Ход не принят', '❌ Ошибка:'])

class PhaseWait:
    """Progress can renew an idle bound, never the original work deadline."""
    def __init__(self, now, total_deadline, idle_seconds=45):
        self.total_deadline=total_deadline;self.idle_seconds=idle_seconds
        self.last_progress=now;self.signature=None
    def observe(self, now, signature):
        changed=signature!=self.signature
        if changed:self.signature=signature;self.last_progress=now
        if now>=self.total_deadline:raise TimeoutError('Original overall work bound')
        if now-self.last_progress>=self.idle_seconds:raise TimeoutError('No observable phase progress for45s')
        return changed

def retain_turn_delivery(result,delivery,binding):
    if not delivery or delivery.get('disposition')!='submission-observed' or delivery.get('inputBindingId')!=binding:return False
    if delivery.get('operationKind')=='turn' and delivery.get('operationRevision')=='live':
        if 'OriginalTurnDelivery' not in result:result['OriginalTurnDelivery']=dict(delivery)
        return result['OriginalTurnDelivery']==delivery
    if delivery.get('operationKind')=='repair':
        repairs=result.setdefault('RepairDeliveries',[])
        if not any(p['operationId']==delivery['operationId'] for p in repairs):repairs.append(dict(delivery))
    return False

def close_relay_execution(queue,seconds):
    # Maintained gated API, not legacy direct close-request publication.
    from relay_contract import request_close, read_close
    request_close(queue)
    deadline=time.monotonic()+seconds
    while time.monotonic()<deadline:
        if read_close(queue) is not None:return True
        pump(.02)
    return False

if len(sys.argv)==2 and sys.argv[1]=='--check-phase-wait':
    # Inert driver checks; no package, processes, clipboard, CLI or game data.
    w=PhaseWait(0,210);w.observe(0,'preparing');w.observe(44,'snapshot');w.observe(88,'request')
    for now in range(100,210):w.observe(now,now)
    try:w.observe(210,'more progress')
    except TimeoutError as e:assert str(e)=='Original overall work bound'
    else:raise AssertionError('Progress extended the original total bound')
    w=PhaseWait(0,210);w.observe(0,'preparing');w.observe(44,'preparing')
    try:w.observe(45,'preparing')
    except TimeoutError as e:assert 'No observable phase progress' in str(e)
    else:raise AssertionError('Idle phase had no bound')
    w=PhaseWait(0,210);w.observe(0,'preparing');w.observe(9,'snapshot');w.observe(20,'request')
    assert current_client_failure('❌ Ошибка: Main run metadata or original owner admission is unavailable.'), 'Current main-menu error must stop preparation before a timeout'
    assert current_client_failure('╭ Ход не принят ╮'), 'Pre-request rejection must fail promptly'
    print(json.dumps({'InertDriverChecks':5,'Passed':5,'ProcessesStarted':0,'RuntimeTestsExecuted':0}));sys.exit(0)
out = Path(sys.argv[1]).resolve()
base = out / 'play'; session = base / 'game_session'; ship = session / '_runtime'
assert session.is_dir() and ship.is_dir() and not (session / 'config.json').exists()
assert not (session / 'game_state').exists()
assert os.environ.get('TERM') == 'dumb'
controlled_refusal = len(sys.argv)==3 and sys.argv[2]=='--controlled-provider-refusal'
relay_mode = len(sys.argv)==3 and sys.argv[2]=='--codex-relay'
total_seconds,work_seconds,client_cleanup_seconds,provider_seconds=(720,660,680,600) if relay_mode else (270,210,230,170)
relay_queue=out/'queue'
assert len(sys.argv)==2 or controlled_refusal or relay_mode, 'Only the fixed inert fixture mode is admitted'
binary = (out/'configured-neutral-cli') if controlled_refusal else Path('/workspace/qualification-1553-opencode-install/package/node_modules/opencode-linux-x64-baseline/bin/opencode')
if not controlled_refusal and not relay_mode:assert hashlib.sha256(binary.read_bytes()).hexdigest() == '77b2cfe4b97df6f15c3673b22100b9f79c711f25ecb9bf513bb82526b15d24fa'
pipe = 'og-' + uuid.uuid4().hex[:12]
quote = lambda v: "'" + str(v).replace("'", "''") + "'"
command = '& ' + quote(binary) + (' --driver-refusal-fixture' if controlled_refusal else ' --mini --pure --no-replay -m opencode/ling-3.1-flash-free')
if relay_mode:
    sys.path.insert(0,str(out/'relay'))
    relay_queue.mkdir()
    command='& '+quote('/usr/bin/python3')+' '+quote(out/'relay/relay_cli.py')+' --session '+quote(session)+' --queue '+quote(relay_queue)+' --model dots-finish-relay'
env = {k:v for k,v in os.environ.items() if not k.upper().startswith('OPENCODE_')}
for key, folder in [('XDG_CONFIG_HOME','config'),('XDG_DATA_HOME','data'),('XDG_STATE_HOME','state'),('XDG_CACHE_HOME','cache'),('TMPDIR','tmp')]:
    path=out/folder;path.mkdir();env[key]=str(path)
for key in ['OPENCODE_DISABLE_PROJECT_CONFIG','OPENCODE_DISABLE_AUTOUPDATE','OPENCODE_DISABLE_EXTERNAL_SKILLS','OPENCODE_DISABLE_CLAUDE_CODE']:env[key]='1'
install = out if controlled_refusal or relay_mode else binary.parents[4]
env.update(NPM_CONFIG_USERCONFIG=str(install/'empty-user.npmrc'),NPM_CONFIG_GLOBALCONFIG=str(install/'empty-global.npmrc'),NPM_CONFIG_CACHE=str(out/'npm-cache'),NPM_CONFIG_IGNORE_SCRIPTS='true')
(session/'config.json').write_text(json.dumps({
    'Language':'ru','MusicEnabled':False,'SoundEnabled':False,'GenerateSceneImages':False,'ShowImagesInConsole':False,
    'GmBridgeEnabled':True,'GmBridgeBackend':'OwnedTerminal','GmMainOwnerBackend':'NativeLineage','GmBridgeAutoStart':False,
    'GmBridgePipeNameOverride':pipe,'GmCliLaunchCommand':command,'GmBridgeShellWorkingDirectory':str(session),'GmWorkerBridgeProfiles':[],
    'GmCliInputProfile':{'TerminalPresentation':'synchronized-mini-v1','DraftObservation':'external-editor-v1','DraftDirectory':env['TMPDIR'],
        'StartupBannerLines':['','█▀▀█  OpenCode','█  █  '+str(session),'▀▀▀▀',''],'AutomaticSubmissionLimit':1,
        'IdleMarker':' BUILD','WorkingMarker':'interrupt','ObservationTimeoutMilliseconds':15000}
},indent=2,ensure_ascii=False)+'\n')
if controlled_refusal or relay_mode:
    settings=json.loads((session/'config.json').read_text())
    settings['GmCliInputProfile']={'IdleMarker':'NEUTRAL READY','PromptPrefix':'> ','WorkingMarker':'NEUTRAL WORKING','ObservationTimeoutMilliseconds':1800}
    if relay_mode:settings['GmCliInputProfile'].update(PromptPrefix='RELAY> ',BlockedMarkers=['RELAY ERROR'],ObservationTimeoutMilliseconds=15000)
    (session/'config.json').write_text(json.dumps(settings,indent=2,ensure_ascii=False)+'\n')
evidence=IncrementalEvidence(out);process_observer=OwnedProcessSnapshots();observed_processes=[];last_process_sample=-5.0
start=time.monotonic(); peers=[]; journal=EvidenceJournal(evidence); original=None; original_record=None; shutdown_attempts=0;cleaning=False;client_exit_attempted=False;client_failure_offset=None;active_phase_deadline=None
offset=0 # Defined even when the first client startup observation fails.
result={'AcceptedGameTurns':0,'GenuineActionsSent':0,'GateAnswers':0,'QueryAnswers':0,'ConfiguredModel':'opencode/ling-3.1-flash-free',
        'ConfiguredCommand':command,'ConfiguredCwd':str(session),'NoReadyOverride':True,'OrdinaryNewGame':False,'LogicalLifecycleVerified':False}
if relay_mode:result.update(ConfiguredModel='dots-finish-relay',TestRelay=True,OpenCodeProviderCalls=0,RelayQueue=str(relay_queue))
if controlled_refusal:result.update(ConfiguredModel='fixed-inert-no-provider',ControlledRefusal=True,ProviderCalls=0)
action='Я осторожно осматриваю берег Моря Хаоса и спрашиваю моего Хранителя, где я оказался.'
class Terminal:
    def __init__(self,name,args,captured_output=False):
        self.name=name;self.master,self.slave=pty.openpty();self.capture=bytearray();self.echo=bytearray();self.eof=False;self.captured_output=captured_output
        fcntl.ioctl(self.slave,termios.TIOCSWINSZ,struct.pack('HHHH',25,100,0,0));self.initial=termios.tcgetattr(self.slave)
        def own():
            os.setsid();fcntl.ioctl(self.slave,termios.TIOCSCTTY,0);os.tcsetpgrp(self.slave,os.getpid())
        self.process=subprocess.Popen(args,cwd=ship,env=env,stdin=self.slave,
            stdout=subprocess.PIPE if captured_output else self.slave,
            stderr=subprocess.STDOUT if captured_output else self.slave,preexec_fn=own)
        evidence.raw(name,b'')
        if captured_output:evidence.raw(name+'-echo',b'')
        observed_processes.append((name,self.process,process_observer.register(self.process.pid)))
        self.streams={self.master:'echo' if captured_output else 'output'}
        if captured_output:self.streams[self.process.stdout.fileno()]='output'
        self.closed_streams=set()
        peers.append(self);journal.append({'At':elapsed(),'Entrypoint':name,'Argv':args,'OriginalPid':self.process.pid,
            'OutputMode':'captured-pipe' if captured_output else 'original-pty','StdinMode':'original-controlling-pty'})
    def send(self,data,phase):
        assert elapsed()<(total_seconds if cleaning else work_seconds) and self.process.poll() is None
        assert active_phase_deadline is None or time.monotonic()<active_phase_deadline
        journal.append({'At':elapsed(),'Terminal':self.name,'Phase':phase,'Input':data.decode('utf-8')})
        assert os.write(self.master,data)==len(data), 'Ambiguous partial foreground write; never replay'
    def text(self,offset=0):
        return re.sub(r'\x1b\[[0-9;? ]*[A-Za-z~]','',self.capture[offset:].decode('utf-8',errors='replace'))
def elapsed():return round(time.monotonic()-start,3)
def pump(delay=.02):
    global last_process_sample
    if elapsed()-last_process_sample>=5:
        last_process_sample=elapsed()
        sample=process_observer.collect([identity for _,_,identity in observed_processes])
        journal.append({'At':elapsed(),'OwnedProcessSnapshot':sample,'OriginalProcessPolls':[
            {'Name':name,'Identity':identity,'ExitCode':process.poll()} for name,process,identity in observed_processes]})
    fds=[fd for p in peers for fd in p.streams if fd not in p.closed_streams]
    readable=select.select(fds,[],[],delay)[0] if fds else []
    for p in peers:
        for fd,kind in p.streams.items():
            if fd not in readable:continue
            try:b=os.read(fd,65536)
            except OSError as ex:
                if fd!=p.master or ex.errno!=errno.EIO:raise
                b=b''
            if not b:p.closed_streams.add(fd)
            target=p.echo if kind=='echo' else p.capture
            if b:
                evidence.raw(p.name+('-echo' if kind=='echo' else ''),b)
                journal.append({'At':elapsed(),'Terminal':p.name,'ObservedStream':kind,'ByteStart':len(target),'ByteEnd':len(target)+len(b)})
            target.extend(b)
        p.eof=len(p.closed_streams)==len(p.streams)
    if elapsed()>total_seconds:raise TimeoutError('Driver total bound')
def until(test,seconds,label):
    deadline=min(start+(total_seconds if cleaning else work_seconds),time.monotonic()+seconds)
    if active_phase_deadline is not None:deadline=min(deadline,active_phase_deadline)
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
class ShippedRpc:
    """One fixture process; each request uses a fresh shipped connector stream."""
    def __init__(self):
        args=rpc_helper_arguments(__file__,ship,session)
        self.logs={}
        try:
            for kind in ['stdout','stderr']:self.logs[kind]=(out/('rpc-helper.'+kind+'.log')).open('wb')
            self.process=subprocess.Popen(args,cwd=ship,env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        except BaseException:
            for log in self.logs.values():log.close()
            raise
        observed_processes.append(('shipped-rpc-fixture',self.process,process_observer.register(self.process.pid)))
        self.pending=bytearray();self.closed=set()
        journal.append({'At':elapsed(),'Entrypoint':'shipped-rpc-fixture','Argv':args,'OriginalPid':self.process.pid})
        try:
            ready=self.frame(5)
            if ready!={'fixtureReady':True}:raise RuntimeError('Shipped RPC helper readiness failed')
        except BaseException:
            try:self.close()
            except Exception as failure:journal.append({'At':elapsed(),'RpcHelperStartupCleanupFailure':str(failure)})
            raise
    def collect(self,delay):
        streams=[(self.process.stdout,'stdout'),(self.process.stderr,'stderr')]
        fds=[p.fileno() for p,kind in streams if kind not in self.closed]
        readable=select.select(fds,[],[],delay)[0] if fds else []
        for stream,kind in streams:
            if kind in self.closed or stream.fileno() not in readable:continue
            data=os.read(stream.fileno(),65536)
            if not data:self.closed.add(kind)
            # Raw diagnostics stream directly to disk without a cumulative quota.
            self.logs[kind].write(data);self.logs[kind].flush()
            if kind=='stdout':self.pending.extend(data)
        if len(self.pending)>1048576:
            raise RuntimeError('Shipped RPC outstanding frame bound')
    def frame(self,seconds):
        deadline=min(start+total_seconds,time.monotonic()+seconds)
        while time.monotonic()<deadline:
            if b'\n' in self.pending:
                line,_,remaining=self.pending.partition(b'\n');self.pending=bytearray(remaining)
                return json.loads(line)
            if 'stdout' in self.closed:raise EOFError('Shipped RPC helper response lost; never replay')
            pump(0);self.collect(.01)
        raise TimeoutError('Original shipped RPC response unknown; never replay')
    def request(self,payload,seconds,identity):
        frame={'id':identity,'payload':json.dumps(payload),'timeoutMilliseconds':int(seconds*1000)}
        self.process.stdin.write((json.dumps(frame)+'\n').encode());self.process.stdin.flush()
        reply=self.frame(seconds+.5)
        if reply.get('id')!=identity or not reply.get('ok'):
            raise RuntimeError('Shipped RPC original operation failed: '+str(reply))
        if len(reply['response'].encode('utf8'))>524288:raise RuntimeError('RPC bound')
        return json.loads(reply['response'])
    def close(self):
        try:
            if self.process.stdin and not self.process.stdin.closed:self.process.stdin.close()
            deadline=time.monotonic()+5
            while time.monotonic()<deadline and (self.process.poll() is None or len(self.closed)!=2):
                pump(0);self.collect(.01)
            if self.process.poll() is None or len(self.closed)!=2:
                raise RuntimeError('Shipped RPC helper exit/I-O unconfirmed; guardian retains physical cleanup')
            self.process.wait()
            self.process.stdout.close();self.process.stderr.close()
            if self.process.returncode!=0:raise RuntimeError('Shipped RPC helper failed')
            return {'ExitCode':self.process.returncode,'StdoutEof':True,'StderrEof':True}
        finally:
            for log in self.logs.values():log.close()

rpc_peer=None
def rpc(payload,seconds=2):
    global rpc_peer
    assert payload['command'] in ['status','diagnostics','shutdown']
    def send_original(identity):
        global rpc_peer
        if rpc_peer is None:rpc_peer=ShippedRpc()
        return rpc_peer.request(payload,seconds,identity)
    return observed_rpc(journal,payload,send_original,elapsed)

def read_json(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def staging_signature():
    # Metadata only in this disposable game's staging namespace. Exclude live
    # daemon/bridge health files and all CLI XDG/auth/session state.
    paths=list((session/'game_state').rglob('*.rollback.*'))
    pending=session/'game_state/control/pending_turn_snapshot'
    if pending.exists():paths+=list(pending.rglob('*'))
    paths += [session/'game_state/control/pending_turn_snapshot.json',
        session/'game_state/control/pending_turn_snapshot.authority.json']
    rows=[]
    for p in paths:
        try:
            s=p.stat()
            if p.is_file():rows.append((str(p.relative_to(session)),s.st_size,s.st_mtime_ns))
        except FileNotFoundError:pass # An observed staging cleanup/replacement.
    return tuple(sorted(rows))
def bootstrap_signature():
    paths=[session/'game_state/control/gm_turn_helper.bootstrap.ps1']
    pack=session/'game_state/control/gm_context_pack'
    if pack.exists():paths+=list(pack.rglob('*'))
    rows=[]
    for p in paths:
        try:
            s=p.stat()
            if p.is_file():rows.append((str(p.relative_to(session)),s.st_size,s.st_mtime_ns))
        except FileNotFoundError:pass
    return tuple(sorted(rows))
def preparation_phase(test,label,peer,bootstrap=False,offset=0):
    global client_failure_offset
    phase=PhaseWait(time.monotonic(),start+work_seconds)
    while True:
        if time.monotonic()>=phase.total_deadline:raise TimeoutError('Original overall work bound: '+label)
        if peer.process.poll() is not None:raise RuntimeError(peer.name+' exited during '+label)
        if bootstrap and any(marker in peer.text() for marker in ['Participating canonical mutation refused.',
            'Original participating admission unavailable','Original operation established a result but continuation is unconfirmed']):
            journal.append({'At':elapsed(),'ObservedPhase':'daemon failure before cleanup'})
            raise RuntimeError('Actual daemon bootstrap failure observed before cleanup')
        if peer.name=='client' and current_client_failure(peer.text(offset)):
            client_failure_offset=offset
            journal.append({'At':elapsed(),'ObservedPhase':'current client failure during '+label})
            raise RuntimeError('Actual current client preparation failure')
        if test():
            journal.append({'At':elapsed(),'ObservedPhase':label+' completed'});return
        signature=(len(peer.capture),bootstrap_signature() if bootstrap else staging_signature())
        if phase.observe(time.monotonic(),signature):
            journal.append({'At':elapsed(),'ObservedPhase':label+' progress','CapturedBytes':len(peer.capture),
                'PreparationFiles':len(signature[1]),'PreparationBytes':sum(r[1] for r in signature[1])})
        pump(.1)
def current_request(offset):
    global client_failure_offset
    phase=PhaseWait(time.monotonic(),start+work_seconds)
    waiting=False
    while True:
        text=client.text(offset)
        if client.process.poll() is not None:raise RuntimeError('Client exited during current preparation')
        if current_client_failure(text):
            client_failure_offset=offset
            journal.append({'At':elapsed(),'ObservedPhase':'client-failure-before-captured-request'})
            raise RuntimeError('Current client reported a real failure before captured request')
        if time.monotonic()>=phase.total_deadline:raise TimeoutError('Original overall work bound')
        request_path=session/'input/turn_request.json'
        if request_path.exists():
            journal.append({'At':elapsed(),'ObservedPhase':'current-request-present'})
            return
        if 'Мастер игры размышляет...' in text and not waiting:
            journal.append({'At':elapsed(),'ObservedPhase':'waiting-ui-without-captured-request'})
            waiting=True # Observation only: never authority to submit/replay.
        signature=(len(client.capture),staging_signature())
        if phase.observe(time.monotonic(),signature):
            journal.append({'At':elapsed(),'ObservedPhase':'preparation-progress','ClientBytes':len(client.capture),
                'StagingFiles':len(signature[1]),'StagingBytes':sum(r[1] for r in signature[1])})
        pump(.1)
def fresh_main(client,offset):
    fresh(client,'Продолжить',offset)
    until(lambda:re.search(r'8\.\s*[^\r\n]*Выход',client.text(offset)) is not None,8,'fresh eight-item main menu')
def fresh_player(client,offset,seconds=12):
    until(lambda:current_player_prompt_at(client.text(offset))>=0,seconds,'fresh cooperative player prompt')
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
    client=Terminal('client',['dotnet',str(ship/'BookOfEternityClient/BookOfEternityClient.dll'),str(base),'--plain-output'],captured_output=True)
    fresh(client,'Тренировка QTE',0);offset=len(client.capture);client.send(b'\r','observed NewGame selected')
    offset=answer(client,'Введите имя вашей души:','Пробная Душа',offset)
    offset=answer(client,'Опишите форму вашей души:','Человеческий силуэт мягкого синего света.',offset)
    if relay_mode:
        # Ordinary preset avoids the separately observed freeform↔seeded-Elyara
        # missing-network-edge defect. No canonical state or validator edits.
        offset=answer(client,'Выберите способ создания Хранителя:','2',offset)
        until(lambda:observed_elyara_choice(client.text(offset)) is not None,12,'rendered exact Elyara preset menu')
        choice=observed_elyara_choice(client.text(offset))
        result['OrdinaryGuardianPreset']={'DisplayName':ELYARA_PRESET_NAME,'ObservedMenuIndex':choice}
        result['ExcludedSetupDefect']='Freeform guardian to seeded Elyara network edges; not qualified by this preset run'
        offset=len(client.capture);client.send((choice+'\r').encode(),'observed exact Elyara preset menu index')
        offset=answer(client,'Выбрать этого хранителя для новой игры?','',offset)
    else:
        offset=answer(client,'Выберите способ создания Хранителя:','1',offset)
        offset=answer(client,'Опишите вашего хранителя:','Спокойный Хранитель маяка, который встречает душу на берегу Моря Хаоса.',offset)
    preparation_phase(lambda:(session/'input/turn_request.json').exists(),'ordinary initial request',client,offset=offset);client_wait=True
    result['InitialBootstrapRequest']=read_json(session/'input/turn_request.json');result['OrdinaryNewGame']=True
    # Observe the actual waiting UI, allow its cancellation listener to start.
    fresh(client,'Мастер игры размышляет...',offset,8);pump(.3)
    offset=len(client.capture);client.send(b'\x1b','cancel initial wait before CLI exists')
    preparation_phase(lambda:'Переходный ход отменён.' in client.text(offset) and re.search(r'8\.\s*[^\r\n]*Выход',client.text(offset)) is not None,'ordinary initial cancellation and current main menu',client,offset=offset);client_wait=False
    assert not (session/'input/turn_request.json').exists()
    result['BootstrapCancelledBeforeCli']=True
    launcher=str(ship/'BookOfEternityClient/Launcher/bookofeternity.ps1')
    bridge=Terminal('bridge',['pwsh','-NoLogo','-NoProfile','-File',launcher,'start-bridge','visible','-SessionPath',str(session)])
    record_path=base/'.boe_runtime/gm-runs/main.json';status_path=session/'game_state/control/gm_bridge_status.json'
    until(lambda:record_path.exists() and status_path.exists() and bool(read_json(status_path).get('transportEndpoint')) and read_json(record_path)['Disposition'] in ['Running','Uncertain'],10,'original Running and published transport')
    record=read_json(record_path);status=rpc({'command':'status'})
    assert status['ok'] and status['status']['terminalOwnerRetained'] and status['status']['terminalRunId']==record['Identity']['RunId']
    original_record=record['Identity'];original={k[0].lower()+k[1:]:v for k,v in original_record.items()};original['backend']=2
    result['ObservedStartupRecord']=record;result['OriginalRunningStatus']=status
    if record['Disposition']!='Running' or status['status']['terminalUncertain']:
        raise RuntimeError('Current original bootstrap failed; no model submission or replay')
    result['OriginalRunningRecord']=record
    assert status['status']['cliLaunchCommand']==command and status['status']['shellWorkingDirectory']==str(session)
    def ready():
        status=rpc({'command':'status'})['status'];result['LastStartupStatus']=status
        if status['terminalUncertain'] or not status['terminalOwnerRetained']:raise RuntimeError('Original owner lost')
        return status['ready']
    until(ready,15,'original derived Ready; no gate/query answers')
    result['ReadinessQualified']=True
    daemon=Terminal('daemon',['pwsh','-NoLogo','-NoProfile','-File',launcher,'start-daemon','visible','--timeout',str(600 if relay_mode else 180),'--log',str(out/'daemon.log'),'-SessionPath',str(session)])
    preparation_phase(lambda:'Waiting for turns...' in daemon.text(),'daemon bootstrap',daemon,bootstrap=True)
    offset=len(client.capture);client.send(b'\r','observed Continue selected')
    preparation_phase(lambda:current_player_prompt_at(client.text(offset))>=0,'ordinary Continue current player prompt',client,offset=offset);client_prompt=True
    provider_bridge_offset=len(bridge.capture)
    offset=len(client.capture);client.send((action+'\r').encode(),'one genuine game action');result['GenuineActionsSent']=1;client_prompt=False;client_wait=True
    current_request(offset)
    request=read_json(session/'input/turn_request.json');result['ActualRequest']=request;result['ActualRequestSHA256']=hashlib.sha256((session/'input/turn_request.json').read_bytes()).hexdigest()
    assert request['playerAction']==action and request['turnNumber']==1
    assert request['sessionId']==result['InitialBootstrapRequest']['sessionId'] and request['requestId']!=result['InitialBootstrapRequest']['requestId']
    (out/'actual-turn-request.json').write_bytes((session/'input/turn_request.json').read_bytes())
    if controlled_refusal:
        # Own fixed fixture emits only a denial, never an accepted GM response.
        (session/'.inert-provider-denial-trigger').write_text('one controlled denial\n')
    story=session/'stories/chaos_sea.jsonl';provider_deadline=min(start+work_seconds,time.monotonic()+provider_seconds)
    while time.monotonic()<provider_deadline:
        pump()
        # Capture transient client rejection evidence before a later rollback can
        # delete it; each artifact is copied once, at first observation.
        for artifact_name in ['error_log.txt','output/validation_repair_request.json',
            'game_state/control/validation_diagnostic_failure_report.json']:
            source_path=session/artifact_name
            if source_path.exists() and not (out/('captured-'+artifact_name.replace('/','__'))).exists():
                (out/('captured-'+artifact_name.replace('/','__'))).write_bytes(source_path.read_bytes())
        # Observe current output before another status RPC can obscure the causal
        # failure. Old startup/turn output is outside this original action's cut.
        provider_text=bridge.text(provider_bridge_offset)
        if re.search(r'(?m)^\s*Forbidden: Domain forbidden[ \r]*$',provider_text):
            result['ObservedProviderFailure']={'Text':'Forbidden: Domain forbidden','At':elapsed(),'BridgeCut':provider_bridge_offset}
            raise RuntimeError('Actual current provider refusal; no automatic retry')
        if current_client_failure(client.text(offset)):
            client_failure_offset=offset
            result['ObservedCurrentClientFailureAt']=elapsed()
            raise RuntimeError('Actual current client failure during provider wait')
        for peer in [client,bridge,daemon]:
            if peer is not None and peer.process.poll() is not None:
                raise RuntimeError(peer.name+' exited during current provider wait')
        current=rpc({'command':'status'})['status'];result['LastBridgeStatus']=current
        if current['terminalUncertain'] or not current['terminalOwnerRetained']:raise RuntimeError('Original owner uncertainty; acceptance remains blocked')
        refused=current.get('promptDelivery')
        retain_turn_delivery(result,refused,result['OriginalRunningStatus']['status']['inputBindingId'])
        if relay_mode:
            for queued in relay_queue.glob('request-*'):
                if not (queued/'request.json').exists() or not (queued/'prompt.txt').exists():continue
                prompt=(queued/'prompt.txt').read_bytes()
                expected=hashlib.sha256(b'1\n'+prompt).hexdigest().upper()
                if refused and refused.get('disposition')=='submission-observed' and refused.get('contentHash')==expected and not (queued/'dispatch.json').exists():
                    (queued/'dispatch.json').write_text(json.dumps(refused,indent=2)+'\n')
            if (relay_queue/'failure.json').exists() or 'RELAY ERROR' in provider_text:
                raise RuntimeError('Actual relay consumer/transport failure; no retry')
        if refused and (refused.get('disposition')=='draft-uncertain' or
            (refused.get('disposition')=='not-written' and refused.get('reason')=='unsupported-draft-shape')):
            result['OriginalPromptDelivery']=refused
            raise RuntimeError('Actual original automatic input refused; no manual reconstruction or replay')
        if story.exists():
            entries=[json.loads(line) for line in story.read_text(encoding='utf-8-sig').splitlines() if line.strip()]
            accepted=[e for e in entries if e.get('turn')==1 and e.get('player')==action and e.get('narrative','').strip()]
            if accepted:
                fresh_player(client,offset,10)
                assert not (session/'input/turn_request.json').exists()
                assert not (session/'game_state/control/pending_turn_snapshot.json').exists()
                assert not (session/'game_state/control/pending_turn_snapshot.authority.json').exists()
                assert not (session/'ready/turn_complete.json').exists() and not (session/'ready/turn_error.json').exists()
                result['AcceptedGameTurns']=1;result['AcceptedStory']=accepted[0];client_wait=False;client_prompt=True
                delivery=result.get('OriginalTurnDelivery');result['OriginalPromptDelivery']=delivery
                result['InputQualified']=bool(delivery and delivery.get('disposition')=='submission-observed' and
                    delivery.get('inputBindingId')==result['OriginalRunningStatus']['status']['inputBindingId'] and
                    delivery.get('operationKind')=='turn' and delivery.get('operationRevision')=='live')
                break
        if any(marker in bridge.text()[-4000:].lower() for marker in ['permission required','allow this','trust this','sign in','authorize access']):
            raise RuntimeError('Unexpected CLI access/auth gate; no answer supplied')
        if (session/'ready/turn_error.json').exists():
            result['ActualTerminalError']=read_json(session/'ready/turn_error.json');raise RuntimeError('Actual game terminal error; no automatic replay')
    if result['AcceptedGameTurns']!=1:raise TimeoutError('One-turn phase ended without actual game acceptance')
    # The shared finally performs client exit under its reserved cleanup bound.
except Exception as ex:
    result['Failure']=type(ex).__name__+': '+str(ex)
finally:
    cleaning=True
    # Settle participating client wait before original owner stop; never replay its action.
    relay_closed=True
    if relay_mode:
        try:relay_closed=close_relay_execution(relay_queue,6)
        except Exception as ex:relay_closed=False;result['RelayCloseFailure']=type(ex).__name__+': '+str(ex)
        result['RelayExecutionClosed']=relay_closed
        if not relay_closed:result['RelayCloseFailure']='Execution/child/I-O closure unconfirmed; skip client cancellation/rollback'
    if client is not None and client.process.poll() is None and relay_closed:
        # Reserve bounded cleanup room within the relay720s cap for the sole original shutdown12s,
        # bridge exit8s, daemon exit16s, EOF8s and connection/disposal margin.
        active_phase_deadline=start+client_cleanup_seconds
        try:
            if client_wait or current_client_failure(client.text(offset)):
                offset=client_failure_offset if client_failure_offset is not None else offset
                pause_acknowledged=False;cancel_sent=False
                def settled():
                    # An actually rendered client error pause is distinct from
                    # a GM trust/access/update gate. Acknowledge at most once.
                    text=client.text(offset)
                    fresh_screen='Продолжить' in text or current_player_prompt_at(text)>=0
                    pause_at=text.rfind('Нажмите любую клавишу для продолжения...')
                    screen_at=max(text.rfind('Продолжить'),current_player_prompt_at(text))
                    if pause_at>=screen_at and pause_at>=0:
                        # A stale player prompt cannot settle a newer error pause.
                        return False
                    if pause_at>=0 and screen_at>pause_at and fresh_screen:
                        # Coalesced output already advanced beyond the pause. An
                        # Enter now would target the new player prompt, not the pause.
                        result['ClientSettlement']='error-pause already left; rollback not established'
                        return True
                    cancelled='Изменения отменены, прежнее состояние восстановлено.' in text
                    pending_absent=not any((session/p).exists() for p in ['input/turn_request.json',
                        'game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json',
                        'game_state/control/pending_turn_snapshot'])
                    if fresh_screen and cancel_sent and cancelled and pending_absent:
                        result['ClientSettlement']='actual cancellation and fresh screen with request absent'
                        return True
                    return fresh_screen and pause_acknowledged
                settlement=PhaseWait(time.monotonic(),active_phase_deadline)
                while not settled():
                    text=client.text(offset)
                    if 'Нажмите любую клавишу для продолжения...' in text:
                        if pause_acknowledged:raise RuntimeError('Client repeated error pause during original cleanup')
                        result['ObservedClientErrorPause']=True
                        error_path=session/'error_log.txt'
                        result['OriginalClientErrorLogAvailable']=error_path.exists()
                        if error_path.exists():(out/'original-client-error.txt').write_bytes(error_path.read_bytes())
                        offset=len(client.capture);client.send(b'\r','observed client error pause: acknowledge once');pause_acknowledged=True
                        result['ClientSettlement']='error-pause-acknowledged; rollback not established'
                    elif not pause_acknowledged and not cancel_sent and not current_client_failure(text) and 'Мастер игры размышляет...' in text:
                        offset=len(client.capture);client.send(b'\x1b','observed current wait: cancel once');cancel_sent=True
                    if client.process.poll() is not None:raise RuntimeError('Client exited before fresh cleanup screen')
                    settlement.observe(time.monotonic(),len(client.capture));pump(.1)
                client_wait=False
                client_prompt=current_player_prompt_at(client.text(offset))>=0
            if client_prompt and not client_exit_attempted:exit_client(client)
            elif client.process.poll() is None:
                client.send(b'\x03','failure: stop original client foreground');until(lambda:client.process.poll() is not None,6,'client foreground stop')
        except Exception as ex:result['ClientCleanupFailure']=type(ex).__name__+': '+str(ex)
        finally:active_phase_deadline=None
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
    # Keep participating admission transport alive until original fenced settlement.
    # Lost/unknown ACK retains logical uncertainty; never force a success or replay.
    if daemon is not None and daemon.process.poll() is None and result['LogicalLifecycleVerified']:
        try:
            daemon.send(b'\x03','stop daemon only after original Stopped ACK');until(lambda:daemon.process.poll() is not None,16,'daemon original stop')
        except Exception as ex:result['DaemonCleanupFailure']=type(ex).__name__+': '+str(ex)
    result['ShutdownAttempts']=shutdown_attempts
    if rpc_peer is not None:
        try:result['RpcHelperClosure']=rpc_peer.close()
        except Exception as ex:result['RpcHelperCleanupFailure']=type(ex).__name__+': '+str(ex)
    for p in peers:
        if p.process.poll() is not None and p.slave>=0:os.close(p.slave);p.slave=-1
    try:until(lambda:all(p.eof for p in peers),8,'actual foreground EOF')
    except Exception as ex:result['IoCleanupFailure']=type(ex).__name__+': '+str(ex)
    result['Terminals']={}
    for p in peers:
        try:restored=termios.tcgetattr(p.master)==p.initial
        except OSError:restored=False
        result['Terminals'][p.name]={'ExitCode':p.process.poll(),'EOF':p.eof,'StreamEOF':{kind:fd in p.closed_streams for fd,kind in p.streams.items()},
            'TermiosRestored':restored,'CapturedBytes':len(p.capture),'EchoBytes':len(p.echo),
            'OutputMode':'captured-pipe' if p.captured_output else 'original-pty'}
        if p.captured_output:
            p.process.stdout.close()
        for fd in [p.master,p.slave]:
            if fd>=0:os.close(fd)
    result['ElapsedSeconds']=elapsed();result['Success']=rpc_helper_closed(result) and result['AcceptedGameTurns']==1 and result.get('InputQualified',False) and result['LogicalLifecycleVerified'] and not any(k.endswith('Failure') for k in result) and all(v['EOF'] and v['TermiosRestored'] for v in result['Terminals'].values())
    if controlled_refusal:
        result['ControlledCleanupVerified']=rpc_helper_closed(result) and bool(result.get('ObservedProviderFailure')) and result['LogicalLifecycleVerified'] and result['ShutdownAttempts']==1 and result.get('ClientSettlement')=='actual cancellation and fresh screen with request absent' and not any(k in result for k in ['ClientCleanupFailure','CleanupFailure','DaemonCleanupFailure','IoCleanupFailure']) and all(v['ExitCode']==0 and v['EOF'] and v['TermiosRestored'] for v in result['Terminals'].values())
    (out/'rpc-journal.json').write_text(json.dumps(journal,indent=2,ensure_ascii=False)+'\n')
    (out/'probe-result.json').write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n')
    print(json.dumps({k:result.get(k) for k in ['Success','AcceptedGameTurns','GenuineActionsSent','Failure','CleanupFailure','ElapsedSeconds']}))
sys.exit(0 if result['Success'] or result.get('ControlledCleanupVerified',False) else 1)
