"""Actual neutral Program/Console.ReadKey, outer UI PTY and unchanged independent guardian."""
import fcntl,json,os,pty,select,socket,struct,subprocess,sys,termios,time,uuid
from pathlib import Path
repo,package,dotnet,bridge=map(Path,sys.argv[1:])
master,slave=pty.openpty()
fcntl.ioctl(slave,termios.TIOCSWINSZ,struct.pack('HHHH',25,80,0,0))
original=termios.tcgetattr(slave)
pipe='neutral-ui-'+uuid.uuid4().hex
transcript=bytearray(); result={}; guardian=None; started=time.monotonic(); shutdown_attempted=False; original_identity=None
def own_ui_terminal():
 # This new child session owns only the fixture's outer UI terminal. TIOCSCTTY(0)
 # never steals another session's tty; foreground ownership delivers SIGWINCH.
 os.setsid()
 fcntl.ioctl(slave,termios.TIOCSCTTY,0)
 os.tcsetpgrp(slave,os.getpid())
def receive():
 if select.select([master],[],[],0.02)[0]:
  try: transcript.extend(os.read(master,65536))
  except OSError: pass
def until(marker):
 deadline=time.monotonic()+2
 while marker not in transcript:
  if time.monotonic()>deadline: raise TimeoutError('Missing foreground observation: '+repr(marker))
  receive()
def rpc(request):
 with socket.socket(socket.AF_UNIX,socket.SOCK_STREAM) as peer:
  peer.settimeout(3)
  peer.connect('/tmp/CoreFxPipe_'+pipe)
  peer.sendall(json.dumps(request).encode()+b'\n')
  reply=bytearray()
  while not reply.endswith(b'\n'):
   block=peer.recv(8192)
   if not block: raise EOFError('Original pipe response closed.')
   reply.extend(block)
  return json.loads(reply)
def shutdown_once():
 global shutdown_attempted
 if shutdown_attempted: return None
 shutdown_attempted=True
 if original_identity is None:
  result['ShutdownFailure']={'stage':'before-connect','reason':'Original Running identity not observed; no shutdown sent.'}
  raise RuntimeError('Original Running shutdown expectation unavailable.')
 request={'command':'shutdown','expectedMainIdentity':original_identity,'rootKey':original_identity['rootKey']}
 try:
  response=rpc(request)
 except Exception as ex:
  result['ShutdownFailure']={'stage':'connect-send-or-read','reason':type(ex).__name__+': '+str(ex)}
  raise
 result['ShutdownReceipt']=response
 return response
try:
 guardian=subprocess.Popen([str(package/'host-guardian'),str(package/'guardian.json'),'15000',str(dotnet),str(bridge),
  '--host','--sessionPath',str(package/'ignored-session'),'--pipeName',pipe,'--neutralPackage',str(package)],cwd=repo,stdin=slave,stdout=slave,stderr=slave,preexec_fn=own_ui_terminal)
 until(b'NEUTRAL READY')
 # Child output precedes creation of the host's accept loop. Wait without any
 # input for this exact owned endpoint; never retry a connected request.
 listener_deadline=time.monotonic()+2
 while not Path('/tmp/CoreFxPipe_'+pipe).exists():
  if guardian.poll() is not None or time.monotonic()>listener_deadline:
   raise TimeoutError('Original foreground pipe did not become available.')
  receive()
 first=rpc({'command':'status'})['status']; binding=first['inputBindingId']; pid=first['shellPid']
 # Metadata is only an expectation for this already-acknowledged original
 # live owner. Never obtain a replacement identity during cleanup.
 record_path=Path(first['sessionPath']).parent/'.boe_runtime/gm-runs/main.json'
 record=json.loads(record_path.read_bytes())
 assert record['Disposition']=='Running' and record['Identity']['RunId']==first['terminalRunId'] and first['terminalOwnerRetained']
 original_identity={k[0].lower()+k[1:]:v for k,v in record['Identity'].items()}
 original_identity['backend']=2
 result['OriginalRunningRecord']=record
 os.write(master,'one Ж😀\r'.encode()); until('RESULT1:one Ж😀'.encode())
 os.write(master,b'two\r'); until(b'RESULT2:two')
 same=rpc({'command':'status'})['status']; assert same['inputBindingId']==binding and same['shellPid']==pid
 result['TwoActualConsoleInputsOneSession']=True
 fcntl.ioctl(slave,termios.TIOCSWINSZ,struct.pack('HHHH',31,93,0,0));until(b'RESIZE 31x93');result['ActualConsoleResize']=True
 os.write(master,b'canonical\r');until(b'CANONICAL_READY');os.write(master,b'\x04');until(b'CANONICAL_EOF')
 result['CanonicalEofRootAlive']=rpc({'command':'status'})['status']['shellPid']==pid
 stopped=rpc({'command':'stopTerminal'});proof=stopped['status']['terminalStop']
 assert stopped['ok'] and proof['state']=='stopped-within-scope' and proof['cleanupComplete'] and not stopped['status']['terminalOwnerRetained']
 result['ActualScopedStop']=True
 assert shutdown_once()['ok']
 guardian.wait(timeout=3);assert guardian.returncode==0
 result['OuterInputModeRestored']=termios.tcgetattr(slave)==original
 assert result['OuterInputModeRestored']
 result['ElapsedSeconds']=time.monotonic()-started
 result['Success']=True
except Exception as ex:
 result['Failure']=type(ex).__name__+': '+str(ex)
finally:
 # Actual guardian retains independent cleanup even when any UI assertion fails.
 if guardian is not None:
  try:
   if guardian.poll() is None: shutdown_once()
  except Exception: pass
  while guardian.poll() is None:
   receive()
   if time.monotonic()-started>20: raise TimeoutError('Independent guardian failed its bounded lifetime.')
  receive()
 os.close(master);os.close(slave)
 (package/'foreground.log').write_bytes(transcript)
 (package/'scenario.json').write_text(json.dumps(result,indent=2)+'\n')
 print(json.dumps(result))
sys.exit(0 if result.get('Success') else 1)
