"""Isolated real relay PTY transport. Generated GM acceptance is not claimed."""
import hashlib,json,os,pty,select,signal,subprocess,sys,termios,time
from pathlib import Path
repo=Path(__file__).resolve().parents[3];mode=sys.argv[1];own=Path(sys.argv[2]);guardian=sys.argv[3]
relay=repo/'tests/fixtures/ProductionMain/codex-gm-relay.py'
if mode=='close-before-snapshot':
 # Run the actual shared core; pause its last witness read before the real snapshot.
 # No host substitution, response generation, fake execution or network calls.
 wrapper=own/'read-barrier.py'
 wrapper.write_text('''import sys,time
from pathlib import Path
sys.path.insert(0,sys.argv.pop(1))
import relay_cli
original=relay_cli.read_bounded
queue=Path(sys.argv[sys.argv.index('--queue')+1])
def read(path,limit=1048576):
    data=original(path,limit)
    if path.name=='pending_turn_snapshot.authority.json' and list(queue.glob('request-*/reply.json')):
        (queue/'before-snapshot').write_text('paused')
        deadline=time.monotonic()+4
        while not (queue/'release-read').exists():
            if time.monotonic()>deadline:raise TimeoutError('Own read barrier deadline')
            time.sleep(.01)
    return data
relay_cli.read_bounded=read
sys.exit(relay_cli.main())
''')
elif mode=='started-metadata-failure':
 wrapper=own/'started-fault.py'
 wrapper.write_text('''import sys
sys.path.insert(0,sys.argv.pop(1))
import relay_cli
original=relay_cli.save
def save(path,value):
    if path.name=='started.json':raise OSError('Controlled started metadata publication failure')
    original(path,value)
relay_cli.save=save
sys.exit(relay_cli.main())
''')
if mode=='guardian-budgets':
    for prefix,limit,expected in [([],30000,0),([],30001,64),(['--live-turn'],300000,0),(['--live-turn'],300001,64),(['--relay-turn'],750000,0),(['--relay-turn'],750001,64)]:
        p=subprocess.run([guardian,*prefix,str(own/('g-'+str(limit)+'.json')),str(limit),'/usr/bin/true','owned-budget-probe'],capture_output=True,timeout=3)
        assert p.returncode==expected,(prefix,limit,p.returncode,p.stderr)
    print('budget boundaries passed');sys.exit(0)
assert relay.exists(),'Missing real persistent relay; immutable submit queue/closure not implemented'
session=own/'game_session';queue=own/'queue';(session/'input').mkdir(parents=True);(session/'game_state/control').mkdir(parents=True);queue.mkdir()
turn={'sessionId':'own-test-session','requestId':'own-request','turnNumber':1}
(session/'input/turn_request.json').write_text(json.dumps(turn));(session/'game_state/control/pending_turn_snapshot.json').write_text(json.dumps(turn))
(session/'game_state/control/pending_turn_snapshot.authority.json').write_text('{}')
bootstrap=session/'game_state/control/gm_turn_helper.bootstrap.ps1'
# Inert helper functions only: they never constitute a model response/gameplay.
bootstrap.write_text('''function Read-BoeJson { param($RelativePath) @{} }
function Resolve-BoeSessionPath { param($RelativePath) Join-Path $env:RELAY_TEST_SESSION $RelativePath }
function Write-BoeJson { param($RelativePath,$Data,$ExpectedReadWitnesses)
 [IO.File]::WriteAllText((Join-Path $env:RELAY_TEST_SESSION 'child-entered'), 'entered')
 if ($env:RELAY_TEST_HOLD -eq '1') { Start-Sleep -Seconds 2 }
 [IO.File]::AppendAllText((Join-Path $env:RELAY_TEST_SESSION 'applied'), 'once') }
function Complete-BoeTurn { param($FilesModified) }
function Complete-BoeValidationRepair { param($FilesModified) }
''')
env=dict(os.environ,RELAY_TEST_SESSION=str(session),RELAY_TEST_HOLD='1' if mode=='close-held-child' else '0')
master,slave=pty.openpty();initial=termios.tcgetattr(slave);p=None;capture=bytearray()
def pump():
 if select.select([master],[],[],.02)[0]:
  try:capture.extend(os.read(master,65536))
  except OSError:pass
def until(test,label,seconds=5):
 stop=time.monotonic()+seconds
 while not test():
  assert p.poll() is None,(label,p.returncode,bytes(capture))
  assert time.monotonic()<stop,(label,bytes(capture));pump()
