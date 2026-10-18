"""Static D02 SOURCE review + fake run_owned fault proof. No SDK/proc/native calls."""
import ast,hashlib,json,pathlib,stat,subprocess,types,zipfile
R=pathlib.Path('/workspace/The-Book-of-Eternity-Reborn');C=pathlib.Path('/workspace/1536-cloud-clean-restore');D=pathlib.Path('/tmp/1536-cloud-tools/review');Q=pathlib.Path('specs/1536-complete-wound-materialization/recovery');P=Q/'l01-delivery-d02'
BASE='e56782962f6620584c59566381c22d813d409896';SHA='96f08c8bb2d534c00bcbca4ff5a279554445d58d'
def h(b):return hashlib.sha256(b).hexdigest()
def git(*args):return subprocess.check_output(['git','-C',str(R),*args])
def j(p):return json.loads(p.read_bytes())
checks=[]
def ck(name,value,detail=None):checks.append(dict(name=name,passed=bool(value),**({'detail':detail} if detail is not None else {})))
source=git('show',SHA+':'+str(P/'phase.py'));b=j(R/P/'bindings.json');control=j(R/P/'control.json');packet=j(R/Q/'l01-delivery-d01-source-packet-20261018.json');proof=j(R/P/'checks-result.json')
(D/'d02-phase-original-source-96f08c8b.py').write_bytes(source)
ck('exact source and pure-check binding',source==(R/P/'phase.py').read_bytes() and h(source)==proof['controllerSha256'])
changes=git('diff','--name-only',BASE,SHA).decode().splitlines();ck('nine bounded paths',len(changes)==9 and all(x.startswith('specs/1536-complete-wound-materialization/') for x in changes),changes)
ck('149 original pins untouched',len(packet['sourcePins'])==149 and not(set(changes)&{x['path'] for x in packet['sourcePins']}) and git('show',BASE+':'+str(Q/'l01-delivery-d01-source-packet-20261018.json'))==(R/Q/'l01-delivery-d01-source-packet-20261018.json').read_bytes())
ck('two closed gates and no game',control['source']['status']=='HOLD_SOURCE_REQUIRED' and control['synthetic']['status']=='HOLD_PREP_REQUIRED' and control['gameplayRelease']==control['crash5Release']=='NONE' and not control['originalCatalog124Replaced'])
ck('source digest/current root',b['sourcePinDigest']==packet['sourcePinDigest'] and b['root']=='/tmp/l01d02-cd3d584b5e27457f' and b['oldOutcomeReplaced'] is False)
for revision in [BASE]+[p['compiledSource'] for p in b['projects']]:
 rows=git('ls-tree','-r','--full-tree',revision).splitlines();rows=[x for x in rows if not x.split(b'\t',1)[1].startswith(b'specs/1536-complete-wound-materialization/')]
 ck('nonfeature Git tree '+revision,dict(count=len(rows),sha256=h(b'\n'.join(rows)+b'\n'))==b['trackedNonFeatureTree'])
miss=[]
for row in b['inputs']:
 p=pathlib.Path(row['path']);data=p.read_bytes()
 if p.is_symlink() or len(data)!=row['bytes'] or h(data)!=row['sha256']:miss.append(str(p))
ck('360 frozen helper/evidence/SDK/package inputs exact',len(b['inputs'])==360 and not miss,miss)
inputmap={p['path']:p for p in b['inputs']};ck('unique input paths',len(inputmap)==len(b['inputs']))
for name in ['l01-managed-stack-s01/smoke.py','l01-delivery-d01/phase.py','t148-m01-run-phase-20261018.py','l01-build-tooling-unit-once-20261018.py','l01-managed-stack-capability-inventory-20261018.json','l01-delivery-d01-source-packet-20261018.json']:
 ck('pre-import input bound '+name,str(R/Q/name) in inputmap)
