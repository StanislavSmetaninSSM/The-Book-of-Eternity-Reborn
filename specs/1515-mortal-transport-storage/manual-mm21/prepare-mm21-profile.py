import pathlib,json,hashlib,uuid,os,sys,re,shutil,stat

def plain_path(path,directory=False):
    path=pathlib.Path(path)
    if any(p.is_symlink() for p in [path,*path.parents]):raise ValueError('Linked profile input')
    mode=path.stat().st_mode
    if not (stat.S_ISDIR(mode) if directory else stat.S_ISREG(mode)):raise ValueError('Special profile input')
    return path

def regular_tree(root):
    root=plain_path(root,True);pending=[root]
    while pending:
        for p in pending.pop().iterdir():
            mode=p.lstat().st_mode
            if stat.S_ISDIR(mode):pending.append(p)
            elif not stat.S_ISREG(mode):raise ValueError('Linked/special profile tree entry')
    return root

def exact_tree(root,pins):
    root=regular_tree(root)
    actual={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in root.rglob('*') if p.is_file()}
    if actual!=pins:raise ValueError('Exact phase-copy inventory changed')
    return actual

def continuation_spec(phase,previous=None):
    spec={'p02':('p01',42,43),'p03':('p02',43,44)}.get(phase)
    if spec is None:raise ValueError('Only reviewed p02/p03 continuation')
    if previous is not None and (type(previous.get('AcceptedTurnNumber')) is not int or previous['AcceptedTurnNumber']!=spec[1]):
        raise ValueError('Predecessor turn differs from closed continuation')
    return spec

def require_previous_manual_save(previous,session,expected_turn,pe):
    if type(previous.get('AcceptedTurnNumber')) is not int or previous['AcceptedTurnNumber']!=expected_turn or not pe.ordinary_save_proved(previous):
        raise ValueError('Accepted ordinary Save predecessor required')
    folder=regular_tree(pathlib.Path(session)/'saves/manual_saves')
    digest=previous['OrdinarySaveProof']['ArchiveSHA256']
    matches=[p for p in folder.rglob('*.zip') if hashlib.sha256(p.read_bytes()).hexdigest()==digest]
    if len(matches)!=1:raise ValueError('Exact unique predecessor manual archive required')
    raw=plain_path(matches[0]).read_bytes()
    if hashlib.sha256(raw).hexdigest()!=digest:raise ValueError('Predecessor archive changed during admission')
    pe.verify_ordinary_save_archive(raw,previous['OrdinarySaveProof']['Before'],expected_turn)
    return digest

def checked_continuation_binding(value):
    if type(value) is not dict or set(value)!={'Phase','PreviousSHA256','GuardianSHA256','SeedSHA256','PhasePackageSHA256','SourcePins','Action','ConsoleRoute'}:
        raise ValueError('Exact continuation binding required')
    continuation_spec(value['Phase'])
    for key in ('PreviousSHA256','GuardianSHA256','SeedSHA256','PhasePackageSHA256'):
        if type(value[key]) is not str or re.fullmatch('[0-9a-f]{64}',value[key]) is None:raise ValueError('Invalid binding digest')
    action=value['Action']
    if type(action) is not str or not action.strip() or len(action.encode())>4096 or any(ord(c)<32 for c in action):raise ValueError('Invalid single action')
    route=value['ConsoleRoute']
    if type(route) is not dict:raise ValueError('Exact continuation route required')
    fields={'permission-with-denial':{'Route','StorageId','ItemId'},'ordinary-save':{'Route'}}.get(route.get('Route'))
    if fields is None or set(route)!=fields or (value['Phase']=='p03' and route.get('Route')!='ordinary-save'):raise ValueError('Exact continuation route required')
    for key in fields-{'Route'}:
        if type(route[key]) is not str or not route[key] or any(ord(c)<32 for c in route[key]):raise ValueError('Invalid target identity')
    return value