try:
 entry=[str(wrapper),str(repo/'tools/gm-relay')] if mode in ['close-before-snapshot','started-metadata-failure'] else [str(relay)]
 p=subprocess.Popen(['/usr/bin/python3',*entry,'--session',str(session),'--queue',str(queue),'--model','inert-transport-no-model'],stdin=slave,stdout=slave,stderr=slave,env=env)
 until(lambda:b'NEUTRAL READY' in capture,'real initial presentation')
 prompt=('real draft Ж🙂 update > \nsecond line '+('x'*9000)).encode()
 os.write(master,b'\x1b[200~'+prompt+b'\x1b[201~')
 until(lambda:prompt.replace(b'\n',b'\r\n') in capture,'exact pasted view');assert not list(queue.glob('request-*')),'Paste alone submitted'
 os.write(master,b'\r');until(lambda:bool(list(queue.glob('request-*/request.json'))),'actual submit')
 q=next(queue.glob('request-*'));req=json.loads((q/'request.json').read_text());assert (q/'prompt.txt').read_bytes()==prompt
 assert req['PromptSHA256']==hashlib.sha256(prompt).hexdigest() and req['TurnIdentity']==turn
 if mode=='paste':
  (queue/'close-request.json').write_text('{}');until(lambda:(queue/'closed.json').exists(),'close idle queue');assert not (session/'applied').exists()
 else:
  packet={'Completion':'turn','Writes':[{'Path':'output/response.json','ExpectedSHA256':'missing','Data':{'text':'synthetic transport only'}}],'FilesModified':['output/response.json']}
  packet_bytes=json.dumps(packet).encode();(q/'response.json').write_bytes(packet_bytes)
  reply={k:req[k] for k in ['QueueId','PromptSHA256','RequestSHA256','TurnRequestSHA256']};reply.update(ResponseSHA256=hashlib.sha256(packet_bytes).hexdigest(),Model='inert-transport-no-model',AgentTask='none')
  if mode=='wrong-reply':reply['QueueId']='foreign'
  if mode=='stale':(session/'input/turn_request.json').write_text(json.dumps(dict(turn,requestId='replacement')))
  (q/'reply.json').write_text(json.dumps(reply))
  if mode=='close-before-snapshot':
   until(lambda:(queue/'before-snapshot').exists(),'real last witness read paused')
   sys.path.insert(0,str(repo/'tools/gm-relay'))
   from relay_contract import request_close
   request_close(queue);(queue/'release-read').write_text('release')
   until(lambda:(queue/'closed.json').exists(),'closed ACK after refused later start')
   assert not (q/'executing-response.json').exists() and not (q/'started.json').exists() and not (session/'applied').exists(),'Close preceded snapshot but real consumer started'
  elif mode=='close-held-child':
   until(lambda:(session/'child-entered').exists(),'actual child entered');(queue/'close-request.json').write_text('{}')
   for _ in range(6):pump()
   assert not (queue/'closed.json').exists(),'Close ACK before held consumer exit'
   until(lambda:(queue/'closed.json').exists(),'actual child exited/drained');assert (session/'applied').read_text()=='once'
  else:
   until(lambda:(q/'execution.json').exists(),'response execution/refusal')
   execution=json.loads((q/'execution.json').read_text())
   if mode in ['wrong-reply','stale']:assert not execution['Executed'] and not (session/'applied').exists(),execution
   else:
    assert execution['Executed'] and execution['ExitCode']==0,execution
    if mode=='started-metadata-failure':
     assert execution['MetadataFailure'] and execution['ChildExited'] and execution['IoDrained'],execution
    os.write(master,b'\r');pump();assert len(list(queue.glob('request-*')))==1 and (session/'applied').read_text()=='once'
   (queue/'close-request.json').write_text('{}');until(lambda:(queue/'closed.json').exists(),'close receipt')
  # A late reply after closure must never execute again.
  (q/'reply.json').write_text(json.dumps(reply));pump()
 assert json.loads((queue/'closed.json').read_text())['ExecutionDisabled']
finally:
 if p is not None:
  p.send_signal(signal.SIGTERM);p.wait(timeout=3);assert p.returncode==0
 assert termios.tcgetattr(slave)==initial,'Relay did not restore original terminal mode'
 os.close(master);os.close(slave)
print(json.dumps({'Case':mode,'TransportOnly':True,'ModelRequests':0,'Passed':True}))