image=[]
for project in b['projects']:
 root=pathlib.Path(project['sourceBin']);rows=[]
 for p in sorted(root.rglob('*')):
  assert not p.is_symlink()
  if p.is_file():
   data=p.read_bytes();rows.append(dict(path=str(p.relative_to(root)),bytes=len(data),sha256=h(data),mode=stat.S_IMODE(p.stat().st_mode)))
 ck(project['key']+' exact complete source closure',rows==project['binFiles'] and len(rows)==(460 if project['key']=='integration' else 439))
 ck(project['key']+' safe unique relative manifest paths',len({x['path'] for x in rows})==len(rows) and all(not pathlib.PurePosixPath(x['path']).is_absolute() and '..' not in pathlib.PurePosixPath(x['path']).parts for x in rows))
 ck(project['key']+' distinct own three metadata files',{pathlib.Path(x['path']).name for x in project['metadataFiles']}=={project['name']+'.csproj.nuget.g.props',project['name']+'.csproj.nuget.g.targets','project.assets.json'} and len(project['metadataFiles'])==3)
 ck(project['key']+' metadata exact',all(len(pathlib.Path(x['path']).read_bytes())==x['bytes'] and h(pathlib.Path(x['path']).read_bytes())==x['sha256'] for x in project['metadataFiles']))
 deps=j(root/(project['name']+'.deps.json'));runtime=j(root/(project['name']+'.runtimeconfig.json'));image.append((project,root,deps,runtime,rows))
 ck(project['key']+' target runtime/config',deps['runtimeTarget']['name']=='.NETCoreApp,Version=v8.0' and runtime['runtimeOptions']['tfm']=='net8.0' and runtime['runtimeOptions']['frameworks']==[dict(name='Microsoft.NETCore.App',version='8.0.0'),dict(name='Microsoft.AspNetCore.App',version='8.0.0')])
 absent=[]
 for libs in deps['targets'].values():
  for lib,obj in libs.items():
   for kind in ['runtime','native','resources']:
    for rel in obj.get(kind,{}):
     if rel.endswith('/_._'):continue
     p=pathlib.Path(rel);cands=[root/p,root/p.name]+([root/p.parent.name/p.name] if kind=='resources' else [])
     if not any(x.is_file() for x in cands):absent.append((lib,kind,rel))
 ck(project['key']+' direct dependency files present',not absent,absent)
 ck(project['key']+' no unaccounted RID runtime targets',all(not obj.get('runtimeTargets') for libs in deps['targets'].values() for obj in libs.values()))
a,z=image;am={x['path']:x['sha256'] for x in a[4]};zm={x['path']:x['sha256'] for x in z[4]};shared=set(am)&set(zm)
ck('423 shared relative names remain unmerged with four known differences',len(shared)==423 and {x for x in shared if am[x]!=zm[x]}=={'BookOfEternityClient.dll','BookOfEternityClient.pdb','BookOfEternityClient.TestSupport.dll','BookOfEternityClient.TestSupport.pdb'})
common=set(a[2]['libraries'])&set(z[2]['libraries']);ck('24 common deps library records identical',len(common)==24 and all(a[2]['libraries'][x]==z[2]['libraries'][x] for x in common))
oldz=zipfile.ZipFile(R/Q/'l01-delivery-d01-first-prep-failure-originals-20261018.zip');old=j(R/Q/'l01-delivery-d01-first-prep-failure-20261018.json');oldrecord=json.loads(oldz.read('phase/record.json'))
integration=next(x for x in image if x[0]['key']=='integration');prefix='BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/'
expected=[dict(path=x['path'][len(prefix):],bytes=x['bytes'],sha256=x['sha256']) for x in oldrecord['binaryInventory'] if x['path'].startswith(prefix)]
ck('integration460 anchored in original failure snapshot',sorted(expected,key=lambda x:x['path'])==sorted([{k:v for k,v in x.items() if k!='mode'} for x in integration[4]],key=lambda x:x['path']))
log=oldz.read('runner/dotnet-test.log').decode();ck('successful integration and original failed catalog coexist','===== Build-integration: exit 0 =====' in log and '===== integration-discovery: exit 0 =====' in log and oldrecord['nativeExit']==124)
b01=j(R/Q/'l01-build-tooling-b01-first-outcome-20261018.json');unit=next(x for x in image if x[0]['key']=='unit');anchors=[]
for x in b01['outputs']:
 p=unit[1]/pathlib.Path(x['path']).name
 if p.is_file():anchors.append(x['project']);ck('B01 anchor '+x['project'],h(p.read_bytes())==x['sha256'])
ck('four in-tree B01 primary anchors including storage host',len(anchors)==4,anchors)
text=source.decode();tree=ast.parse(text)
checks_source={
 'SOURCE and PREP review/hash gates':"review['controllerSha256']==sha(Path(__file__).read_bytes()) and review['bindingsSha256']==sha((HERE/'bindings.json').read_bytes())" in text,
 'input hashes before helper imports':text.index("for row in bindings['inputs']:verify_file(row)")<text.index("lib=runpy.run_path"),
 'source copy no merge':"destination.mkdir(mode=0o700)" in text and "shutil.copy2(source/row['path'],target)" in text,
 'per-project compilation provenance':"compilationProvenance={p['key']:p['compiledSource']" in text,
 'fresh root and phase once-only':"if mode=='catalog':ROOT.mkdir(mode=0o700)" in text and "phase.mkdir(mode=0o700)" in text,
 'properties target exact staged paths':all(v in text for v in ["Path(values['TargetPath'])==staged_bin(project)","Path(values['ProjectAssetsFile'])==ROOT/'artifacts/obj'","values['IsTestProject'].lower()=='true'","values['TargetFramework']=='net8.0'"]),
 'preprocess plus frozen import closure':all(v in text for v in ["'-preprocess:'+str(preprocessed)","<=imported","verify_file(input_map[str(path)])","Original normal obj import selected."]),
 'ordinary aggregate deadline and separate original csc60':"env,start+600,lib,b01['proc_rows']" in text and "phase,'compile',env,60" in text,
 'new process session wrapper and bounded join':"start_new_session=True" in text and "child.wait(timeout=max(1,deadline-time.monotonic()+15))" in text,
 'no direct arbitrary signal calls':not any(isinstance(n,ast.Call) and isinstance(n.func,ast.Attribute) and n.func.attr in ['kill','terminate','killpg','send_signal'] for n in ast.walk(tree)),
 'catalog and plan are no-build':"['-ValidateCatalog','-NoBuild']" in text and "'-PlanOnly','-NoBuild'" in text,
 'ZERO and no hidden build guards':"summary['Tests']['Executed']==0" in text and "p['Name'].startswith('Build-')" in text,
 'exact single diagnostic method/case':"GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss" in text and "[dict(Name=expected,Cases=1)]" in text,
 'normal899 retained before/after':text.count("m01['binaries']()==old['binaryInventory']")==2,
 'reuse exact actual_case separately gated':"case=d01['actual_case'](lib,phase,ROOT/'prepare',expected)" in text and "for expected in (0,7)" in text,
 'finalizer and raw negative mapping retained':"lib['FINALIZE']" in text and 'return 128-result if result<0 else result' in text}
