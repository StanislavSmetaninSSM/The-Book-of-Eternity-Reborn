"""Inert regression of actual phase acceptance functions; no game/process launch."""
import ast, copy, hashlib, json, pathlib, sys, tempfile
source=pathlib.Path(sys.argv[1]);scenario=sys.argv[2]
ns={'json':json,'hashlib':hashlib,'Path':pathlib.Path,'re':__import__('re')}
tree=ast.parse(source.read_text())
functions=[n for n in tree.body if isinstance(n,(ast.FunctionDef,ast.AsyncFunctionDef))]
exec(compile(ast.Module(body=functions,type_ignores=[]),str(source),'exec'),ns)
with tempfile.TemporaryDirectory() as temporary:
    session=str(pathlib.Path(temporary)/'game_session');owner='a'*32
    running={'status':'running','sessionPath':session,'daemonGuardVersion':1,'daemonGuardOwnerId':owner,'terminalSettled':False,'errorCount':2}
    guard={'version':1,'sessionPath':session,'ownerId':owner,'settled':False}
    final=dict(running,status='stopped',terminalSettled=True)
    finalguard=dict(guard,settled=True)
    raw={k:json.dumps(v).encode() for k,v in [('OriginalStatus',running),('OriginalGuard',guard),('FinalStatus',final),('FinalGuard',finalguard)]}
    if scenario=='wrong-owner':raw['FinalGuard']=json.dumps(dict(finalguard,ownerId='b'*32)).encode()
    if scenario=='wrong-session':raw['FinalStatus']=json.dumps(dict(final,sessionPath=session+'-other')).encode()
    if scenario=='missing':raw.pop('FinalGuard')
    if scenario=='malformed':raw['FinalStatus']=b'{'
    if scenario=='null':raw['FinalStatus']=b'null'
    if scenario=='duplicates':raw['FinalStatus']=b'{"status":"running",'+raw['FinalStatus'][1:]
    if scenario=='running':raw['FinalStatus']=json.dumps(dict(final,status='running')).encode()
    if scenario=='false':raw['FinalGuard']=json.dumps(dict(finalguard,settled=False)).encode()
    if scenario=='wrong-type':raw['FinalStatus']=json.dumps(dict(final,daemonGuardVersion=True)).encode()
    if scenario=='original-settled':raw['OriginalGuard']=json.dumps(finalguard).encode()
    evidence={'SessionPath':session,**{k+'RawHex':v.hex() for k,v in raw.items()},**{k+'SHA256':hashlib.sha256(v).hexdigest() for k,v in raw.items()}}
    if scenario=='hash-tamper':evidence['FinalGuardSHA256']='0'*64
    identity={'RunId':'run'}
    terminal={'ExitCode':0,'EOF':True,'TermiosRestored':True,'StreamEOF':{'output':True}}
    status={'terminalRunId':'run','inputBindingId':'binding'}
    ack=dict(status,terminalStop={'identity':{'runId':'run'},'state':'stopped-within-scope','cleanupComplete':True,'authorityRetained':False})
    value={'Success':True,'AcceptedGameTurns':1,'GenuineActionsSent':1,'ShutdownAttempts':1,'InputQualified':True,'LogicalLifecycleVerified':True,'RelayExecutionClosed':True,
      'RpcHelperClosure':{'ExitCode':0,'StdoutEof':True,'StderrEof':True},'Terminals':{name:terminal for name in ['client','bridge','daemon']},
      'OriginalRunningRecord':{'Identity':identity,'Disposition':'Running'},'StoppedRecord':{'Identity':identity,'Disposition':'Stopped','StopEvidence':{'Identity':identity,'Kind':'OwnedScopeEmpty'}},
      'OriginalRunningStatus':{'ok':True,'status':status},'ShutdownReceipt':{'ok':True,'status':ack},'DaemonSettlement':evidence,
      'SeedSessionId':'s','BasePath':temporary,'AcceptedTurnNumber':42}
    if scenario=='lying-success':value.pop('DaemonSettlement')
    if scenario.startswith('continuation-'):
        control=pathlib.Path(session)/'game_state/control';control.mkdir(parents=True)
        locks=pathlib.Path(temporary)/'.boe_runtime/locks';locks.mkdir(parents=True)
        changed=dict(final)
        if scenario=='continuation-running':changed['status']='running'
        if scenario=='continuation-owner':changed['daemonGuardOwnerId']='b'*32
        (control/'gm_daemon_status.json').write_text(json.dumps(changed))
        current_guard=json.dumps(dict(finalguard,ownerId='b'*32)).encode() if scenario=='continuation-guard' else raw['FinalGuard']
        (locks/'gm-daemon-instance.lock').write_bytes(current_guard)
        seed={'kind':'seeded-live-mortal','acceptedGmTurn':False,'setupTurn':41,'sessionId':'s','basePath':temporary}
        guardian={'driverExitCode':0,'echild':True,'emergencySignals':0,'failures':0,'deadline':False}
        if scenario=='continuation-valid':
            assert ns['phase_admission'](seed,value,42,guardian)==43, 'Unchanged settled continuation refused'
        else:
            try:ns['phase_admission'](seed,value,42,guardian)
            except ValueError:pass
            else:raise AssertionError('Lying saved Success admitted changed current daemon pair')
    else:
        actual=ns['phase_cleanup_proved'](value)
        assert actual==(scenario=='valid'),f'Actual phase acceptance {actual}, expected {scenario=="valid"}: {scenario}' 
print('PASS inert daemon settlement '+scenario)
