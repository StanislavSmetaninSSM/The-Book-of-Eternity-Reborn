from pathlib import Path
import json,gzip,hashlib,subprocess,re
R=Path('/workspace/The-Book-of-Eternity-Reborn'); D=R/'specs/1536-complete-wound-materialization/recovery'
C='e79e6c239f3461f6c7542986279ff6dd99796062'; S='8c098487398830c20bd3705261c2a2c507bce5d8'; B='d50d1a9a98e087c8fc5a8908745dfd1e03aaac48'; F='ad5859121253d7972dca51ced6f77927e9e953b4'; E='e522102735317eedc04e42ea1e0b892f73983871'; P='00aa1c2d70e539805bd6ea72941f4c09c0c00c31'
T='BookOfEternityClient.Tests/PendingTurnSnapshotReaderTests.cs'; stem='/tmp/t179pd-observed-green-default-fixture-source-';O=Path(stem+'independent-inspection-20261016.json')
h=lambda b:hashlib.sha256(b).hexdigest()
def git(*a,cwd=R):return subprocess.check_output(['git',*a],cwd=cwd)
def pin(p,rev=C):
 b=git('show',rev+':'+p)
 if rev==C:assert b==(R/p).read_bytes(),p
 return {'path':p,'revision':rev,'blob':git('rev-parse',rev+':'+p).decode().strip(),'bytes':len(b),'sha256':h(b)}
def diff(n,a,b,paths=[]):
 argv=['git','diff','--no-ext-diff','--binary',a,b,'--',*paths];v=subprocess.check_output(argv,cwd=R);q=Path(stem+n+'-20261016.diff');assert not q.exists();q.write_bytes(v)
 return {'argv':argv,'path':str(q),'bytes':len(v),'sha256':h(v)}
scope=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts']
assert git('rev-parse','HEAD').decode().strip()==C and not git('status','--porcelain')
assert git('diff','--name-only',B,S,'--',*scope).decode().splitlines()==[T]
assert git('diff','--numstat',B,S,'--',T).decode().strip()=='15\t3\t'+T
x={'candidate':C,'compiled_source':S,'audited_source':B,'frozen_audit_base':F,'observed_head':C,'clean':True,'scope':scope,'diffs':[diff('full',B,S),diff('fixture',B,S,[T]),diff('production-continuity',P,S,['BookOfEternityClient']),diff('other-runtime-continuity',B,S,[*scope,':(exclude)'+T]),diff('source-carrier-full',S,C),diff('source-carrier-runtime',S,C,scope)]}
for k in [2,3,5]:assert x['diffs'][k]['bytes']==0
# Exact unchanged prefix/suffix around the one modified method, including shared fixture/Dispose.
a=git('show',B+':'+T).decode();b=git('show',S+':'+T).decode();name='    public async Task ReadCurrent_RejectsRawDuplicateManifestProperties()';suffix='    [Theory]\n    [InlineData("top_level")]'
ai=a.index(name);bi=b.index(name);ae=a.index(suffix,ai);be=b.index(suffix,bi)
assert a[:ai]==b[:bi] and a[ae:]==b[be:]
x['single_method_isolation']={'name':name.strip(),'old_start_line':a[:ai].count('\n')+1,'new_start_line':b[:bi].count('\n')+1,'prefix_sha256':h(a[:ai].encode()),'suffix_sha256':h(a[ae:].encode()),'only_one_body_changed':True,'shared_fixture_and_dispose_unchanged':True}
paths=[T,'BookOfEternityClient/Core/FileSystemManager.cs','BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs','BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs','BookOfEternityClient/Core/StrictJsonAuthority.cs','BookOfEternityClient/Services/PendingTurnSnapshotReader.cs','BookOfEternityClient/Services/PendingTurnSnapshotReader.Observed.cs','BookOfEternityClient/Services/PendingTurnSnapshotAuthority.cs','BookOfEternityClient/Services/LiveTurnPreparationService.cs','BookOfEternityClient.Tests/PendingTurnObservedOriginalReaderTests.cs','BookOfEternityClient.Tests/PendingTurnObservedOriginalBarrierTests.cs','BookOfEternityClient.Tests/PendingTurnObservedOriginalReaderConformanceTests.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.BrowserOriginalAdmission.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.ObservedOriginalReader.cs','BookOfEternityClient.Tests/PortableOrdinaryReadTests.cs','BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs','tests/categories.json','tests/selection.json','scripts/test-csharp.ps1','AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md','specs/1536-complete-wound-materialization/spec.md','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md','specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md']
x['source_pins']=[pin(p) for p in paths];x['projections']=[]
for p,start,end in [(T,323,357),('BookOfEternityClient/Core/FileSystemManager.cs',4337,4346),('BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs',398,420),('BookOfEternityClient/Services/PendingTurnSnapshotReader.cs',143,199),(T,967,986),(T,1015,1029)]:
 lines=git('show',S+':'+p).decode().splitlines(keepends=True);text=''.join(lines[start-1:end]);x['projections'].append({'path':p,'revision':S,'first_line':start,'last_line':end,'text':text,'sha256':h(text.encode())})
