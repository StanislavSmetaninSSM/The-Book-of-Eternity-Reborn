"""Meaningful fake-proc checks only: no game/native/controller/process inspection or execution."""
from pathlib import Path
import importlib.util,json,tempfile,shutil,unittest.mock as mock,sys,hashlib
sys.dont_write_bytecode=True
source=Path(sys.argv[1]);spec=importlib.util.spec_from_file_location('profile_under_test',source);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
checks=[]
def require(value,label):assert value,label;checks.append(label)
def stat(pid=123,birth=100,user=10,system=3,state='S'):
 fields=['0']*40;fields[0]=state;fields[1]='1';fields[11]=str(user);fields[12]=str(system);fields[17]='1';fields[19]=str(birth);fields[20]='8192';fields[21]='2'
 return f'{pid} (tricky ) comm) '+ ' '.join(fields)+'\n'
class Fixture:
 def __init__(self,base,name):
  self.root=base/name;self.root.mkdir();self.proc=self.root/'proc';self.proc.mkdir();self.cg=self.root/'cgroup';self.cg.mkdir();self.mutable=self.root/'mutable';self.mutable.mkdir();self.own=self.mutable/'boe-joint-interface-crash-fake';self.own.mkdir();self.childroot=self.own/'tmp'/'fixture';self.childroot.mkdir(parents=True);self.dotnet=self.root/'dotnet';self.repo=self.root/'repo';self.out=self.root/'samples.jsonl';self.pid=self.proc/'123';self.pid.mkdir();self.task=self.pid/'task'/'123';self.task.mkdir(parents=True)
  boot=self.proc/'sys/kernel/random';boot.mkdir(parents=True);(boot/'boot_id').write_text('fake-boot\n')
  for n,v in {'cpu.max':'400000 100000\n','cpu.stat':'usage_usec 10\nnr_throttled 0\nthrottled_usec 0\n','memory.max':'999999\n','memory.current':'100\n','memory.events':'high 0\nmax 0\noom 0\noom_kill 0\n'}.items():(self.cg/n).write_text(v)
  self.parent=dict(Nonce='a'*32,ParentPid=99,Cut=1,Fault=None,Diagnostic=True);self.saveparent();self.argv=self.makeargv();self.saveargv();self.setstat()
  for p in [self.pid,self.task]:(p/'wchan').write_text('futex_wait_queue\n')
  (self.pid/'io').write_text('read_bytes: 0\nwrite_bytes: 1\n');(self.pid/'status').write_text('Name:\tfake\nState:\tS\nThreads:\t1\nVmRSS:\t8 kB\nvoluntary_ctxt_switches:\t1\n')
  self.s=m.Sampler(self.mutable,self.dotnet,self.repo,self.out,self.proc,self.cg)
 def makeargv(self):
  b=self.repo/'BookOfEternityClient.IntegrationTests/bin/Debug/net8.0'
  return [str(self.dotnet/'dotnet'),'exec','--runtimeconfig',str(b/'BookOfEternityClient.IntegrationTests.runtimeconfig.json'),'--depsfile',str(b/'BookOfEternityClient.IntegrationTests.deps.json'),str(b/'BookOfEternityClient.TestSupport.dll'),'engine-joint-interface-resume',str(b/'BookOfEternityClient.IntegrationTests.dll'),str(self.childroot),str(self.own/'resume.json'),'1',str(self.own)]
 def saveargv(self):(self.pid/'cmdline').write_bytes(('\0'.join(self.argv)+'\0').encode())
 def saveparent(self):(self.own/'parent.json').write_text(json.dumps(self.parent))
 def setstat(self,birth=100,user=10):
  for p in [self.pid,self.task]:(p/'stat').write_text(stat(birth=birth,user=user))
 def capture(self):self.s.capture(m.time.monotonic());return json.loads(self.out.read_text().splitlines()[-1])
 def close(self):return self.s.finish(0)