def require_profile_outputs_absent(phase,launcher):
    phase=pathlib.Path(phase);launcher=pathlib.Path(launcher)
    if not phase.is_absolute() or phase!=phase.resolve() or launcher!=phase.parent/('launch-'+phase.name+'.sh'):
        raise ValueError('Exact phase output paths required')
    for p in (phase,*phase.parents):
        if p.is_symlink() or (p.exists() and not p.is_dir()):raise ValueError('Linked/special phase parent')
    for p in (phase/'physical-trace',phase/'launch-profile.json',launcher,phase.parent/('launch-preparation.json' if phase.name=='p01' else 'launch-preparation-'+phase.name+'.json')):
        if os.path.lexists(p):raise ValueError('Profile output already exists; no repair/retry')

def write_profile_artifact(path,raw,mode):
    if mode not in (0o600,0o700):raise ValueError('Private profile mode required')
    plain_path(pathlib.Path(path).parent,True)
    fd=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL,mode)
    with os.fdopen(fd,'wb') as stream:stream.write(raw)
    if stat.S_IMODE(plain_path(path).stat().st_mode)!=mode:
        raise ValueError('Created profile mode differs; retained without repair')

def create_physical_trace_assets(base,phase,nonce):
    base=plain_path(base,True);phase=plain_path(phase,True)
    if not base.is_absolute() or not phase.is_absolute() or base!=base.resolve() or phase!=phase.resolve():
        raise ValueError('Exact canonical/phase paths required')
    trace=phase/'physical-trace'
    if trace==base or base in trace.parents or trace in base.parents:raise ValueError('Trace overlaps canonical play')
    if type(nonce) is not str or re.fullmatch('[0-9a-f]{32}',nonce) is None:raise ValueError('Exact StopNonce required')
    if os.path.lexists(trace):raise ValueError('Trace already exists; no repair/retry')
    trace.mkdir(mode=0o700)
    if stat.S_IMODE(trace.stat().st_mode)!=0o700:raise ValueError('Created trace mode differs; no repair')
    write_profile_artifact(trace/'owner-nonce',nonce.encode('ascii'),0o600)
    return {'Route':'external','Directory':str(trace)}

def write_physical_launch_profile(base,phase,launcher,profile):
    require_profile_outputs_absent(phase,launcher)
    profile=dict(profile)
    profile['PhysicalTrace']=create_physical_trace_assets(base,phase,profile['StopNonce'])
    raw=(json.dumps(profile,indent=2,ensure_ascii=False)+'\n').encode('utf-8')
    write_profile_artifact(phase/'launch-profile.json',raw,0o600)
    return profile,hashlib.sha256(raw).hexdigest()

def require_standalone_seed(root,phase,seed):
    play=root/'play';session=play/'game_session'
    if phase!=root/'p01' or phase!=plain_path(phase,True):
        raise ValueError('Exact standalone phase required')
    if (seed.get('kind')!='seeded-live-mortal' or seed.get('basePath')!=str(play)
        or seed.get('acceptedGmTurn') is not False or type(seed.get('setupTurn')) is not int or seed['setupTurn']!=41
        or type(seed.get('validationIssues')) is not int or seed['validationIssues']!=0
        or any((session/'saves').rglob('*.zip'))):
        raise ValueError('Standalone Load requires a fresh validated setup41 export without prior archives')
    for path in (phase/'queue',phase/'action.txt',phase/'console-route.json',phase/'launch-env',
                 session/'_runtime',play/'.boe_runtime/gm-runs',play/'.boe_runtime/locks/gm-daemon-instance.lock',
                 *(session/'game_state/control'/name for name in (
                     'gm_bridge_status.json','gm_daemon_status.json','pending_turn_snapshot',
                     'pending_turn_snapshot.json','pending_turn_snapshot.authority.json','validation_repair_request.json'))):
        if os.path.lexists(path):raise ValueError('Standalone seed or phase contains old state or output: '+str(path))
    for name in ('input','output','ready'):
        path=session/name
        if os.path.lexists(path) and any(plain_path(path,True).iterdir()):
            raise ValueError('Standalone seed contains prior action material: '+name)