x['prep']={};x['prep_chains']=[];x['evidence_pins']=[]
for kind in ['catalog','plan']:
 prefix=f't179pd-observed-green-default-fixture-prep-{kind}-20261016';idx=json.loads((D/(prefix+'-artifact-index.json')).read_text());assert idx['source_sha']==S and idx['native_exit_code']==0
 for row in idx['artifacts']:
  p=D/row['path'];stored=p.read_bytes();raw=gzip.decompress(stored) if p.suffix=='.gz' else stored;q=Path(row['raw_path']);q=q if q.is_absolute() else R/q
  assert len(raw)==row['raw_bytes'] and h(raw)==row['raw_sha256'] and h(stored)==row['stored_sha256'] and raw==q.read_bytes()
  ip=pin(str(p.relative_to(R)));x['prep_chains'].append({**row,'blob':ip['blob'],'raw_local_stored_committed_equal':True})
 for p in sorted(D.glob(prefix+'*')):x['evidence_pins'].append(pin(str(p.relative_to(R))))
 su=json.loads((D/(prefix+'-summary.json')).read_text());na=json.loads((D/(prefix+'-native-exit.json')).read_text())
 assert su['Revision']['Head']==S and su['Revision']['ChangedFiles']==0 and su['Tests']=={'Total':0,'Executed':0,'Passed':0,'Failed':0}
 assert su['ExitCode']==na['native_exit']==0 and not su['TimedOut'] and su['RuntimeCleanupSucceeded'] and su['OwnedTreeCleanupSucceeded']
 assert na['actual_head']==na['compiled_source']==S and na['test_execution']=='ZERO_REQUIRED'
 assert na['argv']==['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1']+(['-ValidateCatalog'] if kind=='catalog' else ['-SelectionFile','tests/selection.json','-PlanOnly','-NoBuild'])
 x['prep'][kind]={'native':na,'summary':su}
assert len(x['prep_chains'])==12
assert x['prep']['catalog']['summary']['CatalogAudit']=={'Valid':True,'Methods':11718,'Categories':772,'Unmapped':[],'StaleSelectors':[]}
assert x['prep']['catalog']['summary']['WallTime']=='00:02:15.7541749' and x['prep']['plan']['summary']['WallTime']=='00:00:07.7086587'
for k in ['unit','integration']:
 raw=gzip.decompress((D/f't179pd-observed-green-default-fixture-prep-catalog-20261016-Build-{k}.log.gz').read_bytes()).decode();assert '0 Error(s)' in raw
 x['prep']['catalog'][k+'_log_end']=raw.splitlines()[-8:]
plan=json.loads((D/'t179pd-observed-green-default-fixture-prep-plan-20261016-plan.json').read_text());old=json.loads((D/'t179pd-observed-green-admission-fixture-prep-plan-20261016-plan.json').read_text());ds=plan['Descriptors']
assert ds==old['Descriptors'] and plan['Categories']==old['Categories'] and len(ds)==8 and sum(d['EstimatedCases'] for d in ds)==131
methods=[m['Name'] for d in ds for m in d['ExpectedMethodCounts']];assert len(methods)==len(set(methods))==59
x['selection']={'categories':plan['Categories'],'descriptors':ds,'cases':131,'unique_methods':59,'overlap':False,'unchanged_from_audited_plan':True}
bp=D/'t179pd-observed-green-default-fixture-prep-binaries-20261016.json';bi=json.loads(bp.read_text());assert bi['source_sha']==S and len(bi['binaries'])==4
for row in bi['binaries']:assert h((R/row['path']).read_bytes())==row['sha256']
x['binaries']=bi;x['evidence_pins'].append(pin(str(bp.relative_to(R))))
# Revalidate immutable completed finite audit and historical packet pins; do not rerun the audit or tests.
prior={'t179pd-selected-negative-fixture-admission-audit-20261016.md':'6ad5a1ede093b13197af490398c971c3f4e2f7c4c29d32c347c9592a1fc86010','t179pd-selected-negative-fixture-admission-audit-20261016.json':'e2f87314841b353c07a8c6c7dc2e120edfeb51eb673fc1d4294f0090d8522d56','t179pd-selected-negative-fixture-admission-audit-inspection-20261016.json':'643ca6546eae9b8495685fc6ea4bfd38a97c90ec73f3e823b595dc59e5d83744','t179pd-observed-green-admission-fixture-source-review-20261016.md':'44fe41a4d9ba084c7ca64c99843909bf47be0c3619fdd44f36a0e04cce8724eb','t179pd-observed-green-admission-fixture-source-review-20261016.json':'e824061d1bf500c49e5220ff762497b783db3922df2b9ef3f9ceedecbd7131bc','t179pd-observed-green-admission-fixture-late-evidence-review-20261016.md':'93fe5570bb7a47b4ec5d791c221e0c98f71a23e794a4dd0ce840e8d857c0ebaa','t179pd-observed-green-admission-fixture-late-evidence-review-20261016.json':'9f08a78555f06704cc07954357df824304f1938c11b5c74f9a50b34b73cc2be5'}
for n,sha in prior.items():
 p=D/n;assert h(p.read_bytes())==sha and p.read_bytes()==(Path('/tmp')/n).read_bytes();x['evidence_pins'].append(pin(str(p.relative_to(R))))
