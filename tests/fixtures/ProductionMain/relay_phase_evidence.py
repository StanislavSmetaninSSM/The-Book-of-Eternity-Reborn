"""Stateless phase evidence DATA. Never grants admission or owns a process/lease.

Immutable inventories are passed explicitly per phase; mutable world and terminal
proofs are freshly checked at boundaries. No cross-phase cache.
"""
import hashlib, json, re, stat, sys
from pathlib import Path

def parse_observed_diagnostic_trace_row(filename, raw, nonce, root, pids, owner):
    """Strict observed artifact DATA, never authority. Caller checks private files and
    groups physical occurrences separately; missing whole events remain unknowable."""
    def require(condition):
        if not condition:raise ValueError('Invalid observed diagnostic artifact')
    def unique(pairs):
        result={};seen=set()
        for key,value in pairs:
            require(key.casefold() not in seen);seen.add(key.casefold());result[key]=value
        return result
    require(type(raw) is bytes and raw.endswith(b'\n') and raw.count(b'\n')==1)
    prefixes={'physical':b'BOE_CANONICAL_LEASE_OBSERVATION ','marker':b'BOE_MAIN_OPERATION_MARKER '}
    kinds=[kind for kind,prefix in prefixes.items() if raw.startswith(prefix)]
    require(len(kinds)==1);kind=kinds[0]
    try:row=json.loads(raw[len(prefixes[kind]):-1].decode('utf-8'),object_pairs_hook=unique)
    except (UnicodeError,json.JSONDecodeError) as error:raise ValueError('Malformed diagnostic JSON') from error
    require(type(row) is dict)
    common={'Nonce','Occurrence','Root','Stage','ProcessId','CaptureMonotonicTimestamp','OriginalPin'}
    require(set(row)==common|({'Purpose','Attempt','CapturedAtUtc'} if kind=='physical' else {'ObservedAtUtc','OriginalClose'}))
    require(row['Nonce']==nonce and row['Root']==root and re.fullmatch('[0-9a-f]{32}',nonce) is not None)
    require(type(row['ProcessId']) is int and row['ProcessId']>0 and (pids is None or row['ProcessId'] in pids))
    require(type(row['Occurrence']) is int and row['Occurrence']>0)
    require(type(row['CaptureMonotonicTimestamp']) is int and row['CaptureMonotonicTimestamp']>0)
    stages={'physical-opened','lease-granted','physical-dispose-completed','contention-exhausted'} if kind=='physical' else {'active-received','helper-before-ready','helper-ready-written','canonical-current-generation-lease-acquired'}
    require(type(row['Stage']) is str and row['Stage'] in stages)
    require(filename==str(row['ProcessId'])+('-marker-' if kind=='marker' else '-')+str(row['Occurrence'])+'-'+row['Stage']+'.json')
    stamp=row['CapturedAtUtc' if kind=='physical' else 'ObservedAtUtc']
    require(type(stamp) is str and re.fullmatch(r'\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?(?:Z|\+00:00)',stamp) is not None)
    def tuple_fields(value):
        require(type(value) is dict)
        identity=value.get('Identity')
        require(type(identity) is dict and set(identity)=={'RootKey','RunId','GenerationId','Epoch','Backend','HostInstanceId','BootId'})
        require(type(identity['Epoch']) is int and identity['Epoch']>0 and type(identity['Backend']) is int)
        require(all(type(identity[key]) is str for key in ('RootKey','RunId','GenerationId','HostInstanceId','BootId')))
        require(identity==owner)
        for key in ('PinId','CloseId','OperationId'):
            require(type(value.get(key)) is str and re.fullmatch('[0-9a-f]{32}',value[key]) is not None)
    pin=row['OriginalPin']
    if pin is not None:
        tuple_fields(pin)
        require(set(pin)=={'Ok','State','PinId','CloseId','OperationId','Identity','Error'})
        require(pin['Ok'] is True and type(pin['State']) is int and pin['State']==1 and pin['Error'] is None)
    if kind=='physical':
        require(type(row['Purpose']) is str and bool(row['Purpose'].strip()))
        require(type(row['Attempt']) is int and row['Attempt']>0)
    else:
        close=row['OriginalClose'];stage=row['Stage']
        if stage=='active-received':require(pin is not None and close is None)
        elif stage=='canonical-current-generation-lease-acquired':require(pin is None and close is None)
        else:
            require(pin is None);tuple_fields(close)
            require(set(close)=={'PinId','CloseId','OperationId','Identity','Outcome','ClosingFailed'})
            require(type(close['Outcome']) is int and close['Outcome']==0 and close['ClosingFailed'] is False)
    return kind,row

def summarize_observed_diagnostic_trace(directory, nonce, root, owner, client_pid):
    """Settle observed DATA only; physical-only PIDs are not OS-owner inventory."""
    def require(value):
        if not value:raise ValueError('Unsettled observed diagnostic trace')
    directory=_restored_plain_path(Path(directory),True)
    require(stat.S_IMODE(directory.stat().st_mode)==0o700)
    require(type(client_pid) is int and client_pid>0)
    files={};physical=[];marker_files=[];bytecount=0
    marker=_restored_plain_path(directory/'owner-nonce')
    require(stat.S_IMODE(marker.stat().st_mode)==0o600 and marker.read_bytes()==nonce.encode('ascii'))
    for path in sorted(directory.iterdir()):
        path=_restored_plain_path(path);require(stat.S_IMODE(path.stat().st_mode)==0o600)
        raw=path.read_bytes();files[path.name]=sha(raw)
        if path.name=='owner-nonce':continue
        bytecount+=len(raw)
        if raw.startswith(b'BOE_MAIN_OPERATION_MARKER '):marker_files.append((path.name,raw));continue
        kind,row=parse_observed_diagnostic_trace_row(path.name,raw,nonce,root,None,owner)
        require(kind=='physical');physical.append(row)
    groups={}
    for row in physical:groups.setdefault((row['ProcessId'],row['Occurrence']),[]).append(row)
    require(bool(groups))
    by_pin={}
    def pin_key(pin):return tuple(pin[k] for k in ('PinId','CloseId','OperationId'))
    for group in groups.values():
        group.sort(key=lambda r:r['CaptureMonotonicTimestamp'])
        require([r['Stage'] for r in group]==['physical-opened','lease-granted','physical-dispose-completed'])
        base=group[0]
        require(all({k:v for k,v in r.items() if k not in ('Stage','CapturedAtUtc','CaptureMonotonicTimestamp')}==
                    {k:v for k,v in base.items() if k not in ('Stage','CapturedAtUtc','CaptureMonotonicTimestamp')} for r in group))
        require(all(group[i]['CaptureMonotonicTimestamp']<=group[i+1]['CaptureMonotonicTimestamp'] for i in range(2)))
        if base['OriginalPin'] is not None:by_pin.setdefault((base['ProcessId'],pin_key(base['OriginalPin'])),[]).append(group)
    allowed={r['ProcessId'] for r in physical}|{client_pid};markers=[]
    for filename,raw in marker_files:
        kind,row=parse_observed_diagnostic_trace_row(filename,raw,nonce,root,allowed,owner)
        require(kind=='marker');markers.append(row)
    require(len({(r['ProcessId'],r['Occurrence']) for r in markers})==len(markers))
    per_pid={}
    for row in markers:per_pid.setdefault(row['ProcessId'],[]).append(row)
    foreground=0;helpers=0;other_active=0
    for pid,rows in per_pid.items():
        rows.sort(key=lambda r:r['CaptureMonotonicTimestamp']);chunks=[]
        for row in rows:
            if row['Stage']=='active-received':chunks.append([row])
            else:
                require(bool(chunks));chunks[-1].append(row)
        for chunk in chunks:
            active=chunk[0];key=(pid,pin_key(active['OriginalPin']));require(key in by_pin)
            leases=by_pin[key];require(all(active['CaptureMonotonicTimestamp']<=g[0]['CaptureMonotonicTimestamp'] for g in leases))
            stages=[r['Stage'] for r in chunk]
            if stages==['active-received']:
                if pid==client_pid:
                    purposes={g[0]['Purpose'] for g in leases}
                    if {'SessionMutation','SessionFinalization'}<=purposes:foreground+=1
                else:other_active+=1
            else:
                require(stages==['active-received','canonical-current-generation-lease-acquired','helper-before-ready','helper-ready-written'])
                require(all(pin_key(r['OriginalClose'])==key[1] for r in chunk[2:]))
                canonical,before,ready=[r['CaptureMonotonicTimestamp'] for r in chunk[1:]]
                require(any(g[1]['CaptureMonotonicTimestamp']<=canonical<=g[2]['CaptureMonotonicTimestamp']<=before for g in leases))
                require(before<ready or (before==ready and chunk[2]['Occurrence']<chunk[3]['Occurrence']))
                helpers+=1
    require(foreground>0)
    return {'PhysicalEvents':len(physical),'PhysicalTriples':len(groups),'HealthyMarkers':len(markers),
        'HelperReadySequences':helpers,'OtherJoinedActiveObservations':other_active,'ForegroundActiveWithMutationAndFinalization':foreground,
        'IndependentForegroundPid':client_pid,'ObservedPhysicalPids':sorted({r['ProcessId'] for r in physical}),
        'ObservedBytes':bytecount,'Files':files,'ExhaustiveCoverage':False,
        'Limits':'Physical-only PIDs are observed DATA, not an independently owned OS inventory. Foreground tuple joins do not identify a specific Save. No timing neutrality or exhaustive observation claim.'}

def load_physical_trace_profile(out, env):
    """Read only the launcher's independently pinned profile; no asset creation."""
    expected=env.get('BOE_TEST_PHYSICAL_TRACE_PROFILE_SHA256')
    if type(expected) is not str or re.fullmatch('[0-9a-f]{64}',expected) is None:
        raise ValueError('Physical trace profile hash required')
    path=_restored_plain_path(Path(out)/'launch-profile.json')
    raw=path.read_bytes()
    if sha(raw)!=expected:raise ValueError('Physical trace profile changed')
    return strict_settings(raw)

def require_physical_trace_admission(base, out, profile, env):
    """Fresh diagnostic DATA checks, never ownership or canonical admission."""
    if sys.platform!='linux':raise ValueError('External physical trace is Linux-qualified only')
    if type(profile) is not dict or type(profile.get('PhysicalTrace')) is not dict:
        raise ValueError('Physical trace profile required')
    nonce=profile.get('StopNonce');trace=profile['PhysicalTrace']
    if type(nonce) is not str or re.fullmatch('[0-9a-f]{32}',nonce) is None:
        raise ValueError('Physical trace StopNonce required')
    if set(trace)!={'Route','Directory'} or trace.get('Route')!='external':
        raise ValueError('Physical trace external route required')
    raw=trace.get('Directory')
    if type(raw) is not str or not raw or not Path(raw).is_absolute() or str(Path(raw))!=raw or '..' in Path(raw).parts:
        raise ValueError('Physical trace canonical path required')
    base=_restored_plain_path(base,True);out=_restored_plain_path(out,True);directory=Path(raw)
    if directory!=out/'physical-trace' or directory==base or base in directory.parents or directory in base.parents:
        raise ValueError('Physical trace must be outside canonical base/play')
    if (env.get('BOE_TEST_CANONICAL_LEASE_TRACE_ROUTE')!='external'
        or env.get('BOE_TEST_CANONICAL_LEASE_TRACE_DIR')!=raw
        or env.get('BOE_MAIN_STOP_DIAGNOSTIC_NONCE')!=nonce):
        raise ValueError('Physical trace environment differs from pinned profile')
    directory=_restored_plain_path(directory,True)
    if stat.S_IMODE(directory.stat().st_mode)!=0o700:raise ValueError('Physical trace directory must be private')
    marker=_restored_plain_path(directory/'owner-nonce')
    if stat.S_IMODE(marker.stat().st_mode)!=0o600 or marker.stat().st_size!=32:
        raise ValueError('Physical trace marker must be private exact32bytes')
    if marker.read_bytes()!=nonce.encode('ascii'):raise ValueError('Physical trace marker differs')
    return {'Route':'external','Directory':raw,'StopNonce':nonce}

