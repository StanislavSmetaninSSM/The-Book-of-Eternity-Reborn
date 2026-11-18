"""Independent inert Load observation DATA controls, not native ownership proof."""
import copy, json, unittest, tempfile, sys
from pathlib import Path
import relay_phase_evidence as evidence

def fixture():
    a=dict(RootKey='/owned',RunId='a'*32,GenerationId='1'*32,Epoch=1,Backend=2,HostInstanceId='c'*32,BootId='boot')
    b=dict(a,RunId='b'*32,GenerationId='2'*32,Epoch=2)
    ta=dict(RunId=a['RunId'],Backend='LinuxSupervisor',Guarantee='owned',RootPid=101)
    tb=dict(ta,RunId=b['RunId'],RootPid=102)
    expected=dict(Nonce='d'*32,SourceKey='/owned/save.zip',RootKey='/owned',ExpectedGeneration=a['GenerationId'],StartupIdentityA=a,StartupTerminalA=ta)
    operation='e'*32
    def receipt(state,main,terminal):return dict(Ok=True,OperationId=operation,State=state,MainIdentity=main,TerminalIdentity=terminal,Error=None)
    document=dict(kind='boe-load-session-completed',nonce=expected['Nonce'],observation=dict(OperationId=operation,
        SourceKey=expected['SourceKey'],RootKey='/owned',ExpectedGeneration=a['GenerationId'],OriginalStop=receipt('Stopped',a,ta),
        ReplacementFinish=receipt('Running',b,tb),Committed=True,RefreshConfirmed=True,EstablishedGeneration=b['GenerationId']))
    installed=dict(Disposition='Running',Identity=b)
    expected=copy.deepcopy(expected);installed=copy.deepcopy(installed)
    expected['StartupIdentityA']['Backend']='LinuxSupervisor'
    installed['Identity']['Backend']='LinuxSupervisor'
    return expected,copy.deepcopy(document),installed

def wire(value):return json.dumps(value,separators=(',',':')).encode()+b'\n'

def daemon(owner):
    proof={'SessionPath':'/owned/game_session'}
    for prefix,settled in [('Original',False),('Final',True)]:
        status=dict(pid=301,startedAtUtc='2026-11-18T10:00:00Z',heartbeatAtUtc='2026-11-18T10:00:01Z',daemonGuardOwnerId=owner,daemonGuardVersion=1,sessionPath=proof['SessionPath'],status='stopped' if settled else 'running',terminalSettled=settled)
        guard=dict(version=1,sessionPath=proof['SessionPath'],ownerId=owner,settled=settled)
        for name,value in [('Status',status),('Guard',guard)]:
            raw=wire(value);proof[prefix+name+'RawHex']=raw.hex();proof[prefix+name+'SHA256']=evidence.sha(raw)
    return proof

def cleanup(expected,document,installed):
    a=expected['StartupIdentityA'];b=installed['Identity'];t=document['observation']['ReplacementFinish']['TerminalIdentity']
    status=lambda run:dict(ok=True,status=dict(terminalRunId=run,inputBindingId='binding'))
    value=dict(AcceptedGameTurns=0,GenuineActionsSent=0,ShutdownAttempts=1,InputQualified=True,LogicalLifecycleVerified=True,RelayExecutionClosed=True,
        RpcHelperClosure=dict(ExitCode=0,StdoutEof=True,StderrEof=True),
        Terminals={k:dict(ExitCode=0,EOF=True,TermiosRestored=True,StreamEOF=dict(stdout=True,stderr=True)) for k in ('client','bridge','daemon')},
        DaemonSettlement=daemon('3'*32),StartupIdentityA=a,ShutdownIdentityB=copy.deepcopy(document['observation']['ReplacementFinish']['MainIdentity']),
        OriginalRunningRecord=dict(Disposition='Running',Identity=a),OriginalRunningStatus=status(a['RunId']),
        LoadRunningRecord=installed,LoadRunningStatus=status(b['RunId']),StoppedRecord=dict(Disposition='Stopped',Identity=b,StopEvidence=dict(Identity=b,Kind='OwnedScopeEmpty')))
    continuity=dict(SessionPath='/owned/game_session',LoadActionUtc='2026-11-18T10:00:02Z',
        OriginalProcessIdentity=dict(Pid=301,StartTicks=1001),PostLoadProcessIdentity=dict(Pid=301,StartTicks=1001),
        OriginalLauncherIdentity=dict(Pid=300,StartTicks=1000),PostLoadLauncherIdentity=dict(Pid=300,StartTicks=1000),
        OriginalLauncherPoll=None,PostLoadLauncherPoll=None)
    for prefix in ('Original','PostLoad'):
        for name in ('Status','Guard'):
            raw=bytes.fromhex(value['DaemonSettlement']['Original'+name+'RawHex'])
            if prefix=='PostLoad' and name=='Status':
                data=json.loads(raw);data['heartbeatAtUtc']='2026-11-18T10:00:03Z';raw=wire(data)
            continuity[prefix+name+'RawHex']=raw.hex();continuity[prefix+name+'SHA256']=evidence.sha(raw)
    value['PostLoadDaemonContinuity']=continuity
    value['LocalRouteProof']=dict(Route='storage-local-proof',Target=dict(LocationId='location',StorageId='storage',ItemId='item',StorageLabel='storage',StorageName='storage',ItemLabel='item',ContentsCount=1),
        Before=dict(StorySHA256='a'*64,HistorySHA256='b'*64,QueuePins={}),After=dict(StorySHA256='a'*64,HistorySHA256='b'*64,QueuePins={}),
        Stages=[dict(Stage=name,ItemPosition=position,LocationSHA256=('d' if name=='Deposit' else 'c')*64,InventorySHA256=('f' if name=='Deposit' else 'e')*64)
            for name,position in [('Retrieve','inventory'),('Save','inventory'),('Deposit','storage'),('Load','inventory')]],
        SaveArchivePath=expected['SourceKey'],SaveArchiveSHA256='a'*64,RestoreBytesExact=True)
    value['FinalQueuePins']={'close-request.json':'c'*64,'closed.json':'d'*64}

    value['ShutdownReceipt']=status(b['RunId'])
    value['ShutdownReceipt']['status']['terminalStop']=dict(identity={k[0].lower()+k[1:]:v for k,v in t.items()},state='stopped-within-scope',cleanupComplete=True,authorityRetained=False)
    return value

