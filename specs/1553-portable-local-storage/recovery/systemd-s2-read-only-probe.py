import ctypes,ctypes.util,datetime,json,os,stat,subprocess
from pathlib import Path
result={'TimestampUTC':datetime.datetime.now(datetime.timezone.utc).isoformat(),'UID':os.geteuid(),'Operations':'Read-only path/mount/library metadata and existing user bus/manager queries; no unit mutations/settings/installation'}
def meta(path):
 try:
  s=Path(path).stat();return {'Path':str(path),'Exists':True,'Mode':oct(stat.S_IMODE(s.st_mode)),'UID':s.st_uid,'Socket':stat.S_ISSOCK(s.st_mode),'Directory':stat.S_ISDIR(s.st_mode)}
 except OSError as e:return {'Path':str(path),'Exists':False,'Errno':e.errno,'Error':e.strerror}
result['PID1Comm']=Path('/proc/1/comm').read_text().strip()
result['RuntimePaths']=[meta('/run/user/'+str(os.geteuid())),meta('/run/user/'+str(os.geteuid())+'/bus')]
xdg=os.environ.get('XDG_RUNTIME_DIR');result['XdgRuntimeSet']=bool(xdg)
if xdg:result['XdgRuntimeMetadata']=meta(xdg)
bus=os.environ.get('DBUS_SESSION_BUS_ADDRESS');result['UserBusAddressSet']=bool(bus);result['UserBusAddressScheme']=bus.split(':',1)[0] if bus else None
result['SelfCgroup']=Path('/proc/self/cgroup').read_text();mounts=[]
for line in Path('/proc/self/mountinfo').read_text().splitlines():
 before,after=line.split(' - ',1);tail=after.split();head=before.split()
 if tail[0]=='cgroup2':
  mounts.append(dict(MountId=head[0],Device=head[2],Root=head[3],MountPoint=head[4],Options=head[5],Filesystem=tail[0],SuperOptions=tail[2]))
result['CgroupV2Mounts']=mounts
for m in mounts:
 m['MountMetadata']=meta(m['MountPoint'])
 events=Path(m['MountPoint'])/'cgroup.events'
 try:m['VisibleMountEvents']=events.read_text()[:4096]
 except OSError as e:m['VisibleMountEventsError']={'Errno':e.errno,'Error':e.strerror}
result['Commands']=[]
for args in [['systemctl','--version'],['systemctl','--user','show','--property=Version','--property=SystemState'],['busctl','--user','--auto-start=no','--timeout=2','call','org.freedesktop.DBus','/org/freedesktop/DBus','org.freedesktop.DBus','GetNameOwner','s','org.freedesktop.systemd1']]:
 try:
  p=subprocess.run(args,stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=4)
  result['Commands'].append(dict(Args=args,ExitCode=p.returncode,Stdout=p.stdout.decode(errors='replace')[:4096],Stderr=p.stderr.decode(errors='replace')[:4096]))
 except (OSError,subprocess.TimeoutExpired) as e:result['Commands'].append(dict(Args=args,Error=type(e).__name__+': '+str(e)))
name=ctypes.util.find_library('systemd');result['LibsystemdName']=name
if name:
 lib=ctypes.CDLL(name);symbols=['sd_bus_open_user','sd_bus_message_append_basic','sd_bus_message_read_array','sd_bus_match_signal','sd_bus_message_set_auto_start','sd_bus_message_set_allow_interactive_authorization','sd_bus_close_unref']
 result['SdBusSymbols']={s:hasattr(lib,s) for s in symbols}
 if all(result['SdBusSymbols'].values()):
  ptr=ctypes.c_void_p();lib.sd_bus_open_user.argtypes=[ctypes.POINTER(ctypes.c_void_p)];lib.sd_bus_open_user.restype=ctypes.c_int
  r=lib.sd_bus_open_user(ctypes.byref(ptr));result['SdBusOpenUser']={'Return':r,'Errno':-r if r<0 else None}
  if ptr.value:
   lib.sd_bus_close_unref.argtypes=[ctypes.c_void_p];lib.sd_bus_close_unref.restype=ctypes.c_void_p;lib.sd_bus_close_unref(ptr);result['SdBusClientClosed']=True
Path('/tmp/systemd-s2/read-only-capabilities.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