def rpc_helper_closed(result):
    proof=result.get('RpcHelperClosure')
    return isinstance(proof,dict) and type(proof.get('ExitCode')) is int and proof['ExitCode']==0 and proof.get('StdoutEof') is True and proof.get('StderrEof') is True and 'RpcHelperCleanupFailure' not in result

def daemon_consumer_returned(raw, expected):
    """Complete fresh diagnostic DATA only; caller supplies all independent binding."""
    def unique(pairs):
        value={};seen=set()
        for key,item in pairs:
            if key.casefold() in seen:raise ValueError('Duplicate observation field')
            seen.add(key.casefold());value[key]=item
        return value
    try:
        prefix=b'BOE_MAIN_OPERATION_PS ';events=[]
        if raw and not raw.endswith(b'\n'):return None
        for line in raw.splitlines(keepends=True):
            if not line.startswith(prefix):continue
            if not line.endswith(b'\n'):return None
            e=json.loads(line[len(prefix):].decode('utf-8'),object_pairs_hook=unique,
                parse_constant=lambda _:(_ for _ in ()).throw(ValueError('Non-JSON value')))
            if type(e) is not dict:return None
            if e.get('stage')=='daemon-turn-consumer-returned':events.append(e)
        if len(events)!=1:return None
        e=events[0]
        for k in ('nonce','processId','daemonGuardOwnerId','turnIdentity'):
            if e.get(k)!=expected[k]:return None
        if type(e['processId']) is not int or e['processId']<=0:return None
        for k in ('nonce','daemonGuardOwnerId'):
            if type(e[k]) is not str or re.fullmatch('[0-9a-f]{32}',e[k]) is None:return None
        if type(e.get('requestSHA256')) is not str or re.fullmatch('[0-9a-fA-F]{64}',e['requestSHA256']) is None:return None
        if e['requestSHA256'].lower()!=expected['requestSHA256'].lower():return None
        if type(e['turnIdentity']) is not dict or set(e['turnIdentity'])!={'sessionId','requestId','turnNumber'}:return None
        if type(e['turnIdentity']['turnNumber']) is not int:return None
        for k in ('requestAdmitted','consumerReturned','closeObserved','replyReadJoined','stderrReadJoined'):
            if e.get(k) is not True:return None
        for k in ('lost','publicationUncertain','handledError'):
            if e.get(k) is not False:return None
        if type(e.get('helperExitCode')) is not int or e['helperExitCode']!=0:return None
        if any(type(e.get(k)) is not int for k in ('errorCountBefore','errorCountAfter')):return None
        if e['errorCountBefore']<0 or e['errorCountBefore']!=e['errorCountAfter']:return None
        a=e['originalClose'];b=e['terminalClose']
        for close in (a,b):
            if type(close) is not dict or set(close)!={'identity','pinId','closeId','operationId','outcome','closingFailed'}:return None
            if type(close['outcome']) is not int or close['outcome']!=0 or close['closingFailed'] is not False:return None
            identity=close['identity']
            if type(identity) is not dict or set(identity)!={'rootKey','runId','generationId','epoch','backend','hostInstanceId','bootId'}:return None
            if any(type(identity[k]) is not int for k in ('epoch','backend')):return None
            if any(type(identity[k]) is not str for k in ('rootKey','runId','generationId','hostInstanceId','bootId')):return None
            if identity!=expected['mainIdentity']:return None
            for k in ('pinId','closeId','operationId'):
                if type(close[k]) is not str or re.fullmatch('[0-9a-f]{32}',close[k]) is None:return None
        if a!=b:return None
        return e
    except (KeyError,TypeError,ValueError,UnicodeError):return None


def daemon_pair_decode(evidence, prefix, settled):
    def unique(pairs):
        value={};seen=set()
        for key,item in pairs:
            if key.casefold() in seen:raise ValueError('Duplicate daemon JSON field')
            seen.add(key.casefold());value[key]=item
        return value
    def decode(name):
        key=prefix+name
        raw=bytes.fromhex(evidence[key+'RawHex'])
        if hashlib.sha256(raw).hexdigest()!=evidence[key+'SHA256']:raise ValueError('Daemon evidence hash mismatch')
        result=json.loads(raw.decode('utf-8-sig'),object_pairs_hook=unique,
            parse_constant=lambda value:(_ for _ in ()).throw(ValueError('Non-JSON constant')))
        if type(result) is not dict:raise ValueError('Daemon evidence is not an object')
        return result
    status=decode('Status');guard=decode('Guard');session=evidence['SessionPath']
    if type(session) is not str or not session:raise ValueError('Missing exact daemon session')
    owner=status.get('daemonGuardOwnerId')
    if type(owner) is not str or re.fullmatch('[0-9a-f]{32}',owner) is None:raise ValueError('Invalid daemon owner')
    if (type(status.get('daemonGuardVersion')) is not int or status['daemonGuardVersion']!=1 or
        type(guard.get('version')) is not int or guard['version']!=1):raise ValueError('Invalid daemon version')
    if set(guard)!={'version','sessionPath','ownerId','settled'}:raise ValueError('Invalid guard shape')
    if status.get('sessionPath')!=session or guard.get('sessionPath')!=session or guard.get('ownerId')!=owner:
        raise ValueError('Daemon owner/session mismatch')
    if (status.get('status')!=('stopped' if settled else 'running') or
        status.get('terminalSettled') is not settled or guard.get('settled') is not settled):
        raise ValueError('Daemon lifecycle is not exact')
    return owner

def daemon_settlement_proved(evidence):
    try:
        if daemon_pair_decode(evidence,'Original',False)!=daemon_pair_decode(evidence,'Final',True):return False
        # Both raw status bytes were independently hash/schema/owner checked above.
        before=json.loads(bytes.fromhex(evidence['OriginalStatusRawHex']).decode('utf-8-sig'))['errorCount']
        after=json.loads(bytes.fromhex(evidence['FinalStatusRawHex']).decode('utf-8-sig'))['errorCount']
        return type(before) is int and type(after) is int and before>=0 and before==after
    except (KeyError,TypeError,ValueError,UnicodeError):return False

def relay_phase_cleanup_proved(value):
    try:
        if any(type(value[k]) is not int or value[k]!=1 for k in ('AcceptedGameTurns','GenuineActionsSent')):return False
        return value['InputQualified'] is True and relay_owned_cleanup_proved(value,value['OriginalRunningRecord'],value['OriginalRunningStatus'])
    except (KeyError,TypeError):return False

def relay_owned_cleanup_proved(value, running, started):
    """Mechanical closure of the explicitly supplied original main; no acceptance or adoption."""
    try:
        if any(k.endswith('Failure') for k in value):return False
        if not daemon_settlement_proved(value.get('DaemonSettlement')):return False
        if type(value['ShutdownAttempts']) is not int or value['ShutdownAttempts']!=1:return False
        if any(value[k] is not True for k in ('LogicalLifecycleVerified','RelayExecutionClosed')) or not rpc_helper_closed(value):return False
        terminals=value['Terminals']
        if set(terminals)!={'client','bridge','daemon'}:return False
        if not all(type(v['ExitCode']) is int and v['ExitCode']==0 and v['EOF'] is True and v['TermiosRestored'] is True and type(v['StreamEOF']) is dict and bool(v['StreamEOF']) and all(x is True for x in v['StreamEOF'].values()) for v in terminals.values()):return False
        stopped=value['StoppedRecord'];identity=running['Identity']
        if type(identity.get('Epoch')) is not int or identity['Epoch']<1:return False
        if running['Disposition']!='Running' or stopped['Disposition']!='Stopped' or stopped['Identity']!=identity:return False
        if stopped['StopEvidence']['Identity']!=identity or stopped['StopEvidence']['Kind']!='OwnedScopeEmpty':return False
        ack=value['ShutdownReceipt'];proof=ack['status']['terminalStop']
        return (ack['ok'] is True and started['ok'] is True and started['status']['terminalRunId']==identity['RunId'] and
            ack['status']['terminalRunId']==identity['RunId'] and ack['status']['inputBindingId']==started['status']['inputBindingId'] and
            proof['identity']['runId']==identity['RunId'] and proof['state']=='stopped-within-scope' and
            proof['cleanupComplete'] is True and proof['authorityRetained'] is False)
    except (KeyError,TypeError):return False

def sha(data):return hashlib.sha256(data).hexdigest()

def strict_settings(raw):
    def pairs(items):
        result={};seen=set()
        for key,value in items:
            if key.casefold() in seen:raise ValueError('Duplicate config key')
            seen.add(key.casefold());result[key]=value
        return result
    value=json.loads(raw,object_pairs_hook=pairs,parse_constant=lambda _:(_ for _ in ()).throw(ValueError('Non-JSON constant')))
    if not isinstance(value,dict):raise ValueError('Config must be object')
    return value

def put_setting(settings,key,value):
    existing=next((k for k in settings if k.casefold()==key.casefold()),key)
    settings[existing]=value

def relay_package_preflight(out,ship,driver):
    raw=(out/'relay-phase-package.json').read_bytes();manifest=strict_settings(raw)
    if manifest['RuntimePins']!=relay_inventory(ship) or not manifest['RuntimePins']:raise ValueError('Prepared runtime pins mismatch')
    if manifest['RelayPins']!=relay_inventory(out/'relay') or not manifest['RelayPins']:raise ValueError('Prepared relay pins mismatch')
    if manifest['DriverSHA256']!=sha(Path(driver).read_bytes()):raise ValueError('Prepared driver pin mismatch')
    if manifest.get('EvidenceModuleSHA256')!=sha(Path(__file__).read_bytes()):raise ValueError('Prepared evidence module pin mismatch')
    if manifest.get('ConsoleRouteModuleSHA256')!=sha(Path(__file__).with_name('mortal_console_routes.py').read_bytes()):raise ValueError('Prepared console route module pin mismatch')
    return manifest,sha(raw)

TERMINAL_SOURCE_FILES=frozenset({
    'SelectionPrompt/fullroute_inputs.py','ProductionMain/mortal_console_routes.py',
    'SelectionPrompt/terminal/client_session.py','SelectionPrompt/terminal/client_ui.py',
    'SelectionPrompt/terminal/owned_terminal.py','SelectionPrompt/terminal/pty_probe.py',
    'SelectionPrompt/terminal/sidecar.mjs','SelectionPrompt/terminal/policy.mjs',
    'SelectionPrompt/terminal/provenance.json'})
TERMINAL_NODE_SHA256='acbe4b8d9153b841ed0697cbfc4f93e767386620edb745ab9358306c0a6f31aa'

def terminal_package_preflight(out,manifest):
    """Exact fixed terminal package DATA. No import, execution, download or authority."""
    def inventory(root):
        if not root.is_dir() or any(p.is_symlink() for p in [root,*root.parents]):raise ValueError('Terminal root/link refused')
        files={};modes={};pending=[root]
        while pending:
            for p in pending.pop().iterdir():
                mode=p.lstat().st_mode
                if stat.S_ISDIR(mode):pending.append(p)
                elif stat.S_ISREG(mode):
                    key=str(p.relative_to(root));files[key]=sha(p.read_bytes());modes[key]=stat.S_IMODE(mode)
                else:raise ValueError('Terminal linked/special entry refused')
        return files,modes
    root=Path(out)/'terminal-fixture';prefix=Path(out)/'terminal-deps'
    sources,source_modes=inventory(root)
    if set(sources)!=TERMINAL_SOURCE_FILES or sources!=manifest.get('TerminalFixturePins') or source_modes!=manifest.get('TerminalFixtureModes'):
        raise ValueError('Terminal source inventory/hash/mode mismatch')
    if sources['ProductionMain/mortal_console_routes.py']!=manifest.get('ConsoleRouteModuleSHA256'):raise ValueError('Terminal and driver route module differ')
    provenance=strict_settings((root/'SelectionPrompt/terminal/provenance.json').read_bytes())
    if set(provenance)!={'@xterm/headless','@xterm/addon-unicode11'} or provenance['@xterm/headless']['version']!='6.0.0' or provenance['@xterm/addon-unicode11']['version']!='0.9.0':raise ValueError('Terminal exact releases mismatch')
    expected={name:digest for package in provenance.values() for name,digest in package['files'].items()}
    dependencies,dependency_modes=inventory(prefix)
    if len(expected)!=17 or dependencies!=expected or dependencies!=manifest.get('TerminalDependencyPins') or dependency_modes!=manifest.get('TerminalDependencyModes'):
        raise ValueError('Terminal dependency inventory/hash/mode mismatch')
    node=Path(manifest['TerminalNodePath'])
    if not node.is_absolute() or any(p.is_symlink() for p in [node,*node.parents]) or not stat.S_ISREG(node.stat().st_mode):raise ValueError('Terminal Node path refused')
    if manifest.get('TerminalNodeSHA256')!=TERMINAL_NODE_SHA256 or sha(node.read_bytes())!=TERMINAL_NODE_SHA256:raise ValueError('Terminal Node pin mismatch')
    return root,node,prefix

