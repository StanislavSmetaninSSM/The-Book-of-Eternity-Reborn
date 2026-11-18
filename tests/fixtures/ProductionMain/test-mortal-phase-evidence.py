"""Cheap source-path mutation tests. No game/native/helper process is executed."""
import ast, copy, hashlib, json, os, re, sys, tempfile, types, unittest
from pathlib import Path
import relay_phase_evidence as phase_evidence
SOURCE=Path(__file__).with_name('live-mortal-phase.py')
TREE=ast.parse(SOURCE.read_text())
NS=dict(phase_evidence=phase_evidence,__file__=str(SOURCE),Path=Path,json=json,hashlib=hashlib,re=re,os=os,sys=sys)
exec(compile(ast.Module(body=[n for n in TREE.body if isinstance(n,ast.FunctionDef)],type_ignores=[]),str(SOURCE),'exec'),NS)
# Test-only reuse of the existing positive lifecycle DATA builder, no runtime AST imports.
GENERAL=Path(__file__).with_name('test-relay-phase-contract.py')
VALID_NS=dict(json=json,hashlib=hashlib)
exec(compile(ast.Module(body=[n for n in ast.parse(GENERAL.read_text()).body if isinstance(n,ast.FunctionDef) and n.name=='valid'],type_ignores=[]),str(GENERAL),'exec'),VALID_NS)
def valid():return VALID_NS['valid']()
def inventory(root):return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(root.rglob('*')) if p.is_file()}
def assign_name(node,name):return isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id==name for t in node.targets)

