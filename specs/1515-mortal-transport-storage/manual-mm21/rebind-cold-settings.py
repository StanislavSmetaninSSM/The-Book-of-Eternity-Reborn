"""Closed operational settings helpers. No launcher, game, or authority creation."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import stat
import uuid

HERE=Path(__file__).resolve().parent

def load_module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    return module

_evidence=load_module('rebind_evidence',HERE.parents[2]/'tests/fixtures/ProductionMain/relay_phase_evidence.py')
_cold=load_module('rebind_cold',HERE/'run-cold-load.py')

def exact_paths(root,phase):
    root=Path(root);phase=Path(phase)
    if not root.is_absolute() or root!=root.resolve() or phase!=root/'recovered-live' or phase!=phase.resolve():
        raise ValueError('Noncanonical closed rebind paths')
    return root,phase

def build_closed_settings_patch(before,expected_sha,root,phase,pipe):
    root,phase=exact_paths(root,phase)
    if hashlib.sha256(before).hexdigest()!=expected_sha:raise ValueError('Config pin mismatch')
    if not isinstance(pipe,str) or not re.fullmatch(r'og-[0-9a-f]{12}',pipe):raise ValueError('Invalid private pipe name')
    original=_evidence.strict_settings(before);settings=dict(original)
    def quote(value):return "'"+str(value).replace("'","''")+"'"
    session=root/'play/game_session'
    command="& '/usr/bin/python3' "+quote(phase/'relay/relay_cli.py')+' --session '+quote(session)+' --queue '+quote(phase/'queue')+' --model dots-finish-relay'
    changes={'GmBridgePipeNameOverride':pipe,'GmCliLaunchCommand':command,'GmBridgeShellWorkingDirectory':str(session)}
    for key,value in changes.items():_evidence.put_setting(settings,key,value)
    changed={key.casefold() for key in original.keys()|settings.keys() if original.get(key)!=settings.get(key)}
    if changed!={key.casefold() for key in changes}:raise ValueError('Expected exactly three operational changes')
    return (json.dumps(settings,ensure_ascii=False,indent=2)+'\n').encode('utf-8')

def require_closed_rebind(root,phase,guardian,probe):
    root,phase=exact_paths(root,phase)
    if not _cold.cold_run_succeeded(0,guardian,probe):raise ValueError('Cold route not normally closed')
    if os.path.lexists(phase):raise ValueError('Phase must be absent; no reuse')
    play=root/'play';session=play/'game_session'
    forbidden=[play/'.boe_runtime/gm-runs/main.json',session/'game_state/control/gm_bridge_status.json',session/'input',session/'ready',session/'game_state/control/pending_turn_snapshot.json',session/'game_state/control/pending_turn_snapshot.authority.json',session/'game_state/control/pending_turn_snapshot',session/'game_state/control/validation_repair_request.json']
    for path in forbidden:
        if not os.path.lexists(path):continue
        mode=path.lstat()
        if stat.S_ISDIR(mode.st_mode) and path in forbidden[2:4] and not any(path.iterdir()):continue
        raise ValueError('Active or pending artifact: '+str(path))

def write_closed_settings_patch(path,before_sha,next_bytes):
    """Same exclusive-temp/fsync/replace pattern as the existing phase preparer.
    Caller binds full retained inventories and exact semantic diff before this write.
    Failed partials remain; there is no retry or cleanup repair.
    """
    path=Path(path)
    if path!=path.resolve():raise ValueError('Aliased config path')
    mode=path.lstat()
    if not stat.S_ISREG(mode.st_mode) or mode.st_nlink!=1:raise ValueError('Linked/special config')
    if hashlib.sha256(path.read_bytes()).hexdigest()!=before_sha:raise ValueError('Config changed')
    _evidence.strict_settings(next_bytes)
    temporary=path.with_name('config.phase-'+uuid.uuid4().hex+'.tmp')
    fd=os.open(temporary,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
    with os.fdopen(fd,'wb') as stream:
        stream.write(next_bytes);stream.flush();os.fsync(stream.fileno())
    if hashlib.sha256(path.read_bytes()).hexdigest()!=before_sha:raise ValueError('Config changed before replacement')
    os.replace(temporary,path)