def relay_published_owner_join(record,status,pipe,previous):
    identity=record.get('Identity',{})
    run=identity.get('RunId')
    predecessor=previous['StoppedRecord']['Identity']['RunId'] if previous else None
    return (record.get('Disposition')=='Running' and isinstance(run,str) and bool(run) and run!=predecessor and
        status.get('terminalRunId')==run and status.get('pipeName')==pipe and
        isinstance(status.get('transportEndpoint'),dict) and bool(status['transportEndpoint']))

def relay_inventory(root):
    root=Path(root)
    return {str(p.relative_to(root)):sha(p.read_bytes()) for p in sorted(root.rglob('*')) if p.is_file() and '__pycache__' not in p.parts}

def relay_world_inventory(session):
    # Includes exact final status/control/story bytes. No cached authority or semantic reconstruction.
    result={}
    for name in ('game_state','stories','lore','input','output','ready'):
        for relative,digest in relay_inventory(Path(session)/name).items():result[name+'/'+relative]=digest
    return result

def relay_continuation_settings(raw,pipe,command,session):
    value=strict_settings(raw)
    for key,item in [('GmBridgePipeNameOverride',pipe),('GmCliLaunchCommand',command),('GmBridgeShellWorkingDirectory',session)]:put_setting(value,key,item)
    return value

def guardian_proved(value):
    return (type(value) is dict and all(type(value.get(k)) is int and value[k]==0 for k in ('driverExitCode','emergencySignals','failures')) and value.get('echild') is True and value.get('deadline') is False)

def require_fresh_owner(identity,previous):
    if type(identity.get('Epoch')) is not int or identity['Epoch']<1:raise ValueError('Invalid current owner epoch')
    if previous:
        old=previous['StoppedRecord']['Identity']
        if (type(old.get('Epoch')) is not int or identity['Epoch']<=old['Epoch'] or identity['RunId']==old['RunId'] or
            any(identity[k]!=old[k] for k in ('RootKey','GenerationId'))):raise ValueError('Retained phase did not get a fresh same-generation owner')

def retained_bytes_proved(session,previous):
    session=Path(session)
    if relay_inventory(session/'_runtime')!=previous['RuntimePins']:raise ValueError('Retained runtime changed')
    if relay_world_inventory(session)!=previous['FinalWorldPins']:raise ValueError('Retained world changed')
    if sha((session/'config.json').read_bytes())!=previous['FinalConfigSHA256']:raise ValueError('Retained config changed')

def receipt_package_proved(helper,pins):
    helper=Path(helper)
    required={helper.name,helper.stem+'.deps.json',helper.stem+'.runtimeconfig.json'}
    if type(pins) is not dict or not required.issubset(pins) or relay_inventory(helper.parent)!=pins:
        raise ValueError('Full receipt helper dependency package changed')

def execution_closed(value):
    return (type(value) is dict and set(value)=={'Executed','ExitCode','ChildExited','IoDrained'} and
        type(value['ExitCode']) is int and value['ExitCode']==0 and
        all(value[k] is True for k in ('Executed','ChildExited','IoDrained')))


def rejected_restored_for_cleanup(request, keys_before, keys_after, action_text,
                                  story_before, story_after, pending_paths,
                                  relay_observation):
    """Cleanup observation only, never acceptance or canonical restoration authority."""
    try:
        if type(request['turnNumber']) is not int or request['turnNumber'] < 0:return False
        if any(type(request[k]) is not str or not request[k] or '|' in request[k]
               for k in ('sessionId','requestId')):return False
        key=f"{request['sessionId']}|{request['requestId']}|{request['turnNumber']}"
        if (type(keys_before) is not list or type(keys_after) is not list or
            any(type(k) is not str for k in keys_before+keys_after) or
            len(set(keys_before))!=len(keys_before) or len(set(keys_after))!=len(keys_after) or
            key in keys_before or key not in keys_after):return False
        marker='↩ Изменения мира не были приняты; состояние до хода восстановлено.'
        matches=list(re.finditer(r'(?m)^'+re.escape(marker)+r'\r?$',action_text))
        if len(matches)!=1:return False
        end=matches[0].end();heading=action_text.rfind('Ваш ход');prompt=action_text.rfind('\n >  ')
        if heading<=end or prompt<=heading:return False
        if action_text.rfind('Мастер игры размышляет...')>end:return False
        if action_text.rfind('Нажмите любую клавишу для продолжения...')>prompt:return False
        if story_before!=story_after or pending_paths:return False
        identity={k:request[k] for k in ('sessionId','requestId','turnNumber')}
        if relay_observation['RequestIdentity']!=identity or relay_observation['ValidatedOriginalQueue'] is not True:return False
        executions=relay_observation['Executions']
        return bool(executions) and all(execution_closed(x) for x in executions)
    except (KeyError,TypeError,ValueError):return False


TERMINAL_LEDGER_PATH = 'game_state/control/gm_observed_terminal_requests.json'


def _terminal_ledger_keys(raw):
    value=strict_settings(raw)
    if (type(value.get('schemaVersion')) is not int or value['schemaVersion']!=1 or
        type(value.get('keys')) is not list or any(type(k) is not str for k in value['keys']) or
        len(set(value['keys']))!=len(value['keys'])):raise ValueError('Invalid terminal ledger DATA')
    return value['keys']


def capture_terminal_ledger_witness(session):
    # Exactly one pre-action source read. Permission/other I/O errors propagate.
    session=str(Path(session).resolve())
    try:raw=(Path(session)/TERMINAL_LEDGER_PATH).read_bytes()
    except FileNotFoundError:raw=None
    if raw is not None:_terminal_ledger_keys(raw)
    return json.dumps({'SessionPath':session,'RelativePath':TERMINAL_LEDGER_PATH,
        'Present':raw is not None,'RawHex':raw.hex() if raw is not None else None,
        'SHA256':sha(raw) if raw is not None else None},sort_keys=True).encode('utf-8')


def bind_terminal_ledger_witness(witness_bytes, request_bytes):
    if type(witness_bytes) is not bytes or type(request_bytes) is not bytes:raise ValueError('Missing immutable capture bytes')
    request=strict_settings(request_bytes)
    identity={k:request[k] for k in ('sessionId','requestId','turnNumber')}
    if (type(identity['turnNumber']) is not int or identity['turnNumber']<0 or
        any(type(identity[k]) is not str or not identity[k] or '|' in identity[k] for k in ('sessionId','requestId'))):raise ValueError('Invalid original request identity')
    return json.dumps({'WitnessSHA256':sha(witness_bytes),'RequestSHA256':sha(request_bytes),
                       'RequestIdentity':identity},sort_keys=True).encode('utf-8')


def terminal_ledger_witness_keys(witness_bytes, binding_bytes, session, request_bytes):
    # Detached DATA only. Original immutable bytes are retained by the driver;
    # the binding does not prove when an arbitrary caller constructed its data.
    try:
        if any(type(x) is not bytes for x in (witness_bytes,binding_bytes,request_bytes)):return None
        binding=strict_settings(binding_bytes)
        if binding!=strict_settings(bind_terminal_ledger_witness(witness_bytes,request_bytes)):return None
        value=strict_settings(witness_bytes)
        if set(value)!={'SessionPath','RelativePath','Present','RawHex','SHA256'}:return None
        if value['SessionPath']!=str(Path(session).resolve()) or value['RelativePath']!=TERMINAL_LEDGER_PATH:return None
        if value['Present'] is False:
            return [] if value['RawHex'] is None and value['SHA256'] is None else None
        if value['Present'] is not True or type(value['RawHex']) is not str:return None
        raw=bytes.fromhex(value['RawHex'])
        if sha(raw)!=value['SHA256']:return None
        return _terminal_ledger_keys(raw)
    except (ValueError,KeyError,TypeError,UnicodeError):return None

# T022 fixed reviewed gameplay DATA names, never runtime ownership.
RESTORED_MORTAL_DATA_PATHS = (
    'game_state/afterlife/afterlife_active_threats.json',
    'game_state/afterlife/afterlife_chronicles.json',
    'game_state/afterlife/afterlife_global_flags.json',
    'game_state/afterlife/afterlife_story_outline.json',
    'game_state/afterlife/entity_profiles/guardian_mirror.json',
    'game_state/afterlife/entity_profiles/shining_senator_mirel.json',
    'game_state/chaos_sea/guardian_politics.json',
    'game_state/control/pending_dice_state.json',
    'game_state/control/pending_ink_actions.json',
    'game_state/control/progression_schedule.json',
    'game_state/core/game_settings.json',
    'game_state/core/player_status.json',
    'game_state/core/system_mods.json',
    'game_state/factions/faction_chronicles.json',
    'game_state/factions/faction_core.json',
    'game_state/factions/faction_custom.json',
    'game_state/factions/faction_projects.json',
    'game_state/factions/faction_resources.json',
    'game_state/factions/faction_structure.json',
    'game_state/history/chat_log.json',
    'game_state/inventory/item_identity_index.json',
    'game_state/inventory/items.json',
    'game_state/meta/achievements.json',
    'game_state/meta/character_chronicle.json',
    'game_state/meta/guardians.json',
    'game_state/meta/soul_state.json',
    'game_state/misc/characteristics.json',
    'game_state/misc/storage_access.json',
    'game_state/npcs/item_journals.json',
    'game_state/npcs/npc_activities.json',
    'game_state/npcs/npc_core.json',
    'game_state/npcs/npc_journals.json',
    'game_state/player/computed_characteristics.json',
    'game_state/player/experience.json',
    'game_state/player/player_status.json',
    'game_state/player/stat_points.json',
    'game_state/player/transformation.json',
    'game_state/quests/quest_history.json',
    'game_state/resources/resource_definitions.json',
    'game_state/resources/resource_history.json',
    'game_state/resources/resource_owner_authority.json',
    'game_state/resources/resource_state.json',
    'game_state/shining_abode/faction_chronicles.json',
    'game_state/world/current_location.json',
    'game_state/world/location_identity_index.json',
    'game_state/world/mortal_living_world_history.json',
    'game_state/world/world_events.json',
    'game_state/world/world_map.json',
    'game_state/world/world_time.json',
    'lore/codex_entries.json',
    'lore/current_world/cultures.json',
    'lore/current_world/geography.json',
    'lore/current_world/history.json',
    'lore/current_world/threats.json',
    'lore/current_world/world_setting.json',
    'output/debug_logs.json',
    'output/interface_updates.json',
    'output/narrative_response.json',
    'stories/mortal_life_2.jsonl',
)

def _restored_plain_path(path, directory=False):
    path=Path(path)
    if not path.is_absolute() or path!=path.resolve():raise ValueError('Aliased diagnostic path')
    for part in (path,*path.parents):
        if part.is_symlink():raise ValueError('Linked diagnostic path')
    if directory:
        if not path.is_dir():raise ValueError('Missing diagnostic directory')
    elif not path.is_file() or path.stat().st_nlink!=1:raise ValueError('Not one owned regular DATA file')
    return path

