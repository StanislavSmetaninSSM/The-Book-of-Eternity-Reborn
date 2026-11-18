"""Stateless phase evidence DATA. Never grants admission or owns a process/lease.

Immutable inventories are passed explicitly per phase; mutable world and terminal
proofs are freshly checked at boundaries. No cross-phase cache.
"""
import hashlib, json, re
from pathlib import Path

def rpc_helper_closed(result):
    proof=result.get('RpcHelperClosure')
    return isinstance(proof,dict) and type(proof.get('ExitCode')) is int and proof['ExitCode']==0 and proof.get('StdoutEof') is True and proof.get('StderrEof') is True and 'RpcHelperCleanupFailure' not in result

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
    try:return daemon_pair_decode(evidence,'Original',False)==daemon_pair_decode(evidence,'Final',True)
    except (KeyError,TypeError,ValueError,UnicodeError):return False

def relay_phase_cleanup_proved(value):
    try:
        if any(k.endswith('Failure') for k in value):return False
        if not daemon_settlement_proved(value.get('DaemonSettlement')):return False
        if any(type(value[k]) is not int or value[k]!=1 for k in ('AcceptedGameTurns','GenuineActionsSent','ShutdownAttempts')):return False
        if any(value[k] is not True for k in ('InputQualified','LogicalLifecycleVerified','RelayExecutionClosed')) or not rpc_helper_closed(value):return False
        terminals=value['Terminals']
        if set(terminals)!={'client','bridge','daemon'}:return False
        if not all(type(v['ExitCode']) is int and v['ExitCode']==0 and v['EOF'] is True and v['TermiosRestored'] is True and type(v['StreamEOF']) is dict and bool(v['StreamEOF']) and all(x is True for x in v['StreamEOF'].values()) for v in terminals.values()):return False
        running=value['OriginalRunningRecord'];stopped=value['StoppedRecord'];identity=running['Identity']
        if type(identity.get('Epoch')) is not int or identity['Epoch']<1:return False
        if running['Disposition']!='Running' or stopped['Disposition']!='Stopped' or stopped['Identity']!=identity:return False
        if stopped['StopEvidence']['Identity']!=identity or stopped['StopEvidence']['Kind']!='OwnedScopeEmpty':return False
        ack=value['ShutdownReceipt'];proof=ack['status']['terminalStop'];started=value['OriginalRunningStatus']
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
    return manifest,sha(raw)

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
