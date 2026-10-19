"""Read-only G01 source/binding review. Exact-source AST fakes never launch processes."""
from pathlib import Path
import ast, hashlib, json, subprocess, types
R=Path('/workspace/The-Book-of-Eternity-Reborn'); C=Path('/workspace/1536-cloud-clean-restore')
OUT=Path('/tmp/1536-cloud-tools/review'); REV='9d8d08093bb769f85ec79a88d98669f1570e0d57'; BASE='4e379a92e27d488bc602f743fc739507d6a96648'
P='specs/1536-complete-wound-materialization/recovery/'; G=P+'l01-game-capture-g01/'
H=lambda b:hashlib.sha256(b).hexdigest()
def git(*a,root=R):return subprocess.check_output(['git','-C',str(root),*a])
def at(p,rev=REV):return git('show',rev+':'+p)
def data(p):return json.loads(at(p))
def bind(p):b=at(p);return dict(path=p,bytes=len(b),sha256=H(b))
checks=[]
def check(name,value,detail=None):
 checks.append(dict(name=name,passed=bool(value),**({'detail':detail} if detail is not None else {})))
 if not value:raise AssertionError(name)
packet=data(G+'source-packet.json');cfg=data(G+'bindings.json');control=data(G+'control.json')
check('Writer exact clean candidate',git('rev-parse','HEAD').decode().strip()==REV and not git('status','--porcelain'))
check('Independent clone exact clean candidate',git('rev-parse','HEAD',root=C).decode().strip()==REV and not git('status','--porcelain',root=C))
receipt_path=Path('/tmp/1536-cloud-tools/g01-recovery-9d8d0809.json');receipt=json.loads(receipt_path.read_text())
check('Recovery receipt exact remote and independent clean recovery',receipt['sha']==REV and receipt['remoteShaVerified'] and receipt['clean'] and not receipt['localWriterObjectsCopied'] and receipt['changedPaths']==206 and receipt['currentPinsVerified']==161)
changed=git('diff','--name-only',BASE,REV).decode().splitlines()
check('Exactly17 bounded changed paths',len(changed)==17)
check('Changed sources bytes Git writer independent clone exact',all(at(p)==(R/p).read_bytes()==(C/p).read_bytes() for p in changed))
check('No production TestSupport unit source changes',not git('diff','--name-only',BASE,REV,'--','BookOfEternityClient','BookOfEternityClient.TestSupport','BookOfEternityClient.Tests'))
check('161 distinct current pins exact',len(packet['sourcePins'])==len({r['path'] for r in packet['sourcePins']})==161 and all(len(at(r['path']))==r['bytes'] and H(at(r['path']))==r['sha256'] and at(r['path'])==(C/r['path']).read_bytes() for r in packet['sourcePins']))
check('Current canonical pin digest exact',H(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['sourcePinDigest']=='1c8c987efcc7e04d8e6bbb9f265029c58ed05fc3ebeadbda1deb49645687baef')
prior=data(P+'l01-delivery-d01-source-packet-20261018.json')
deltas=[dict(path=r['path'],priorSha256=r['sha256'],sha256=H(at(r['path'])),bytes=len(at(r['path']))) for r in prior['sourcePins'] if H(at(r['path']))!=r['sha256']]
check('Historical D01 exactly3 declared current deltas',deltas==packet['explicitD01Deltas'] and len(deltas)==3)
for name in ['l01-delivery-d01-source-packet-20261018.json','l01-managed-stack-capability-control-20261018.json','t179pd-ni-iobs-l01-source-packet-20261018.json','t179pd-ni-iobs-l01-cloud-qualification-20261018.json','t179pd-ni-iobs-l01-method-row-ledger-20261018.json','t148-m01-source-packet-20261018.json']:
 check('Historical packet/ledger unchanged '+name,at(P+name)==at(P+name,BASE))
for group in packet['historicalPins']:
 old=data(group['path']);delta=[dict(path=r['path'],priorSha256=r['sha256'],sha256=H(at(r['path'])),bytes=len(at(r['path']))) for r in old['sourcePins'] if H(at(r['path']))!=r['sha256']]
 check('Historical explicit exception list '+Path(group['path']).name,delta==group['explicitCurrentDeltas'])
check('Controller exact requested SHA',H(at(G+'phase.py'))=='db5c593d0bd599ea9857b9e33e9a50082134c82146dec11630cc6a85d8394aca')
check('Bindings exact requested SHA',H(at(G+'bindings.json'))=='e0d6aa868b52dd92fec36f7c34b01106fc06792c289add93ce0780ffe6d3f200')
check('395 distinct immutable input declarations',len(cfg['inputs'])==len({r['path'] for r in cfg['inputs']})==395)
# Read each declared tool/import input; no invocation, SDK query or procfs read.
check('All395 declared physical input bytes currently match',all(len(Path(r['path']).read_bytes())==r['bytes'] and H(Path(r['path']).read_bytes())==r['sha256'] for r in cfg['inputs']))
check('Request executable/library/helper input15 hashes match',len(cfg['requestInputs'])==15 and all(len(Path(r['path']).read_bytes())==r['bytes'] and H(Path(r['path']).read_bytes())==r['sha256'] for r in cfg['requestInputs']))
check('Retained unit439 manifest and4 metadata project groups',len(cfg['unit']['binFiles'])==439 and len(cfg['metadata'])==4)
def unit_tree(rev):
 rows=git('ls-tree','-r','--full-tree',rev).splitlines();rows=[r for r in rows if not r.split(b'\t',1)[1].startswith((b'specs/1536-complete-wound-materialization/',b'BookOfEternityClient.IntegrationTests/')) and r.split(b'\t',1)[1] not in (b'tests/categories.json',b'tests/selection.json')]
 return dict(count=len(rows),sha256=H(b'\n'.join(rows)+b'\n'))
check('Complete outside-integration unit tree46787 exact versus d3',unit_tree(REV)==unit_tree(cfg['unit']['compiledSource'])==cfg['unchangedUnitTree'] and cfg['unchangedUnitTree']['count']==46787)
check('No new root consumed',not Path(cfg['root']).exists())
crash='BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrash.cs';old=at(crash,BASE).decode();new=at(crash).decode()
check('All original Assert lines identical ordered', [l.strip() for l in old.splitlines() if 'Assert.' in l]==[l.strip() for l in new.splitlines() if 'Assert.' in l])
check('Original child220 wait retained and starts before consumer', 'WaitAsync(TimeSpan.FromSeconds(220))' in new and new.index('var nativeWait =')<new.index('captureTask = JointInterfaceManagedCapture.ObserveAsync'))
check('Original cleanup code and game return retained',all(x in new for x in ['child.Kill(entireProcessTree: true)','await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10))','if (cleanupFailure is not null && failure is null) throw cleanupFailure;']))
check('Retention is capture-only before container deletion','bool retainCapture = false' in new and new.index('if (retainCapture)')<new.index('new CleanupOwnedFixture(root, output).Dispose();'))
for p in ['BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs','BookOfEternityClient.IntegrationTests/JointInterfaceManagedCapturePolicy.cs',G+'PolicyProbe.cs']:
 check('Accepted delivery/policy/probe unchanged '+Path(p).name,at(p)==at(p,BASE))
cat=data('tests/categories.json');oldcat=json.loads(at('tests/categories.json',BASE))
# Catalog root shape can be object or array; compare outside the exact owner semantically.
def categories(x):return x if isinstance(x,list) else x['categories']
a=categories(cat);b=categories(oldcat);target='fr063-joint-interface-crash-diagnostic'
check('Other categories byte-semantic unchanged',[r for r in a if r['id']!=target]==[r for r in b if r['id']!=target])
a1=next(r for r in a if r['id']==target);b1=next(r for r in b if r['id']==target)
check('Diagnostic membership/bounds unchanged',{k:v for k,v in a1.items() if k not in ('responsibility','changeHints')}=={k:v for k,v in b1.items() if k not in ('responsibility','changeHints')})
check('Selection singleton only',len(data('tests/selection.json')['selections'])==1 and data('tests/selection.json')['selections'][0]['category']==target)
check('Resolved option1 and no execution gate',control['phaseClarification']['userChoice']==1 and control['phaseClarification']['resolution']==packet['phaseContract'] and control['sourceAdmission']['status']=='HOLD_WHOLE_SOURCE' and control['actualAdmission']['status']=='HOLD_PREP' and control['actualGameCasesExecuted']==control['diagnosticRequests']==control['dumpArtifactsCreated']==0)
check('Private lifecycle prohibition explicit',control['dumpLifecycle']['automaticDeletion']=='NONE' and control['dumpLifecycle']['commitAttachArchive']=='FORBIDDEN_CORE_OR_RAW_REPORT')
phase=at(G+'phase.py').decode();watch=at(G+'watchdog.py').decode();parent=at('BookOfEternityClient.IntegrationTests/JointInterfaceManagedCapture.cs').decode();ps=at(G+'Collect.ps1').decode();mapper=at(G+'MapFrames.cs').decode()
check('Tools/ZERO and actual admissions separate with consumed lease',all(s in phase for s in ['SOURCE_ACCEPTED_G01_ZERO_ONLY','PREP_ACCEPTED_G01_ONE_ACTUAL_CASE','CONSUMED_FOR_NAMED_RUN_NO_RETRY','phase.mkdir(mode=0o700)','sourcePinDigest','prepRecords']))
check('Fresh integration no restore + strict actual property/import/catalog/plan before PREP',all(s in phase for s in ['deadline=start+600',"'--no-restore'","verify(input_map[str(path)])","['-ValidateCatalog','-NoBuild']","'-PlanOnly','-NoBuild'",'privateBinClosures']))
check('Offline map has no capture/target launch', 'WriteDump' not in mapper and 'Process.Start' not in mapper and 'offlineOnly = true' in mapper)
check('No observer signal targets resumer',"signal.pidfd_send_signal(fd,signal.SIGKILL)" in watch and 'signal.pidfd_send_signal(target' not in watch and "owned.append(('collector', collector_fd))" in watch and "owned.append(('createdump',fd))" in watch)
check('Maximum declared socket106 bytes',len((cfg['root']+'/actual/tmp/g-'+('9'*12)+'/tmp/dotnet-diagnostic-'+('9'*10)+'-'+('9'*20)+'-socket').encode())==106)
# Source-bound fake execution: only exact launch statements and finally body with inert substitutes.
tree=ast.parse(watch);main=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='main');outer=next(n for n in main.body if isinstance(n,ast.Try))
branch=next(n for n in outer.body if isinstance(n,ast.If) and isinstance(n.test,ast.Name) and n.test.id=='cancellation')
launch=branch.orelse;first=next(i for i,n in enumerate(launch) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='child' for t in n.targets));last=next(i for i,n in enumerate(launch) if isinstance(n,ast.While))
launch_code=compile(ast.fix_missing_locations(ast.Module(body=launch[first:last],type_ignores=[])),'G01-exact-launch-AST','exec');cleanup_code=compile(ast.fix_missing_locations(ast.Module(body=outer.finalbody,type_ignores=[])),'G01-exact-finally-AST','exec')
source_lib=ast.parse(at(P+'l01-managed-stack-s01/smoke.py').decode());safe_funcs=[n for n in source_lib.body if isinstance(n,ast.FunctionDef) and n.name in ('close_observed','observe_collector')]
libenv={'json':json};exec(compile(ast.fix_missing_locations(ast.Module(body=safe_funcs,type_ignores=[])),'S01-exact-observers-AST','exec'),libenv)
final_tree=ast.parse(at(P+'t148-m01-run-phase-20261018.py').decode());ff=next(n for n in final_tree.body if isinstance(n,ast.FunctionDef) and n.name=='finalize');fe={};exec(compile(ast.fix_missing_locations(ast.Module(body=[ff],type_ignores=[])),'M01-exact-finalize-AST','exec'),fe)
class FakeFile:
 def __truediv__(self,key):return self
 def read_text(self):return ''
