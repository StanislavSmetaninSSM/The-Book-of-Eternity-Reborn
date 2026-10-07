"""Execute the real driver's current wait/cleanup AST without its live entrypoint.

Only inert peers/clock and committed public og10 bytes are supplied. This neither
imports private CLI state nor fabricates a runtime/provider response.
"""
import ast, copy, gzip, hashlib, json, re, sys, tempfile
from pathlib import Path
from types import SimpleNamespace

repo=Path(__file__).resolve().parents[3]
source=repo/'tests/fixtures/ProductionMain/live-game-one-turn.py'
tree=ast.parse(source.read_text())
entry=next(n for n in tree.body if isinstance(n,ast.Try) and any(isinstance(x,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='client' for t in x.targets) for x in n.body))
clock=[0.0]
class Peer:
    def __init__(self,text='',code=None):
        self.capture=bytearray(text.encode());self.code=code;self.sent=[];self.name='bridge'
        self.process=SimpleNamespace(poll=lambda:self.code)
    def text(self,offset=0):return re.sub(r'\x1b\[[0-9;? ]*[A-Za-z~]','',self.capture[offset:].decode('utf-8',errors='replace'))
    def send(self,data,phase):
        self.sent.append(data)
        if data==b'\x03':self.code=0
        if data==b'\x1b' and mode=='missing-rollback':request.unlink()
        self.capture.extend('Ваш ход\n🌊 > '.encode())
def wrap(nodes,name,ns):
    body=copy.deepcopy(nodes)
    assignments={n.id for node in body for n in ast.walk(node) if isinstance(n,ast.Name) and isinstance(n.ctx,ast.Store)}
    module=ast.Module(body=[ast.FunctionDef(name=name,args=ast.arguments(posonlyargs=[],args=[],kwonlyargs=[],kw_defaults=[],defaults=[]),
        body=[ast.Global(names=sorted(assignments))]+body,decorator_list=[])],type_ignores=[])
    exec(compile(ast.fix_missing_locations(module),str(source),'exec'),ns)
    return ns[name]
def pinned(name):
    base=repo/'specs/1553-portable-local-storage/recovery/evidence/opencode-live/provider-domain-refusal'
    manifest=json.loads((base/'manifest.json').read_text())
    pin=next(p for p in manifest['Artifacts'] if p['Path']==name)
    stored=(base/pin['StoredPath']).read_bytes();raw=gzip.decompress(stored)
    assert len(raw)==pin['Bytes'] and hashlib.sha256(raw).hexdigest()==pin['SHA256']
    assert hashlib.sha256(stored).hexdigest()==pin['StoredSHA256']
    return raw