def restored_mortal_diagnostic_admission(descriptor, source_probe_bytes, source_guardian_bytes,
        source_request_bytes, source_config_bytes, data_root, new_base, nonce):
    """Validate detached historical DATA only. Does not acquire or transfer authority."""
    try:
        d=descriptor
        if d.get('kind')!='restored-accepted-data-diagnostic' or d.get('DiagnosticOnly') is not True:return None
        if not re.fullmatch('[0-9a-f]{32}',nonce or ''):return None
        if d.get('RuntimeSource')!='58df9c846eb29606735c2908c38eaf1ceaa8b4ff':return None
        if type(d.get('AcceptedTurn')) is not int or d['AcceptedTurn']!=44 or type(d.get('ExpectedTurn')) is not int or d['ExpectedTurn']!=45:return None
        if not isinstance(d.get('ExcludedRequestId'),str) or not d['ExcludedRequestId']:return None
        if sha(source_probe_bytes)!=d['SourceProbeSHA256'] or sha(source_guardian_bytes)!=d['SourceGuardianSHA256']:return None
        source=strict_settings(source_probe_bytes);guardian=strict_settings(source_guardian_bytes)
        request=strict_settings(source_request_bytes);config=strict_settings(source_config_bytes)
        if type(config) is not dict or source.get('Success') is not True or source.get('RestoredAcceptedDataDiagnostic') is True:return None
        if not relay_phase_cleanup_proved(source) or not guardian_proved(guardian):return None
        old=Path(d['SourceBasePath']);base=_restored_plain_path(new_base,True);data=_restored_plain_path(data_root,True)
        if not old.is_absolute() or old!=old.resolve() or str(base)!=d['BasePath']:return None
        for left,right in [(base,old),(data,old),(base,data)]:
            if left==right or left in right.parents or right in left.parents:return None
        if base.stat().st_mode&0o777!=0o700 or any(base.iterdir()):return None
        identity=source['OriginalRunningRecord']['Identity']
        if identity['RootKey']!=str(old) or source['StoppedRecord']['Identity']!=identity or source['BasePath']!=str(old):return None
        if source['DaemonSettlement']['SessionPath']!=str(old/'game_session'):return None
        if source['SeedSessionId']!=d['SourceSessionId'] or source['ActualRequest']!=request or sha(source_request_bytes)!=source['ActualRequestSHA256']:return None
        if request.get('sessionId')!=d['SourceSessionId'] or type(request.get('turnNumber')) is not int or request['turnNumber']!=44:return None
        if not isinstance(request.get('requestId'),str) or not request['requestId'] or request['requestId']==d['ExcludedRequestId']:return None
        if sha(source_config_bytes)!=source['FinalConfigSHA256']:return None
        pins=d['DataPins'];full=source['FinalWorldPins']
        if set(pins)!=set(RESTORED_MORTAL_DATA_PATHS) or len(full)!=106:return None
        # The source includes historical operational evidence, but none is imported.
        if not set(pins)<set(full):return None
        for name in set(full)-set(pins):
            if not name.startswith('game_state/control/') or '..' in Path(name).parts:return None
        if any(not isinstance(v,str) or not re.fullmatch('[0-9a-f]{64}',v) for v in full.values()):return None
        actual={}
        for path in data.rglob('*'):
            if path.is_symlink():return None
            if path.is_dir():continue
            path=_restored_plain_path(path);actual[str(path.relative_to(data))]=path.read_bytes()
        if set(actual)!=set(pins):return None
        if any(sha(value)!=pins[name] or pins[name]!=full[name] for name,value in actual.items()):return None
        storyname='stories/mortal_life_2.jsonl';historyname='game_state/world/mortal_living_world_history.json'
        if source['StoryPath']!=str(old/'game_session'/storyname) or sha(actual[storyname])!=source['StoryAfterSHA256']:return None
        rows=[strict_settings(line) for line in actual[storyname].decode('utf-8-sig').splitlines() if line.strip()]
        if [x.get('turn') for x in rows]!=[41,42,43,44] or rows[-1]!=source['AcceptedStory']:return None
        if rows[-1].get('realm')!='Mortal World' or rows[-1].get('player')!=request.get('playerAction') or not rows[-1].get('narrative'):return None
        if source['AcceptedTurnNumber']!=44 or sha(actual[historyname])!=source['LivingWorldHistorySHA256']:return None
        history=strict_settings(actual[historyname]);entries=history['entries'];receipt=source['TypedReceiptEvidence']
        if len(entries)!=3 or [x.get('turn') for x in entries]!=[42,43,44]:return None
        if any(x.get('sessionId')!=d['SourceSessionId'] for x in entries):return None
        if receipt.get('valid') is not True or receipt.get('publicationAuthority') is not False:return None
        if type(receipt.get('priorCount')) is not int or receipt['priorCount']!=2 or type(receipt.get('currentCount')) is not int or receipt['currentCount']!=3:return None
        for key,value in [('sessionId',request['sessionId']),('requestId',request['requestId']),('turn',44)]:
            if receipt.get(key)!=value or entries[-1].get(key)!=value:return None
        if not re.fullmatch('[0-9A-F]{64}',receipt.get('snapshotToken','')):return None
        for key in ['snapshotToken','declarationFingerprint','receiptFingerprint']:
            if entries[-1].get(key)!=receipt.get(key):return None
            if key!='snapshotToken' and not re.fullmatch('sha256:[0-9a-f]{64}',receipt.get(key,'')):return None
        clock=strict_settings(actual['game_state/world/world_time.json']);schedule=strict_settings(actual['game_state/control/progression_schedule.json'])
        if type(clock.get('currentTimeInMinutes')) is not int or clock['currentTimeInMinutes']!=1440 or entries[-1].get('toMinute')!=1440:return None
        if schedule.get('currentRealm')!='Mortal World' or schedule.get('currentWorldTimeInMinutes')!=1440 or schedule.get('hasAuthoritativeWorldTimeBaseline') is not True:return None
        chat=strict_settings(actual['game_state/history/chat_log.json'])
        if chat.get('sessionId')!=d['SourceSessionId']:return None
        dice=strict_settings(actual['game_state/control/pending_dice_state.json']);ink=strict_settings(actual['game_state/control/pending_ink_actions.json'])
        if dice.get('isFateLocked') is not False or type(dice.get('preGeneratedDices1d20')) is not list or len(dice['preGeneratedDices1d20'])!=20:return None
        if any(type(x) is not int or not 1<=x<=20 for x in dice['preGeneratedDices1d20']):return None
        if type(dice.get('gachaBaseResult')) is not dict or ink!={'pendingActions':[]}:return None
        return 45
    except (OSError,ValueError,KeyError,TypeError,AttributeError):return None

def restored_diagnostic_request_proved(descriptor, request_bytes):
    try:
        request=strict_settings(request_bytes)
        return (descriptor.get('kind')=='restored-accepted-data-diagnostic' and descriptor.get('DiagnosticOnly') is True and
            request.get('sessionId')==descriptor['SourceSessionId'] and type(request.get('turnNumber')) is int and
            request['turnNumber']==descriptor['ExpectedTurn']==45 and isinstance(request.get('requestId'),str) and
            bool(request['requestId']) and request['requestId']!=descriptor['ExcludedRequestId'])
    except (ValueError,KeyError,TypeError):return False

def require_restored_diagnostic_owner(identity,source,new_base,daemon_owner):
    """Observed new authority join only; accepted source is DATA, not previous admission."""
    require_fresh_owner(identity,None)
    old=source['OriginalRunningRecord']['Identity']
    if identity.get('RootKey')!=str(new_base):raise ValueError('Diagnostic owner root mismatch')
    for key in ('GenerationId','RunId'):
        value=identity.get(key)
        if not isinstance(value,str) or not value.strip() or value==old[key]:raise ValueError('Diagnostic owner identity reused or empty')
    old_daemon=daemon_pair_decode(source['DaemonSettlement'],'Final',True)
    if not isinstance(daemon_owner,str) or not daemon_owner.strip() or daemon_owner==old_daemon:raise ValueError('Diagnostic daemon owner reused or empty')


def _load_durable_identity(value):
    """Comparison-only copy of the existing string-enum durable wire identity.

    DEBUG observation identity uses default numeric enum serialization. Do not
    rewrite either captured record or accept arbitrary enum coercions.
    """
    if type(value) is not dict or type(value.get('Backend')) is not str or value['Backend'] not in ('WindowsJob','LinuxSupervisor'):
        raise ValueError('Invalid durable main backend encoding')
    return dict(value,Backend={'WindowsJob':1,'LinuxSupervisor':2}[value['Backend']])


def validated_load_observation(client_output, action_cut, expected, installed_record):
    """Validate captured Load DATA, not authority. Caller owns fresh output/cut and expected tuple.

    B comes only from the one validated retained-connection client observation;
    status, PID and epoch cannot substitute for that observation.
    """
    def require(condition, message):
        if not condition:raise ValueError(message)
    def guid(value):return type(value) is str and re.fullmatch('[0-9a-f]{32}',value) is not None
    def identity(value):
        require(type(value) is dict and set(value)=={'RootKey','RunId','GenerationId','Epoch','Backend','HostInstanceId','BootId'},'Invalid main identity shape')
        require(all(guid(value[k]) for k in ('RunId','GenerationId','HostInstanceId')) and
            type(value['Epoch']) is int and value['Epoch']>0 and type(value['Backend']) is int and value['Backend'] in (1,2) and
            all(type(value[k]) is str and value[k] for k in ('RootKey','BootId')),'Invalid main identity values')
    def terminal(value):
        require(type(value) is dict and set(value)=={'RunId','Backend','Guarantee','RootPid'} and guid(value['RunId']) and
            all(type(value[k]) is str and value[k] for k in ('Backend','Guarantee')) and
            type(value['RootPid']) is int and value['RootPid']>0,'Invalid terminal identity')
    require(type(client_output) is bytes and type(action_cut) is int and 0<=action_cut<=len(client_output),'Invalid client output cut')
    require(type(expected) is dict and set(expected)=={'Nonce','SourceKey','RootKey','ExpectedGeneration',
        'StartupIdentityA','StartupTerminalA'},'Incomplete expected Load tuple')
    require(all(guid(expected[k]) for k in ('Nonce','ExpectedGeneration')) and
        all(type(expected[k]) is str and expected[k] for k in ('SourceKey','RootKey')),'Invalid expected Load tuple')
    identity(_load_durable_identity(expected['StartupIdentityA']));terminal(expected['StartupTerminalA'])
    fresh=client_output[action_cut:]
    marker=b'boe-load-session-completed'
    require(fresh.count(marker)==1,'Missing or duplicate fresh Load observation')
    position=fresh.index(marker)
    start=fresh.rfind(b'{',0,position)
    require(start>=0 and fresh[start:position]==b'{"kind":"','Partial or malformed Load observation prefix')
    end=fresh.find(b'\n',position)
    require(end>=0,'Incomplete Load observation line')
    document=strict_settings(fresh[start:end].decode('utf-8'))
    require(set(document)=={'kind','nonce','observation'} and document['kind']==marker.decode() and
        document['nonce']==expected['Nonce'],'Foreign Load observation')
    observed=document['observation']
    require(type(observed) is dict and set(observed)=={'OperationId','SourceKey','RootKey','ExpectedGeneration','OriginalStop',
        'ReplacementFinish','Committed','RefreshConfirmed','EstablishedGeneration'},'Invalid Load observation shape')
    require(all(observed[k]==expected[k] for k in ('SourceKey','RootKey','ExpectedGeneration')) and
        guid(observed['OperationId']) and guid(observed['EstablishedGeneration']) and observed['Committed'] is True and observed['RefreshConfirmed'] is True,'Load binding or completion mismatch')
    for key,state in (('OriginalStop','Stopped'),('ReplacementFinish','Running')):
        receipt=observed[key]
        require(type(receipt) is dict and set(receipt)=={'Ok','OperationId','State','MainIdentity','TerminalIdentity','Error'} and
            receipt['Ok'] is True and receipt['OperationId']==observed['OperationId'] and receipt['State']==state and
            receipt['Error'] is None,'Load receipt is not exact successful completion')
        identity(receipt['MainIdentity']);terminal(receipt['TerminalIdentity'])
        require(receipt['TerminalIdentity']['RunId']==receipt['MainIdentity']['RunId'],'Load terminal/main mismatch')
    a=observed['OriginalStop']['MainIdentity'];b=observed['ReplacementFinish']['MainIdentity']
    require(a==_load_durable_identity(expected['StartupIdentityA']) and observed['OriginalStop']['TerminalIdentity']==expected['StartupTerminalA'] and
        a['RootKey']==expected['RootKey'] and a['GenerationId']==expected['ExpectedGeneration'],'Original Load owner mismatch')
    require(b['RootKey']==a['RootKey'] and b['GenerationId']==observed['EstablishedGeneration'] and
        b['GenerationId']!=a['GenerationId'] and b['RunId']!=a['RunId'] and b['Epoch']>a['Epoch'] and
        b['Backend']==a['Backend'],'Replacement Load owner mismatch')
    # Post-action metadata is an independent consistency check, never the source
    # of B or of the internally generated operation ID.
    require(type(installed_record) is dict and installed_record.get('Disposition')=='Running' and
        _load_durable_identity(installed_record.get('Identity'))==b,'Installed replacement consistency mismatch')
    return document


