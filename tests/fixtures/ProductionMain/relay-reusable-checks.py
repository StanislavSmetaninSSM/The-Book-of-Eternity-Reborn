"""Shared worker protocol on isolated synthetic bytes; no terminal/model/network."""
import hashlib, importlib, json, sys
from pathlib import Path

repo=Path(__file__).resolve().parents[3];mode=sys.argv[1];own=Path(sys.argv[2])
tools=repo/'tools/gm-relay'
assert (tools/'relay_contract.py').is_file(), 'Missing shared relay worker contract'
sys.path.insert(0,str(tools));c=importlib.import_module('relay_contract')
session=own/'session';queue=own/'queue';session.mkdir();queue.mkdir();q=queue/'request-1-own';q.mkdir()
turn=dict(sessionId='own-session',requestId='own-request',turnNumber=1)
paths=['input/turn_request.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json']
for p,value in zip(paths,[turn,turn,{}]):
    f=session/p;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(json.dumps(value).encode())
sha=lambda b:hashlib.sha256(b).hexdigest()
prompt='exact Ж🙂\nline two'.encode();request_bytes=(session/paths[0]).read_bytes()
header=dict(QueueId=q.name,Kind='turn',Model='inert-worker',SessionPath=str(session),RequestPath=paths[0],PromptSHA256=sha(prompt),RequestSHA256=sha(request_bytes),TurnRequestSHA256=sha(request_bytes),TurnIdentity=turn,Witnesses={p:sha((session/p).read_bytes()) for p in paths},OriginalRelayPid=1,SubmittedMonotonic=0)
(q/'request.json').write_text(json.dumps(header));(q/'prompt.txt').write_bytes(prompt);(q/'game-request.json').write_bytes(request_bytes)
packet=b'{"Completion":"turn","Writes":[],"FilesModified":[]}'
def raises(kind,operation):
    try:operation()
    except kind:return
    raise AssertionError('Expected '+kind.__name__)
if mode=='incomplete':
    (q/'prompt.txt').unlink();raises(c.RelayPending,lambda:c.read_request(queue,q))
elif mode=='changed-prompt':
    (q/'prompt.txt').write_bytes(b'foreign');raises(c.RelayMismatch,lambda:c.read_request(queue,q))
elif mode=='wrong-queue':
    header['QueueId']='foreign';(q/'request.json').write_text(json.dumps(header));raises(c.RelayMismatch,lambda:c.read_request(queue,q))
else:
    request=c.read_request(queue,q);assert request.prompt==prompt and request.game_request==request_bytes
    if mode=='read':
        assert request.model=='inert-worker' and request.turn_identity==turn
    elif mode=='changed-request':
        (session/paths[0]).write_bytes(b'{}');raises(c.RelayMismatch,lambda:c.publish_response(request,packet,adapter_id='inert'))
        assert not (q/'response.json').exists()
    elif mode=='closed':
        c.request_close(queue);raises(c.RelayClosed,lambda:c.publish_response(request,packet,adapter_id='inert'))
        assert not (q/'response.json').exists() and c.read_close(queue) is None
    elif mode=='duplicate':
        c.publish_response(request,packet,adapter_id='inert')
        raises(c.RelayAnswered,lambda:c.publish_response(request,packet+b' ',adapter_id='second'))
        assert (q/'response.json').read_bytes()==packet
        reply=json.loads((q/'reply.json').read_bytes());assert reply['ResponseSHA256']==sha(packet) and reply['AgentTask']=='inert'
    elif mode=='packet-bound':
        raises(c.RelayMismatch,lambda:c.publish_response(request,b'x'*1048577,adapter_id='inert'))
        assert not (q/'response.json').exists()
    elif mode=='missing-adapter':
        raises(c.RelayMismatch,lambda:c.publish_response(request,packet,adapter_id=''))
        assert not (q/'response.json').exists()
    elif mode=='atomic-no-replace':
        target=own/'one.json';c.atomic_write_once(target,b'original')
        raises(FileExistsError,lambda:c.atomic_write_once(target,b'replacement'))
        assert target.read_bytes()==b'original' and not list(own.glob('*.tmp.*'))
    elif mode=='partial-publication':
        real=c.atomic_write_once
        def fail_reply(path,data):
            if path.name=='reply.json':raise OSError('Controlled publication interruption')
            real(path,data)
        c.atomic_write_once=fail_reply
        raises(OSError,lambda:c.publish_response(request,packet,adapter_id='inert'));c.atomic_write_once=real
        assert (q/'response.json').read_bytes()==packet and not (q/'reply.json').exists()
        raises(c.RelayAnswered,lambda:c.publish_response(request,packet,adapter_id='never-repeat'))
    elif mode=='close-unconfirmed':
        c.request_close(queue);c.request_close(queue);assert c.read_close(queue) is None
        (queue/'closed.json').write_text(json.dumps(dict(ExecutionDisabled=True,ChildExited=True,IoDrained=False)))
        raises(c.RelayMismatch,lambda:c.read_close(queue))
        (queue/'closed.json').write_text(json.dumps(dict(ExecutionDisabled=True,ChildExited=True,IoDrained=True)))
        assert c.read_close(queue)['IoDrained'] and not (q/'execution.json').exists()
    else:raise AssertionError(mode)
print(json.dumps(dict(Case=mode,Passed=True,ModelRequests=0)))