with tempfile.TemporaryDirectory(prefix='own-driver-inert-') as folder:
    session=Path(folder);(session/'input').mkdir();request=session/'input/turn_request.json';request.write_text('{}')
    client=Peer();bridge=Peer();daemon=Peer()
    def pump(*unused):clock[0]+=.1
    result={'AcceptedGameTurns':0,'LogicalLifecycleVerified':False}
    ns=dict(time=SimpleNamespace(monotonic=lambda:clock[0]),Path=Path,re=re,json=json,hashlib=hashlib,
        session=session,start=0,client=client,bridge=bridge,daemon=daemon,offset=0,result=result,journal=[],
        client_failure_offset=None,client_wait=True,client_prompt=False,client_exit_attempted=False,
        active_phase_deadline=None,provider_bridge_offset=0,original={'rootKey':str(session),'runId':'original'},original_record={},
        shutdown_attempts=0,pump=pump,elapsed=lambda:clock[0],until=lambda test,*unused:test(),rpc=lambda *unused:{'status':{'terminalUncertain':False,'terminalOwnerRetained':True}},
        read_json=lambda p:json.loads(p.read_text()),exit_client=lambda p:setattr(p,'code',0))
    # Only definitions before the first live argument-handling statement. No entrypoint executes.
    prefix=[]
    for n in tree.body:
        if isinstance(n,ast.If):break
        if isinstance(n,(ast.FunctionDef,ast.ClassDef)):prefix.append(n)
    exec(compile(ast.Module(body=copy.deepcopy(prefix),type_ignores=[]),str(source),'exec'),ns)
    mode=sys.argv[1]
    if mode in ['provider','client-error','process-exit','old-provider','old-client']:
        first=next(i for i,n in enumerate(entry.body) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='story' for t in n.targets))
        nodes=entry.body[first:]
        if mode=='provider':bridge.capture=bytearray(pinned('bridge.raw')[:71484]);expected='provider'
        elif mode=='client-error':client.capture=bytearray('❌ Ошибка: original admission refused\nНажмите любую клавишу для продолжения...'.encode());expected='client'
        elif mode=='process-exit':bridge.code=17;expected='exited'
        elif mode=='old-provider':
            bridge.capture=bytearray(pinned('bridge.raw')[:71484]);ns['provider_bridge_offset']=len(bridge.capture);expected=None
        else:
            client.capture=bytearray('❌ Ошибка: old turn\nНажмите любую клавишу для продолжения...'.encode());ns['offset']=len(client.capture);expected=None
        try:wrap(nodes,'current_wait',ns)()
        except RuntimeError as ex:
            assert expected in str(ex).lower(),str(ex)
            assert clock[0]<1,'Current failure was detected only after a deadline'
        except TimeoutError as ex:
            if expected is not None:raise AssertionError('Actual current failure fell through to provider timeout') from ex
        else:raise AssertionError('Current failure was ignored')
    elif mode in ['error-pause','coalesced-pause','early-startup','missing-rollback']:
        client.capture=bytearray('❌ Ошибка: original admission refused\nНажмите любую клавишу для продолжения...'.encode())
        if mode=='coalesced-pause':client.capture.extend('Ваш ход\n🌊 > '.encode())
        if mode=='early-startup':
            client.capture.clear();ns['client_wait']=False;ns.pop('offset')
            assign=next((n for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='offset' for t in n.targets)),None)
            if assign is not None:exec(compile(ast.Module(body=[copy.deepcopy(assign)],type_ignores=[]),str(source),'exec'),ns)
        if mode=='missing-rollback':
            client.capture=bytearray('Мастер игры размышляет...'.encode())
            pending=session/'game_state/control/pending_turn_snapshot.json';pending.parent.mkdir(parents=True);pending.write_text('{}')
        node=next(n for n in entry.finalbody if isinstance(n,ast.If) and ast.unparse(n.test).startswith('client is not None'))
        wrap([node],'actual_client_cleanup',ns)()
        if mode=='missing-rollback':
            assert 'ClientCleanupFailure' in result and 'actual cancellation' not in result.get('ClientSettlement',''),result
            assert pending.exists();print(json.dumps({'Case':mode,'Passed':True,'ProviderCalls':0}));sys.exit(0)
        assert client.code==0 and 'ClientCleanupFailure' not in result,result
        expected=[b'\r'] if mode=='error-pause' else ([b'\x03'] if mode=='early-startup' else [])
        assert client.sent==expected,'Gesture targeted an already-left pause or an unobserved wait'
        assert request.exists(),'Acknowledging an error pause cannot delete or imply rollback'
    elif mode=='unknown-stop':
        ns['rpc']=lambda *unused:{'ok':False,'status':{'terminalStop':{'cleanupComplete':True,'authorityRetained':False}}}
        node=next(n for n in entry.finalbody if isinstance(n,ast.If) and ast.unparse(n.test)=='original is not None')
        daemon_node=next(n for n in entry.finalbody if isinstance(n,ast.If) and ast.unparse(n.test).startswith('daemon is not None'))
        wrap([node,daemon_node],'actual_original_stop',ns)()
        assert ns['shutdown_attempts']==1 and not result['LogicalLifecycleVerified'] and 'CleanupFailure' in result
        assert request.exists(),'Physical cleanup cannot clear pending or replay an unknown stop'
        assert not daemon.sent,'Unknown original stop interrupted the daemon before Stopped ACK'
    else:raise AssertionError('Unknown inert case')
    print(json.dumps({'Case':mode,'Passed':True,'ProviderCalls':0,'HistoricalRootsChanged':0,'ClockSeconds':clock[0]}))
