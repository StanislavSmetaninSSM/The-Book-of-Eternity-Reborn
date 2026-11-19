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


def relay_load_phase_cleanup_proved(value, client_output, action_cut, expected):
    """Separate Load-only cleanup join. Never overwrites startup A or adopts B from status."""
    try:
        observed=validated_load_observation(client_output,action_cut,expected,value['LoadRunningRecord'])['observation']
        a=observed['OriginalStop']['MainIdentity'];b=observed['ReplacementFinish']['MainIdentity']
        if _load_durable_identity(value['StartupIdentityA'])!=a or value['ShutdownIdentityB']!=b:return False
        if _load_durable_identity(value['OriginalRunningRecord']['Identity'])!=a or value['OriginalRunningRecord']['Disposition']!='Running':return False
        if value['OriginalRunningStatus']['ok'] is not True or value['OriginalRunningStatus']['status']['terminalRunId']!=a['RunId']:return False
        # Load replaces main A with B, not the retained root/session daemon.
        if not retained_daemon_load_continuity_proved(value['DaemonSettlement'],value['PostLoadDaemonContinuity']):return False
        terminal=observed['ReplacementFinish']['TerminalIdentity']
        if value['ShutdownReceipt']['status']['terminalStop']['identity']!={
            'runId':terminal['RunId'],'backend':terminal['Backend'],'guarantee':terminal['Guarantee'],'rootPid':terminal['RootPid']}:return False
        running=value['LoadRunningRecord'];status=value['LoadRunningStatus']
        if _load_durable_identity(running['Identity'])!=b or running['Disposition']!='Running':return False
        if status['ok'] is not True or status['status']['terminalRunId']!=b['RunId']:return False
        # The existing fixed stop proof is reused only against observed B. Original
        # caller evidence remains untouched; no process action occurs in this module.
        if any(type(value[k]) is not int or value[k]!=0 for k in ('AcceptedGameTurns','GenuineActionsSent')):return False
        if not local_load_route_proved(value['LocalRouteProof'],value['FinalQueuePins']):return False
        if value['LocalRouteProof']['SaveArchivePath']!=expected['SourceKey']:return False
        if not history_witness_matches_pins(value['LocalRouteProof']['After']['History'],value['FinalWorldPins']):return False
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