class FakeChild:
 pid=4242
 def __init__(self,code):self.code=code;self.waits=[]
 def wait(self,timeout):
  self.waits.append(timeout)
  if self.code is None:raise TimeoutError('fake direct collector still running')
  return self.code
proofs=[]
for case in ['pidfd_setup_failure','natural7_before_cleanup_observation']:
 child=FakeChild(None if case=='pidfd_setup_failure' else 7);signals=[];launches=[]
 def popen(*a,**k):launches.append('Popen');return child
 def pidfd(pid):raise OSError('injected pidfd setup failure')
 record=dict(nativeExit=None,observerErrors=[],collector=None,collectorLaunches=0,diagnosticRequests=0,apiOutcome='UNREACHED')
 env=dict(child=None,collector_fd=None,out=None,err=None,dumps={},record=record,command=['fake'],root=FakeFile(),env={},started=0,
  time=types.SimpleNamespace(monotonic=lambda:100.0),os=types.SimpleNamespace(pidfd_open=pidfd,getpid=lambda:100,close=lambda fd:None),
  subprocess=types.SimpleNamespace(Popen=popen,DEVNULL=-3),select=types.SimpleNamespace(select=lambda r,w,x,t:(list(r),[],[])),
  signal=types.SimpleNamespace(SIGKILL=9,pidfd_send_signal=lambda fd,s:signals.append((fd,s))),
  lib={'identity':lambda p:dict(pid=p,parentPid=100),'close_observed':libenv['close_observed'],'observe_collector':libenv['observe_collector']})
 if case=='pidfd_setup_failure':
  try:exec(launch_code,env)
  except BaseException as e:record['observerErrors'].append(repr(e))
 else:
  env.update(child=child,collector_fd=3);record.update(collectorLaunches=1,collector=dict(nativeExit=None,joined=False));record['observerErrors'].append('Injected observer error before next poll')
 exec(cleanup_code,env);exitcode=fe['finalize'](record,lambda r:None,lambda r:None)
 proofs.append(dict(case=case,record=record,simulatedPopenCalls=len(launches),simulatedSignals=signals,waits=child.waits,finalizerExit=exitcode,nativeActions=0))
 if case=='pidfd_setup_failure':check('Reproduced S01 untracked live collector after pidfd failure',len(launches)==1 and not signals and record['collectorLaunches']==0 and record['collector'] is None and any('Collector join' in x for x in record['observerErrors']))
 else:check('Reproduced S03 known natural7 stored only as cleanupExit and masked by2',record['nativeExit'] is None and record['collector']['cleanupExit']==7 and not signals and exitcode==2)
