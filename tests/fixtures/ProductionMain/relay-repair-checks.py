"""Real relay + real current GM helper; isolated protocol bytes, no live model."""
import errno,hashlib,json,os,pty,select,signal,subprocess,sys,termios,time
from pathlib import Path
repo=Path(__file__).resolve().parents[3];mode=sys.argv[1];own=Path(sys.argv[2]);session=own/'game_session';queue=own/'queue'
control=session/'game_state/control';control.mkdir(parents=True);(session/'input').mkdir();(session/'game_state/meta').mkdir();queue.mkdir()
sha=lambda b:hashlib.sha256(b).hexdigest()
turn=dict(sessionId='repair-test-session',requestId='repair-test-request',turnNumber=1)
paths=['input/turn_request.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json','game_state/control/validation_repair_request.json']
repair=dict(turn,attempt=1,metadataDiagnosticOnly=False,fullTurnResubmissionRequired=False,errors=['controlled protocol coverage only'])
for path,value in zip(paths,[turn,turn,{},repair]):(session/path).write_text(json.dumps(value))
(session/'game_state/meta/soul_state.json').write_text('{"currentRealm":"Chaos Sea"}')
helper=repo/'BookOfEternityClient/Launcher/GM_Turn_Helper.ps1'
(control/'gm_turn_helper.bootstrap.ps1').write_text(". '"+str(helper)+"'\nInitialize-BoeGmTurnHelper -GameSessionPath '"+str(session)+"'\n")
packet=dict(Completion='repair',Writes=[],FilesModified=[]);packet_bytes=json.dumps(packet).encode()
ready=control/'validation_repair_ready.json';terminal=session/'ready/turn_complete.json'
if mode=='helper-signature':
    req=dict(Kind='repair',Witnesses={p:sha((session/p).read_bytes()) for p in paths});request=own/'request.json';response=own/'response.json'
    request.write_text(json.dumps(req));response.write_bytes(packet_bytes)
    p=subprocess.run(['pwsh','-NoProfile','-File',str(Path(__file__).with_name('relay-apply-response.ps1')),'-SessionPath',str(session),'-RequestPath',str(request),'-ResponsePath',str(response)],capture_output=True,timeout=6)
    (own/'consumer.log').write_bytes(p.stdout+p.stderr)
    assert p.returncode==0,(p.returncode,(p.stdout+p.stderr).decode('utf8','replace'))
    assert json.loads(ready.read_text())['requestId']==turn['requestId'] and not terminal.exists()
    print('Actual ordinary Complete-BoeValidationRepair interface passed;0 model calls');sys.exit(0)
daemon=(repo/'BookOfEternityClient/game_master_daemon.ps1').read_text()
assert '$message = "REPAIR MODE for rejected turn #' in daemon,'Fixture must track actual daemon entrypoint'
prompt=('REPAIR MODE for rejected turn #1 (requestId=repair-test-request, attempt=1). Read '+str(control/'validation_repair_request.json')+'. Do NOT create a new turn.').encode()
master,slave=pty.openpty();initial=termios.tcgetattr(slave);capture=bytearray();p=None
def pump():
    if select.select([master],[],[],.02)[0]:
        try:capture.extend(os.read(master,65536))
        except OSError as e:
            if e.errno!=errno.EIO:raise
def until(test,label):
    deadline=time.monotonic()+5
    while not test():
        pump();assert p.poll() is None,(label,p.returncode,bytes(capture));assert time.monotonic()<deadline,(label,bytes(capture))
try:
    p=subprocess.Popen(['/usr/bin/python3',str(Path(__file__).with_name('codex-gm-relay.py')),'--session',str(session),'--queue',str(queue),'--model','inert-repair-no-model'],stdin=slave,stdout=slave,stderr=slave)
    until(lambda:b'NEUTRAL READY' in capture,'observed ready')
    os.write(master,b'\x1b[200~'+prompt+b'\x1b[201~');until(lambda:prompt in capture,'fresh exact draft');assert not list(queue.glob('request-*'))
    os.write(master,b'\r');until(lambda:bool(list(queue.glob('request-*/request.json'))),'actual submit')
    q=next(queue.glob('request-*'));req=json.loads((q/'request.json').read_text());source=control/'validation_repair_request.json'
    assert req['Kind']=='repair',('Actual daemon repair misclassified',req)
    assert req['RequestPath']=='game_state/control/validation_repair_request.json'
    assert (q/'game-request.json').read_bytes()==source.read_bytes() and req['RequestSHA256']==sha(source.read_bytes())
    assert req['TurnRequestSHA256']==sha((session/'input/turn_request.json').read_bytes()) and req['TurnIdentity']==turn
    assert (q/'prompt.txt').read_bytes()==prompt
    if mode=='stale-repair':source.write_text(json.dumps(dict(repair,attempt=2)))
    (q/'response.json').write_bytes(packet_bytes)
    reply={k:req[k] for k in ['QueueId','PromptSHA256','RequestSHA256','TurnRequestSHA256']}
    reply.update(ResponseSHA256=sha(packet_bytes),Model='inert-repair-no-model',AgentTask='inert-case')
    (q/'reply.json').write_text(json.dumps(reply));until(lambda:(q/'execution.json').exists(),'actual consumer settlement')
    outcome=json.loads((q/'execution.json').read_text())
    if mode=='stale-repair':assert not outcome['Executed'] and not ready.exists() and not terminal.exists(),outcome
    else:
        assert outcome['Executed'] and outcome['ExitCode']==0,outcome
        ready_signal=json.loads(ready.read_text());assert all(ready_signal[k]==v for k,v in turn.items()) and ready_signal['status']=='success'
        assert not terminal.exists(),'Repair minted a new turn terminal signal'
finally:
    (queue/'close-request.json').write_text('{}')
    if p is not None:
        until(lambda:(queue/'closed.json').exists(),'close before cleanup')
        p.send_signal(signal.SIGTERM);p.wait(timeout=3);assert p.returncode==0
    assert termios.tcgetattr(slave)==initial
    (own/'relay.raw').write_bytes(capture);os.close(master);os.close(slave)
print(json.dumps({'Case':mode,'RealHelperConsumer':True,'ModelCalls':0,'Passed':True}))