def load_phase_owner_join(value, client_output, action_cut, expected):
    """Exact A→B and retained-daemon DATA join, shared by two fixed routes only."""
    observed=validated_load_observation(client_output,action_cut,expected,value['LoadRunningRecord'])['observation']
    a=observed['OriginalStop']['MainIdentity'];b=observed['ReplacementFinish']['MainIdentity']
    if _load_durable_identity(value['StartupIdentityA'])!=a or value['ShutdownIdentityB']!=b:raise ValueError('Load owner/daemon join refused')
    if _load_durable_identity(value['OriginalRunningRecord']['Identity'])!=a or value['OriginalRunningRecord']['Disposition']!='Running':raise ValueError('Load owner/daemon join refused')
    if value['OriginalRunningStatus']['ok'] is not True or value['OriginalRunningStatus']['status']['terminalRunId']!=a['RunId']:raise ValueError('Load owner/daemon join refused')
    # Load replaces main A with B, not the retained root/session daemon.
    if not retained_daemon_load_continuity_proved(value['DaemonSettlement'],value['PostLoadDaemonContinuity']):raise ValueError('Load owner/daemon join refused')
    terminal=observed['ReplacementFinish']['TerminalIdentity']
    if value['ShutdownReceipt']['status']['terminalStop']['identity']!={
        'runId':terminal['RunId'],'backend':terminal['Backend'],'guarantee':terminal['Guarantee'],'rootPid':terminal['RootPid']}:raise ValueError('Load owner/daemon join refused')
    running=value['LoadRunningRecord'];status=value['LoadRunningStatus']
    if _load_durable_identity(running['Identity'])!=b or running['Disposition']!='Running':raise ValueError('Load owner/daemon join refused')
    if status['ok'] is not True or status['status']['terminalRunId']!=b['RunId']:raise ValueError('Load owner/daemon join refused')
    return running,status


def relay_load_phase_cleanup_proved(value, client_output, action_cut, expected):
    """Separate Load-only cleanup join. Never overwrites startup A or adopts B from status."""
    try:
        running,status=load_phase_owner_join(value,client_output,action_cut,expected)
        if any(type(value[k]) is not int or value[k]!=0 for k in ('AcceptedGameTurns','GenuineActionsSent')):return False
        if not local_load_route_proved(value['LocalRouteProof'],value['FinalQueuePins']):return False
        if value['LocalRouteProof']['SaveArchivePath']!=expected['SourceKey']:return False
        if not history_witness_matches_pins(value['LocalRouteProof']['After']['History'],value['FinalWorldPins']):return False
        return relay_owned_cleanup_proved(value,running,status)
    except (KeyError,TypeError,ValueError,UnicodeError):return False


def relay_self_save_load_turn_cleanup_proved(value, client_output, action_cut, expected):
    """Initial setup's own Save, real A→B, one B-bound GM turn and normal B close."""
    try:
        running,status=load_phase_owner_join(value,client_output,action_cut,expected)
        if value['ConsoleRoute']!={'Route':'self-save-load-turn'} or type(value['AcceptedTurnNumber']) is not int or value['AcceptedTurnNumber']!=42:return False
        if value['LoadObservation']!=validated_load_observation(client_output,action_cut,expected,running):return False
        if value['PostLoadDaemonContinuity']['OriginalGuardRawHex']!=value['PostLoadDaemonContinuity']['PostLoadGuardRawHex']:return False
        if any(type(value[k]) is not int or value[k]!=1 for k in ('AcceptedGameTurns','GenuineActionsSent')):return False
        if value['InputQualified'] is not True or value['TurnRunningRecord']!=running or value['TurnRunningStatus']!=status:return False
        require_self_save_turn_status(value,status,True)
        require_self_save_turn_status(value,value['PostLoadPreActionStatus'],True)
        current=status['status'];binding=current['inputBindingId']
        fresh=value['PostLoadPreActionStatus']
        if (value['PostLoadPreActionRecord']!=running or fresh['ok'] is not True or fresh['status']['terminalRunId']!=current['terminalRunId']
            or fresh['status']['inputBindingId']!=binding or fresh['status']['terminalOwnerRetained'] is not True
            or fresh['status']['terminalUncertain'] is not False or fresh['status']['ready'] is not True):return False
        if (type(binding) is not str or not binding or binding==value['OriginalRunningStatus']['status']['inputBindingId']
            or current['terminalOwnerRetained'] is not True or current['terminalUncertain'] is not False or current['ready'] is not True):return False
        delivery=value['OriginalTurnDelivery']
        if (delivery['inputBindingId']!=binding or delivery['disposition']!='submission-observed'
            or delivery['operationKind']!='turn' or delivery['operationRevision']!='live'):return False
        replies=value['RelayResponseEvidence']
        if type(replies) is not list or len(replies)!=1 or replies[0]['Kind']!='turn' or replies[0]['Dispatch']['inputBindingId']!=binding:return False
        proof=value['SelfSaveLoadProof'];before=proof['Before'];pin=proof['Archive']
        if (before['Turn']!=41 or before['Queue']!={} or before!=proof['AfterSave'] or before!=proof['AfterLoad'] or before!=proof['PreActionReadback']
            or any(proof[k] is not True for k in ('ArchiveVerified','RestoreBytesExact','CleanOutcomeBeforeFirstPlayerClear'))):return False
        if (set(pin)!={'Path','Name','SHA256','Mode'} or type(pin['Mode']) is not int or not 0<=pin['Mode']<=0o777 or pin['Path']!=expected['SourceKey'] or Path(pin['Path']).name!=pin['Name']
            or re.fullmatch('[0-9a-f]{64}',pin['SHA256']) is None or pin['SHA256']!=value['LoadArchiveSHA256']
            or proof['FinalArchiveReadback']!=pin or value['FinalConfigSHA256']!=before['ConfigSHA256']):return False
        content=proof['ArchiveContentProof'];manifest_raw=bytes.fromhex(content['ManifestRawHex'])
        if (any(content[k] is not True for k in ('ManifestVerified','ConfigBytesExact','RuntimeAbsent'))
            or content['ConfigSHA256']!=before['ConfigSHA256'] or content['ManifestSHA256']!=sha(manifest_raw)
            or type(content['ManifestLength']) is not int or content['ManifestLength']!=len(manifest_raw)
            or type(content['ManifestEntries']) is not int or content['ManifestEntries']<=0):return False
        request=value['ActualRequest'];request_raw=bytes.fromhex(value['ActualRequestRawHex'])
        if (strict_settings(request_raw)!=request or sha(request_raw)!=value['ActualRequestSHA256']
            or request['sessionId']!=value['SeedSessionId'] or type(request['turnNumber']) is not int or request['turnNumber']!=42):return False
        queue=value['SelfSaveQueue']
        if (type(queue) is not list or len(queue)!=1 or queue!=value['FinalSelfSaveQueue'] or queue[0]['Kind']!='turn'
            or queue[0]['QueueId']!=replies[0]['QueueId'] or queue[0]['TurnRequestSHA256']!=sha(request_raw)):return False
        checked_mortal_outcome(value['TypedReceiptEvidence'],request)
        original=bytes.fromhex(before['Files'][before['StoryPath']]);after=bytes.fromhex(value['AcceptedStoryAfterRawHex'])
        if (checked_mortal_story_append(original,after,request)!=value['AcceptedStory']
            or sha(original)!=value['StoryBeforeSHA256'] or sha(after)!=value['StoryAfterSHA256']
            or value['FinalWorldPins'][before['StoryPath']]!=sha(after)
            or not history_witness_matches_pins(value['TypedReceiptEvidence']['historyAfter'],value['FinalWorldPins'])):return False
        closure=value['ClientTerminalClosure'];helper=value['ReceiptHelper']
        if (any(closure[k] is not True for k in ('ChildRestoredConfiguredBaseline','OwnerRestoredOriginal','TerminalNormalClose','EOF'))
            or type(closure['ExitCode']) is not int or closure['ExitCode']!=0
            or any(helper[k] is not True for k in ('Completed','StdoutEOF','StderrEOF'))
            or type(helper['ExitCode']) is not int or helper['ExitCode']!=0):return False
        return relay_owned_cleanup_proved(value,running,status)
    except (KeyError,TypeError,ValueError,UnicodeError):return False


def retained_daemon_load_continuity_proved(settlement, continuity):
    """Live continuity DATA for the same daemon, not final settlement or authority.

    Only original running evidence is available at this boundary. Final Load
    cleanup separately requires the unchanged full daemon settlement predicate.
    """
    from datetime import datetime
    def timestamp(value):
        if type(value) is not str:raise ValueError('Invalid heartbeat')
        parsed=datetime.fromisoformat(value.replace('Z','+00:00'))
        if parsed.tzinfo is None:raise ValueError('Missing heartbeat timezone')
        return parsed
    def process(value):
        return type(value) is dict and set(value)=={'Pid','StartTicks'} and all(type(value[k]) is int and value[k]>0 for k in value)
    def status(prefix,source):return strict_settings(bytes.fromhex(source[prefix+'StatusRawHex']))
    try:
        if continuity['SessionPath']!=settlement['SessionPath']:return False
        owner=daemon_pair_decode(settlement,'Original',False)
        if any(daemon_pair_decode(continuity,prefix,False)!=owner for prefix in ('Original','PostLoad')):return False
        initial=status('Original',settlement);before=status('Original',continuity);after=status('PostLoad',continuity)
        if any(type(x['pid']) is not int or x['pid']<=0 for x in (initial,before,after)):return False
        if not initial['pid']==before['pid']==after['pid']:return False
        if not initial['startedAtUtc']==before['startedAtUtc']==after['startedAtUtc']:return False
        timestamp(initial['startedAtUtc'])
        if not timestamp(after['heartbeatAtUtc'])>max(timestamp(before['heartbeatAtUtc']),timestamp(continuity['LoadActionUtc'])):return False
        for kind in ('Process','Launcher'):
            old=continuity['Original'+kind+'Identity'];new=continuity['PostLoad'+kind+'Identity']
            if not process(old) or not process(new) or old!=new:return False
        if continuity['OriginalProcessIdentity']['Pid']!=initial['pid']:return False
        return continuity['OriginalLauncherPoll'] is None and continuity['PostLoadLauncherPoll'] is None
    except (KeyError,TypeError,ValueError,UnicodeError):return False