class LoadEvidenceTests(unittest.TestCase):
    def setUp(self):self.expected,self.document,self.installed=fixture()
    def validate(self,raw=None,cut=0):return evidence.validated_load_observation(wire(self.document) if raw is None else raw,cut,self.expected,self.installed)
    def test_production_codec_backend_forms_join_without_mutating_raw(self):
        self.expected['StartupIdentityA']['Backend']='LinuxSupervisor'
        self.installed['Identity']['Backend']='LinuxSupervisor'
        before=copy.deepcopy((self.expected,self.document,self.installed))
        self.assertEqual(self.document,self.validate())
        self.assertEqual(before,(self.expected,self.document,self.installed))
    def test_production_codec_backend_forms_cleanup(self):
        self.expected['StartupIdentityA']['Backend']='LinuxSupervisor'
        self.installed['Identity']['Backend']='LinuxSupervisor'
        value=cleanup(self.expected,self.document,self.installed)
        value['ShutdownIdentityB']=copy.deepcopy(self.document['observation']['ReplacementFinish']['MainIdentity'])
        self.assertTrue(evidence.relay_load_phase_cleanup_proved(value,wire(self.document),0,self.expected))
    def test_backend_wire_forms_are_strict_not_generic_coercion(self):
        for field,value in [('durable',2),('durable','2'),('durable','linuxsupervisor'),('durable',True),('observation','LinuxSupervisor'),('observation','2'),('observation',True),('observation',0),('observation',3)]:
            before=copy.deepcopy((self.expected,self.document,self.installed))
            if field=='durable':self.expected['StartupIdentityA']['Backend']=value
            else:self.document['observation']['OriginalStop']['MainIdentity']['Backend']=value
            with self.subTest(field=field,value=value),self.assertRaises(ValueError):self.validate()
            self.expected,self.document,self.installed=before
    def test_complete_pair_after_fresh_cut(self):
        prior=wire(self.document)+b'old prompt '
        self.assertEqual(self.document,self.validate(prior+wire(self.document),len(prior)))
    def test_missing_stale_partial_duplicate(self):
        raw=wire(self.document)
        for value,cut in [(b'',0),(raw,len(raw)),(raw[:-1],0),(raw+raw,0),(raw,4)]:
            with self.subTest(value=value,cut=cut),self.assertRaises(ValueError):self.validate(value,cut)
    def test_foreign_preknown_binding(self):
        for field in ('Nonce','SourceKey','RootKey','ExpectedGeneration'):
            old=self.expected[field];self.expected[field]='f'*32
            with self.subTest(field=field),self.assertRaises(ValueError):self.validate()
            self.expected[field]=old
    def test_operation_id_is_internal_but_all_three_must_match(self):
        self.document['observation']['ReplacementFinish']['OperationId']='f'*32
        with self.assertRaises(ValueError):self.validate()
    def test_warning_partial_commit_and_lost_receipt(self):
        original=copy.deepcopy(self.document)
        for field,value in [('Committed',False),('RefreshConfirmed',False),('OriginalStop',None),('ReplacementFinish',None)]:
            self.document=copy.deepcopy(original);self.document['observation'][field]=value
            with self.subTest(field=field),self.assertRaises(ValueError):self.validate()
        self.document=original;self.document['observation']['ReplacementFinish']['State']='StartedNotReady'
        with self.assertRaises(ValueError):self.validate()
    def test_duplicate_json_and_non_json_constants(self):
        raw=wire(self.document)
        for changed in [raw.replace(b'"Committed":true',b'"Committed":true,"committed":true'),raw.replace(b'"Epoch":2',b'"Epoch":NaN')]:
            with self.assertRaises(ValueError):self.validate(changed)
    def test_strict_types(self):
        for path,value in [(('Committed',),1),(('OriginalStop','Ok'),1),(('ReplacementFinish','MainIdentity','Epoch'),True),(('ReplacementFinish','TerminalIdentity','RootPid'),True)]:
            original=copy.deepcopy(self.document);node=self.document['observation']
            for key in path[:-1]:node=node[key]
            node[path[-1]]=value
            with self.subTest(path=path),self.assertRaises(ValueError):self.validate()
            self.document=original
    def test_foreign_original_and_terminal(self):
        for target,key in [('MainIdentity','HostInstanceId'),('TerminalIdentity','RunId')]:
            original=copy.deepcopy(self.document);self.document['observation']['OriginalStop'][target][key]='f'*32
            with self.assertRaises(ValueError):self.validate()
            self.document=original
    def test_replacement_cannot_be_adopted_from_current_metadata(self):
        self.installed=copy.deepcopy(self.installed);self.installed['Identity']['RunId']='f'*32
        with self.assertRaises(ValueError):self.validate()
    def test_replacement_distinct_generation_and_run(self):
        for key in ('RunId','GenerationId','Epoch'):
            original=copy.deepcopy(self.document);self.document['observation']['ReplacementFinish']['MainIdentity'][key]=self.expected['StartupIdentityA'][key]
            with self.assertRaises(ValueError):self.validate()
            self.document=original
    def test_load_cleanup_preserves_A_and_proves_exact_B(self):
        value=cleanup(self.expected,self.document,self.installed);before=copy.deepcopy(value)
        self.assertTrue(evidence.relay_load_phase_cleanup_proved(value,wire(self.document),0,self.expected))
        self.assertEqual(before,value)
        self.assertFalse(evidence.relay_phase_cleanup_proved(value)) # A cannot stand in for B.
    def test_cleanup_rejects_A_stop_foreign_B_and_relabelled_daemon(self):
        original=cleanup(self.expected,self.document,self.installed)
        for mutation in ('A-stop','foreign-terminal','foreign-daemon','missing-B','not-settled'):
            value=copy.deepcopy(original)
            if mutation=='A-stop':value['StoppedRecord']['Identity']=self.expected['StartupIdentityA']
            elif mutation=='foreign-terminal':value['ShutdownReceipt']['status']['terminalStop']['identity']['rootPid']=999
            elif mutation=='foreign-daemon':value['DaemonSettlement']=daemon('5'*32)
            elif mutation=='missing-B':del value['ShutdownIdentityB']
            else:value['DaemonSettlement']['FinalStatusSHA256']='0'*64
            with self.subTest(mutation=mutation):self.assertFalse(evidence.relay_load_phase_cleanup_proved(value,wire(self.document),0,self.expected))
    def test_ordinary_no_load_contract_remains_unchanged(self):
        value=cleanup(self.expected,self.document,self.installed)
        value['AcceptedGameTurns']=1;value['GenuineActionsSent']=1
        value['OriginalRunningRecord']=value['LoadRunningRecord'];value['OriginalRunningStatus']=value['LoadRunningStatus']
        for key in ['LoadRunningRecord','LoadRunningStatus','PostLoadDaemonContinuity','StartupIdentityA','ShutdownIdentityB']:del value[key]
        self.assertTrue(evidence.relay_phase_cleanup_proved(value))

    def test_retained_daemon_positive_requires_real_continuity_implementation(self):
        value=cleanup(self.expected,self.document,self.installed)
        self.assertTrue(evidence.retained_daemon_load_continuity_proved(value['DaemonSettlement'],value['PostLoadDaemonContinuity']))
    def test_live_continuity_original_only_but_final_cleanup_still_requires_settlement(self):
        value=cleanup(self.expected,self.document,self.installed)
        original_only={key:item for key,item in value['DaemonSettlement'].items() if not key.startswith('Final')}
        self.assertTrue(evidence.retained_daemon_load_continuity_proved(original_only,value['PostLoadDaemonContinuity']))
        value['DaemonSettlement']=original_only
        self.assertFalse(evidence.relay_load_phase_cleanup_proved(value,wire(self.document),0,self.expected))
    def test_retained_daemon_missing_stale_replaced_or_exited_refuses(self):
        original=cleanup(self.expected,self.document,self.installed)
        for mutation in ('missing','pid-reused','launcher-replaced','launcher-exited','stale-heartbeat','foreign-owner','corrupt-raw'):
            value=copy.deepcopy(original);continuity=value['PostLoadDaemonContinuity']
            if mutation=='missing':continuity={}
            elif mutation=='pid-reused':continuity['PostLoadProcessIdentity']['StartTicks']+=1
            elif mutation=='launcher-replaced':continuity['PostLoadLauncherIdentity']['Pid']+=1
            elif mutation=='launcher-exited':continuity['PostLoadLauncherPoll']=0
            elif mutation=='corrupt-raw':continuity['PostLoadGuardSHA256']='0'*64
            else:
                status=json.loads(bytes.fromhex(continuity['PostLoadStatusRawHex']))
                if mutation=='stale-heartbeat':status['heartbeatAtUtc']=continuity['LoadActionUtc']
                else:status['daemonGuardOwnerId']='f'*32
                raw=wire(status);continuity['PostLoadStatusRawHex']=raw.hex();continuity['PostLoadStatusSHA256']=evidence.sha(raw)
            with self.subTest(mutation=mutation):self.assertFalse(evidence.retained_daemon_load_continuity_proved(value['DaemonSettlement'],continuity))

    def test_local_zero_GM_and_route_mutations_refuse(self):
        original=cleanup(self.expected,self.document,self.installed)
        for mutation in ('gm','story','queue','stage','restore','save-source','final-request'):
            value=copy.deepcopy(original);proof=value['LocalRouteProof']
            if mutation=='gm':value['AcceptedGameTurns']=1
            elif mutation=='story':proof['After']['StorySHA256']='0'*64
            elif mutation=='queue':proof['After']['QueuePins']['request-1.json']='0'*64
            elif mutation=='stage':proof['Stages'][3]['InventorySHA256']='0'*64
            elif mutation=='restore':proof['RestoreBytesExact']=1
            elif mutation=='save-source':proof['SaveArchivePath']='/foreign/save.zip'
            else:value['FinalQueuePins']['request-1.json']='0'*64
            with self.subTest(mutation=mutation):self.assertFalse(evidence.relay_load_phase_cleanup_proved(value,wire(self.document),0,self.expected))
    def test_ordinary_mechanical_negative_fences_preserved(self):
        value=cleanup(self.expected,self.document,self.installed)
        value.update(AcceptedGameTurns=1,GenuineActionsSent=1,OriginalRunningRecord=value['LoadRunningRecord'],OriginalRunningStatus=value['LoadRunningStatus'])
        for key,replacement in [('AcceptedGameTurns',True),('GenuineActionsSent',0),('InputQualified',False),('ShutdownAttempts',2),('LogicalLifecycleVerified',False),('RelayExecutionClosed',False),('RpcHelperClosure',{}),('Terminals',{}),('DaemonSettlement',{}),('StoppedRecord',{}),('ShutdownReceipt',{}),('CleanupFailure','failed')]:
            changed=copy.deepcopy(value);changed[key]=replacement
            with self.subTest(key=key):self.assertFalse(evidence.relay_phase_cleanup_proved(changed))
    def test_package_route_module_hash_required(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);ship=root/'runtime';ship.mkdir();(ship/'client').write_bytes(b'client')
            (root/'relay').mkdir();(root/'relay'/'source').write_bytes(b'relay')
            driver=root/'driver.py';driver.write_bytes(b'driver')
            manifest=dict(RuntimePins=evidence.relay_inventory(ship),RelayPins=evidence.relay_inventory(root/'relay'),DriverSHA256=evidence.sha(driver.read_bytes()),
                EvidenceModuleSHA256=evidence.sha(Path(evidence.__file__).read_bytes()),ConsoleRouteModuleSHA256=evidence.sha(Path(evidence.__file__).with_name('mortal_console_routes.py').read_bytes()))
            package=root/'relay-phase-package.json';package.write_text(json.dumps(manifest))
            self.assertEqual(manifest,evidence.relay_package_preflight(root,ship,driver)[0])
            manifest['ConsoleRouteModuleSHA256']='0'*64;package.write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError,'console route module pin'):evidence.relay_package_preflight(root,ship,driver)

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(LoadEvidenceTests)
    expected_count=22
    if suite.countTestCases()!=expected_count:raise SystemExit('Unexpected Load test count')
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    ok=result.testsRun==expected_count and result.wasSuccessful() and not result.skipped
    print(json.dumps({'Suite':'relay-load-evidence','Expected':expected_count,'Executed':result.testsRun,'Failures':len(result.failures),'Errors':len(result.errors),'Skipped':len(result.skipped),'Success':ok}))
    raise SystemExit(0 if ok else 1)