audit=json.loads((D/'t179pd-selected-negative-fixture-admission-audit-inspection-20261016.json').read_text());assert audit['candidate']==F and audit['compiled_source']==B and len(audit['audit_rows'])==59
hist=[]
for row in audit['evidence_pins']:
 current=pin(row['path']);assert current['sha256']==row['sha256'] and current['blob']==row['blob'];hist.append(current)
x['audit_continuity']={'finite_methods':59,'cases':131,'historical_chains_previously_reviewed':42,'immutable_audit_sha256':prior['t179pd-selected-negative-fixture-admission-audit-inspection-20261016.json'],'historical_evidence_pins_reverified':hist,'prior_failures_retained':[{k:p[k] for k in ['prefix','actual','compiled_source','tests','failures']} for p in audit['packets'] if p['tests']['Failed']>0]}
ret=[]
for p in audit['packets']:
 if p['prefix'] in ['t179pd-observed-green-orf02-reader-20261016','t179pd-observed-green-orf02-barrier-20261016','t179pd-observed-green-admission-fixture-conformance-20261016','t179pd-observed-green-admission-fixture-browser-20261016']:
  assert p['tests']['Failed']==0;ret.append({k:p[k] for k in ['prefix','actual','compiled_source','tests','wall','strict_root_count']})
assert sum(p['tests']['Passed'] for p in ret)==46
x['retained_original_provenance']=ret
# Reader/barrier source from e522 unchanged; conformance/browser from d50 unchanged. Production identical throughout.
for p in paths[9:14]:
 rev=E if p in paths[9:11] else B
 assert git('show',rev+':'+p)==git('show',S+':'+p)
rp=D/'t179pd-observed-green-default-fixture-wip-selected-recovery-20261016.json';re=json.loads(rp.read_text());rr=Path(re['directory']);assert re['sha']==S and re['count']==len(re['files'])==873
assert git('rev-parse','HEAD',cwd=rr).decode().strip()==S and not git('status','--porcelain',cwd=rr)
gd=Path(git('rev-parse','--absolute-git-dir',cwd=rr).decode().strip());assert not (gd/'objects/info/alternates').exists()
for row in re['files']:
 v=(rr/row['path']).read_bytes();assert len(v)==row['bytes'] and h(v)==row['sha256'];assert v==git('show',S+':'+row['path'])==git('show',S+':'+row['path'],cwd=rr)
x['recovery']={'receipt_pin':pin(str(rp.relative_to(R))),'source':S,'files_verified':873,'file_pins':re['files'],'clean':True,'no_alternates':True,'transport_attestation':re['source'],'remote_readback_receipt':re['remoteReadback'],'network_independently_repeated':False,'restored_build_runtime':'NOT_RUN','limit':'Live clone proves compiled source8c; preparation carrier e79 is newer and is not claimed restored here.'}
assert not git('status','--porcelain') and git('rev-parse','HEAD').decode().strip()==C
assert not O.exists();O.write_text(json.dumps(x,indent=2,ensure_ascii=False)+'\n')
print(json.dumps({'inspection':str(O),'sha256':h(O.read_bytes()),'source_pins':len(x['source_pins']),'evidence_pins':len(x['evidence_pins']),'historical_pins':len(hist),'prep_chains':12,'recovery_files':873,'diffs':x['diffs'],'selection':[(d['Category'],d['EstimatedCases'],d['BudgetMinutes']) for d in ds]},indent=2))