def local_load_route_proved(proof, final_queue_pins):
    """Fixed retrieve/save/deposit/Load DATA, with no GM request or accepted turn."""
    def digest(value):return type(value) is str and re.fullmatch('[0-9a-f]{64}',value) is not None
    def pins(value):return type(value) is dict and all(type(k) is str and digest(v) for k,v in value.items())
    try:
        if proof['Route']!='storage-local-proof' or proof['RestoreBytesExact'] is not True:return False
        target=proof['Target']
        if set(target)!={'LocationId','StorageId','ItemId','StorageLabel','StorageName','ItemLabel','ContentsCount'}:return False
        if any(type(target[k]) is not str or not target[k] for k in target if k!='ContentsCount'):return False
        if type(target['ContentsCount']) is not int or target['ContentsCount']<1:return False
        before=proof['Before'];after=proof['After']
        if before!=after or set(before)!={'StorySHA256','History','QueuePins'}:return False
        if not digest(before['StorySHA256']) or not history_witness_valid(before['History']) or not pins(before['QueuePins']) or not pins(final_queue_pins):return False
        if any('request-' in k for k in list(before['QueuePins'])+list(final_queue_pins)):return False
        # Maintained relay_contract.request_close adds these names; execution lock
        # is coordination DATA, never a GM request. All earlier queue bytes persist.
        added=set(final_queue_pins)-set(before['QueuePins'])
        if not {'close-request.json','closed.json'}.issubset(final_queue_pins):return False
        if not added.issubset({'close-request.json','closed.json','.execution.lock'}):return False
        if any(final_queue_pins.get(k)!=v for k,v in before['QueuePins'].items()):return False
        if type(proof['SaveArchivePath']) is not str or not Path(proof['SaveArchivePath']).is_absolute() or not digest(proof['SaveArchiveSHA256']):return False
        stages=proof['Stages']
        if type(stages) is not list or len(stages)!=4:return False
        for stage,name,position in zip(stages,('Retrieve','Save','Deposit','Load'),('inventory','inventory','storage','inventory')):
            if set(stage)!={'Stage','ItemPosition','LocationSHA256','InventorySHA256'} or stage['Stage']!=name or stage['ItemPosition']!=position:return False
            if not all(digest(stage[k]) for k in ('LocationSHA256','InventorySHA256')):return False
        for key in ('LocationSHA256','InventorySHA256'):
            if not stages[0][key]==stages[1][key]==stages[3][key] or stages[2][key]==stages[1][key]:return False
        return True
    except (KeyError,TypeError,ValueError):return False


def optional_history_witness(raw):
    """Exact detached bytes/presence, never a receipt or publication authority."""
    if raw is not None and not isinstance(raw, bytes):raise ValueError('History must be bytes or physical absence')
    return {'present':raw is not None,'sha256':hashlib.sha256(raw).hexdigest() if raw is not None else None}



def history_witness_at(session):
    """Fresh optional history read: only physical absence is optional; IO/JSON validity is not inferred."""
    path=Path(session)/'game_state/world/mortal_living_world_history.json'
    if path.is_symlink():raise ValueError('Linked history is not captured canonical DATA')
    try:raw=path.read_bytes()
    except FileNotFoundError:raw=None
    return optional_history_witness(raw)


def history_witness_valid(value):
    return (isinstance(value,dict) and set(value)=={'present','sha256'} and type(value['present']) is bool
        and ((value['present'] and isinstance(value['sha256'],str) and re.fullmatch('[0-9a-f]{64}',value['sha256']) is not None)
             or (not value['present'] and value['sha256'] is None)))


def history_witness_matches_pins(value,pins):
    """Joins outcome DATA to independently fresh canonical inventory, including absence."""
    path='game_state/world/mortal_living_world_history.json'
    return history_witness_valid(value) and isinstance(pins,dict) and (pins.get(path)==value['sha256'] if value['present'] else path not in pins)


def checked_mortal_outcome(value,request):
    """Strict union of appended receipt and production-checked nonapplicability; no missing-field fallback."""
    common={'valid','publicationAuthority','kind','sessionId','requestId','turn','snapshotToken','priorCount','currentCount','historyBefore','historyAfter'}
    if not isinstance(value,dict) or value.get('valid') is not True or value.get('publicationAuthority') is not False:
        raise ValueError('Typed outcome validation failed')
    kind=value.get('kind');extra={'declarationFingerprint','receiptFingerprint'} if kind=='ReceiptAppended' else {'applicability'} if kind=='NotRequired' else None
    if extra is None or set(value)!=common|extra:raise ValueError('Unknown or mixed typed outcome fields')
    if not isinstance(request,dict) or type(request.get('turnNumber')) is not int or any(type(request.get(k)) is not str or not request[k] for k in ('sessionId','requestId')):raise ValueError('Invalid explicit request')
    if any(value[k]!=request[k] for k in ('sessionId','requestId')) or type(value['turn']) is not int or value['turn']!=request['turnNumber']:
        raise ValueError('Typed outcome belongs to another request')
    if not isinstance(value['snapshotToken'],str) or not re.fullmatch('[0-9A-F]{64}',value['snapshotToken']):raise ValueError('Missing snapshot token')
    prior=value['priorCount'];current=value['currentCount']
    if type(prior) is not int or prior<0 or type(current) is not int or current<0:raise ValueError('Invalid receipt counts')
    before=value['historyBefore'];after=value['historyAfter']
    if not history_witness_valid(before) or not history_witness_valid(after):raise ValueError('Invalid history presence/hash')
    if not before['present'] and prior!=0:raise ValueError('Absent prior history has receipts')
    if kind=='ReceiptAppended':
        if current!=prior+1 or not after['present'] or any(not isinstance(value[k],str) or not re.fullmatch('sha256:[0-9a-f]{64}',value[k]) for k in extra):
            raise ValueError('Expected exactly one new typed receipt')
    else:
        a=value['applicability'];fields={'originalMinute','resultMinute','lastWorldMinute','lastFactionMinute','worldCycleMinutes','factionCycleMinutes'}
        if current!=prior or before!=after or not isinstance(a,dict) or set(a)!=fields or any(type(a[k]) is not int or a[k]<0 for k in fields):
            raise ValueError('Invalid nonapplicable outcome')
        if (a['worldCycleMinutes']<=0 or a['factionCycleMinutes']<=0 or a['resultMinute']<a['originalMinute']
            or a['lastWorldMinute']>a['originalMinute'] or a['lastFactionMinute']>a['originalMinute']
            or a['resultMinute']-a['lastWorldMinute']>=a['worldCycleMinutes'] or a['resultMinute']-a['lastFactionMinute']>=a['factionCycleMinutes']):
            raise ValueError('Nonapplicable outcome claims due/invalid clock')
    return value


def require_native_bootstrap_path_budget(temp_root):
    # NativeLineageOwner currently checks this before constructing a socket,
    # including the inherited-pipe fallback. Mirror, never weaken, that guard.
    count=len((str(Path(temp_root)/('boe-native-'+'0'*32)/'owner')).encode('utf-8'))
    if count>107:raise ValueError(f'Fixture native bootstrap path is {count} UTF-8 bytes; maximum 107. Choose a fresh shorter phase root.')
    return count

# Finite scenario DATA only; resource_owner_authority is recomposed by Save and
# deliberately not byte-compared. This is not a full-state/ownership manifest.
ORDINARY_SAVE_PATHS = (
    'game_state/meta/soul_state.json', 'game_state/core/player_status.json',
    'game_state/world/world_time.json', 'game_state/control/progression_schedule.json',
    'game_state/world/world_map.json', 'game_state/world/location_identity_index.json',
    'game_state/world/current_location.json', 'game_state/inventory/items.json',
    'game_state/inventory/item_identity_index.json', 'game_state/misc/vehicles.json',
    'game_state/npcs/npc_core.json', 'game_state/npcs/npc_journals.json',
    'game_state/npcs/item_journals.json',
)

def ordinary_save_snapshot(session, story, queue, result, *, storage_retrieve=False):
    """One immutable scoped snapshot per boundary; no ownership or permission."""
    session=_restored_plain_path(Path(session),True)
    story=_restored_plain_path(Path(story));story_name=story.relative_to(session).as_posix()
    if not story_name.startswith('stories/') or len(Path(story_name).parts)!=2:
        raise ValueError('Ordinary Save active story path differs')
    if (type(result.get('AcceptedTurnNumber')) is not int or result['AcceptedTurnNumber']<42
        or any(type(result.get(k)) is not int or result[k]!=1 for k in ('AcceptedGameTurns','GenuineActionsSent'))):
        raise ValueError('Ordinary Save requires one accepted actual action')
    for name in ('input/turn_request.json','game_state/control/pending_turn_snapshot.json',
        'game_state/control/pending_turn_snapshot.authority.json','game_state/control/pending_turn_snapshot',
        'ready/turn_complete.json','ready/turn_error.json'):
        path=session/name
        if path.exists() or path.is_symlink():raise ValueError('Pending turn at Save boundary: '+name)
    storage_paths=('game_state/misc/storage_access.json',) if storage_retrieve else ()
    files={name:_restored_plain_path(session/name).read_bytes().hex()
           for name in (*ORDINARY_SAVE_PATHS,*storage_paths,story_name)}
    if sha(bytes.fromhex(files[story_name]))!=result.get('StoryAfterSHA256'):
        raise ValueError('Accepted story changed before Save')
    optional=('game_state/world/mortal_living_world_history.json',)
    if storage_retrieve:optional+=('game_state/world/location_storage_contents.json',)
    for name in optional:
        p=session/name
        files[name]=_restored_plain_path(p).read_bytes().hex() if p.exists() or p.is_symlink() else None
    queue=_restored_plain_path(Path(queue),True);queue_pins={}
    for p in queue.rglob('*'):
        if p.is_symlink():raise ValueError('Linked Save queue entry')
        if p.is_dir():continue
        queue_pins[p.relative_to(queue).as_posix()]=sha(_restored_plain_path(p).read_bytes())
    return {'Turn':result['AcceptedTurnNumber'],'StoryPath':story_name,'Files':files,'Queue':queue_pins,
        'AcceptedGameTurns':result['AcceptedGameTurns'],'GenuineActionsSent':result['GenuineActionsSent']}

def verify_ordinary_save_archive(raw, snapshot, expected_turn):
    """Verify the scoped accepted DATA, not synthesized/control archive contents."""
    import io,zipfile
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        names=archive.namelist();seen=set()
        for name in names:
            path=Path(name)
            if (not name or '\\' in name or path.is_absolute() or '..' in path.parts
                or path.as_posix()!=name or name.casefold() in seen):
                raise ValueError('Aliased or duplicate Save member')
            seen.add(name.casefold())
        if names.count('save_metadata.json')!=1:raise ValueError('Save metadata ambiguous')
        metadata=strict_settings(archive.read('save_metadata.json'))
        if (metadata.get('saveName')!='s' or type(metadata.get('turnNumber')) is not int
            or metadata['turnNumber']!=expected_turn or snapshot['Turn']!=expected_turn):
            raise ValueError('Save turn differs from accepted turn')
        for name,value in snapshot['Files'].items():
            if value is None:
                if name in names:raise ValueError('Save fabricated absent scoped DATA: '+name)
            elif names.count(name)!=1 or archive.read(name)!=bytes.fromhex(value):
                raise ValueError('Save scoped DATA differs: '+name)
    return True

def ordinary_save_proved(result):
    try:
        p=result['OrdinarySaveProof'];before=p['Before'];after=p['After'];turn=result['AcceptedTurnNumber']
        if (type(turn) is not int or type(p['Turn']) is not int or p['Turn']!=turn or before['Turn']!=turn
            or any(type(result[k]) is not int or result[k]!=1 for k in ('AcceptedGameTurns','GenuineActionsSent'))
            or before!=after or p['ArchiveVerified'] is not True or p['CleanOutcomeBeforeFirstPlayerClear'] is not True):return False
        if not isinstance(p['ArchiveSHA256'],str) or re.fullmatch('[0-9a-f]{64}',p['ArchiveSHA256']) is None:return False
        story_sha=sha(bytes.fromhex(before['Files'][before['StoryPath']]))
        return story_sha==p['StorySHA256']==result['StoryAfterSHA256']
    except (KeyError,TypeError,ValueError):return False

