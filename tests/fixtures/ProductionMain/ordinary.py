"""Bounded real ordinary entrypoints in shipped layout; no neutralPackage or provider."""
import fcntl,json,os,pty,select,socket,struct,subprocess,sys,termios,time,uuid,signal,shutil
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
 assert all(shutil.which(name) is None for name in ['cc','gcc','clang','c++'])
 result['CompilerAbsentAtPlayerStartup']=True
 if mode.startswith('refuse-'):
  originalRecord=session.parent/'.boe_runtime/gm-runs/main.json';recordBefore=originalRecord.read_bytes() if originalRecord.exists() else None
  configPath=session/'config.json';cfg=json.loads(configPath.read_text())
  if mode=='refuse-auto':cfg['GmMainOwnerBackend']='Auto'
  if mode=='refuse-systemd':cfg['GmMainOwnerBackend']='SystemdUser'
  if mode=='refuse-command':cfg['GmCliLaunchCommand']=''
  if mode=='refuse-cwd':cfg['GmBridgeShellWorkingDirectory']=str(folder/'must-not-create')
  configPath.write_text(json.dumps(cfg))
  if mode=='refuse-package':(ship/'BookOfEternityGMBridge/runtimes/linux-x64/native/boe-lineage-supervisor').unlink()
  before={str(p):p.read_bytes() for p in session.rglob('*') if p.is_file()};result['BeforeFileCount']=len(before)
 if mode=='launcher':
  cfg=json.loads((session/'config.json').read_text());cfg['GmBridgePipeNameOverride']=pipe;(session/'config.json').write_text(json.dumps(cfg))
  args=['pwsh','-NoLogo','-NoProfile','-File',str(ship/'BookOfEternityClient/Launcher/bookofeternity.ps1'),'start-bridge','visible','-SessionPath',str(session)]
 elif mode=='daemon':args=['pwsh','-NoLogo','-NoProfile','-File',str(ship/'BookOfEternityClient/game_master_daemon.ps1'),'-GameSessionPath',str(session),'-PollingInterval','50','-LogFile',str(folder/'daemon.log')]
 else:args=['dotnet',str(ship/'BookOfEternityGMBridge/BookOfEternityGMBridge.dll'),'--host','--sessionPath',str(session),'--pipeName',pipe]
 result['Argv']=args
 process=subprocess.Popen(args,cwd=ship,stdin=slave,stdout=slave,stderr=slave,preexec_fn=own_terminal);pidfd=os.pidfd_open(process.pid)
 if mode.startswith('refuse-'):
  process.wait(timeout=5);receive();assert process.returncode!=0
  assert b'TTY_READY' not in capture
  if mode=='refuse-cold':assert originalRecord.read_bytes()==recordBefore and json.loads(recordBefore)['Disposition']=='Uncertain'
  else:assert not originalRecord.exists()
  assert not (folder/'must-not-create').exists()
  assert {str(p):p.read_bytes() for p in session.rglob('*') if p.is_file()}==before
  result['RefusedBeforeCreationAndCanonicalEffects']=True
 elif mode=='daemon':
  until(b'Waiting for turns...',12);result['PortableDaemonStartup']=True
  # Only this original unreaped direct child; independent outer guardian owns descendants.
  signal.pidfd_send_signal(pidfd,signal.SIGINT);result['ExitSignal']='SIGINT original daemon pidfd';process.wait(timeout=3)
 else:
  until(b'NEUTRAL READY')
  cfg=json.loads((session/'config.json').read_text());assert ('CONFIGURED_CWD:'+cfg['GmBridgeShellWorkingDirectory']).encode() in capture
  assert b'CONFIGURED_ARG2:gm-model-sentinel' in capture and b'CONFIGURED_ARG4:a b' in capture
  # Initial connection availability only: no operation has been written/replayed.
  deadline=time.monotonic()+3
  while not Path('/tmp/CoreFxPipe_'+pipe).exists() and time.monotonic()<deadline:receive()
  status=rpc({'command':'status'})['status'];binding=status['inputBindingId'];pid=status['shellPid'];result['OriginalStatus']=status
  record=json.loads((session.parent/'.boe_runtime/gm-runs/main.json').read_text());assert record['Disposition']=='Running';result['DurableRunning']=True
  for n,text in enumerate(['one Ж😀','two'],1):
   os.write(master,(text+'\r').encode());until(('RESULT'+str(n)+':'+text).encode())
  assert rpc({'command':'resize','columns':93,'rows':31})['ok'];until(b'RESIZE 31x93');result['ActualResize']=True
  assert rpc({'command':'addText','text':'canonical\r'})['ok'];until(b'CANONICAL_READY')
  assert rpc({'command':'addText','text':'\x04'})['ok'];until(b'CANONICAL_EOF');result['CanonicalEofRootAlive']=True
  same=rpc({'command':'status'})['status'];assert same['shellPid']==pid and same['inputBindingId']==binding;result['TwoInputsOneOriginal']=True
  if mode=='bridge':
   (folder/'first-epoch.log').write_bytes(capture);capture.clear()
   assert rpc({'command':'restartCLI'})['ok'];until(b'NEUTRAL READY')
   fresh=rpc({'command':'status'})['status'];record2=json.loads((session.parent/'.boe_runtime/gm-runs/main.json').read_text())
   assert record2['Identity']['Epoch']==record['Identity']['Epoch']+1 and fresh['inputBindingId']!=binding and fresh['shellPid']!=pid
   assert b'RESULT1:' not in capture;record=record2;result['FreshEpochAfterConfirmedStopNoReplay']=True
  identity={k[0].lower()+k[1:]:v for k,v in record['Identity'].items()}; identity['backend']=2
  stopped=rpc({'command':'shutdown','rootKey':identity['rootKey'],'expectedMainIdentity':identity})
  assert stopped['ok'],stopped
  process.wait(timeout=4);assert process.returncode==0
  settled=json.loads((session.parent/'.boe_runtime/gm-runs/main.json').read_text());assert settled['Disposition']=='Stopped';result['DurableStopped']=True
 result['Success']=True
except Exception as ex:result['Failure']=type(ex).__name__+': '+str(ex)
finally:
 if process is not None:
  if process.poll() is None:
   try:
    record=json.loads((session.parent/'.boe_runtime/gm-runs/main.json').read_text());identity={k[0].lower()+k[1:]:v for k,v in record['Identity'].items()};identity['backend']=2
    rpc({'command':'shutdown','rootKey':identity['rootKey'],'expectedMainIdentity':identity})
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