with tempfile.TemporaryDirectory(prefix='l01-fake-proc-check-')as tmp:
 base=Path(tmp)
 x=Fixture(base,'positive');a=x.capture();require(len(a['entries'])==1 and not a['gaps'],'exact positive child selected');e=a['entries'][0];require(e['identity']==dict(pid=123,startTick=100),'process birth binding');require(e['statAfter']['utime']==10 and e['statAfter']['stime']==3,'raw CPU ticks parsed');require(e['tasks'][0]['tid']==123 and e['tasks'][0]['startTick']==100,'thread birth binding');require(e['binding']['nonce']=='a'*32,'nonce binding');x.setstat(user=25);a2=x.capture();require(a2['entries'][0]['statAfter']['utime']-e['statAfter']['utime']==15,'CPU delta keeps process/thread distinct');f=x.close();require(f['samplerTotalCpuSeconds']>=0 and a2['samplerCpuSeconds']>=0,'overhead recorded')
 x=Fixture(base,'foreign');x.argv[12]=str(base/'foreign');x.saveargv();a=x.capture();require(not a['entries'] and not x.s.identities,'foreign root excluded');x.close()
 x=Fixture(base,'escape');x.argv[9]=str(x.own/'tmp'/'..'/'..'/'escape');x.saveargv();a=x.capture();require(not a['entries'] and a['gaps'],'noncanonical containment escape held');x.close()
 x=Fixture(base,'noncechange');x.capture();x.parent['Nonce']='b'*32;x.saveparent();a=x.capture();require(a['gaps'] and any('binding changed'in z['error']for z in a['gaps']),'same identity changed nonce held');require(a['priorIdentityReconciliation'][0]['observation']=='unknown-live-identity-not-selected','unselected live prior identity reconciled');x.close()
 x=Fixture(base,'missingparent');original=Path.read_bytes
 def missing_parent(p):
  if p==x.own/'parent.json':raise FileNotFoundError('injected missing parent while PID live')
  return original(p)
 with mock.patch.object(Path,'read_bytes',missing_parent):a=x.capture()
 require(not a['entries'] and any(z['stage']=='process-file-missing-still-live-or-reused'for z in a['gaps']),'missing nonstat file does not imply process gone');x.close()
 x=Fixture(base,'deniedio');original=Path.read_text
 def denied_io(p,*a,**kw):
  if p==x.pid/'io':raise PermissionError('injected IO read denial')
  return original(p,*a,**kw)
 with mock.patch.object(Path,'read_text',denied_io):a=x.capture()
 require(len(a['entries'])==1 and any(z['stage']=='io'for z in a['gaps']) and 'io'not in a['entries'][0],'read denial retained without fabricated zero');require(x.close()['status']=='OBSERVATION_GAPS_FREEZE_NEXT_RELEASE','gaps freeze advancement')
 x=Fixture(base,'taskmissing');original=Path.read_text
 def missing_wchan(p,*a,**kw):
  if p==x.task/'wchan':raise FileNotFoundError('injected missing wchan while TID live')
  return original(p,*a,**kw)
 with mock.patch.object(Path,'read_text',missing_wchan):a=x.capture()
 require(any(z['stage']=='task-file-missing-still-live-or-reused'for z in a['gaps']),'missing task field reprobes live TID');require(any(z['stage']=='incomplete-task-sample'for z in a['gaps']),'incomplete task retained aggregate gap');x.close()
 x=Fixture(base,'gone');x.capture();shutil.rmtree(x.pid);a=x.capture();require(a['priorIdentityReconciliation'][0]['observation']=='confirmed-absent-on-stat-reprobe','prior child absence doubleprobed');require((123,100)in x.s.retired,'confirmed gone identity retired');x.close()
 x=Fixture(base,'reuse');x.capture();x.setstat(birth=101);a=x.capture();require(a['entries'][0]['identity']['startTick']==101,'new birth separate identity');require(a['priorIdentityReconciliation'][0]['observation']=='prior-identity-ended-pid-reused','reused PID old identity explicitly reconciled');require(len(x.s.identities)==2,'CPU counters never pool reused identities');x.close()
 x=Fixture(base,'malformed');(x.pid/'stat').write_text('broken');a=x.capture();require(not a['entries'] and a['gaps'],'malformed stat held');x.close()
 x=Fixture(base,'truncated');
 for tid in range(124,380):
  p=x.pid/'task'/str(tid);p.mkdir();(p/'stat').write_text(stat(pid=tid,birth=101));(p/'wchan').write_text('0\n')
 a=x.capture();require(len(a['entries'][0]['tasks'])==256 and any(z['stage']=='task-limit'for z in a['gaps']),'256 thread truncation explicit');x.close()
 x=Fixture(base,'midreadreuse');original=Path.read_bytes;n=0
 def mutate_birth(p):
  global n
  if p==x.pid/'stat':
   n+=1
   if n==2:return stat(birth=101).encode()
  return original(p)
 with mock.patch.object(Path,'read_bytes',mutate_birth):a=x.capture()
 require(not a['entries'] and any('identity/argv changed'in z['error']for z in a['gaps']),'birth mutation during sample rejected');x.close()
 x=Fixture(base,'missingtaskgone');original=Path.read_bytes;n=0
 def remove_task(p):
  global n
  if p==x.task/'stat':
   n+=1
   if n==1:shutil.rmtree(x.task);raise FileNotFoundError('injected actual TID disappearance')
  return original(p)
 with mock.patch.object(Path,'read_bytes',remove_task):a=x.capture()
 require(a['entries'][0]['tasks'][0]['observation']=='confirmed-absent-on-stat-reprobe' and any(z['stage']=='incomplete-task-sample'for z in a['gaps']),'actual task disappearance retained as explicit gap');x.close()
report=dict(kind='WRITER_FAKE_PROC_ACCOUNTING_CHECK_NOT_NATIVE_OR_INDEPENDENT_ACCEPTANCE',source=str(source),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),checkerSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),checksPassed=len(checks),checks=checks,findings=[],gameNativeExecuted=False)
out=Path(sys.argv[2]);assert not out.exists();out.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
