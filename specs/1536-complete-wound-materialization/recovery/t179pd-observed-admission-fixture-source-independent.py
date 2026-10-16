from pathlib import Path
import json,gzip,hashlib,subprocess,sys
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery'
C=sys.argv[1];S='d50d1a9a98e087c8fc5a8908745dfd1e03aaac48';B='865be2d2a9e74473180faf3c20c2e281c7b76a71';E='e522102735317eedc04e42ea1e0b892f73983871';P='00aa1c2d70e539805bd6ea72941f4c09c0c00c31';T='BookOfEternityClient.Tests/PendingTurnObservedOriginalReaderConformanceTests.cs'
H=Path('/tmp/t179pd-observed-admission-fixture-history-independent.json');O=Path('/tmp/t179pd-observed-admission-fixture-source-independent.json')
h=lambda b:hashlib.sha256(b).hexdigest()
def git(*a,cwd=R):return subprocess.check_output(['git',*a],cwd=cwd)
def pin(p):
 b=git('show',C+':'+p);assert b==(R/p).read_bytes(),p
 return {'path':p,'revision':C,'blob':git('rev-parse',C+':'+p).decode().strip(),'bytes':len(b),'sha256':h(b)}
def diff(n,a,b,paths=[]):
 argv=['git','diff','--no-ext-diff','--binary',a,b,'--',*paths];v=subprocess.check_output(argv,cwd=R);p=Path('/tmp/t179pd-observed-admission-fixture-'+n+'.diff');assert not p.exists();p.write_bytes(v)
 return {'argv':argv,'path':str(p),'bytes':len(v),'sha256':h(v)}
scope=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts']
assert git('rev-parse','HEAD').decode().strip()==C and not git('status','--porcelain')
assert git('diff','--name-only',E,S,'--',*scope).decode().splitlines()==[T]
x={'candidate':C,'source':S,'failed_packet_base':B,'previous_compiled_source':E,'production_source':P,'clean':True,'diffs':[diff('full',B,S),diff('fixture',B,S,[T]),diff('production-continuity',P,S,['BookOfEternityClient']),diff('other-runtime-continuity',E,S,[*scope,':(exclude)'+T]),diff('source-carrier-full',S,C),diff('source-carrier-runtime',S,C,scope)]}
for k in [2,3,5]:assert x['diffs'][k]['bytes']==0
assert git('diff','--no-ext-diff','--binary',E,S,'--',T)==Path(x['diffs'][1]['path']).read_bytes()
paths=[T,'BookOfEternityClient/Core/FileSystemManager.cs','BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs','BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs','BookOfEternityClient/Core/StrictJsonAuthority.cs','BookOfEternityClient/Services/PendingTurnSnapshotReader.cs','BookOfEternityClient/Services/PendingTurnSnapshotReader.Observed.cs','BookOfEternityClient/Services/PendingTurnSnapshotAuthority.cs','BookOfEternityClient/Services/LiveTurnPreparationService.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.ObservedOriginalReader.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.BrowserOriginalAdmission.cs','BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs','BookOfEternityClient.Tests/PendingTurnObservedOriginalReaderTests.cs','BookOfEternityClient.Tests/PendingTurnObservedOriginalBarrierTests.cs','BookOfEternityClient.Tests/PortableOrdinaryReadTests.cs','tests/categories.json','tests/selection.json','scripts/test-csharp.ps1','AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md','specs/1536-complete-wound-materialization/spec.md','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md','specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md']
x['source_pins']=[pin(p) for p in paths];x['prep']={};x['prep_chains']=[];x['evidence_pins']=[]
for kind in ['catalog','plan']:
 prefix=f't179pd-observed-green-admission-fixture-prep-{kind}-20261016';idx=json.loads((D/(prefix+'-artifact-index.json')).read_text());assert idx['source_sha']==S and idx['native_exit_code']==0
 for a in idx['artifacts']:
  p=D/a['path'];st=p.read_bytes();raw=gzip.decompress(st) if p.suffix=='.gz' else st;q=Path(a['raw_path']);q=q if q.is_absolute() else R/q
  assert len(raw)==a['raw_bytes'] and h(raw)==a['raw_sha256'] and h(st)==a['stored_sha256'] and raw==q.read_bytes()
  ip=pin(str(p.relative_to(R)));x['prep_chains'].append({**a,'blob':ip['blob'],'raw_local_stored_committed_equal':True})
 for p in sorted(D.glob(prefix+'*')):x['evidence_pins'].append(pin(str(p.relative_to(R))))
 su=json.loads((D/(prefix+'-summary.json')).read_text());na=json.loads((D/(prefix+'-native-exit.json')).read_text())
 assert su['Revision']['Head']==S and su['Revision']['ChangedFiles']==0 and su['Tests']=={'Total':0,'Executed':0,'Passed':0,'Failed':0}
 assert su['ExitCode']==na['native_exit']==0 and not su['TimedOut'] and su['RuntimeCleanupSucceeded'] and su['OwnedTreeCleanupSucceeded'];assert na['actual_head']==na['compiled_source']==S
 assert na['argv']==['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1']+(['-ValidateCatalog'] if kind=='catalog' else ['-SelectionFile','tests/selection.json','-PlanOnly','-NoBuild'])
 x['prep'][kind]={'native':na,'summary':su}
