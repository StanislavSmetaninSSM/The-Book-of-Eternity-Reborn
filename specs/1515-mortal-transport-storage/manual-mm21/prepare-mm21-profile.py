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

def checked_continuation_binding(value):
    if type(value) is not dict or set(value)!={'Phase','PreviousSHA256','GuardianSHA256','SeedSHA256','PhasePackageSHA256','SourcePins','Action','ConsoleRoute'}:
        raise ValueError('Exact continuation binding required')
    if value['Phase']!='p02':raise ValueError('Only reviewed p02 continuation')
    for key in ('PreviousSHA256','GuardianSHA256','SeedSHA256','PhasePackageSHA256'):
        if type(value[key]) is not str or re.fullmatch('[0-9a-f]{64}',value[key]) is None:raise ValueError('Invalid binding digest')
    action=value['Action']
    if type(action) is not str or not action.strip() or len(action.encode())>4096 or any(ord(c)<32 for c in action):raise ValueError('Invalid single action')
    route=value['ConsoleRoute']
    if type(route) is not dict or set(route)!={'Route','StorageId','ItemId'} or route['Route']!='permission-with-denial':raise ValueError('Exact permission route required')
    for key in ('StorageId','ItemId'):
        if type(route[key]) is not str or not route[key] or any(ord(c)<32 for c in route[key]):raise ValueError('Invalid target identity')
    return value

