"""Bounded real ordinary entrypoints in shipped layout; no neutralPackage or provider."""
import fcntl,json,os,pty,select,socket,struct,subprocess,sys,termios,time,uuid,signal
from pathlib import Path
mode,folder,ship,session=sys.argv[1:];folder,ship,session=map(Path,(folder,ship,session))
master,slave=pty.openpty();fcntl.ioctl(slave,termios.TIOCSWINSZ,struct.pack('HHHH',25,80,0,0));initial=termios.tcgetattr(slave)
pipe='m1-'+uuid.uuid4().hex;capture=bytearray();result={'Success':False,'Mode':mode};process=None;pidfd=None;started=time.monotonic()
def own_terminal():
 os.setsid();fcntl.ioctl(slave,termios.TIOCSCTTY,0);os.tcsetpgrp(slave,os.getpid())
def receive():
 if select.select([master],[],[],.02)[0]:
  try:capture.extend(os.read(master,65536))
  except OSError:pass
 if len(capture)>262144:raise RuntimeError('Capture exceeded bound')
def until(marker,seconds=5):
 deadline=time.monotonic()+seconds
 while marker not in capture:
  if process.poll() is not None:receive();raise RuntimeError('Entrypoint exited before '+repr(marker)+'; '+capture.decode(errors='replace'))
  if time.monotonic()>deadline:raise TimeoutError('Missing '+repr(marker)+'; '+capture.decode(errors='replace'))
  receive()
def rpc(payload):
 with socket.socket(socket.AF_UNIX,socket.SOCK_STREAM) as peer:
  peer.settimeout(4);peer.connect('/tmp/CoreFxPipe_'+pipe);peer.sendall(json.dumps(payload).encode()+b'\n');reply=bytearray()
  while not reply.endswith(b'\n'):
   b=peer.recv(8192)
   if not b:raise EOFError('Original pipe closed')
   reply.extend(b)
  return json.loads(reply)
try:
 if mode=='launcher':
  cfg=json.loads((session/'config.json').read_text());cfg['GmBridgePipeNameOverride']=pipe;(session/'config.json').write_text(json.dumps(cfg))
  args=['pwsh','-NoLogo','-NoProfile','-File',str(ship/'BookOfEternityClient/Launcher/bookofeternity.ps1'),'start-bridge','visible','-SessionPath',str(session)]
 elif mode=='daemon':args=['pwsh','-NoLogo','-NoProfile','-File',str(ship/'BookOfEternityClient/game_master_daemon.ps1'),'-GameSessionPath',str(session),'-PollingInterval','50','-LogFile',str(folder/'daemon.log')]
 else:args=['dotnet',str(ship/'BookOfEternityGMBridge/BookOfEternityGMBridge.dll'),'--host','--sessionPath',str(session),'--pipeName',pipe]
 result['Argv']=args
 process=subprocess.Popen(args,cwd=ship,stdin=slave,stdout=slave,stderr=slave,preexec_fn=own_terminal);pidfd=os.pidfd_open(process.pid)
 if mode=='daemon':
  until(b'Waiting for turns...',12);result['PortableDaemonStartup']=True
  # Only this original unreaped direct child; independent outer guardian owns descendants.
  signal.pidfd_send_signal(pidfd,signal.SIGINT);result['ExitSignal']='SIGINT original daemon pidfd';process.wait(timeout=3)
 else:
  until(b'NEUTRAL READY')
  cfg=json.loads((session/'config.json').read_text());assert ('CONFIGURED_CWD:'+cfg['GmBridgeShellWorkingDirectory']).encode() in capture
  assert b'CONFIGURED_ARG2:gm-model-sentinel' in capture and b'CONFIGURED_ARG4:a b' in capture
  status=rpc({'command':'status'})['status'];binding=status['inputBindingId'];pid=status['shellPid'];result['OriginalStatus']=status
  record=json.loads((session.parent/'.boe_runtime/gm-runs/main.json').read_text());assert record['Disposition']=='Running';result['DurableRunning']=True
  for n,text in enumerate(['one Ж😀','two'],1):
   os.write(master,(text+'\r').encode());until(('RESULT'+str(n)+':'+text).encode())
  same=rpc({'command':'status'})['status'];assert same['shellPid']==pid and same['inputBindingId']==binding;result['TwoInputsOneOriginal']=True
  identity={k[0].lower()+k[1:]:v for k,v in record['Identity'].items()}; identity['backend']='linux-supervisor'
  stopped=rpc({'command':'shutdown','rootKey':identity['rootKey'],'expectedMainIdentity':identity})
  assert stopped['ok'],stopped
  process.wait(timeout=4);assert process.returncode==0
  settled=json.loads((session.parent/'.boe_runtime/gm-runs/main.json').read_text());assert settled['Disposition']=='Stopped';result['DurableStopped']=True
 result['Success']=True
except Exception as ex:result['Failure']=type(ex).__name__+': '+str(ex)
finally:
 if process is not None:
  if process.poll() is None:
   try:rpc({'command':'shutdown'})
   except Exception:pass
  try:
   while process.poll() is None and time.monotonic()-started<25:receive()
   if process.poll() is None:signal.pidfd_send_signal(pidfd,signal.SIGTERM);process.wait(timeout=1)
  except Exception:pass
  receive();result['EntrypointExitCode']=process.poll()
 if pidfd is not None:os.close(pidfd)
 os.close(master);os.close(slave);result['ElapsedSeconds']=time.monotonic()-started
 (folder/'ordinary.log').write_bytes(capture);(folder/'scenario.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
sys.exit(0 if result['Success'] else 1)