# Cold Load is accepted DATA plus closure, never a retained original owner.
def require_cold_loaded_namespace(base):
    base=_restored_plain_path(Path(base),True);session=_restored_plain_path(base/'game_session',True)
    for path in (base/'.boe_runtime/gm-runs/main.json',session/'game_state/control/gm_bridge_status.json',
        session/'game_state/control/pending_turn_snapshot.json',session/'game_state/control/pending_turn_snapshot.authority.json',
        session/'game_state/control/pending_turn_snapshot',session/'game_state/control/validation_repair_request.json',
        session/'input/turn_request.json',session/'ready/turn_complete.json',session/'ready/turn_error.json'):
        if path.exists() or path.is_symlink():raise ValueError('Cold host has active/pending state: '+str(path))

def cold_loaded_tree(root):
    root=_restored_plain_path(Path(root),True);files={};directories={'.':stat.S_IMODE(root.stat().st_mode)}
    for path in root.rglob('*'):
        mode=path.lstat();name=path.relative_to(root).as_posix()
        if stat.S_ISDIR(mode.st_mode):directories[name]=stat.S_IMODE(mode.st_mode)
        elif stat.S_ISREG(mode.st_mode) and mode.st_nlink==1:files[name]={'sha256':sha(path.read_bytes()),'mode':stat.S_IMODE(mode.st_mode)}
        else:raise ValueError('Linked/special cold host entry')
    return {'Files':files,'Directories':directories}

def require_cold_loaded_owner(identity,expected):
    keys={'RootKey','GenerationId','RunId','Epoch','Backend','HostInstanceId','BootId'}
    if type(identity) is not dict or set(identity)!=keys:raise ValueError('Incomplete fresh owner identity')
    if (identity['RootKey']!=expected['RootKey'] or identity['GenerationId']!=expected['GenerationId']
        or identity['RunId'] in expected['ForbiddenRunIds'] or type(identity['Epoch']) is not int or identity['Epoch']<1
        or identity['Backend']!='LinuxSupervisor'):raise ValueError('Cold host owner differs')
    for key in ('RunId','GenerationId','HostInstanceId'):
        if type(identity[key]) is not str or re.fullmatch('[0-9a-f]{32}',identity[key]) is None:raise ValueError('Malformed owner identifier')
    if type(identity['BootId']) is not str or re.fullmatch('[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}',identity['BootId']) is None:
        raise ValueError('Malformed owner boot identity')

def require_portable_cold_daemon_witness(base, archive_bytes):
    """Require fresh absence; return the authenticated historical owner as DATA only."""
    import io,zipfile
    base=_restored_plain_path(Path(base),True)
    session=_restored_plain_path(base/'game_session',True)
    relative='game_state/control/gm_daemon_status.json'
    status=session/relative
    guard=base/'.boe_runtime/locks/gm-daemon-instance.lock'
    if any(path.exists() or path!=path.resolve() or any(parent.is_symlink() for parent in (path,*path.parents))
        for path in (status,guard)):
        raise ValueError('Cold host has local daemon status or guard evidence')
    with zipfile.ZipFile(io.BytesIO(archive_bytes)) as archive:
        names=archive.namelist()
        if names.count(relative)!=1 or names.count('save_manifest.json')!=1:
            raise ValueError('Historical daemon archive evidence is ambiguous or absent')
        row=archive.getinfo(relative)
        manifest_row=archive.getinfo('save_manifest.json')
        if not 0<row.file_size<=65536 or not 0<manifest_row.file_size<=1048576:
            raise ValueError('Historical daemon status size differs')
        raw=archive.read(row)
        manifest=strict_settings(archive.read(manifest_row))
        matches=[entry for entry in manifest['entries'] if entry['path']==relative]
        if (len(matches)!=1 or type(matches[0]['length']) is not int
            or matches[0]['length']!=len(raw) or type(matches[0]['sha256']) is not str or matches[0]['sha256'].lower()!=sha(raw)):
            raise ValueError('Historical daemon status manifest differs')
    former=strict_settings(raw)
    owner=former.get('daemonGuardOwnerId')
    if type(former.get('daemonGuardVersion')) is not int or former['daemonGuardVersion']!=1 or not isinstance(owner,str) or re.fullmatch('[0-9a-f]{32}',owner) is None:
        raise ValueError('Historical daemon owner DATA missing')
    return owner

def require_cold_loaded_host(descriptor,out,*,previous,route,current_max):
    """Authenticate closed cold/rebind evidence and current DATA. Does not acquire
    authority, prepare packages, copy state, start a process or update settings.
    The caller separately verifies actual runtime/helper/terminal packages.
    """
    if (descriptor.get('Kind')!='cold-loaded-save43-fresh-host' or previous is not None or route!='ordinary-save'
        or type(current_max) is not int or current_max!=43 or descriptor.get('ExpectedCurrentTurn')!=43 or descriptor.get('ExpectedTurn')!=44):
        raise ValueError('Not the closed cold Save43 fresh-host route')
    base=_restored_plain_path(Path(descriptor['BasePath']),True);root=base.parent;out=_restored_plain_path(Path(out),True)
    if base!=root/'play' or out!=root/'recovered-live' or str(out)!=descriptor['PhasePath']:
        raise ValueError('Cold phase/root differs')
    temp=Path(descriptor['TemporaryRoot'])
    if temp!=root/'rt' or temp!=temp.resolve() or Path(descriptor['RuntimeRoot'])!=root/'host-runtime':raise ValueError('Cold private paths differ')
    require_native_bootstrap_path_budget(temp)
    require_cold_loaded_namespace(base);session=base/'game_session';config=session/'config.json'
    def pinned(value):
        raw=_restored_plain_path(Path(value['Path'])).read_bytes()
        if sha(raw)!=value['SHA256']:raise ValueError('Cold evidence pin differs')
        return raw
    cold=strict_settings(pinned(descriptor['ColdOutcome']));rebound=strict_settings(pinned(descriptor['RebindOutcome']))
    old_config=pinned(descriptor['OriginalConfig'])
    if cold.get('OriginalWrapperExit')!=0 or cold.get('ProbeSuccess') is not True or rebound.get('WrapperExit')!=0:
        raise ValueError('Cold/rebind operation not successful')
    if rebound.get('ColdOutcomeSHA256')!=descriptor['ColdOutcome']['SHA256']:raise ValueError('Rebind not bound to this cold Load')
    for path,digest in cold['Artifacts'].items():
        raw=old_config if path==str(config) else _restored_plain_path(Path(path)).read_bytes()
        if sha(raw)!=digest:raise ValueError('Cold artifact changed: '+path)
    for path,digest in rebound['Artifacts'].items():
        if sha(_restored_plain_path(Path(path)).read_bytes())!=digest:raise ValueError('Rebind artifact changed: '+path)
    # Reuse the actual cold route's strict driver/child/sidecar closure predicate.
    import importlib.util
    source=Path(__file__).resolve().parents[3]/'specs/1515-mortal-transport-storage/manual-mm21/run-cold-load.py'
    spec=importlib.util.spec_from_file_location('cold_host_closure',source);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    guardian=strict_settings((root/'guardian.json').read_bytes());probe=strict_settings((root/'probe/result.json').read_bytes())
    if not module.cold_run_succeeded(int((root/'exit.txt').read_text()),guardian,probe):raise ValueError('Cold closure not proven')
    if cold_loaded_tree(base)!=descriptor['PlayRows'] or cold_loaded_tree(root/'runtime')!=descriptor['ColdRuntimeRows']:
        raise ValueError('Cold retained inventory changed')
    relay=cold_loaded_tree(out/'relay');expected=descriptor['RelayRows']
    if relay['Files']!={name.removeprefix('relay/'):value for name,value in expected['Files'].items()} or relay['Directories']!={'.':0o700}:
        raise ValueError('Rebound relay changed')
    queue=_restored_plain_path(out/'queue',True)
    if any(queue.iterdir()) or stat.S_IMODE(queue.stat().st_mode)!=0o700:raise ValueError('Cold queue not private and empty')
    story=session/'stories/mortal_life_2.jsonl';entries=[strict_settings(line) for line in story.read_text(encoding='utf-8-sig').splitlines() if line.strip()]
    if [row.get('turn') for row in entries]!=[41,42,43]:raise ValueError('Cold accepted story differs')
    all_turns=[row['turn'] for p in (session/'stories').glob('*.jsonl') for row in map(strict_settings,p.read_text(encoding='utf-8-sig').splitlines()) if row]
    if max(all_turns)!=43:raise ValueError('Cold current maximum differs')
    chat=strict_settings((session/'game_state/history/chat_log.json').read_bytes())
    if chat.get('sessionId')!=descriptor['SessionId']:raise ValueError('Cold session differs')
    proof=strict_settings((root/'probe/loaded-proof.json').read_bytes())
    name=descriptor['ArchiveName']
    if Path(name).name!=name or proof['ArchiveName']!=name or proof['ArchiveSHA256']!=descriptor['ArchiveSHA256']:raise ValueError('Cold archive differs')
    archive=_restored_plain_path(session/'saves/manual_saves'/name).read_bytes()
    if sha(archive)!=descriptor['ArchiveSHA256']:raise ValueError('Cold archive digest differs')
    files={p:_restored_plain_path(session/p).read_bytes().hex() for p in (*ORDINARY_SAVE_PATHS,'stories/mortal_life_2.jsonl')}
    history=session/'game_state/world/mortal_living_world_history.json'
    files['game_state/world/mortal_living_world_history.json']=history.read_bytes().hex() if history.exists() else None
    verify_ordinary_save_archive(archive,{'Turn':43,'Files':files},43)
    if {p:sha(bytes.fromhex(v)) for p,v in files.items() if v is not None}!=proof['ScopedSHA256']:raise ValueError('Cold scoped proof differs')
    config_bytes=config.read_bytes();settings=strict_settings(config_bytes)
    quote=lambda value:"'"+str(value).replace("'","''")+"'"
    command="& '/usr/bin/python3' "+quote(out/'relay/relay_cli.py')+' --session '+quote(session)+' --queue '+quote(queue)+' --model dots-finish-relay'
    if settings.get('GmCliLaunchCommand')!=command or settings.get('GmBridgeShellWorkingDirectory')!=str(session):raise ValueError('Cold operational command differs')
    if re.fullmatch('og-[0-9a-f]{12}',settings.get('GmBridgePipeNameOverride','')) is None:raise ValueError('Cold pipe differs')
    generation=strict_settings((base/'.boe_runtime/session-generation/current.json').read_bytes())['GenerationId']
    forbidden=[]
    if type(descriptor.get('SourceOwnerEvidence')) is not list or len(descriptor['SourceOwnerEvidence'])!=2:raise ValueError('Pinned source owners required')
    for evidence in descriptor['SourceOwnerEvidence']:
        old=strict_settings(pinned(evidence))['OriginalRunningRecord']['Identity']
        if old['RootKey']==str(base) or old['GenerationId']==generation or re.fullmatch('[0-9a-f]{32}',old['RunId']) is None:raise ValueError('Not distinct source owner DATA')
        forbidden.append(old['RunId'])
    if len(set(forbidden))!=2:raise ValueError('Ambiguous source owner DATA')
    historical_owner=require_portable_cold_daemon_witness(base,archive)
    return {'ForbiddenRunIds':forbidden,'ArchivedDaemonOwnerId':historical_owner,'ExpectedTurn':44,'SessionId':descriptor['SessionId'],'BasePath':str(base),'RuntimeRoot':descriptor['RuntimeRoot'],
        'TemporaryRoot':str(temp),'ConfigBytes':config_bytes,'Command':command,'Pipe':settings['GmBridgePipeNameOverride'],'GenerationId':generation}