check('S01 cleanup has no final createdump rediscovery',not any(isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=='observe_children' for x in outer.finalbody for n in ast.walk(x)))
check('S02 positive cancel file written but collector does not inspect it','Path.Combine(dir, "cancel")' in parent and "root/'cancel'" in watch and "'cancel'" not in ps and '"cancel"' not in ps)
check('S04 mapper only intMaxValue, no actual body-bound validation','il > int.MaxValue' in mapper and 'p.Offset <= il' in mapper and 'GetMethodBody' not in mapper and 'GetILBytes' not in mapper)
check('S04 source-position counterexample accepted by current scalar range',0x7fffffff<=0x7fffffff and max([0,2,4])<=0x7fffffff)
result=dict(kind='G01_WHOLE_SOURCE_READONLY_CHECKS',candidate=REV,base=BASE,checks=checks,passed=sum(x['passed'] for x in checks),sourceFindingsExpected=['G01-S01','G01-S02','G01-S03','G01-S04'],sourceBoundFakes=proofs,sourcePinDigest=packet['sourcePinDigest'],controllerSha256=H(at(G+'phase.py')),bindingsSha256=H(at(G+'bindings.json')),changedPathBindings=[bind(p) for p in changed],readOnlyNativeActions=0,limits='Passing checks verify bindings and reproduce source defects; they do not accept SOURCE or execute proposed CLR/PS/native proofs.',recovery=dict(path=str(receipt_path),sha256=H(receipt_path.read_bytes())))
out=OUT/'l01-game-capture-g01-source-independent-checks-20261019.json';out.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps(dict(path=str(out),checks=len(checks),sha256=H(out.read_bytes())),ensure_ascii=False))
