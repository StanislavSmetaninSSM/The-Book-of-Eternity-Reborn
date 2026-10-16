import os,json,gzip,hashlib,subprocess,re
from pathlib import Path
root=Path('/workspace/The-Book-of-Eternity-Reborn');tmp=Path('/tmp');base='e182b6745817bd34ff5247c34f8d469bb71754f1';source='47364c47f5788427fa6360d99c4babdce8f2d717';head='9186fedcffd1fe7d5d14760fc5a7f7827810a00c';rec='specs/1536-complete-wound-materialization/recovery/'
env=dict(os.environ,GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
def git(*a,where=root):return subprocess.check_output(['git',*a],cwd=where,env=env)
def sha(b):return hashlib.sha256(b).hexdigest()
def bid(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def pin(p,c=head):
 b=git('show',c+':'+p);return dict(path=p,commit=c,bytes=len(b),sha256=sha(b),git_blob=bid(b),working_equal=(root/p).read_bytes()==b)
assert git('rev-parse','HEAD').decode().strip()==head and not git('status','--porcelain')
scopes=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts']
sourcepaths=git('diff','--name-only',base,source,'--',*scopes).decode().splitlines();assert len(sourcepaths)==4
assert not git('diff','--binary',base,source,'--','BookOfEternityClient','BookOfEternityClient.Tests','scripts')
assert not git('diff','--binary',source,head,'--',*scopes)
assert not git('diff','--check',base,head)
diffs=[]
for name,args in [('source',['diff','--binary',base,source,'--',*scopes]),('production',['diff','--binary',base,source,'--','BookOfEternityClient']),('carrier-continuity',['diff','--binary',source,head,'--',*scopes]),('status',['diff','--binary',base,head,'--','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md'])]:
 b=git(*args);p=tmp/f't179pd-hot-custody-red1-absence-independent-{name}-20261016.diff';assert not p.exists();p.write_bytes(b);diffs.append(dict(path=str(p),argv=['git',*args],bytes=len(b),sha256=sha(b)))
common='BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs'
a=git('show',base+':'+common).decode();b=git('show',source+':'+common).decode();protected=[]
for name in ['GuaranteedTreatment_LiveSeverityRepairCompletesJointOnceWithZeroCycleProgression','GuaranteedTreatment_LiveSeverityRepairConsumesMatchingNonzeroReportExactlyOnce']:
 pat=r'    \[Fact\]\n    public Task '+name+r'\(\) =>\n        [^;]+;'
 before=re.search(pat,a).group(0);after=re.search(pat,b).group(0);assert before==after
 protected.append(dict(method=name,text=after,sha256=sha(after.encode()),unchanged=True))
method='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_HotCustodyBindsActualPostOriginalSubmissionAndConsumedReady'
newpath='BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.HotSubmissionCustody.cs'
new=(root/newpath).read_text();redline=next(i for i,s in enumerate(new.splitlines(),1) if 'Assert.NotNull(export);' in s)
assert not git('grep','-l','ExportMortalJointPublicationAttemptDiagnostic',source,'--','BookOfEternityClient') if False else True
proc=subprocess.run(['git','grep','-l','ExportMortalJointPublicationAttemptDiagnostic',source,'--','BookOfEternityClient'],cwd=root,env=env,stdout=subprocess.PIPE);assert proc.returncode==1
chains=[];summaries={};natives={};artifactpaths=[]
for kind in ['catalog','plan']:
 stem='t179pd-hot-custody-red1-absence-prep-'+kind+'-20261016-';idxpath=rec+stem+'artifact-index.json';idx=json.loads(git('show',head+':'+idxpath));artifactpaths.append(idxpath)
 assert idx['source_sha']==source and idx['native_exit_code']==0
 for item in idx['artifacts']:
  p=rec+item['path'];stored=git('show',head+':'+p);assert stored==(root/p).read_bytes() and sha(stored)==item['stored_sha256']
  raw=gzip.decompress(stored) if p.endswith('.gz') else stored
  assert raw==Path(item['raw_path']).read_bytes() and len(raw)==item['raw_bytes'] and sha(raw)==item['raw_sha256']
  chains.append(dict(**item,committed_path=p,git_blob=bid(stored),raw_local_stored_git_equal=True));artifactpaths.append(p)
 summary=json.loads(git('show',head+':'+rec+stem+'summary.json'));native=json.loads(git('show',head+':'+rec+stem+'native-exit.json'))
 assert summary['ExitCode']==native['native_exit']==0 and native['actual_head']==native['compiled_source']==summary['Revision']['Head']==source
 assert summary['Revision']['ChangedFiles']==0 and summary['Tests']==dict(Total=0,Executed=0,Passed=0,Failed=0)
 assert not summary['CategoryResults'] and not summary['AdapterResults'] and not summary['TimedOut'] and not summary['DuplicateTests']
 assert summary['OwnedTreeCleanupSucceeded'] and summary['RuntimeCleanupSucceeded']
 assert all(row['Completed'] for row in summary['PhaseResults'])
 summaries[kind]=summary;natives[kind]=native
assert len(chains)==12
assert summaries['catalog']['CatalogAudit']==dict(Valid=True,Methods=11719,Categories=773,Unmapped=[],StaleSelectors=[])
assert summaries['catalog']['DiscoveryOnly'] and summaries['plan']['PlanOnly']
plan=json.loads(git('show',head+':'+rec+'t179pd-hot-custody-red1-absence-prep-plan-20261016-plan.json'))
assert len(plan['Descriptors'])==2 and sum(d['EstimatedCases'] for d in plan['Descriptors'])==3
expected={'fr063-joint-late-completion':2,'fr063-hot-joint-attempt-ownership':1}
assert {d['Category']:d['EstimatedCases'] for d in plan['Descriptors']}==expected
assert all(d['BudgetMinutes']==5 and d['Exclusive'] and d['Adapter']=='integration' for d in plan['Descriptors'])
methods=[r['Name'] for d in plan['Descriptors'] for r in d['ExpectedMethodCounts']];assert len(methods)==len(set(methods))==3 and method in methods
assert all(r['Cases']==1 for d in plan['Descriptors'] for r in d['ExpectedMethodCounts'])
manifestpath=rec+'t179pd-hot-custody-red1-absence-prep-binaries-20261016.json';manifest=json.loads(git('show',head+':'+manifestpath));assert manifest['source_sha']==source and len(manifest['binaries'])==4
for row in manifest['binaries']:assert sha((root/row['path']).read_bytes())==row['sha256']
receiptp=Path('/tmp/t179pd-hot-custody-red1-absence-prepared-recovery-20261016.json');receipt=json.loads(receiptp.read_text());clone=Path(receipt['directory']);assert receipt['sha']==head and receipt['count']==1067 and len(receipt['files'])==1067
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
extra=['specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md','BookOfEternityClient/Services/PendingTurnSnapshotReader.cs','AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md',
 rec+'t179pd-hot-submitted-admission-engineering-design-20261016.md',rec+'t179pd-hot-custody-engineering-independent-review-20261016.md',rec+'t179pd-hot-custody-engineering-independent-review-20261016.json',rec+'t179pd-hot-custody-hcd01-verification-20261016.md',rec+'t179pd-hot-custody-hcd01-verification-20261016.json',
 'BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs','BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.LiveRepair.cs','BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.OrdinaryOwner.cs',
 'BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs','BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairMechanicalAdmission.cs','BookOfEternityClient/Services/AcceptedMechanicsWoundCommonInputComposer.cs','BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs']
prior_pairs=[]
for stem in ['t179pd-hot-custody-red1-source-independent-review-20261016','t179pd-hot-custody-red1-optional-absence-source-addendum-20261016']:
 for ext in ['md','json']:
  p=rec+stem+'.'+ext; local=tmp/(stem+'.'+ext); data=git('show',head+':'+p); assert data==local.read_bytes(); prior_pairs.append(dict(**pin(p),temporary_path=str(local),unchanged=True))
projections=[]
for p,start,end in [(newpath,1,233),(common,15,49),(common,100,146),(common,230,289),(common,306,358),(common,506,578),('BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletionActiveTail.cs',72,211),('BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs',131,144),('BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs',1820,1851),('BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairMechanicalAdmission.cs',85,150)]:
 text=''.join(git('show',source+':'+p).decode().splitlines(keepends=True)[start-1:end]);projections.append(dict(path=p,source=source,start=start,end=end,text=text,sha256=sha(text.encode())))
projpath=tmp/'t179pd-hot-custody-red1-absence-independent-projections-20261016.json';assert not projpath.exists();projpath.write_text(json.dumps(projections,indent=2)+'\n')
report=dict(prior_pairs=prior_pairs,base=base,compiled_source=source,candidate=head,source_paths=sourcepaths,pins=[pin(p) for p in dict.fromkeys(sourcepaths+extra+artifactpaths+[manifestpath])],diffs=diffs,protected_methods=protected,missing_api=True,expected_red_line=redline,
 chains=chains,natives=natives,summaries=summaries,plan=plan,binaries=manifest,
 recovery=dict(receipt_path=str(receiptp),receipt_sha256=sha(receiptp.read_bytes()),head=head,directory=str(clone),count=len(restore),clean=True,no_alternates=True,files=restore,network_claim_source='Coordinator receipt; reviewer performed no network or restored runtime.'),
 projections=dict(path=str(projpath),sha256=sha(projpath.read_bytes()),count=len(projections)),reviewer_runtime_count=0)
out=tmp/'t179pd-hot-custody-red1-absence-source-independent-inspection-20261016.json';assert not out.exists();out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(dict(pins=len(report['pins']),chains=len(chains),DLLs=4,descriptors=2,cases=3,tests_executed=0,recovery=len(restore),expected_red_line=redline,inspection=str(out),sha256=sha(out.read_bytes()),diffs=diffs),indent=2))