def require_route_load_nonce(route, profile, environment):
    """Opt-in observation only. Default routes cannot accidentally enable active Load."""
    nonce=environment.get('BOE_TEST_LOAD_OBSERVATION_NONCE')
    if route not in ('storage-local-proof','self-save-load-turn'):
        if 'BOE_TEST_LOAD_OBSERVATION_NONCE' in environment or 'LoadObservationNonce' in profile:
            raise ValueError('Unexpected active Load observation nonce')
        return None
    if type(nonce) is not str or re.fullmatch('[0-9a-f]{32}',nonce) is None:
        raise ValueError('Local Load requires a fresh prepared diagnostic nonce')
    if route=='self-save-load-turn' and (profile.get('LoadObservationNonce')!=nonce or profile.get('GMDecisionDeferred') is not True):
        raise ValueError('Self-Save Load nonce/GM decision differs from pinned launch profile')
    return nonce


def require_self_save_initial(session, previous, restored, cold, expected_turn):
    """New setup41 only, no predecessor, restored/cold host or pre-existing ZIP menu."""
    session=_restored_plain_path(Path(session),True)
    if previous is not None or restored is not False or cold is not False or type(expected_turn) is not int or expected_turn!=42:
        raise ValueError('Self-Save route requires fresh initial setup41')
    if any(p for folder in ('manual_saves','autosaves','checkpoint_saves') for p in (session/'saves'/folder).glob('*.zip')):
        raise ValueError('Self-Save route refuses pre-existing ZIP choices')


def self_save_archive_pin(session, archive, raw, *, final=False):
    """Fresh exact sole ZIP readback; never selects by friendly name or latest file."""
    session=_restored_plain_path(Path(session),True);archive=_restored_plain_path(Path(archive))
    if archive.parent!=session/'saves/manual_saves' or type(raw) is not bytes:
        raise ValueError('Self-Save archive path differs')
    folders=('manual_saves',) if final else ('manual_saves','autosaves','checkpoint_saves')
    files=[p for folder in folders for p in (session/'saves'/folder).glob('*.zip')]
    if files!=[archive] or archive.read_bytes()!=raw:raise ValueError('Self-Save exact manual ZIP/menu changed')
    return {'Path':str(archive),'Name':archive.name,'SHA256':sha(raw),'Mode':stat.S_IMODE(archive.stat().st_mode)}


def self_save_load_snapshot(session, story, queue):
    """Actual initial scoped DATA, never a fabricated accepted-turn result/authority."""
    session=_restored_plain_path(Path(session),True);story=_restored_plain_path(Path(story));queue=_restored_plain_path(Path(queue),True)
    if story!=session/'stories/mortal_life_2.jsonl' or list((session/'stories').glob('*.jsonl'))!=[story]:
        raise ValueError('Self-Save initial story inventory differs')
    raw=story.read_bytes();rows=[strict_settings(line) for line in raw.decode('utf-8-sig').splitlines() if line.strip()]
    if len(rows)!=1 or type(rows[0].get('turn')) is not int or rows[0]['turn']!=41:
        raise ValueError('Self-Save requires only setup41, not accepted gameplay')
    for relative in ('input/turn_request.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json',
        'game_state/control/pending_turn_snapshot','game_state/control/validation_repair_request.json','ready/turn_complete.json','ready/turn_error.json'):
        path=session/relative
        if path.exists() or path.is_symlink():raise ValueError('Pending self-Save boundary: '+relative)
    if any(queue.iterdir()):raise ValueError('Self-Save/Load must precede every GM request')
    files={}
    for relative in ORDINARY_SAVE_PATHS:
        path=session/relative
        files[relative]=_restored_plain_path(path).read_bytes().hex() if path.exists() or path.is_symlink() else None
    history='game_state/world/mortal_living_world_history.json';path=session/history
    files[history]=_restored_plain_path(path).read_bytes().hex() if path.exists() or path.is_symlink() else None
    story_name=story.relative_to(session).as_posix();files[story_name]=raw.hex()
    config=_restored_plain_path(session/'config.json').read_bytes()
    return {'Turn':41,'StoryPath':story_name,'Files':files,'Queue':{},'ConfigSHA256':sha(config),'ConfigRawHex':config.hex()}


def self_save_runtime_root(base):
    """Only the new route's independently packaged sibling survives exact Load replacement."""
    base=_restored_plain_path(Path(base),True);session=_restored_plain_path(base/'game_session',True)
    nested=session/'_runtime'
    if nested.exists() or nested.is_symlink():raise ValueError('Self-Save active Load refuses canonical _runtime')
    return _restored_plain_path(base.parent/'package',True)


def verify_self_save_archive(raw, snapshot):
    """Detached own-archive check reusing scoped ZIP checks plus its full integrity manifest."""
    import io,zipfile
    verify_ordinary_save_archive(raw,snapshot,41)
    config=bytes.fromhex(snapshot['ConfigRawHex'])
    if sha(config)!=snapshot['ConfigSHA256']:raise ValueError('Self-Save config snapshot differs')
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        names=archive.namelist()
        control='game_state/control/'
        host_nodes=('_runtime','.boe_runtime',control+'gm_daemon_status.json',control+'gm_bridge_status.json',
                    control+'pending_turn_snapshot',control+'pending_turn_snapshot.json',
                    control+'pending_turn_snapshot.authority.json')
        if any(name.casefold()==node or name.casefold().startswith(node+'/') for name in names for node in host_nodes):
            raise ValueError('Own Save must not contain runtime payload')
        if names.count('save_manifest.json')!=1 or names.count('config.json')!=1 or archive.read('config.json')!=config:
            raise ValueError('Own Save manifest/config absent or changed')
        manifest_raw=archive.read('save_manifest.json');manifest=strict_settings(manifest_raw)
        if (set(manifest)!={'schemaVersion','algorithm','entries'} or type(manifest['schemaVersion']) is not int
            or manifest['schemaVersion']!=1 or manifest['algorithm']!='SHA-256' or type(manifest['entries']) is not list):
            raise ValueError('Own Save full manifest schema differs')
        seen=set()
        for row in manifest['entries']:
            if type(row) is not dict or set(row)!={'path','length','sha256'}:raise ValueError('Own Save manifest entry differs')
            path=row['path']
            if (type(path) is not str or path=='save_manifest.json' or path.casefold() in seen or names.count(path)!=1
                or type(row['length']) is not int or row['length']<0 or type(row['sha256']) is not str
                or re.fullmatch('[0-9a-fA-F]{64}',row['sha256']) is None):raise ValueError('Own Save manifest path/value differs')
            seen.add(path.casefold());payload=archive.read(path)
            if len(payload)!=row['length'] or sha(payload)!=row['sha256'].lower():raise ValueError('Own Save payload length/hash differs')
        if seen!={name.casefold() for name in names if name!='save_manifest.json'}:
            raise ValueError('Own Save manifest coverage differs')
    return {'ManifestVerified':True,'ConfigBytesExact':True,'RuntimeAbsent':True,'ConfigSHA256':sha(config),
        'ManifestRawHex':manifest_raw.hex(),'ManifestLength':len(manifest_raw),'ManifestSHA256':sha(manifest_raw),'ManifestEntries':len(seen)}


def require_self_save_launch(out, seed_path, seed_raw, action_raw, receipt_helper, profile, previous, optimized):
    """Independent binding of the closed initial route before operational side effects."""
    out=_restored_plain_path(Path(out),True);root=out.parent
    if (out.name!='p01' or Path(seed_path)!=root/'seed-manifest.json' or Path(receipt_helper)!=root/'receipt-helper/BookOfEternityClient.TestSupport.dll'
        or previous is not None or type(optimized) is not int or optimized!=0):raise ValueError('Self-Save initial phase/helper/optimization differs')
    if (profile.get('Route')!='self-save-load-turn' or type(profile.get('ExpectedTurn')) is not int or profile['ExpectedTurn']!=42
        or profile.get('PreviousResult','missing') is not None or profile.get('PreviousSHA256','missing') is not None
        or profile.get('SeedSHA256')!=sha(seed_raw) or profile.get('ActionSHA256')!=sha(action_raw)):
        raise ValueError('Self-Save launch profile does not bind this seed/action')
    seed=strict_settings(seed_raw)
    if seed.get('kind')!='seeded-live-mortal' or seed.get('basePath')!=str(root/'play'):
        raise ValueError('Self-Save seed is not this fresh initial root')


def require_self_save_turn_status(value, status, require_ready=False):
    """Fresh B main, full terminal tuple and nonempty input binding before any dispatch."""
    expected=value['LoadObservation']['observation']['ReplacementFinish'];terminal=expected['TerminalIdentity']
    current=status.get('status');wanted=value['TurnRunningStatus']['status']
    if (status.get('ok') is not True or type(current) is not dict or current.get('terminalRunId')!=expected['MainIdentity']['RunId']
        or type(current.get('inputBindingId')) is not str or not current['inputBindingId']
        or current['inputBindingId']!=wanted.get('inputBindingId') or current['inputBindingId']==value['OriginalRunningStatus']['status'].get('inputBindingId')
        or type(current.get('backend')) is not str or current['backend']!=terminal['Backend']
        or type(current.get('terminalGuarantee')) is not str or current['terminalGuarantee']!=terminal['Guarantee']
        or type(current.get('shellPid')) is not int or current['shellPid']!=terminal['RootPid']
        or current.get('terminalUncertain') is not False or current.get('terminalOwnerRetained') is not True
        or (require_ready and current.get('ready') is not True)):
        raise ValueError('Fresh self-Save B terminal/input tuple differs')
    delivery=current.get('promptDelivery')
    if delivery is not None and (type(delivery) is not dict or delivery.get('inputBindingId')!=current['inputBindingId']):
        raise ValueError('Self-Save B status carries foreign dispatch binding')


def checked_mortal_story_append(before,after,request):
    if not after.startswith(before):raise ValueError('Prior story bytes changed')
    rows=[json.loads(row) for row in after[len(before):].decode('utf-8-sig').splitlines() if row.strip()]
    if len(rows)!=1:raise ValueError('Expected exactly one appended story row')
    row=rows[0]
    if row.get('turn')!=request['turnNumber'] or row.get('realm')!='Mortal World' or row.get('player')!=request['playerAction'] or not row.get('narrative','').strip():
        raise ValueError('Story does not match actual request')
    if 'requestId' in row and row['requestId']!=request['requestId']:raise ValueError('Conflicting optional story identity')
    return row


def require_self_save_queue(queue, request_bytes, *, complete=False, closed=False):
    """Exactly one original turn queue, never a repair program or extra directory."""
    queue=_restored_plain_path(Path(queue),True);request=strict_settings(request_bytes)
    identity={key:request[key] for key in ('sessionId','requestId','turnNumber')}
    directories=[];allowed={'.execution.lock'}|({'close-request.json','closed.json'} if closed else set())
    for path in queue.iterdir():
        if path.is_symlink():raise ValueError('Linked self-Save queue entry')
        if path.is_dir():
            if re.fullmatch('request-[1-9][0-9]*-[0-9a-f]{32}',path.name) is None:raise ValueError('Unexpected self-Save queue directory')
            directories.append(_restored_plain_path(path,True))
        elif path.name not in allowed:raise ValueError('Unexpected self-Save queue file')
        else:_restored_plain_path(path)
    if len(directories)>1 or (complete and len(directories)!=1):raise ValueError('Self-Save route requires exactly one turn request, no repair')
    rows=[]
    for path in directories:
        metadata=path/'request.json'
        if not metadata.exists() and not metadata.is_symlink():
            if complete:raise ValueError('Self-Save turn metadata incomplete')
            continue
        value=strict_settings(_restored_plain_path(metadata).read_bytes())
        if value.get('Kind')!='turn' or value.get('TurnIdentity')!=identity or value.get('TurnRequestSHA256')!=sha(request_bytes):
            raise ValueError('Self-Save queue is repair/foreign turn')
        rows.append({'QueueId':path.name,'Kind':'turn','TurnRequestSHA256':sha(request_bytes)})
    return rows
