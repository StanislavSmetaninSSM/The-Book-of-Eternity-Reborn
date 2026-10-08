"""Inert execution of actual driver receipt/close functions, never a live turn."""
import ast,copy,json,tempfile
from pathlib import Path
source=Path(__file__).with_name('live-game-one-turn.py');tree=ast.parse(source.read_text())
names={'retain_turn_delivery','close_relay_execution'}
nodes=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names]
assert {n.name for n in nodes}==names,'Original turn receipt and execution-close gate are not implemented'
clock=[0.0];ns=dict(json=json,time=type('Clock',(),{'monotonic':lambda self:clock[0]})(),elapsed=lambda:clock[0])
exec(compile(ast.Module(body=copy.deepcopy(nodes),type_ignores=[]),str(source),'exec'),ns)
result={};turn=dict(disposition='submission-observed',inputBindingId='original',operationKind='turn',operationRevision='live',operationId='turn-A')
repair=dict(turn,operationKind='repair',operationRevision='repair-1',operationId='repair-B')
ns['retain_turn_delivery'](result,turn,'original');ns['retain_turn_delivery'](result,repair,'original')
ns['retain_turn_delivery'](result,repair,'original')
assert result['OriginalTurnDelivery']==turn and result['RepairDeliveries']==[repair]
assert ns['retain_turn_delivery'](result,dict(turn,inputBindingId='foreign'),'original') is False
with tempfile.TemporaryDirectory(prefix='own-relay-driver-') as folder:
 queue=Path(folder);rolled_back=[];ns.update(pump=lambda *args:clock.__setitem__(0,clock[0]+.1))
 assert ns['close_relay_execution'](queue,1) is False,'Unknown close enabled rollback'
 assert (queue/'close-request.json').exists()
 (queue/'closed.json').write_text(json.dumps(dict(ExecutionDisabled=True,ChildExited=True,IoDrained=False)))
 assert ns['close_relay_execution'](queue,1) is False,'Undrained I/O enabled rollback'
 (queue/'closed.json').write_text(json.dumps(dict(ExecutionDisabled=True,ChildExited=True,IoDrained=True)))
 assert ns['close_relay_execution'](queue,1) is True
print('Actual driver original/repair receipts and unclosed rollback gate passed;0 model calls')