def require_standalone_preparation(root,phase):
    raw=lambda path:plain_path(path).read_bytes()
    sha=lambda value:hashlib.sha256(value).hexdigest()
    receipt=json.loads(raw(root/'preparation-manifest.json'))
    inputs=raw(root/'preparation-inputs.json')
    seed_bytes=raw(root/'seed-manifest.json');seed=json.loads(seed_bytes)
    if (receipt.get('prepared') is not True or receipt.get('acceptedGmTurn') is not False
        or receipt.get('NoGameOrRelayLaunched') is not True or json.loads(inputs).get('Route')!='self-save-load-turn'
        or receipt.get('inputSHA256')!=sha(inputs) or receipt.get('seedManifestSHA256')!=sha(seed_bytes)
        or receipt.get('packageSHA256')!=sha(raw(phase/'relay-phase-package.json')) or receipt.get('seed')!=seed):
        raise ValueError('Completed standalone preparation and current inputs differ')
    session=root/'play/game_session';story='stories/mortal_life_2.jsonl'
    if (seed.get('storyPath')!=story or list(plain_path(session/'stories',True).iterdir())!=[session/story]
        or seed.get('storyFiles')!=[{'path':story,'sha256':sha(raw(session/story))}]):
        raise ValueError('Standalone setup story differs')
    session_id=seed.get('sessionId')
    if (type(session_id) is not str or not session_id
        or json.loads(raw(session/'game_state/history/chat_log.json'))!={'sessionId':session_id,'entries':[]}):
        raise ValueError('Standalone setup chat/session differs')
    return seed

