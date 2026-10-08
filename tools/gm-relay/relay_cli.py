"""Maintained persistent developer CLI transport for separately generated GM data.

No model/network call, canned answer, terminal capability override or run authority.
This process and its fixed helper child are owned by the original M1 terminal.
"""
import argparse,hashlib,json,os,signal,subprocess,sys,time,uuid
from pathlib import Path
from relay_contract import atomic_write_once, execution_gate, RelayGateUnavailable
from relay_platform import Terminal, poll_child_output
def sha(data):return hashlib.sha256(data).hexdigest()
def write_once(path,data):
    atomic_write_once(path,data)
def save(path,value):write_once(path,(json.dumps(value,ensure_ascii=False,indent=2)+'\n').encode())
def read_bounded(path,limit=1048576):
    with path.open('rb') as f:data=f.read(limit+1)
    if len(data)>limit:raise ValueError('Bounded response/input exceeded')
    return data
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--session',required=True);parser.add_argument('--queue',required=True);parser.add_argument('--model',default='gpt-6.1-sol')
    args=parser.parse_args();session=Path(args.session).resolve();queue=Path(args.queue).resolve();queue.mkdir(exist_ok=True)
    if any(queue.iterdir()):raise ValueError('Relay requires its original fresh queue; no replay')
    terminal=Terminal(sys.stdin.fileno(),sys.stdout.fileno())
    stop=False;closing=False;active=None;child=None;output=bytearray();draft=bytearray();paste=None;escape=bytearray();ordinal=0;metadata_error=None
    def stopped(*unused):
        nonlocal stop;stop=True
    signal.signal(signal.SIGTERM,stopped);signal.signal(signal.SIGINT,stopped)
    def render(state='NEUTRAL READY',text=b''):
        terminal.write(b'\x1b[2J\x1b[H'+state.encode()+b'\r\nRELAY> '+text.replace(b'\n',b'\r\n')+b'\r\n')
    def close_ack():
        if not (queue/'closed.json').exists():save(queue/'closed.json',dict(ExecutionDisabled=True,ChildExited=True,IoDrained=True,RelayPid=os.getpid()))
    def record_execution(value):
        nonlocal active
        save(active/'execution.json',value);active=None
    def capture_request():
        nonlocal ordinal,active,draft
        if not draft or closing or active is not None or child is not None:return
        text=bytes(draft).decode('utf8');kind='repair' if text.startswith('REPAIR MODE for rejected turn #') else 'turn'
        source='game_state/control/validation_repair_request.json' if kind=='repair' else 'input/turn_request.json'
        paths=[source,'input/turn_request.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json']
        witnesses={p:sha(read_bounded(session/p)) for p in dict.fromkeys(paths)}
        turn=json.loads(read_bounded(session/'input/turn_request.json'));pending=json.loads(read_bounded(session/'game_state/control/pending_turn_snapshot.json'))
        identity={k:turn[k] for k in ['sessionId','requestId','turnNumber']}
        if any(pending.get(k)!=v for k,v in identity.items()):raise ValueError('Current pending identity mismatch')
        ordinal+=1
        if ordinal>3:raise ValueError('Bounded relay submissions exceeded; never retry')
        active=queue/('request-'+str(ordinal)+'-'+uuid.uuid4().hex);active.mkdir()
        save(active/'request.json',dict(QueueId=active.name,Kind=kind,Model=args.model,SessionPath=str(session),RequestPath=source,
            PromptSHA256=sha(bytes(draft)),RequestSHA256=witnesses[source],TurnRequestSHA256=witnesses['input/turn_request.json'],
            TurnIdentity=identity,Witnesses=witnesses,OriginalRelayPid=os.getpid(),SubmittedMonotonic=time.monotonic()))
        write_once(active/'prompt.txt',bytes(draft));write_once(active/'game-request.json',read_bounded(session/source))
        draft.clear();render('NEUTRAL WORKING')
    try:
        render()
        while not stop:
            if (queue/'close-request.json').exists():closing=True
            if child is not None:
                part=poll_child_output(child.stdout)
                if part is not None:
                    output.extend(part)
                    if len(output)>1048576:raise ValueError('Response child output bound exceeded')
                    if not part and child.poll() is not None:
                        child.stdout.close();code=child.returncode;child=None
                        write_once(active/'consumer.log',bytes(output))
                        settled=dict(Executed=True,ExitCode=code,ChildExited=True,IoDrained=True)
                        if metadata_error:settled['MetadataFailure']=metadata_error
                        record_execution(settled);output.clear()
                        if code or metadata_error:render('RELAY ERROR')
                        elif not closing:render()
            if closing and child is None:
                close_ack();render('RELAY CLOSED')
                # Execution disabled permanently; a late response stays inert.
                while not stop:time.sleep(.02)
                break
            if active is not None and child is None and (active/'reply.json').exists():
                try:
                    req=json.loads(read_bounded(active/'request.json'));reply=json.loads(read_bounded(active/'reply.json'));packet=read_bounded(active/'response.json')
                    for key in ['QueueId','PromptSHA256','RequestSHA256','TurnRequestSHA256']:
                        if reply.get(key)!=req[key]:raise ValueError('Response identity mismatch: '+key)
                    if reply.get('Model')!=args.model or not reply.get('AgentTask'):raise ValueError('Response model/task mismatch')
                    if reply.get('ResponseSHA256')!=sha(packet):raise ValueError('Response bytes mismatch')
                    for path,witness in req['Witnesses'].items():
                        if not (session/path).is_file() or sha(read_bounded(session/path))!=witness:raise ValueError('Original request/pending changed')
                    # Shared API close and this reservation serialize on the same stable inode.
                    # Release after snapshot: an already-authorized original child may settle.
                    with execution_gate(queue):
                        if (queue/'close-request.json').exists() or (queue/'closed.json').exists():
                            closing=True
                            continue
                        write_once(active/'executing-response.json',packet)
                    consumer=Path(__file__).with_name('relay-apply-response.ps1')
                    child=subprocess.Popen(['pwsh','-NoLogo','-NoProfile','-File',str(consumer),'-SessionPath',str(session),'-RequestPath',str(active/'request.json'),'-ResponsePath',str(active/'executing-response.json')],
                        cwd=session,stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
                    save(active/'started.json',dict(ChildPid=child.pid,ResponseSHA256=sha(packet),AgentTask=reply['AgentTask'],Model=reply['Model']))
                except RelayGateUnavailable:
                    raise # Serialization uncertainty cannot mint execution or a close ACK.
                except Exception as ex:
                    failure=type(ex).__name__+': '+str(ex)
                    if child is None:record_execution(dict(Executed=False,Failure=failure))
                    else:metadata_error=failure # Popen succeeded; retain active/child until actual exit/I-O.
                    render('RELAY ERROR')
            data=terminal.read(.02)
            if data is None:continue
            if not data:break
            for b in data:
                if closing or child is not None or active is not None:continue
                if paste is not None:
                    # ConPTY maps incoming LF to console CR, even in raw mode.
                    # Only paste content maps back to LF; actual CR still submits.
                    if os.name=='nt' and b==13:b=10
                    paste.append(b)
                    if paste.endswith(b'\x1b[201~'):
                        draft.extend(paste[:-6]);paste=None
                        text=bytes(draft).decode('utf8')
                        if len(text)>65000 or len(text.split('\n'))>120 or any(ord(c)<32 and c!='\n' for c in text):raise ValueError('Unsupported neutral draft shape')
                        render(text=bytes(draft))
                    elif len(paste)>262144:raise ValueError('Paste byte bound exceeded')
                elif escape:
                    escape.append(b)
                    if escape==b'\x1b[200~':escape.clear();paste=bytearray()
                    elif not b'\x1b[200~'.startswith(escape):raise ValueError('Unknown terminal input sequence')
                elif b==27:escape.append(b)
                elif b==13:capture_request()
                elif b in [3,4]:stop=True
                else:raise ValueError('Only neutral bracketed-paste/submit gestures admitted')
    except Exception as ex:
        if not (queue/'failure.json').exists():save(queue/'failure.json',dict(Failure=type(ex).__name__+': '+str(ex)))
        render('RELAY ERROR')
        while not stop:time.sleep(.02)
    finally:
        # The original supervisor has independent cleanup authority. No close ACK
        # is minted on an unresolved child; its physical cleanup cannot prove stop.
        if child is not None:
            try:
                child.terminate();child.wait(timeout=2)
                child.stdout.close()
            except subprocess.TimeoutExpired:pass
        terminal.close()
    return 0
if __name__=='__main__':sys.exit(main())