class MortalEvidenceTests(unittest.TestCase):
    def test_valid_cleanup(self):self.assertTrue(NS['phase_cleanup_proved'](valid()))
    def test_actual_cleanup_rejects_bool_count(self):
        value=valid();value['AcceptedGameTurns']=True
        self.assertFalse(NS['phase_cleanup_proved'](value))
    def test_actual_cleanup_rejects_truthy_authority_flag(self):
        value=valid();value['LogicalLifecycleVerified']='true'
        self.assertFalse(NS['phase_cleanup_proved'](value))
    def startup(self,old_status=False,old_run=False,epoch=2,generation='gen',predicate_only=False):
        calls=[n for n in ast.walk(TREE) if isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=='until' and len(n.args)>2 and isinstance(n.args[2],ast.Constant) and n.args[2].value=='original Running and published transport']
        self.assertEqual(len(calls),1)
        class Existing:
            def exists(self):return True
        record_path=Existing();status_path=Existing()
        prior={'StoppedRecord':{'Identity':{'RunId':'old','RootKey':'/own','GenerationId':'gen','Epoch':1}}}
        record={'Disposition':'Running','Identity':{'RunId':'old' if old_run else 'new','RootKey':'/own','GenerationId':generation,'Epoch':epoch}}
        status={'transportEndpoint':{'kind':'linux-fifo-v1','address':'current'},'terminalRunId':'old' if old_status else record['Identity']['RunId'],'pipeName':'old-pipe' if old_status else 'new-pipe','terminalOwnerRetained':True,'terminalUncertain':False,'cliLaunchCommand':'cmd','shellWorkingDirectory':'/own/game_session'}
        scope=dict(NS,record_path=record_path,status_path=status_path,pipe='new-pipe',previous=prior,
            read_json=lambda path:record if path is record_path else status)
        predicate=eval(compile(ast.Expression(calls[0].args[0]),str(SOURCE),'eval'),scope)
        if predicate_only:return predicate()
        if not predicate():return False
        # Execute the actual post-predicate owner/RPC checks up to readiness polling.
        enclosing=next(n for n in TREE.body if isinstance(n,ast.Try) and any(isinstance(x,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='client' for t in x.targets) for x in n.body))
        start=next(i for i,n in enumerate(enclosing.body) if assign_name(n,'record'))
        end=next(i for i,n in enumerate(enclosing.body) if isinstance(n,ast.FunctionDef) and n.name=='ready')
        scope.update(rpc=lambda _:dict(ok=True,status=status),result={},command='cmd',session=Path('/own/game_session'))
        try:exec(compile(ast.Module(body=enclosing.body[start:end],type_ignores=[]),str(SOURCE),'exec'),scope)
        except (AssertionError,ValueError,RuntimeError):return False
        return True
    def test_actual_wait_does_not_return_on_stale_status(self):self.assertFalse(self.startup(old_status=True,predicate_only=True))
    def test_valid_new_owner(self):self.assertTrue(self.startup())
    def test_actual_startup_rejects_predecessor_status(self):self.assertFalse(self.startup(old_status=True))
    def test_actual_startup_rejects_same_run(self):self.assertFalse(self.startup(old_run=True))
    def test_actual_startup_rejects_nonincreasing_epoch(self):self.assertFalse(self.startup(epoch=1))
    def test_actual_startup_rejects_changed_generation(self):self.assertFalse(self.startup(generation='other'))

    def phase(self,mutation=None,continuation=False,closing=False,guardian_mutant=None,owner_mutant=None):
        with tempfile.TemporaryDirectory(prefix='me-',dir='/tmp') as tmp:
            root=Path(tmp);base=root/'world';session=base/'game_session';out=root/'p';out.mkdir()
            for name in ['_runtime','stories','game_state/control','game_state/world','lore']:(session/name).mkdir(parents=True,exist_ok=True)
            (session/'_runtime/client.dll').write_bytes(b'runtime')
            (session/'game_state/world/state.json').write_bytes(b'{}')
            (session/'config.json').write_bytes(b'{}')
            story=session/'stories/mortal_life_2.jsonl';row=dict(turn=41,realm='Mortal World',player='setup',narrative='setup')
            story.write_text(json.dumps(row)+'\n')
            helper=root/'helper';helper.mkdir();dll=helper/'evidence.dll';dll.write_bytes(b'dll')
            (helper/'evidence.deps.json').write_bytes(b'deps');(helper/'evidence.runtimeconfig.json').write_bytes(b'runtimeconfig');(helper/'dependency.dll').write_bytes(b'dependency')
            relay=out/'relay';relay.mkdir();(relay/'relay.py').write_bytes(b'relay')
            seed=dict(kind='seeded-live-mortal',acceptedGmTurn=False,setupTurn=41,sessionId='s',basePath=str(base),setupStoryEntrySha256=NS['sha'](story.read_bytes().splitlines()[0]))
            seed_path=root/'seed.json';seed_path.write_text(json.dumps(seed));action=root/'action.txt';action.write_text('Осмотреть берег')
            package=dict(RuntimePins=inventory(session/'_runtime'),RelayPins=inventory(relay),DriverSHA256=NS['sha'](SOURCE.read_bytes()),ReceiptHelperPins=inventory(helper))
            module=SOURCE.with_name('relay_phase_evidence.py')
            package['EvidenceModuleSHA256']=NS['sha'](module.read_bytes()) if module.exists() else 'not-created-before-green'
            (out/'relay-phase-package.json').write_text(json.dumps(package))
            if continuation:
                prior=valid();prior['OriginalRunningRecord']['Identity']['RootKey']=str(base)
                request=dict(sessionId='s',requestId='request42',turnNumber=42,playerAction='Осмотреть берег')
                accepted=dict(turn=42,realm='Mortal World',player=request['playerAction'],narrative='Берег осмотрен')
                story.write_bytes(story.read_bytes()+json.dumps(accepted).encode()+b'\n')
                history=session/'game_state/world/mortal_living_world_history.json';history.write_bytes(b'[]')
                request_bytes=json.dumps(request).encode()
                prior.update(SeedManifestSha256=NS['sha'](seed_path.read_bytes()),StoryPath=str(story),StoryAfterSHA256=NS['sha'](story.read_bytes()),ActualRequest=request,ActualRequestSHA256=NS['sha'](request_bytes),AcceptedStory=accepted,LivingWorldHistorySHA256=NS['sha'](history.read_bytes()),PhasePackage=package,
                    TypedReceiptEvidence=dict(valid=True,publicationAuthority=False,sessionId='s',requestId='request42',turn=42,priorCount=0,currentCount=1,snapshotToken='A'*64,declarationFingerprint='sha256:'+'b'*64,receiptFingerprint='sha256:'+'c'*64))
                owners=base/'.boe_runtime/gm-runs';owners.mkdir(parents=True);(owners/'main.json').write_text(json.dumps(prior['StoppedRecord']))
                prior.update(SeedSessionId='s',BasePath=str(base),AcceptedTurnNumber=42,RuntimePins=package['RuntimePins'],ReceiptHelperPins=package['ReceiptHelperPins'],FinalConfigSHA256=NS['sha']((session/'config.json').read_bytes()))
                proof=prior['DaemonSettlement'];proof['SessionPath']=str(session)
                for prefix,settled in [('Original',False),('Final',True)]:
                    for name in ['Status','Guard']:
                        raw=json.loads(bytes.fromhex(proof[prefix+name+'RawHex']));raw['sessionPath']=str(session);data=json.dumps(raw).encode();proof[prefix+name+'RawHex']=data.hex();proof[prefix+name+'SHA256']=NS['sha'](data)
                (session/'game_state/control/gm_daemon_status.json').write_bytes(bytes.fromhex(proof['FinalStatusRawHex']))
                guard=base/'.boe_runtime/locks';guard.mkdir(parents=True);(guard/'gm-daemon-instance.lock').write_bytes(bytes.fromhex(proof['FinalGuardRawHex']))
                prior['FinalWorldPins']={}
                for name in ('game_state','stories','lore','input','output','ready'):
                    prior['FinalWorldPins'].update({name+'/'+k:v for k,v in inventory(session/name).items()})
                if mutation:(session/mutation).write_bytes(b'mutated')
                guardian=dict(driverExitCode=0,echild=True,emergencySignals=0,failures=0,deadline=False)
                if guardian_mutant:guardian.update(guardian_mutant)
                predecessor=root/'previous';predecessor.mkdir()
                if owner_mutant:
                    owner_path=owners/'main.json';owner=json.loads(owner_path.read_bytes())
                    if owner_mutant=='missing':owner.pop('StopEvidence')
                    if owner_mutant=='foreign':owner['StopEvidence']['Identity']=dict(owner['Identity'],RunId='foreign')
                    raw=json.dumps(owner)
                    if owner_mutant=='duplicate':raw=raw[:-1]+',"Disposition":"Stopped"}'
                    owner_path.write_text(raw)
                prior_path=predecessor/'probe-result.json';prior_path.write_text(json.dumps(prior))
                (predecessor/'guardian.json').write_text(json.dumps(guardian))
                (predecessor/'actual-turn-request.json').write_bytes(request_bytes)
            if mutation and not closing and not continuation:
                target=helper/mutation.removeprefix('helper/') if mutation.startswith('helper/') else session/mutation
                target.write_bytes(b'mutated')
            argv=['driver',str(out),str(seed_path),str(action),str(dll)]+([str(prior_path)] if continuation else [])
            scope=dict(NS,sys=types.SimpleNamespace(argv=argv),os=types.SimpleNamespace(environ={'TERM':'dumb'}))
            first=next(i for i,n in enumerate(TREE.body) if isinstance(n,ast.Assert) and 'len(sys.argv) in (5, 6)' in ast.unparse(n.test))
            last=next(i for i,n in enumerate(TREE.body[first:],first) if assign_name(n,'controlled_refusal'))
            exec(compile(ast.Module(body=TREE.body[first:last],type_ignores=[]),str(SOURCE),'exec'),scope)
            if closing:
                result=valid();result.update(RuntimePins=package['RuntimePins'],PhasePackage=package,ReceiptHelperPins=package['ReceiptHelperPins'])
                if mutation:
                    target=helper/mutation.removeprefix('helper/') if mutation.startswith('helper/') else session/mutation
                    target.write_bytes(b'mutated')
                final=next(n for n in TREE.body if isinstance(n,ast.Try) and n.finalbody)
                begin=next(i for i,n in enumerate(final.finalbody) if assign_name(n,'daemon_evidence'))
                end=next(i for i,n in enumerate(final.finalbody) if isinstance(n,ast.If) and isinstance(n.test,ast.Name) and n.test.id=='controlled_refusal')
                scope.update(result=result,journal=[],elapsed=lambda:0.1,daemon_pair_capture=lambda *args:args[1])
                exec(compile(ast.Module(body=final.finalbody[begin:end],type_ignores=[]),str(SOURCE),'exec'),scope)
                return result['Success']
            return scope['expected_turn']
    def test_valid_closing(self):self.assertTrue(self.phase(closing=True))
    def test_closing_runtime_mutation_refused(self):self.assertFalse(self.phase('_runtime/client.dll',closing=True))
    def test_closing_helper_dependency_mutation_refused(self):self.assertFalse(self.phase('helper/dependency.dll',closing=True))
    def test_valid_initial_setup(self):self.assertEqual(42,self.phase())
    def test_initial_runtime_mutation_refused(self):
        with self.assertRaises((ValueError,AssertionError)):self.phase('_runtime/client.dll')
    def test_receipt_dependency_mutation_refused(self):
        with self.assertRaises((ValueError,AssertionError)):self.phase('helper/dependency.dll')
    def test_receipt_runtimeconfig_mutation_refused(self):
        with self.assertRaises((ValueError,AssertionError)):self.phase('helper/evidence.runtimeconfig.json')
    def test_valid_continuation(self):self.assertEqual(43,self.phase(continuation=True))
    def test_predecessor_world_mutation_refused(self):
        with self.assertRaises((ValueError,AssertionError)):self.phase('game_state/world/state.json',True)
    def test_predecessor_config_mutation_refused(self):
        with self.assertRaises((ValueError,AssertionError)):self.phase('config.json',True)

