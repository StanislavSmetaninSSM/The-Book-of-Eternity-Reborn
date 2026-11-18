"""#1589 source-only relay lifecycle/continuation contract; no process or game startup."""
import ast, copy, hashlib, json, pathlib, re, tempfile, unittest
from pathlib import Path
SOURCE=Path(__file__).with_name('live-game-one-turn.py')
ns=dict(Path=Path,json=json,hashlib=hashlib,re=re)
module=ast.parse(SOURCE.read_text())
exec(compile(ast.Module(body=[n for n in module.body if isinstance(n,ast.FunctionDef)],type_ignores=[]),str(SOURCE),'exec'),ns)

def valid():
    identity={'RunId':'main','RootKey':'/own','GenerationId':'gen','Epoch':1}
    proof={'SessionPath':'/own/game_session'}
    for prefix,settled in [('Original',False),('Final',True)]:
        status=dict(status='stopped' if settled else 'running',sessionPath='/own/game_session',daemonGuardVersion=1,daemonGuardOwnerId='a'*32,terminalSettled=settled)
        guard=dict(version=1,sessionPath='/own/game_session',ownerId='a'*32,settled=settled)
        for kind,row in [('Status',status),('Guard',guard)]:
            raw=json.dumps(row).encode();proof[prefix+kind+'RawHex']=raw.hex();proof[prefix+kind+'SHA256']=hashlib.sha256(raw).hexdigest()
    return dict(Success=True,AcceptedGameTurns=1,GenuineActionsSent=1,ShutdownAttempts=1,InputQualified=True,
        LogicalLifecycleVerified=True,RelayExecutionClosed=True,DaemonSettlement=proof,
        RpcHelperClosure=dict(ExitCode=0,StdoutEof=True,StderrEof=True),
        Terminals={k:dict(ExitCode=0,EOF=True,TermiosRestored=True,StreamEOF={'raw':True}) for k in ('client','bridge','daemon')},
        OriginalRunningRecord=dict(Disposition='Running',Identity=identity),
        StoppedRecord=dict(Disposition='Stopped',Identity=identity,StopEvidence=dict(Identity=identity,Kind='OwnedScopeEmpty')),
        OriginalRunningStatus=dict(ok=True,status=dict(terminalRunId='main',inputBindingId='binding')),
        ShutdownReceipt=dict(ok=True,status=dict(terminalRunId='main',inputBindingId='binding',terminalStop=dict(identity=dict(runId='main'),state='stopped-within-scope',cleanupComplete=True,authorityRetained=False))))