assert len(x['prep_chains'])==12
assert x['prep']['catalog']['summary']['CatalogAudit']=={'Valid':True,'Methods':11718,'Categories':772,'Unmapped':[],'StaleSelectors':[]}
plan=json.loads((D/'t179pd-observed-green-admission-fixture-prep-plan-20261016-plan.json').read_text());ds=plan['Descriptors'];old=json.loads((D/'t179pd-observed-green-orf02-prep-plan-20261016-plan.json').read_text());assert ds==old['Descriptors'] and plan['Categories']==old['Categories'];assert len(ds)==8 and sum(d['EstimatedCases'] for d in ds)==131
methods=[m['Name'] for d in ds for m in d['ExpectedMethodCounts']];assert len(methods)==len(set(methods))==59
x['selection']={'descriptors':ds,'categories':plan['Categories'],'cases':131,'unique_methods':59,'method_overlap':False,'exactly_unchanged_from_e522_prep':True}
bp=D/'t179pd-observed-green-admission-fixture-prep-binaries-20261016.json';bi=json.loads(bp.read_text());assert bi['source_sha']==S and len(bi['binaries'])==4
for a in bi['binaries']:assert h((R/a['path']).read_bytes())==a['sha256']
x['binaries']=bi;x['evidence_pins'].append(pin(str(bp.relative_to(R))))
prior=[('t179pd-observed-green-orf02-source-review-20261016.md','6d250767a6626d933c0700a15ba5794d1aa20a018b4aafadfa66474253012ece'),('t179pd-observed-green-orf02-source-review-20261016.json','b2322e909c9293acd69507b57d2b41276acf3fcd14a89a28cb91323b3d34b4ea'),('t179pd-observed-green-orf02-late-evidence-review-20261016.md','66ec7febabf66fd98223fa9bcc13a664ea7723273934638a014e2002aa69f4b3'),('t179pd-observed-green-orf02-late-evidence-review-20261016.json','93c9eb87e853b4f5ee26b8be57c2cce20c4ab4b08354f78e4393dcde03408b42')]
for n,sha in prior:
 p=D/n;assert h(p.read_bytes())==sha and p.read_bytes()==(Path('/tmp')/n).read_bytes();x['evidence_pins'].append(pin(str(p.relative_to(R))))
x['prior_report_immutability']=True
history=json.loads(H.read_text());assert h(H.read_bytes())=='642f60d723ae52dbcfb5a9623093ca21d249d0f6c67eaa1ac50ce4ef121f604a'
for row in history['evidence_pins']:assert h((R/row['path']).read_bytes())==row['sha256'];pin(row['path'])
x['history']={'path':str(H),'sha256':h(H.read_bytes()),'inspection':history}
rp=Path('/tmp/t179pd-observed-green-admission-fixture-prep-selected-recovery-20261016.json');re=json.loads(rp.read_text());rr=Path(re['directory']);assert re['sha']==C and re['count']==len(re['files'])==765
assert git('rev-parse','HEAD',cwd=rr).decode().strip()==C and not git('status','--porcelain',cwd=rr)
gd=Path(git('rev-parse','--absolute-git-dir',cwd=rr).decode().strip());assert not (gd/'objects/info/alternates').exists()
for a in re['files']:
 v=(rr/a['path']).read_bytes();assert h(v)==a['sha256'] and len(v)==a['bytes'];assert v==git('show',C+':'+a['path'])==git('show',C+':'+a['path'],cwd=rr)
x['recovery']={'receipt':str(rp),'sha256':h(rp.read_bytes()),'source':C,'files_verified':765,'file_pins':re['files'],'clean':True,'no_alternates':True,'transport_attestation':re['source'],'remote_readback_receipt':re['remoteReadback'],'network_independently_repeated':False,'restored_build_runtime':'NOT_RUN','note':'Current clone equals preparation carrier ec50; d50/741 receipt is historical.'}
historical=D/'t179pd-observed-green-admission-fixture-wip-selected-recovery-20261016.json'
hr=json.loads(historical.read_text());assert hr['sha']==S and hr['count']==len(hr['files'])==741
for a in hr['files']:
 v=git('show',S+':'+a['path']);assert h(v)==a['sha256'] and len(v)==a['bytes']
x['historical_recovery']={'receipt_pin':pin(str(historical.relative_to(R))),'source':S,'files_verified_against_git':741,'live_directory_not_claimed_at_historical_sha':True}
x['build_log_ends']={}
for k in ['unit','integration']:
 raw=gzip.decompress((D/f't179pd-observed-green-admission-fixture-prep-catalog-20261016-Build-{k}.log.gz').read_bytes()).decode();assert '0 Error(s)' in raw;x['build_log_ends'][k]=raw.splitlines()[-8:]
assert not O.exists();O.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'inspection':str(O),'sha256':h(O.read_bytes()),'source_pins':len(x['source_pins']),'evidence_pins':len(x['evidence_pins']),'prep_chains':12,'history_chains':18,'recovery_files':765,'diffs':x['diffs'],'binaries':bi},indent=2))