if len(sys.argv) not in (2,3):raise ValueError('Destination and optional reviewed continuation binding required')
D=pathlib.Path(sys.argv[1])
if not D.is_absolute() or D!=D.resolve() or any(x.is_symlink() for x in [D,*D.parents]) or D.parent!=pathlib.Path('/workspace/shared') or re.fullmatch(r'mm[0-9]{2}-[0-9]{8}',D.name) is None:raise ValueError('Invalid prepared destination')
prepared=json.loads(plain_path(D/'preparation-inputs.json').read_text())
regular_tree(D/'play')
assert prepared['destination']==str(D) and prepared['owningBuildManifestSHA256']==os.environ['MM_REVIEWED_INPUT_SHA256']
phase='p02' if len(sys.argv)==3 else 'p01'
P=D/phase;R=pathlib.Path('/workspace/shared/mortal-isolated-20261119');h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();inv=lambda root:{str(p.relative_to(root)):h(p) for p in root.rglob('*') if p.is_file()};before=inv(D/'play');os.umask(0o077)
a='Выхожу из покоев, ищу представителя совета и прошу проводить меня к его хозяйственному двору, показать припаркованную грузовую повозку и закрытую кладовую с разметочным мелом, объяснить владельца и порядок выдачи. Ничего не беру и не перемещаю'
previous_path=None;route={'Route':'birth'};binding_bytes=None
if phase=='p02':
    binding_path=pathlib.Path(sys.argv[2])
    if not binding_path.is_absolute() or binding_path!=binding_path.resolve() or any(x.is_symlink() for x in [binding_path,*binding_path.parents]):raise ValueError('Aliased continuation binding')
    binding_bytes=plain_path(binding_path).read_bytes()
    if hashlib.sha256(binding_bytes).hexdigest()!=os.environ['MM_REVIEWED_CONTINUATION_SHA256']:raise ValueError('Continuation binding changed')
    binding=checked_continuation_binding(json.loads(binding_bytes))
    required_sources=[pathlib.Path(__file__),pathlib.Path(__file__).with_name('submit-mm21-once.py'),R/'tests/fixtures/ProductionMain/live-mortal-phase.py',R/'tests/fixtures/ProductionMain/relay_phase_evidence.py',R/'tests/fixtures/ProductionMain/mortal_console_routes.py']
    for f in required_sources:plain_path(f)
    if binding['SourcePins']!={str(x.relative_to(R)):h(x) for x in required_sources}:raise ValueError('Continuation sources changed before import')
    prior=plain_path(D/'p01',True);previous_path=prior/'probe-result.json'
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
    if previous['BasePath']!=str(D/'play') or previous['SeedSessionId']!=seed['sessionId'] or previous['AcceptedTurnNumber']!=42 or previous['SeedManifestSha256']!=binding['SeedSHA256'] or previous['PhasePackage']!=package:raise ValueError('Predecessor identity/package changed')
    pe.retained_bytes_proved(D/'play/game_session',previous);pe.receipt_package_proved(D/'receipt-helper/BookOfEternityClient.TestSupport.dll',package['ReceiptHelperPins'])
    if pe.strict_settings(plain_path(D/'play/.boe_runtime/gm-runs/main.json').read_bytes())!=previous['StoppedRecord']:raise ValueError('Stopped original owner changed')
    for name,f in [('Status',D/'play/game_session/game_state/control/gm_daemon_status.json'),('Guard',D/'play/.boe_runtime/locks/gm-daemon-instance.lock')]:
        if plain_path(f).read_bytes().hex()!=previous['DaemonSettlement']['Final'+name+'RawHex']:raise ValueError('Settled daemon evidence changed')
    route=routes.checked_route(binding['ConsoleRoute']);a=binding['Action']
    routes.storage_target(pe.strict_settings((D/'play/game_session/game_state/world/current_location.json').read_bytes()),route['StorageId'],route['ItemId'],False)
    for name in ('input/turn_request.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json','game_state/control/pending_turn_snapshot','ready/turn_complete.json','ready/turn_error.json'):
        f=D/'play/game_session'/name
        if f.exists() or f.is_symlink():raise ValueError('Pending continuation: '+name)
    if P.exists() or P.is_symlink() or (D/'launch-p02.sh').exists() or (D/'launch-p02.sh').is_symlink():raise ValueError('Continuation destination occupied')
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
assert not(P/'action.txt').exists();(P/'action.txt').write_bytes(a.encode());(P/'console-route.json').write_text(json.dumps(route)+'\n')
for n in ['config','data','state','cache','tmp','home']:(P/'launch-env'/n).mkdir(parents=True,mode=0o700)
failure=uuid.uuid4().hex;stop=uuid.uuid4().hex
profile={'Route':route['Route'],'ExpectedTurn':43 if previous_path else 42,'PreviousResult':str(previous_path) if previous_path else None,'PreviousSHA256':h(previous_path) if previous_path else None,'GMDecisionDeferred':bool(previous_path),'SeedSHA256':h(D/'seed-manifest.json'),'FailureNonce':failure,'StopNonce':stop,'Budgets':{'provider':600,'work':660,'clientCleanup':680,'driver':720,'guardian':750},'Driver':str(R/'tests/fixtures/ProductionMain/live-mortal-phase.py'),'ReceiptHelper':str(D/'receipt-helper/BookOfEternityClient.TestSupport.dll'),'ActionSHA256':h(P/'action.txt'),'RuntimeCount':len(inv(D/'play/game_session/_runtime')),'HelperCount':len(inv(D/'receipt-helper')),'PrivateDirectoriesCreated':phase+'/launch-env/*; driver creates own phase config,data,state,cache,tmp exactly once','OperationalConfigWritten':False}
(P/'launch-profile.json').write_text(json.dumps(profile,indent=2,ensure_ascii=False)+'\n')
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
'''.replace('PREPARED_DESTINATION',str(D)).replace('PREPARED_PHASE',phase).replace('PREPARED_PREVIOUS','\"$ROOT/p01/probe-result.json\"' if previous_path else '').replace('=FAILURE_NONCE','= '+failure).replace('=STOP_NONCE','= '+stop).replace('= ','=')
(D/('launch-'+phase+'.sh')).write_text(script);(D/('launch-'+phase+'.sh')).chmod(0o700)
regular_tree(D/'play')
assert inv(D/'play')==before
if binding_bytes is not None:
    exact_tree(prior/'relay',package['RelayPins']);exact_tree(P/'relay',package['RelayPins'])
    pe.terminal_package_preflight(prior,package);pe.terminal_package_preflight(P,package)
    regular_tree(D/'receipt-helper');pe.receipt_package_proved(D/'receipt-helper/BookOfEternityClient.TestSupport.dll',package['ReceiptHelperPins'])
    assert binding_path.read_bytes()==binding_bytes
    assert binding['SourcePins']=={str(x.relative_to(R)):h(x) for x in required_sources}
    assert h(previous_path)==binding['PreviousSHA256'] and h(prior/'guardian.json')==binding['GuardianSHA256']
j={'Kind':'inert-launch-profile','NoExecution':True,'WorldBefore':before,'WorldAfterExact':True,'SourcePins':{str(p.relative_to(R)):h(p) for p in [R/'tests/fixtures/ProductionMain/live-mortal-phase.py',R/'tests/fixtures/ProductionMain/relay_phase_evidence.py',R/'tests/fixtures/ProductionMain/mortal_console_routes.py']},'ProfileSHA256':h(P/'launch-profile.json'),'LaunchSHA256':h(D/('launch-'+phase+'.sh')),'ActionSHA256':h(P/'action.txt'),'PhaseInventory':inv(P)}
(D/('launch-preparation.json' if phase=='p01' else 'launch-preparation-p02.json')).write_text(json.dumps(j,indent=2)+'\n');print(json.dumps({k:v for k,v in j.items() if k.endswith('SHA256')},indent=2));print('LaunchPreparationSHA',h(D/('launch-preparation.json' if phase=='p01' else 'launch-preparation-p02.json')))