def render_launch_script(D,phase,previous_path,failure,stop,profile_sha,cold=False,*,load_nonce=None):
    if load_nonce is not None and (cold or phase!='p01' or previous_path is not None
            or type(load_nonce) is not str or re.fullmatch('[0-9a-f]{32}',load_nonce) is None):
        raise ValueError('A Load observation nonce requires the exact standalone Load route')
    script='''#!/bin/bash
set -euo pipefail
umask 077
ROOT=PREPARED_DESTINATION
P="$ROOT/PREPARED_PHASE"
for marker in launch.log launch-origin.json guardian.json probe-result.json queue; do
 test ! -e "$P/$marker" && test ! -L "$P/$marker" || exit 71
done
exec > "$P/launch.log" 2>&1
export POWERSHELL_TELEMETRY_OPTOUT=1
export POWERSHELL_UPDATECHECK=Off
export DOTNET_CLI_TELEMETRY_OPTOUT=1
source /workspace/shared/isolated-restore-20261119/env.sh
export BOE_TERMINAL_NODE=/opt/codex/runtimes/codex-primary-runtime/dependencies/node/bin/node
export BOE_TERMINAL_PREFIX="$P/terminal-deps"
export PYTHONDONTWRITEBYTECODE=1
export TERM=dumb
export HOME="$P/launch-env/home" XDG_CONFIG_HOME="$P/launch-env/config" XDG_DATA_HOME="$P/launch-env/data" XDG_STATE_HOME="$P/launch-env/state" XDG_CACHE_HOME="$P/launch-env/cache" TMPDIR="$P/launch-env/tmp"
export TMP="$TMPDIR" TEMP="$TMPDIR"
export BOE_TEST_GAME_LOOP_FAILURE_NONCE=FAILURE_NONCE
export BOE_MAIN_STOP_DIAGNOSTIC_NONCE=STOP_NONCE
export BOE_TEST_CANONICAL_LEASE_TRACE_ROUTE=external
export BOE_TEST_CANONICAL_LEASE_TRACE_DIR="$P/physical-trace"
export BOE_TEST_PHYSICAL_TRACE_PROFILE_SHA256=PROFILE_SHA
export BOE_TEST_DIAGNOSTIC_ROOT="$P"
export NPM_CONFIG_USERCONFIG="$P/empty-user.npmrc" NPM_CONFIG_GLOBALCONFIG="$P/empty-global.npmrc" NPM_CONFIG_CACHE="$P/launch-env/cache/npm" NPM_CONFIG_IGNORE_SCRIPTS=true
unset BOE_TEST_LOAD_OBSERVATION_NONCE
/usr/bin/python3 - "$P" <<'ORIGIN'
import datetime,json,os,pathlib,sys,time
p=pathlib.Path(sys.argv[1])/'launch-origin.json'
record={'OriginalDriverStartedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'MonotonicSeconds':time.monotonic(),'BootId':pathlib.Path('/proc/sys/kernel/random/boot_id').read_text().strip()}
with p.open('x') as f:json.dump(record,f);f.flush();os.fsync(f.fileno())
p.chmod(0o400)
ORIGIN
date -u --iso-8601=ns
cat /proc/sys/kernel/random/boot_id
printf 'original_shell_pid=%s\\n' "$$"
cat /proc/$$/stat
cd /workspace/shared/mortal-isolated-20261119
set +e
"$ROOT/native/host-guardian" --relay-turn "$P/guardian.json" 750000 /usr/bin/python3 tests/fixtures/ProductionMain/live-mortal-phase.py "$P" "$ROOT/seed-manifest.json" "$P/action.txt" "$ROOT/receipt-helper/BookOfEternityClient.TestSupport.dll" PREPARED_PREVIOUS
guardian_exit=$?
set -e
printf '%s\\n' "$guardian_exit" > "$P/guardian-process.exit"
/usr/bin/python3 - "$P" "$guardian_exit" <<'ENDPY'
import json,pathlib,sys
p=pathlib.Path(sys.argv[1]);g=int(sys.argv[2]);code=g if 1<=g<=255 else 98
try:
 r=json.loads((p/'guardian.json').read_text())
 if g==0 and type(r) is dict:
  driver=r.get('driverExitCode')
  clean=all(type(r.get(k)) is int and r[k]==0 for k in ('driverExitCode','emergencySignals','failures')) and r.get('echild') is True and r.get('deadline') is False
  code=0 if clean else (driver if type(driver) is int and 1<=driver<=255 else 98)
except Exception: code=g if 1<=g<=255 else 98
(p/'launch.exit').write_text(str(code)+'\\n')
raise SystemExit(code)
ENDPY
exit $?
'''.replace('PREPARED_DESTINATION',str(D)).replace('PREPARED_PHASE',phase).replace('PREPARED_PREVIOUS','"$ROOT/'+previous_path.parent.name+'/probe-result.json"' if previous_path else '').replace('=FAILURE_NONCE','= '+failure).replace('=STOP_NONCE','= '+stop).replace('=PROFILE_SHA','='+profile_sha).replace('= ','=')
    if cold:
        if phase!='recovered-live' or previous_path is not None:raise ValueError('Closed cold launch route required')
        if not D.is_absolute() or D!=D.resolve():raise ValueError('Cold root alias')
        script=script.replace('guardian.json probe-result.json queue;', 'guardian.json probe-result.json;')
        script=script.replace('exec > "$P/launch.log" 2>&1', '/usr/bin/python3 - "$ROOT" "$P" <<\'COLD_QUEUE\'\nimport pathlib,stat,sys\nroot=pathlib.Path(sys.argv[1]);phase=pathlib.Path(sys.argv[2]);queue=phase/\'queue\'\nif phase!=root/\'recovered-live\' or phase!=phase.resolve():raise ValueError(\'Cold phase alias\')\nif any(p.is_symlink() for p in [queue,*queue.parents]):raise ValueError(\'Linked cold queue\')\nif not queue.is_dir() or stat.S_IMODE(queue.stat().st_mode)!=0o700 or any(queue.iterdir()):raise ValueError(\'Cold queue must be private and empty\')\nif stat.S_IMODE(phase.stat().st_mode)!=0o700:raise ValueError(\'Cold phase must be private\')\ntmp=root/\'rt\'\nif tmp.exists() or tmp.is_symlink():raise ValueError(\'Cold short temp must be absent\')\nCOLD_QUEUE\n'+'exec > "$P/launch.log" 2>&1')
        script=script.replace('"$ROOT/seed-manifest.json"','"$ROOT/cold-host-descriptor.json"').replace('TMPDIR="$P/launch-env/tmp"','TMPDIR="$ROOT/rt"')
    if load_nonce is not None:
        script=script.replace('unset BOE_TEST_LOAD_OBSERVATION_NONCE\n',
                              'export BOE_TEST_LOAD_OBSERVATION_NONCE='+load_nonce+'\n')
    return script