# Each strict JSON mutant is a separate reported leaf rather than a subtest aggregate.
def cleanup_mutant(path,value):
    def test(self):
        proof=valid();target=proof
        for key in path[:-1]:target=target[key]
        target[path[-1]]=value
        self.assertFalse(NS['phase_cleanup_proved'](proof))
    return test
for label,path,value in [
    ('epoch_bool',['OriginalRunningRecord','Identity','Epoch'],True),
    ('actions_bool',['GenuineActionsSent'],True),('shutdown_bool',['ShutdownAttempts'],True),
    ('terminal_exit_bool',['Terminals','client','ExitCode'],False),
    ('terminal_eof_string',['Terminals','client','EOF'],'true'),
    ('terminal_termios_string',['Terminals','client','TermiosRestored'],'true'),
    ('terminal_stream_string',['Terminals','client','StreamEOF','raw'],'true'),
    ('rpc_exit_bool',['RpcHelperClosure','ExitCode'],False),
    ('rpc_eof_string',['RpcHelperClosure','StdoutEof'],'true'),
    ('input_flag_string',['InputQualified'],'true'),('relay_flag_string',['RelayExecutionClosed'],'true')]:
    setattr(MortalEvidenceTests,'test_strict_'+label,cleanup_mutant(path,value))
def guardian_mutant(key,value):
    def test(self):
        with self.assertRaises((ValueError,AssertionError)):
            self.phase(continuation=True,guardian_mutant={key:value})
    return test
for key,value in [('driverExitCode',False),('emergencySignals',False),('failures',False),('echild','true'),('deadline','false')]:
    setattr(MortalEvidenceTests,'test_strict_guardian_'+key,guardian_mutant(key,value))

def owner_mutant(kind):
    def test(self):
        with self.assertRaises((ValueError,AssertionError)):
            self.phase(continuation=True,owner_mutant=kind)
    return test
for kind in ('missing','foreign','duplicate'):
    setattr(MortalEvidenceTests,'test_current_stopped_'+kind,owner_mutant(kind))

if __name__=='__main__':unittest.main(verbosity=2)
