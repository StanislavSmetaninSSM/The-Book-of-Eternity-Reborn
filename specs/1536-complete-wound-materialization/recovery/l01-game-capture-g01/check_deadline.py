"""Source-bound advancing-clock regression, all process/handle callbacks inert."""
from pathlib import Path
import ast, hashlib, json, types
source=Path(__file__).with_name('watchdog.py');watch=source.read_text()
t=ast.parse(watch);m=next(n for n in t.body if isinstance(n,ast.FunctionDef) and n.name=='main');tr=next(n for n in m.body if isinstance(n,ast.Try));code=compile(ast.fix_missing_locations(ast.Module(body=tr.finalbody,type_ignores=[])),'G01223a-exact-finally','exec')
clock=[100.0];signals=[];waits=[];censuses=[];dumps={};ended=set();record=dict(nativeExit=None,collector=dict(nativeExit=None,joined=False),collectorLaunches=1,observerErrors=[])
class Child:
 pid=4242
 native=None
 def poll(self):return self.native
 def kill(self):signals.append(dict(kind='direct',at=clock[0]))
 def wait(self,timeout):waits.append(dict(timeout=timeout,at=clock[0]));return self.native
child=Child()
class P:
 def __init__(self,*a):pass
 def __truediv__(self,k):return self
 def resolve(self,strict=True):return '/createdump'
 def read_bytes(self):return b'createdump\0target\0dump\0'
 def read_text(self):return ''
def census():
 censuses.append(clock[0])
 if len(censuses)==2:dumps[6060]=dict(identity=dict(pid=6060,parentPid=100),command=['createdump','target','dump'],fd=4)
def signal(fd,sig):
 signals.append(dict(fd=fd,at=clock[0]))
 if fd==4:ended.add(fd)
def select_fn(r,w,x,timeout):
 if timeout>0 and 3 in r and child.native is None:
  clock[0]+=timeout;child.native=-9;ended.add(3)
 return ([fd for fd in r if fd in ended],[],[])
def close_observed(rec,label,f):
 try:f()
 except BaseException as e:rec['observerErrors'].append(repr(e))
env=dict(record=record,child=child,collector_fd=3,dumps=dumps,out=None,err=None,root=P(),started=100,
 time=types.SimpleNamespace(monotonic=lambda:clock[0]),observe_children=census,select=types.SimpleNamespace(select=select_fn),
 signal=types.SimpleNamespace(SIGKILL=9,pidfd_send_signal=signal),Path=P,cfg={'Createdump':'/createdump'},os=types.SimpleNamespace(close=lambda fd:None),
 lib={'identity':lambda p:dict(pid=p,parentPid=100),'close_observed':close_observed,'observe_collector':lambda rec,read:None})
exec(code,env)

checks=[dict(name='no signal at or after shared deadline',passed=not any(s['at']>=105 for s in signals)),
 dict(name='no positive wait at or after shared deadline',passed=not any(w['at']>=105 and w['timeout']>0 for w in waits)),
 dict(name='exhaustion is explicit HOLD',passed=bool(record['observerErrors'])),
 dict(name='forced cleanup not promoted to native',passed=record['nativeExit'] is None)]
result=dict(kind='G01_ADVANCING_CLOCK_FAKE',sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),checks=checks,passed=sum(c['passed'] for c in checks),
 cleanupStart=100,sharedDeadline=105,censuses=censuses,signals=signals,waits=waits,record=record,nativeProcesses=0,gameCases=0,apiRequests=0)
print(json.dumps(result,indent=2));raise SystemExit(0 if all(c['passed'] for c in checks) else 1)