def main():
    if len(sys.argv) not in (2,3):raise ValueError('Destination and optional reviewed continuation binding or standalone Load route required')
    D=pathlib.Path(sys.argv[1])
    if not D.is_absolute() or D!=D.resolve() or any(x.is_symlink() for x in [D,*D.parents]) or D.parent!=pathlib.Path('/workspace/shared') or re.fullmatch(r'mm[0-9]{2}-[0-9]{8}',D.name) is None:raise ValueError('Invalid prepared destination')
    prepared=json.loads(plain_path(D/'preparation-inputs.json').read_text())
    regular_tree(D/'play')
    assert prepared['destination']==str(D) and prepared['owningBuildManifestSHA256']==os.environ['MM_REVIEWED_INPUT_SHA256']
    binding_bytes=None;binding=None
    self_save_load=len(sys.argv)==3 and sys.argv[2]=='--self-save-load-turn'
    if (self_save_load and prepared.get('Route')!='self-save-load-turn') or (not self_save_load and 'Route' in prepared):
        raise ValueError('Profile does not match prepared seed route')
    if len(sys.argv)==3 and not self_save_load:
        binding_path=pathlib.Path(sys.argv[2])
        if not binding_path.is_absolute() or binding_path!=binding_path.resolve() or any(x.is_symlink() for x in [binding_path,*binding_path.parents]):raise ValueError('Aliased continuation binding')
        binding_bytes=plain_path(binding_path).read_bytes()
        if hashlib.sha256(binding_bytes).hexdigest()!=os.environ['MM_REVIEWED_CONTINUATION_SHA256']:raise ValueError('Continuation binding changed')
        binding=checked_continuation_binding(json.loads(binding_bytes))
    phase=binding['Phase'] if binding is not None else 'p01' 
    P=D/phase;R=pathlib.Path('/workspace/shared/mortal-isolated-20261119');h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();inv=lambda root:{str(p.relative_to(root)):h(p) for p in root.rglob('*') if p.is_file()};before=inv(D/'play');os.umask(0o077)
    require_profile_outputs_absent(P,D/('launch-'+phase+'.sh'))
    a='Выхожу из покоев, ищу представителя совета и прошу проводить меня к его хозяйственному двору, показать припаркованную грузовую повозку и закрытую кладовую с разметочным мелом, объяснить владельца и порядок выдачи. Ничего не беру и не перемещаю'
    previous_path=None;route={'Route':'birth'}
    if self_save_load:
        seed=require_standalone_preparation(D,P)
        require_standalone_seed(D,P,seed)
        route={'Route':'self-save-load-turn'}
        a='Осматриваю комнату, в которой нахожусь, и прислушиваюсь. Ничего не беру, не перемещаю и не покидаю комнату.'
    if binding is not None:
        required_sources=[pathlib.Path(__file__),pathlib.Path(__file__).with_name('submit-mm21-once.py'),R/'tests/fixtures/ProductionMain/live-mortal-phase.py',R/'tests/fixtures/ProductionMain/relay_phase_evidence.py',R/'tests/fixtures/ProductionMain/mortal_console_routes.py']
        for f in required_sources:plain_path(f)
        if binding['SourcePins']!={str(x.relative_to(R)):h(x) for x in required_sources}:raise ValueError('Continuation sources changed before import')
        prior=plain_path(D/continuation_spec(phase)[0],True);previous_path=prior/'probe-result.json'
        regular_tree(prior/'relay');regular_tree(D/'receipt-helper')
        for key,f in [('PreviousSHA256',previous_path),('GuardianSHA256',prior/'guardian.json'),('SeedSHA256',D/'seed-manifest.json'),('PhasePackageSHA256',prior/'relay-phase-package.json')]:
            if h(plain_path(f))!=binding[key]:raise ValueError('Predecessor binding changed: '+key)
        sys.path.insert(0,str(R/'tests/fixtures/ProductionMain'))
        import relay_phase_evidence as pe,mortal_console_routes as routes
        previous=pe.strict_settings(previous_path.read_bytes());seed=pe.strict_settings((D/'seed-manifest.json').read_bytes())
        package,package_sha=pe.relay_package_preflight(prior,D/'play/game_session/_runtime',R/'tests/fixtures/ProductionMain/live-mortal-phase.py')
        pe.terminal_package_preflight(prior,package)
        exact_tree(prior/'relay',package['RelayPins'])
        if previous.get('Success') is not True or not pe.relay_phase_cleanup_proved(previous) or not pe.guardian_proved(pe.strict_settings((prior/'guardian.json').read_bytes())):raise ValueError('Predecessor cleanup not proven')
        if previous['BasePath']!=str(D/'play') or previous['SeedSessionId']!=seed['sessionId'] or previous['AcceptedTurnNumber']!=continuation_spec(phase,previous)[1] or previous['SeedManifestSha256']!=binding['SeedSHA256'] or previous['PhasePackage']!=package:raise ValueError('Predecessor identity/package changed')
        if phase=='p03':require_previous_manual_save(previous,D/'play/game_session',continuation_spec(phase)[1],pe)
        pe.retained_bytes_proved(D/'play/game_session',previous);pe.receipt_package_proved(D/'receipt-helper/BookOfEternityClient.TestSupport.dll',package['ReceiptHelperPins'])
        if pe.strict_settings(plain_path(D/'play/.boe_runtime/gm-runs/main.json').read_bytes())!=previous['StoppedRecord']:raise ValueError('Stopped original owner changed')
        for name,f in [('Status',D/'play/game_session/game_state/control/gm_daemon_status.json'),('Guard',D/'play/.boe_runtime/locks/gm-daemon-instance.lock')]:
            if plain_path(f).read_bytes().hex()!=previous['DaemonSettlement']['Final'+name+'RawHex']:raise ValueError('Settled daemon evidence changed')
        route=routes.checked_route(binding['ConsoleRoute']);a=binding['Action']
        if route['Route']=='permission-with-denial':
            routes.storage_target(pe.strict_settings((D/'play/game_session/game_state/world/current_location.json').read_bytes()),route['StorageId'],route['ItemId'],False)
        for name in ('input/turn_request.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json','game_state/control/pending_turn_snapshot','ready/turn_complete.json','ready/turn_error.json'):
            f=D/'play/game_session'/name
            if f.exists() or f.is_symlink():raise ValueError('Pending continuation: '+name)
        if P.exists() or P.is_symlink() or (D/('launch-'+phase+'.sh')).exists() or (D/('launch-'+phase+'.sh')).is_symlink():raise ValueError('Continuation destination occupied')
        for name in ('empty-user.npmrc','empty-global.npmrc'):
            if plain_path(prior/name).read_bytes()!=b'':raise ValueError('Nonempty npm config')
        P.mkdir(mode=0o700)
        for name in ('relay','terminal-fixture','terminal-deps'):shutil.copytree(prior/name,P/name)
        (P/'relay-phase-package.json').write_bytes((prior/'relay-phase-package.json').read_bytes())
        for name in ('empty-user.npmrc','empty-global.npmrc'):
            if plain_path(prior/name).read_bytes()!=b'':raise ValueError('Nonempty npm config')
            (P/name).write_bytes(b'')
        (P/'continuation-binding.json').write_bytes(binding_bytes)
        for f in P.rglob('*'):
            f.chmod(0o700 if f.is_dir() or f.stat().st_mode&0o111 else 0o600)
        pe.terminal_package_preflight(P,package)
        exact_tree(P/'relay',package['RelayPins'])
        if pe.relay_package_preflight(P,D/'play/game_session/_runtime',R/'tests/fixtures/ProductionMain/live-mortal-phase.py')[0]!=package:raise ValueError('Copied phase package changed')
    if self_save_load:
        write_profile_artifact(P/'action.txt',a.encode(),0o600)
        write_profile_artifact(P/'console-route.json',(json.dumps(route)+'\n').encode(),0o600)
        (P/'launch-env').mkdir(mode=0o700)
    else:
        assert not(P/'action.txt').exists();(P/'action.txt').write_bytes(a.encode());(P/'console-route.json').write_text(json.dumps(route)+'\n')
    for n in ['config','data','state','cache','tmp','home']:(P/'launch-env'/n).mkdir(parents=not self_save_load,mode=0o700)
    failure=uuid.uuid4().hex;stop=uuid.uuid4().hex
    profile={'Route':route['Route'],'ExpectedTurn':continuation_spec(phase,previous)[2] if previous_path else 42,'PreviousResult':str(previous_path) if previous_path else None,'PreviousSHA256':h(previous_path) if previous_path else None,'GMDecisionDeferred':bool(previous_path),'SeedSHA256':h(D/'seed-manifest.json'),'FailureNonce':failure,'StopNonce':stop,'Budgets':{'provider':600,'work':660,'clientCleanup':680,'driver':720,'guardian':750},'Driver':str(R/'tests/fixtures/ProductionMain/live-mortal-phase.py'),'ReceiptHelper':str(D/'receipt-helper/BookOfEternityClient.TestSupport.dll'),'ActionSHA256':h(P/'action.txt'),'RuntimeCount':len(inv(D/'play/game_session/_runtime')),'HelperCount':len(inv(D/'receipt-helper')),'PrivateDirectoriesCreated':phase+'/launch-env/*; driver creates own phase config,data,state,cache,tmp exactly once','OperationalConfigWritten':False}
    load_nonce=uuid.uuid4().hex if self_save_load else None
    if self_save_load:
        profile['GMDecisionDeferred']=True
        profile['LoadObservationNonce']=load_nonce
        profile['RuntimeCount']=len(inv(D/'package'))
    profile,profile_sha=write_physical_launch_profile(D/'play',P,D/('launch-'+phase+'.sh'),profile)
    script=render_launch_script(D,phase,previous_path,failure,stop,profile_sha,load_nonce=load_nonce)
    write_profile_artifact(D/('launch-'+phase+'.sh'),script.encode('utf-8'),0o700)
    regular_tree(D/'play')
    assert inv(D/'play')==before
    if binding_bytes is not None:
        exact_tree(prior/'relay',package['RelayPins']);exact_tree(P/'relay',package['RelayPins'])
        pe.terminal_package_preflight(prior,package);pe.terminal_package_preflight(P,package)
        regular_tree(D/'receipt-helper');pe.receipt_package_proved(D/'receipt-helper/BookOfEternityClient.TestSupport.dll',package['ReceiptHelperPins'])
        assert binding_path.read_bytes()==binding_bytes
        assert binding['SourcePins']=={str(x.relative_to(R)):h(x) for x in required_sources}
        if phase=='p03':require_previous_manual_save(previous,D/'play/game_session',continuation_spec(phase)[1],pe)
        assert h(previous_path)==binding['PreviousSHA256'] and h(prior/'guardian.json')==binding['GuardianSHA256']
    j={'Kind':'inert-launch-profile','NoExecution':True,'WorldBefore':before,'WorldAfterExact':True,'SourcePins':{str(p.relative_to(R)):h(p) for p in [R/'tests/fixtures/ProductionMain/live-mortal-phase.py',R/'tests/fixtures/ProductionMain/relay_phase_evidence.py',R/'tests/fixtures/ProductionMain/mortal_console_routes.py']},'ProfileSHA256':h(P/'launch-profile.json'),'LaunchSHA256':h(D/('launch-'+phase+'.sh')),'ActionSHA256':h(P/'action.txt'),'PhaseInventory':inv(P)}
    write_profile_artifact(D/('launch-preparation.json' if phase=='p01' else 'launch-preparation-'+phase+'.json'),(json.dumps(j,indent=2)+'\n').encode(),0o600);print(json.dumps({k:v for k,v in j.items() if k.endswith('SHA256')},indent=2));print('LaunchPreparationSHA',h(D/('launch-preparation.json' if phase=='p01' else 'launch-preparation-'+phase+'.json')))

if __name__=='__main__':main()
