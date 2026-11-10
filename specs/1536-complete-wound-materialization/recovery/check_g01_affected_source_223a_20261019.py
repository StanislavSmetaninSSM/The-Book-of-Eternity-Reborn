"""Read-only focused re-review; advancing-clock cleanup witness uses no OS processes."""
from pathlib import Path
import ast,json,hashlib,subprocess,types
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review')
G='specs/1536-complete-wound-materialization/recovery/l01-game-capture-g01/';REV='223a5e09a3998e528e2d6e514ecc91589b5c57c8';OLD='9d8d08093bb769f85ec79a88d98669f1570e0d57'
H=lambda b:hashlib.sha256(b).hexdigest()
def git(*a,root=R):return subprocess.check_output(['git','-C',str(root),*a])
def at(p,rev=REV):return git('show',rev+':'+p)
def read(p):return json.loads(at(p))
rows=[]
def ck(n,b):rows.append(dict(name=n,passed=bool(b)));assert b,n
ck('Exact clean writer and independent clone',all(git('rev-parse','HEAD',root=r).decode().strip()==REV and not git('status','--porcelain',root=r) for r in [R,C]))
receipt=Path('/tmp/1536-cloud-tools/g01-recovery-223a5e09.json');rec=json.loads(receipt.read_text());ck('Recovery214paths162pins',rec['sha']==REV and rec['remoteShaVerified'] and rec['clean'] and rec['changedPaths']==214 and rec['currentPinsVerified']==162)
p=read(G+'source-packet.json');ck('162 current pins and canonical digest',len(p['sourcePins'])==162 and H(json.dumps(p['sourcePins'],sort_keys=True,separators=(',',':')).encode())==p['sourcePinDigest']=='fab6a66189aa71da5df02cf4580926ef9879775be5d8dd3919309b7b5bfce74f')
ck('Current pins exact Git and restore',all(len(at(r['path']))==r['bytes'] and H(at(r['path']))==r['sha256'] and (C/r['path']).read_bytes()==at(r['path']) for r in p['sourcePins']))
ck('Controller and bindings requested hashes',H(at(G+'phase.py'))=='92155d2e8b6add95230fd68bba54ac27a71be2ad52685cbaf007c0d12e3ef89c' and H(at(G+'bindings.json'))=='9c6f4d1c6f3d80b4d5240b7f12560cac73b3adfbc4015f3828f7cf7754fe9960')
for name in ['JointInterfaceManagedCapture.cs','JointInterfaceManagedCapturePolicy.cs','JointInterfaceDiagnosticDelivery.cs','GameEngineTurnLifecycleTests.JointInterfaceCrash.cs']:
 path='BookOfEternityClient.IntegrationTests/'+name;ck('C# parent/wiring/policy unchanged '+name,at(path)==at(path,OLD))
ck('Category/selection/production unchanged',not git('diff','--name-only',OLD,REV,'--','tests/categories.json','tests/selection.json','BookOfEternityClient','BookOfEternityClient.TestSupport'))
oldchecks=D/'l01-game-capture-g01-source-independent-checks-20261019.json';oldreview=D/'l01-game-capture-g01-source-independent-review-20261019.json'
for name in ['l01-game-capture-g01-source-independent-review-20261019.json','l01-game-capture-g01-source-independent-review-20261019.md','l01-game-capture-g01-source-independent-checks-20261019.json','check_g01_whole_source_20261019.py']:
 ck('Original HOLD artifact exact '+name,at('specs/1536-complete-wound-materialization/recovery/'+name)==(D/name).read_bytes())
watch=at(G+'watchdog.py').decode();ps=at(G+'Collect.ps1').decode();mapper=at(G+'MapFrames.cs').decode();phase=at(G+'phase.py').decode()
ck('Launch accounted before identity/pidfd',watch.index("record['collectorLaunches'] = 1",watch.index('child = subprocess.Popen'))<watch.index("record['collector']['identity'] =",watch.index('child = subprocess.Popen'))<watch.index('collector_fd = os.pidfd_open'))
ck('Natural poll precedes forced fallback',watch.index('native = child.poll()',watch.index('finally:'))<watch.index('child.kill()'))
ck('Initial and after-positive-join census',watch.count('observe_children()')>=4 and "if record['collector']['joined']:" in watch and 'future target-service spawn' in watch)
ck('Known natural and forced cleanup distinct',"if not collector_forced or code != -signal.SIGKILL:" in watch and "else: record['collector']['cleanupExit']=code" in watch)
ck('PS pure positive cancellation predicate added',"if ($ParentCancelled) { return 'PARENT_OBSERVER_ERROR' }" in ps and '[IO.File]::Exists' in ps)
ck('Mapper method body and exclusive upper bound', 'method.RelativeVirtualAddress == 0' in mapper and 'pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()' in mapper and 'il >= body.Length' in mapper)
ck('Original3 map assertions plus2 exact boundary negatives',all(t in phase for t in ["'valid'","'wrong-mvid'","'zero-token'","'body-end'","'outside-body'","'IL_OFFSET_OUTSIDE_METHOD_BODY'"]))
for name,count in [('watchdog-fault-red.json',0),('watchdog-fault-green.json',4),('watchdog-fault-green-final.json',5)]:
 d=read(G+name);ck('Preserved fake proof '+name,d['passed']==count and d['nativeProcesses']==d['gameCases']==d['apiRequests']==0)
final=read(G+'watchdog-fault-green-final.json');ck('Final5fakeproof bound to current watchdog',final['sourceSha256']==H(at(G+'watchdog.py')) and len(final['cases'])==5)
ck('Published fake uses constant clock so no exhaustion witness', 'monotonic=lambda:100.0' in at(G+'check_watchdog.py').decode())
# Execute only current finally AST with a clock that reaches the shared deadline.
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
ck('Reproduced late-createdump signal at exhausted deadline',any(s.get('fd')==4 and s['at']>=105 for s in signals))
ck('Reproduced positive join timeout after exhausted remainder',any(w['at']>=105 and w['timeout']>0 for w in waits))
ck('No explicit deadline HOLD generated by current branch',not record['observerErrors'])
control=read(G+'control.json');ck('All native admissions still held',control['sourceAdmission']['status'].startswith('HOLD') and control['actualAdmission']['status']=='HOLD_PREP' and control['actualGameCasesExecuted']==control['diagnosticRequests']==control['dumpArtifactsCreated']==0)
ck('Unused run root',not Path(read(G+'bindings.json')['root']).exists())
result=dict(kind='G01_FOCUSED_AFFECTED_SOURCE_CHECKS',candidate=REV,base=OLD,checks=rows,passed=len(rows),nativeActions=0,
 advancingClockWitness=dict(cleanupStart=100,sharedDeadline=105,censuses=censuses,signals=signals,waits=waits,record=record),
 remainingFindings=['G01-S01 shared5 exhausted actions','G01-S02 File.Exists collapses UNKNOWN to absent'],resolved=['G01-S03','G01-S04'],
 sourcePinDigest=p['sourcePinDigest'],controllerSha256=H(at(G+'phase.py')),bindingsSha256=H(at(G+'bindings.json')),
 recovery=dict(path=str(receipt),sha256=H(receipt.read_bytes())))
o=D/'l01-game-capture-g01-affected-source-checks-223a-20261019.json';o.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(path=str(o),passed=len(rows),sha256=H(o.read_bytes()))))