class RelayPhaseContractTests(unittest.TestCase):
    def test_actual_driver_success_assignment_rejects_missing_pair(self):
        assignments=[n for n in ast.walk(module) if isinstance(n,ast.Assign) and any(
            isinstance(t,ast.Subscript) and isinstance(t.value,ast.Name) and t.value.id=='result' and
            isinstance(t.slice,ast.Constant) and t.slice.value=='Success' for t in n.targets)]
        self.assertEqual(len(assignments),1)
        value=valid();value.pop('DaemonSettlement')
        scope=dict(ns,result=value,relay_mode=True)
        observed=eval(compile(ast.Expression(assignments[0].value),str(SOURCE),'eval'),scope)
        self.assertFalse(observed, 'Actual driver accepted EOF/exit without original daemon settlement')

    def test_actual_startup_predicate_rejects_predecessor_status(self):
        calls=[n for n in ast.walk(module) if isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=='until' and len(n.args)>2 and isinstance(n.args[2],ast.Constant) and n.args[2].value=='original Running and published transport']
        self.assertEqual(len(calls),1)
        class Existing:
            def __init__(self,name):self.name=name
            def exists(self):return True
        record=Existing('record');status=Existing('status')
        scope=dict(ns,record_path=record,status_path=status,pipe='new-pipe',previous={'StoppedRecord':{'Identity':{'RunId':'old'}}},
            read_json=lambda path: {'Disposition':'Running','Identity':{'RunId':'new'}} if path is record else {'terminalRunId':'old','pipeName':'old-pipe','transportEndpoint':{'kind':'linux-fifo-v1','address':'old'}})
        predicate=eval(compile(ast.Expression(calls[0].args[0]),str(SOURCE),'eval'),scope)
        self.assertFalse(predicate(), 'Actual startup admitted old status alongside new main record')
    def test_actual_forensic_failure_preserves_observed_normal_exit(self):
        blocks=[n for n in ast.walk(module) if isinstance(n,ast.If) and isinstance(n.test,ast.Name) and n.test.id=='accepted']
        self.assertEqual(len(blocks),1)
        class Absent:
            def __truediv__(self,_):return self
            def exists(self):return False
        class Story:
            def read_bytes(self):return b'row'
        seen=[]
        def fail(*args):raise ValueError('injected forensic mismatch')
        scope=dict(ns,accepted=[{'narrative':'answer'}],relay_mode=True,story=Story(),story_before=b'',request={},session=Absent(),
            client=None,offset=0,fresh_player=lambda *args:seen.append('fresh'),client_wait=True,client_prompt=False,
            checked_relay_story_append=fail,result={'AcceptedGameTurns':0})
        with self.assertRaises(ValueError):exec(compile(ast.Module(body=blocks[0].body[:-1],type_ignores=[]),str(SOURCE),'exec'),scope)
        self.assertEqual(seen,['fresh'])
        self.assertFalse(scope['client_wait']);self.assertTrue(scope['client_prompt'])
        self.assertEqual(scope['result']['AcceptedGameTurns'],0)
    def test_old_exit_eof_success_is_not_settlement(self):
        value=valid();value.pop('DaemonSettlement')
        self.assertFalse(ns['relay_phase_cleanup_proved'](value))
    def test_exact_cleanup_and_negatives(self):
        self.assertTrue(ns['relay_phase_cleanup_proved'](valid()))
        for key in ('InputQualified','LogicalLifecycleVerified','RelayExecutionClosed'):
            value=valid();value[key]=False;self.assertFalse(ns['relay_phase_cleanup_proved'](value))
        for peer in ('client','bridge','daemon'):
            value=valid();value['Terminals'][peer]['ExitCode']=1;self.assertFalse(ns['relay_phase_cleanup_proved'](value))
        value=valid();value['RpcHelperCleanupFailure']='not joined';self.assertFalse(ns['relay_phase_cleanup_proved'](value))
    def test_stale_wrong_typed_duplicate_daemon_pair_rejected(self):
        for defect in ('unsettled','owner','session','version','duplicate','extra'):
            value=valid();p=value['DaemonSettlement'];raw=bytes.fromhex(p['FinalGuardRawHex']);row=json.loads(raw)
            if defect=='unsettled':row['settled']=False
            if defect=='owner':row['ownerId']='b'*32
            if defect=='session':row['sessionPath']='/different'
            if defect=='version':row['version']=True
            if defect=='extra':row['extra']=1
            raw=json.dumps(row).encode()
            if defect=='duplicate':raw=raw[:-1]+b',"settled":true}'
            p['FinalGuardRawHex']=raw.hex();p['FinalGuardSHA256']=hashlib.sha256(raw).hexdigest()
            self.assertFalse(ns['relay_phase_cleanup_proved'](value),defect)
    def test_story_exact_prefix_one_row(self):
        request=dict(turnNumber=2,requestId='r',playerAction='question')
        row=dict(turn=2,realm='Chaos Sea',player='question',narrative='answer')
        before=b'{"turn":1}\n';after=before+json.dumps(row).encode()+b'\n'
        self.assertEqual(ns['checked_relay_story_append'](before,after,request),row)
        for wrong in (after.replace(before,b'{"turn":0}\n'),after+json.dumps(row).encode(),before):
            with self.assertRaises(ValueError):ns['checked_relay_story_append'](before,wrong,request)
    def test_continuation_rejects_incomplete_before_disk_access(self):
        for field in ('Success','InputQualified','RelayExecutionClosed'):
            p=valid();p[field]=False
            with self.assertRaises(ValueError):ns['relay_continuation_preflight'](Path('/not-a-world'),p,{})
    def test_unchanged_config_preserved(self):
        prior={'Unrelated':{'a':1},'gmworkerbridgeprofiles':[{'Name':'disabled','Enabled':False}],
               'GmCliInputProfile':{'PromptPrefix':'RELAY> '},'GmBridgePipeNameOverride':'old','GmCliLaunchCommand':'old','GmBridgeShellWorkingDirectory':'old'}
        got=ns['relay_continuation_settings'](json.dumps(prior).encode(),'newpipe','newcommand','/same')
        self.assertEqual(got['Unrelated'],prior['Unrelated']);self.assertEqual(got['gmworkerbridgeprofiles'],prior['gmworkerbridgeprofiles'])
        self.assertEqual(got['GmCliInputProfile'],prior['GmCliInputProfile'])
        with self.assertRaises(ValueError):ns['relay_continuation_settings'](b'{"x":1,"X":2}','p','c','s')

    def test_positive_continuation_then_mutation_refusals(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=Path(tmp);session=base/'game_session';session.mkdir()
            value=valid();value['BasePath']=str(base)
            proof=value['DaemonSettlement'];proof['SessionPath']=str(session)
            for prefix in ('Original','Final'):
                for kind in ('Status','Guard'):
                    key=prefix+kind;row=json.loads(bytes.fromhex(proof[key+'RawHex']));row['sessionPath']=str(session)
                    raw=json.dumps(row).encode();proof[key+'RawHex']=raw.hex();proof[key+'SHA256']=hashlib.sha256(raw).hexdigest()
            files={'game_state/control/gm_daemon_status.json':bytes.fromhex(proof['FinalStatusRawHex']),
                   '_runtime/client.dll':b'fixed binary','config.json':b'{"Unrelated":1}'}
            request=dict(sessionId='s',requestId='r',turnNumber=1,playerAction='question')
            row=dict(turn=1,realm='Chaos Sea',player='question',narrative='answer');files['stories/chaos_sea.jsonl']=json.dumps(row).encode()+b'\n'
            for relative,raw in files.items():
                path=session/relative;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
            for relative,raw in [('.boe_runtime/locks/gm-daemon-instance.lock',bytes.fromhex(proof['FinalGuardRawHex'])),('.boe_runtime/gm-runs/main.json',json.dumps(value['StoppedRecord']).encode())]:
                path=base/relative;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
            value.update(ActualRequest=request,AcceptedStory=row,StoryPath=str(session/'stories/chaos_sea.jsonl'),StoryAfterSHA256=ns['sha'](files['stories/chaos_sea.jsonl']),
                RuntimePins=ns['relay_inventory'](session/'_runtime'),FinalWorldPins=ns['relay_world_inventory'](session),FinalConfigSHA256=ns['sha'](files['config.json']))
            guardian=dict(driverExitCode=0,echild=True,emergencySignals=0,failures=0,deadline=False)
            self.assertEqual(ns['relay_continuation_preflight'](base,value,guardian),2)
            for relative in files:
                path=session/relative;original=path.read_bytes();path.write_bytes(original+b' ')
                with self.assertRaises(ValueError):ns['relay_continuation_preflight'](base,value,guardian)
                path.write_bytes(original)
            for key,wrong in [('echild',False),('emergencySignals',1),('driverExitCode',1),('deadline',True)]:
                with self.assertRaises(ValueError):ns['relay_continuation_preflight'](base,value,dict(guardian,**{key:wrong}))
            pending=session/'input/turn_request.json';pending.parent.mkdir();pending.write_text('{}')
            value['FinalWorldPins']=ns['relay_world_inventory'](session)
            with self.assertRaises(ValueError):ns['relay_continuation_preflight'](base,value,guardian)
    def test_actual_turn_repair_queue_and_execution_joins(self):
        with tempfile.TemporaryDirectory() as tmp:
            queue=Path(tmp);session=queue/'session'
            request=dict(sessionId='s',requestId='r',turnNumber=2,playerAction='question');raw=json.dumps(request).encode()
            identity={k:request[k] for k in ('sessionId','requestId','turnNumber')}
            for index,kind in enumerate(('turn','repair'),1):
                folder=queue/f'request-{index}-inert';folder.mkdir()
                source=raw if kind=='turn' else json.dumps(dict(identity,revalidationAttempt=2,issues=['repair'])).encode()
                sourcepath='input/turn_request.json' if kind=='turn' else 'game_state/control/validation_repair_request.json'
                prompt=(kind+' prompt').encode();response=json.dumps({'Writes':[{'Path':'output/narrative_response.json','Data':{'response':kind+' answer'}}]}).encode()
                meta=dict(QueueId=folder.name,Kind=kind,SessionPath=str(session),TurnIdentity=identity,TurnRequestSHA256=ns['sha'](raw),RequestSHA256=ns['sha'](source),PromptSHA256=ns['sha'](prompt),RequestPath=sourcepath,Witnesses={sourcepath:ns['sha'](source),'input/turn_request.json':ns['sha'](raw)})
                dispatch=dict(disposition='submission-observed',inputBindingId='binding',contentHash=ns['sha'](b'1\n'+prompt).upper(),operationKind=kind,operationRevision='live' if kind=='turn' else '2')
                for name,data in [('game-request.json',source),('prompt.txt',prompt),('executing-response.json',response)]: (folder/name).write_bytes(data)
                for name,data in [('request.json',meta),('dispatch.json',dispatch),('reply.json',dict(QueueId=folder.name,PromptSHA256=ns['sha'](prompt),RequestSHA256=ns['sha'](source),TurnRequestSHA256=ns['sha'](raw),ResponseSHA256=ns['sha'](response))),('started.json',dict(ResponseSHA256=ns['sha'](response))),('execution.json',dict(Executed=True,ExitCode=0,ChildExited=True,IoDrained=True))]: (folder/name).write_text(json.dumps(data))
            self.assertEqual(len(ns['relay_response_evidence'](queue,raw,'binding',session,'repair answer')),2)
            for changed in ('wrong narrative','turn answer'):
                with self.assertRaises(ValueError):ns['relay_response_evidence'](queue,raw,'binding',session,changed)
            with self.assertRaises(ValueError):ns['relay_response_evidence'](queue,raw,'foreign',session,'repair answer')
            target=folder/'execution.json';target.write_text(json.dumps(dict(Executed=True,ExitCode=0,ChildExited=True,IoDrained=False)))
            with self.assertRaises(ValueError):ns['relay_response_evidence'](queue,raw,'binding',session,'repair answer')
            target.unlink()
            with self.assertRaises(FileNotFoundError):ns['relay_response_evidence'](queue,raw,'binding',session,'repair answer')

if __name__=='__main__':unittest.main()
