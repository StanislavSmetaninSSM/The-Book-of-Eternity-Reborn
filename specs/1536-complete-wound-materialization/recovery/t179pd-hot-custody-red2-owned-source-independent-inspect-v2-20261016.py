import os,json,gzip,hashlib,subprocess,re
from pathlib import Path
R=Path('/workspace/The-Book-of-Eternity-Reborn');T=Path('/tmp');rec='specs/1536-complete-wound-materialization/recovery/'
base='f71c3984355c70cc81f73f52cdb7007ff193edd8';source='d474cdd8546f8157b6ffd71840f268062ee05ed0';head='22e31d09999ff9cd8616116ff0e19f517a192426';prod='3fdc16e8a954458f1e56bafa42ca24a66c06d020';oldhead='fbcf407c2755d4ce063a35184e8011adc03b2840';stem='t179pd-hot-custody-red2-owned-source-independent'
env=dict(os.environ,GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
def git(*a,where=R):return subprocess.check_output(['git',*a],cwd=where,env=env)
def sha(b):return hashlib.sha256(b).hexdigest()
def bid(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def save(p,b):
 if p.exists():
  assert p.read_bytes()==b
 else:
  with p.open('xb') as f:f.write(b)
 return dict(path=str(p),bytes=len(b),sha256=sha(b))
def pin(p,c=head):
 b=git('show',c+':'+p);return dict(path=p,commit=c,bytes=len(b),sha256=sha(b),git_blob=bid(b),working_equal=(R/p).read_bytes()==b)
assert git('rev-parse','HEAD').decode().strip()==head and not git('status','--porcelain')
scopes=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts'];p='BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.HotSubmissionReadyDrift.cs'
assert git('diff','--name-only',base,source,'--',*scopes).decode().splitlines()==[p]
assert not git('diff','--binary',prod,source,'--','BookOfEternityClient') and not git('diff','--binary',source,head,'--',*scopes)
assert not git('diff','--check',base,head)
a=git('show',base+':'+p).decode();b=git('show',source+':'+p).decode();assert len(b.splitlines())==250
# Exact retained stimulus/setup/oracle equivalence, excluding only the declared task-supervision change.
prefix='        var engine = CreateGameEngine';assert a.split(prefix)[0]==b.split('        var input = new QueuedConsoleInputSource([]);')[0]
start='        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession';assert a.split(start)[1].split('            var gmRepair = Task.Run')[0]==b.split(start)[1].split('            using var publisherStop')[0]
suffix='            Assert.True(accepted); Assert.True(readyOpened);';assert a.split(suffix)[1]==b.split(suffix)[1]
redline=next(i for i,l in enumerate(b.splitlines(),1) if 'Assert.NotNull(export);' in l);assert redline==228
missing=subprocess.run(['git','grep','-l','ExportHotOrdinarySubmissionDiagnostic',source,'--','BookOfEternityClient'],cwd=R,env=env,stdout=subprocess.PIPE);assert missing.returncode==1
common='BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs';assert git('show',base+':'+common)==git('show',source+':'+common)
for path in ['tests/categories.json','tests/selection.json','AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md']:
 assert git('show',oldhead+':'+path)==git('show',head+':'+path)
diffs=[]
for name,args in [('affected-source',['diff','--binary',base,source,'--',*scopes]),('production-continuity',['diff','--binary',prod,source,'--','BookOfEternityClient']),('carrier-continuity',['diff','--binary',source,head,'--',*scopes]),('status',['diff','--binary',oldhead,head,'--','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md','specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md']),('carrier-all',['diff','--binary',source,head])]:
 diffs.append(dict(**save(T/(stem+'-'+name+'-20261016.diff'),git(*args)),argv=['git',*args]))
chains=[];paths=[];summaries={};natives={};warnings={}
for kind in ['catalog','plan']:
 prefix='t179pd-hot-custody-red2-owned-prep-'+kind+'-20261016-';ip=rec+prefix+'artifact-index.json';idx=json.loads(git('show',head+':'+ip));paths.append(ip)
 assert idx['source_sha']==source and idx['native_exit_code']==0
 for row in idx['artifacts']:
  q=rec+row['path'];stored=git('show',head+':'+q);assert stored==(R/q).read_bytes() and sha(stored)==row['stored_sha256']
  raw=gzip.decompress(stored) if q.endswith('.gz') else stored
  assert raw==Path(row['raw_path']).read_bytes() and len(raw)==row['raw_bytes'] and sha(raw)==row['raw_sha256']
  chains.append(dict(**row,committed_path=q,git_blob=bid(stored),raw_local_stored_git_equal=True));paths.append(q)
  for project in ['unit','integration']:
   if q.endswith('Build-'+project+'.log.gz'):
    log=raw.decode();w=re.findall(r'(\d+) Warning\(s\)',log);e=re.findall(r'(\d+) Error\(s\)',log);assert e[-1]=='0';warnings[project]=int(w[-1])
 s=json.loads(git('show',head+':'+rec+prefix+'summary.json'));n=json.loads(git('show',head+':'+rec+prefix+'native-exit.json'))
 assert s['ExitCode']==n['native_exit']==0 and n['actual_head']==n['compiled_source']==s['Revision']['Head']==source
 assert s['Revision']['ChangedFiles']==0 and s['Tests']==dict(Total=0,Executed=0,Passed=0,Failed=0)
 assert not s['CategoryResults'] and not s['AdapterResults'] and not s['TimedOut'] and not s['DuplicateTests']
 assert s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and all(x['Completed'] for x in s['PhaseResults'])
 summaries[kind]=s;natives[kind]=n
assert len(chains)==12 and warnings==dict(unit=35,integration=133)
assert summaries['catalog']['CatalogAudit']==dict(Valid=True,Methods=11722,Categories=774,Unmapped=[],StaleSelectors=[])
assert summaries['catalog']['DiscoveryOnly'] and summaries['plan']['PlanOnly']
assert not summaries['catalog']['Selection']['UnderfilledDescriptors']
assert summaries['plan']['Selection']['CompletedDescriptors']==summaries['plan']['Selection']['CompletedCases']==0
plan=json.loads(git('show',head+':'+rec+'t179pd-hot-custody-red2-owned-prep-plan-20261016-plan.json'))
oldplan=json.loads(git('show',oldhead+':'+rec+'t179pd-hot-custody-red2-prep-plan-20261016-plan.json'))
assert plan['Descriptors']==oldplan['Descriptors'] and plan['Categories']==oldplan['Categories'] and plan['Reasons']==oldplan['Reasons']
assert len(plan['Descriptors'])==2 and sum(d['EstimatedCases'] for d in plan['Descriptors'])==5
assert summaries['plan']['Selection']['UnderfilledDescriptors']==[d['Name'] for d in plan['Descriptors']]
methods=[m['Name'] for d in plan['Descriptors'] for m in d['ExpectedMethodCounts']];assert len(methods)==len(set(methods))==5 and all(m['Cases']==1 for d in plan['Descriptors'] for m in d['ExpectedMethodCounts'])
assert all(d['BudgetMinutes']==5 and d['Exclusive'] and d['Adapter']=='integration' and not d['FrontendBuild'] for d in plan['Descriptors'])
mp=rec+'t179pd-hot-custody-red2-owned-prep-binaries-20261016.json';manifest=json.loads(git('show',head+':'+mp));assert manifest['source_sha']==source and len(manifest['binaries'])==4
for row in manifest['binaries']:assert sha((R/row['path']).read_bytes())==row['sha256']
rp=T/'t179pd-hot-custody-red2-owned-prepared-recovery-20261016.json';receipt=json.loads(rp.read_text());clone=Path(receipt['directory']);assert receipt['sha']==head and receipt['count']==len(receipt['files'])==1250
assert git('rev-parse','HEAD',where=clone).decode().strip()==head and not git('status','--porcelain',where=clone)
alternates=Path(git('rev-parse','--git-path','objects/info/alternates',where=clone).decode().strip());alternates=alternates if alternates.is_absolute() else clone/alternates;assert not alternates.exists()
tree={}
for item in git('ls-tree','-r','-z',head).split(b'\0'):
 if item:
  meta,path=item.split(b'\t',1);tree[path.decode()]=meta.decode().split()[2]
restored=[]
for row in receipt['files']:
 data=(clone/row['path']).read_bytes();assert data==(R/row['path']).read_bytes() and len(data)==row['bytes'] and sha(data)==row['sha256'] and bid(data)==tree[row['path']]
 restored.append(dict(**row,git_blob=bid(data),equal=True))
prior=[]
for ext,expected in [('md','619a9c648c6a68e79c52ca4f060efb5263c91e2b59b638bc688f7b0d70a07fcc'),('json','2fbb7d034b9742e6bf98867e57b864da65dfbe88e3fefce7c4d4294986a7867c')]:
 q=rec+'t179pd-hot-custody-red2-source-independent-review-20261016.'+ext;data=git('show',head+':'+q);assert data==(T/Path(q).name).read_bytes() and sha(data)==expected;prior.append(pin(q))
oldidx=rec+'t179pd-hot-custody-red2-source-independent-review-20261016-diff-index.json';priorchains=[]
for row in json.loads(git('show',head+':'+oldidx)):
 q=rec+row['path'];data=git('show',head+':'+q);raw=gzip.decompress(data);assert sha(data)==row['stored_sha256'] and sha(raw)==row['raw_sha256'] and len(raw)==row['raw_bytes'] and raw==Path(row['raw_path']).read_bytes() and raw==git(*row['argv'][1:]);priorchains.append(dict(**row,git_blob=bid(data),verified=True))
extra=['AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md','specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md','tests/categories.json','tests/selection.json',p,common,'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs','BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs','BookOfEternityClient/Core/SessionOperationContext.cs','BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',rec+'t179pd-hot-custody-finite-causal-fixture-design-20261016.md',rec+'t179pd-hot-custody-finite-causal-fixture-design-20261016.json',rec+'t179pd-hot-custody-red2-source-independent-inspection-20261016.json',oldidx]
projections=[]
for path,first,last in [(p,88,98),(p,125,169),(p,170,194),(p,211,250),('BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs',11073,11098),('BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs',11350,11357),('BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs',131,144),('BookOfEternityClient/Core/SessionOperationContext.cs',120,155),('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',3363,3429),('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',3430,3457)]:
 txt=''.join(git('show',source+':'+path).decode().splitlines(keepends=True)[first-1:last]);projections.append(dict(path=path,source=source,start=first,end=last,text=txt,sha256=sha(txt.encode())))
proj=save(T/(stem+'-projections-20261016.json'),(json.dumps(projections,indent=2)+'\n').encode())
report=dict(base=base,compiled_source=source,candidate=head,production_reference=prod,source_paths=[p],source_stat=git('diff','--stat',base,source,'--',*scopes).decode(),pins=[pin(q) for q in dict.fromkeys(extra+paths+[mp])],diffs=diffs,stimulus_setup_and_oracle_suffix_unchanged=True,protected_late2_whole_file_unchanged=True,catalog_selection_budget_unchanged=True,missing_api=True,expected_red_line=redline,chains=chains,natives=natives,summaries=summaries,build_warnings=warnings,plan=plan,binaries=manifest,prior_blocked_pair=prior,prior_diff_chains=priorchains,recovery=dict(receipt_path=str(rp),receipt_sha256=sha(rp.read_bytes()),head=head,directory=str(clone),count=1250,clean=True,no_alternates=True,files=restored,remote_claim=receipt['remoteReadback'],network_claim_source='Coordinator receipt only; reviewer no network/restored runtime'),projections=dict(**proj,count=len(projections)),reviewer_runtime_count=0)
out=save(T/(stem+'-inspection-20261016.json'),(json.dumps(report,indent=2)+'\n').encode());print(json.dumps(dict(inspection=out,pins=len(report['pins']),diffs=len(diffs),projections=len(projections),new_chains=len(chains),prior_diff_chains=len(priorchains),DLLs=4,descriptors=2,cases=5,actual_tests=0,recovered=1250,intended_red_line=redline),indent=2))
