import os,json,gzip,hashlib,subprocess,re
from pathlib import Path
root=Path('/workspace/The-Book-of-Eternity-Reborn');tmp=Path('/tmp')
base='61391d3d33d82f7487faa575abf7a42b5e22f99f';source='f71c3984355c70cc81f73f52cdb7007ff193edd8';head='fbcf407c2755d4ce063a35184e8011adc03b2840';production='3fdc16e8a954458f1e56bafa42ca24a66c06d020'
rec='specs/1536-complete-wound-materialization/recovery/';stem='t179pd-hot-custody-red2-source-independent'
env=dict(os.environ,GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
def git(*a,where=root):return subprocess.check_output(['git',*a],cwd=where,env=env)
def sha(b):return hashlib.sha256(b).hexdigest()
def bid(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def save(p,b):
 with p.open('xb') as f:f.write(b)
 return dict(path=str(p),bytes=len(b),sha256=sha(b))
def pin(p,c=head):
 b=git('show',c+':'+p);return dict(path=p,commit=c,bytes=len(b),sha256=sha(b),git_blob=bid(b),working_equal=(root/p).read_bytes()==b)
assert git('rev-parse','HEAD').decode().strip()==head and not git('status','--porcelain')
scopes=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts']
newpath='BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.HotSubmissionReadyDrift.cs'
sourcepaths=git('diff','--name-only',base,source,'--',*scopes).decode().splitlines();assert sourcepaths==[newpath,'tests/categories.json','tests/selection.json']
assert not git('diff','--binary',production,source,'--','BookOfEternityClient')
assert not git('diff','--binary',source,head,'--',*scopes)
assert not git('diff','--check',base,head)
diffs=[]
for name,args in [('source',['diff','--binary',base,source,'--',*scopes]),('production-continuity',['diff','--binary',production,source,'--','BookOfEternityClient']),('carrier-continuity',['diff','--binary',source,head,'--',*scopes]),('status',['diff','--binary',base,head,'--','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md','specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md']),('carrier-all',['diff','--binary',source,head])]:
 b=git(*args);diffs.append(dict(**save(tmp/(stem+'-'+name+'-20261016.diff'),b),argv=['git',*args]))
common='BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs'
a=git('show',base+':'+common).decode();b=git('show',source+':'+common).decode();assert a==b
protected=[]
for name in ['GuaranteedTreatment_LiveSeverityRepairCompletesJointOnceWithZeroCycleProgression','GuaranteedTreatment_LiveSeverityRepairConsumesMatchingNonzeroReportExactlyOnce']:
 pat=r'    \[Fact\]\n    public Task '+name+r'\(\) =>\n        [^;]+;'
 before=re.search(pat,a).group(0);after=re.search(pat,b).group(0);assert before==after
 protected.append(dict(method=name,text=after,sha256=sha(after.encode()),unchanged=True))
proc=subprocess.run(['git','grep','-l','ExportHotOrdinarySubmissionDiagnostic',source,'--','BookOfEternityClient'],cwd=root,env=env,stdout=subprocess.PIPE);assert proc.returncode==1
text=git('show',source+':'+newpath).decode();assert len(text.splitlines())==209
redline=next(i for i,s in enumerate(text.splitlines(),1) if 'Assert.NotNull(export);' in s);assert redline==187
chains=[];summaries={};natives={};artifactpaths=[]
for kind in ['catalog','plan']:
 prefix='t179pd-hot-custody-red2-prep-'+kind+'-20261016-';idxpath=rec+prefix+'artifact-index.json';idx=json.loads(git('show',head+':'+idxpath));artifactpaths.append(idxpath)
 assert idx['source_sha']==source and idx['native_exit_code']==0
 for item in idx['artifacts']:
  p=rec+item['path'];stored=git('show',head+':'+p);assert stored==(root/p).read_bytes() and sha(stored)==item['stored_sha256']
  raw=gzip.decompress(stored) if p.endswith('.gz') else stored
  assert raw==Path(item['raw_path']).read_bytes() and len(raw)==item['raw_bytes'] and sha(raw)==item['raw_sha256']
  chains.append(dict(**item,committed_path=p,git_blob=bid(stored),raw_local_stored_git_equal=True));artifactpaths.append(p)
 summary=json.loads(git('show',head+':'+rec+prefix+'summary.json'));native=json.loads(git('show',head+':'+rec+prefix+'native-exit.json'))
 assert summary['ExitCode']==native['native_exit']==0 and native['actual_head']==native['compiled_source']==summary['Revision']['Head']==source
 assert summary['Revision']['ChangedFiles']==0 and summary['Tests']==dict(Total=0,Executed=0,Passed=0,Failed=0)
 assert not summary['CategoryResults'] and not summary['AdapterResults'] and not summary['TimedOut'] and not summary['DuplicateTests']
 assert summary['OwnedTreeCleanupSucceeded'] and summary['RuntimeCleanupSucceeded'] and all(row['Completed'] for row in summary['PhaseResults'])
 summaries[kind]=summary;natives[kind]=native
assert len(chains)==12
assert summaries['catalog']['CatalogAudit']==dict(Valid=True,Methods=11722,Categories=774,Unmapped=[],StaleSelectors=[])
assert summaries['catalog']['DiscoveryOnly'] and summaries['plan']['PlanOnly']
plan=json.loads(git('show',head+':'+rec+'t179pd-hot-custody-red2-prep-plan-20261016-plan.json'))
assert len(plan['Descriptors'])==2 and sum(d['EstimatedCases'] for d in plan['Descriptors'])==5
assert {d['Category']:d['EstimatedCases'] for d in plan['Descriptors']}=={'fr063-joint-late-completion':2,'fr063-hot-joint-ready-custody-drift':3}
assert all(d['BudgetMinutes']==5 and d['Exclusive'] and d['Adapter']=='integration' and not d['FrontendBuild'] for d in plan['Descriptors'])
methods=[r['Name'] for d in plan['Descriptors'] for r in d['ExpectedMethodCounts']];assert len(methods)==len(set(methods))==5 and all(r['Cases']==1 for d in plan['Descriptors'] for r in d['ExpectedMethodCounts'])
newmethods=re.findall(r'public Task (HotSubmission_\w+)\(\)',text);assert len(newmethods)==3
assert all('BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.'+m in methods for m in newmethods)
catalog=json.loads(git('show',head+':tests/categories.json'));old=json.loads(git('show',base+':tests/categories.json'))
owner=next(c for c in catalog['categories'] if c['id']=='fr063-hot-joint-ready-custody-drift')
assert [c for c in catalog['categories'] if c['id']!=owner['id']]==old['categories']
manifestpath=rec+'t179pd-hot-custody-red2-prep-binaries-20261016.json';manifest=json.loads(git('show',head+':'+manifestpath));assert manifest['source_sha']==source and len(manifest['binaries'])==4
for row in manifest['binaries']:assert sha((root/row['path']).read_bytes())==row['sha256']
receiptp=tmp/'t179pd-hot-custody-red2-prepared-recovery-20261016.json';receipt=json.loads(receiptp.read_text());clone=Path(receipt['directory']);assert receipt['sha']==head and receipt['count']==1213 and len(receipt['files'])==1213
assert git('rev-parse','HEAD',where=clone).decode().strip()==head and not git('status','--porcelain',where=clone)
alternates=Path(git('rev-parse','--git-path','objects/info/alternates',where=clone).decode().strip());alternates=alternates if alternates.is_absolute() else clone/alternates;assert not alternates.exists()
tree={}
for x in git('ls-tree','-r','-z',head).split(b'\0'):
 if x:
  meta,p=x.split(b'\t',1);tree[p.decode()]=meta.decode().split()[2]
restore=[]
for row in receipt['files']:
 data=(clone/row['path']).read_bytes();assert data==(root/row['path']).read_bytes() and len(data)==row['bytes'] and sha(data)==row['sha256'] and bid(data)==tree[row['path']]
 restore.append(dict(**row,git_blob=bid(data),equal=True))
consultpath=rec+'t179pd-hot-custody-finite-causal-fixture-design-20261016.json';consult=json.loads(git('show',head+':'+consultpath));consultpins=[];projectioncount=0
assert sha(git('show',head+':'+consultpath.replace('.json','.md')))==consult['markdown_sha256']
for p in consult['source_pins']:
 b=git('show',p['commit']+':'+p['path']);assert sha(b)==p['sha256'] and bid(b)==p['git_blob']
 for proj in p['projections']:
  section=''.join(b.decode().splitlines(keepends=True)[proj['start_line']-1:proj['end_line']]);assert section==proj['text'] and sha(section.encode())==proj['sha256'];projectioncount+=1
 consultpins.append(p)
assert len(consultpins)==24 and projectioncount==39
extra=['AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md','specs/1536-complete-wound-materialization/spec.md','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md','specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md',consultpath,consultpath.replace('.json','.md'),common,
 'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs','BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs','BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.HotSubmissionCustody.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.QtePhysicalInjuryConsumerHeldPublication.cs',
 'BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs','BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs','BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs','BookOfEternityClient/Core/SessionOperationContext.cs','BookOfEternityClient/Core/FileSystemManager.OrdinaryReads.cs','BookOfEternityClient/Services/PendingTurnSnapshotReader.cs','BookOfEternityClient/Services/PendingTurnSnapshotReader.Observed.cs','BookOfEternityClient/Services/LiveTurnPreparationService.cs']
prior=[]
for ext in ['md','json']:
 p=rec+'t179pd-hot-custody-hs01-causal-red-independent-review-20261016.'+ext;b=git('show',head+':'+p);assert b==(tmp/Path(p).name).read_bytes();prior.append(pin(p))
projections=[]
for p,start,end in [(newpath,15,74),(newpath,75,123),(newpath,124,152),(newpath,153,209),('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',3354,3429),('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',3455,3475),('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',3549,3569),('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',8330,8360),('BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs',855,915),('BookOfEternityClient/Core/FileSystemManager.OrdinaryReads.cs',75,140),('BookOfEternityClient/Core/SessionOperationContext.cs',110,155),('BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs',11073,11098),('BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs',131,144)]:
 t=''.join(git('show',source+':'+p).decode().splitlines(keepends=True)[start-1:end]);projections.append(dict(path=p,source=source,start=start,end=end,text=t,sha256=sha(t.encode())))
proj=save(tmp/(stem+'-projections-20261016.json'),(json.dumps(projections,indent=2)+'\n').encode())
report=dict(base=base,compiled_source=source,candidate=head,production_reference=production,source_paths=sourcepaths,source_stat=git('diff','--stat',base,source,'--',*scopes).decode(),pins=[pin(p) for p in dict.fromkeys(sourcepaths+extra+artifactpaths+[manifestpath])],diffs=diffs,protected_methods=protected,missing_api=True,expected_red_line=redline,
 chains=chains,natives=natives,summaries=summaries,plan=plan,category=owner,binaries=manifest,prior_causal_review=prior,
 consultation=dict(pins=consultpins,verified_pins=24,verified_projections=39),
 recovery=dict(receipt_path=str(receiptp),receipt_sha256=sha(receiptp.read_bytes()),head=head,directory=str(clone),count=len(restore),clean=True,no_alternates=True,files=restore,remote_claim=receipt['remoteReadback'],network_claim_source='Coordinator receipt; reviewer performed no network or restored runtime.'),
 projections=dict(**proj,count=len(projections)),reviewer_runtime_count=0)
out=save(tmp/(stem+'-inspection-20261016.json'),(json.dumps(report,indent=2)+'\n').encode())
print(json.dumps(dict(pins=len(report['pins']),chains=len(chains),DLLs=4,descriptors=2,cases=5,tests_executed=0,recovery=len(restore),consultation_pins=24,consultation_projections=39,expected_red_line=redline,inspection=out,diffs=diffs),indent=2))