for name,value in checks_source.items():ck(name,value)
ck('original four pure copy checks source-bound',proof['passed']==4 and proof['sdkOrProcessActions']==0 and proof['firstRedObservation']['exit']==1)
# Execute only original run_owned AST against pure fakes; no processes/files created.
fn=next(x for x in tree.body if isinstance(x,ast.FunctionDef) and x.name=='run_owned')
class FakeFile:
 def __init__(self,path='root',fault=False):self.path=path;self.fault=fault
 def __truediv__(self,name):return FakeFile(self.path+'/'+str(name),self.fault)
 def __str__(self):return self.path
 def open(self,*args):return self
 def __enter__(self):return self
 def __exit__(self,*args):
  if self.fault:raise OSError('fake output close fault')
class Child:
 pid=4242
 def __init__(self,code):self.code=code
 def wait(self,timeout):return self.code
faultrows=[]
for native in [0,7,124,-9]:
 for fault in ['none','record-persist','output-close']:
  ns=dict(time=types.SimpleNamespace(monotonic=lambda:100.0),os=types.SimpleNamespace(getpid=lambda:42),subprocess=types.SimpleNamespace(Popen=lambda *a,**kw:Child(native),DEVNULL=-3),REPO=FakeFile())
  exec(compile(ast.Module(body=[fn],type_ignores=[]),'<original-run_owned-only>','exec'),ns)
  publications=[]
  def write(path,value):
   publications.append(dict(path=str(path),nativeExit=value['nativeExit']))
   if fault=='record-persist' and str(path).endswith('.record.json'):raise OSError('fake final command record persistence failure')
  lib=dict(write=write,identity=lambda pid:dict(pid=pid,parentPid=42))
  try:ret=ns['run_owned'](['fake-command'],FakeFile(fault=fault=='output-close'),'command',{},200,lib,lambda pid:([],{}));row=dict(native=native,fault=fault,returned=True,returnedNative=ret['nativeExit'],observerErrors=ret['observerErrors'])
  except BaseException as error:row=dict(native=native,fault=fault,returned=False,escaped=repr(error),lastPublication=publications[-1])
  faultrows.append(row)
ck('fake proof baseline and output-close retain exact native',all(x['returned'] and x['returnedNative']==x['native'] for x in faultrows if x['fault']!='record-persist'))
ck('D02-S01 original persistence defect reproduced for all four native statuses',all(not x['returned'] and x['lastPublication']['nativeExit']==x['native'] for x in faultrows if x['fault']=='record-persist'))
receiptpath=pathlib.Path('/tmp/1536-cloud-tools/d01-carrier-recovery-96f08c8b.json');receipt=j(receiptpath)
ck('exact recovered source receipt',receipt['sha']==SHA and receipt['remoteShaVerified'] and receipt['clean'] and receipt['changedPaths']==134 and receipt['currentPinsVerified']==149 and not receipt['productionChanges'])
ck('new nine files exact Git/clone copies',all((R/p).read_bytes()==git('show',SHA+':'+p)==(C/p).read_bytes() for p in changes))
result=dict(kind='D02_ORIGINAL_SOURCE_STATIC_AND_FAKE_FAULT_CHECKS',candidate=SHA,controllerSha256=h(source),bindingsSha256=h((R/P/'bindings.json').read_bytes()),checks=checks,passed=sum(x['passed'] for x in checks),failed=[x for x in checks if not x['passed']],faultProof=faultrows,semanticFinding='D02-S01: final unguarded per-command write prevents known native result return; correction required.',nativeActions=0,sourceSnapshot=str(D/'d02-phase-original-source-96f08c8b.py'),recoveryReceiptSha256=h(receiptpath.read_bytes()))
path=D/'l01-delivery-d02-source-original-checks-20261018.json';path.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['passed','failed','controllerSha256','bindingsSha256']}));print('checksSHA',h(path.read_bytes()))
