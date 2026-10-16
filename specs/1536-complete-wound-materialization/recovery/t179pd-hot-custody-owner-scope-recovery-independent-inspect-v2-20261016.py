from pathlib import Path
import os,subprocess,json,hashlib,difflib
R=Path('/workspace/The-Book-of-Eternity-Reborn');T=Path('/tmp');rec='specs/1536-complete-wound-materialization/recovery/';source='d474cdd8546f8157b6ffd71840f268062ee05ed0';base='22e31d09999ff9cd8616116ff0e19f517a192426';head='f186abee075654859d536073d8a582f213af0529';stem='t179pd-hot-custody-owner-scope-recovery-independent'
env=dict(os.environ,GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
def git(*a,where=R):return subprocess.check_output(['git',*a],cwd=where,env=env)
def sha(b):return hashlib.sha256(b).hexdigest()
def bid(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def save(path,b):
 with path.open('xb') as f:f.write(b)
 return dict(path=str(path),bytes=len(b),sha256=sha(b))
def pin(path,c=head):
 b=git('show',c+':'+path);return dict(path=path,commit=c,git_blob=bid(b),sha256=sha(b),bytes=len(b),writer_equal=(R/path).read_bytes()==b)
assert git('rev-parse','HEAD').decode().strip()==head and not git('status','--porcelain')
scopes=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts'];assert not git('diff','--binary',source,head,'--',*scopes)
assert not git('diff','--check',base,head)
receiptp=T/'t179pd-hot-custody-red2-owner-scope-recovery-20261016.json';receipt=json.loads(receiptp.read_text());clone=Path(receipt['directory'])
assert receipt['sha']==head and receipt['count']==len(receipt['files'])==1348 and receipt['new_directory'] and receipt['clean'] and receipt['noAlternates'] and receipt['restoredBuildOrRuntime']=='NOT_RUN'
assert git('rev-parse','HEAD',where=clone).decode().strip()==head and not git('status','--porcelain',where=clone)
alternates=Path(git('rev-parse','--git-path','objects/info/alternates',where=clone).decode().strip());alternates=alternates if alternates.is_absolute() else clone/alternates;assert not alternates.exists()
origin=git('remote','get-url','origin',where=clone).decode().strip();assert origin=='https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git'
tree={}
for item in git('ls-tree','-r','-z',head).split(b'\0'):
 if item:
  meta,path=item.split(b'\t',1);tree[path.decode()]=meta.decode().split()[2]
restored=[];pathset=set()
for row in receipt['files']:
 p=row['path'];assert p not in pathset;pathset.add(p);b=(clone/p).read_bytes();assert b==(R/p).read_bytes() and len(b)==row['bytes'] and sha(b)==row['sha256'] and bid(b)==tree[p];restored.append(dict(**row,git_blob=bid(b),verified=True))
actualpaths={str(p.relative_to(clone)) for p in clone.rglob('*') if '.git' not in p.parts and p.is_file()};assert pathset==actualpaths
specs=[('t179pd-automatic-player-time-policy-engineering-independent-review-20261016.json',['source_evidence.candidate_pins'],52),('t179pd-hot-submitted-admission-engineering-design-20261016.json',['source_pins'],105),('t179pd-hot-custody-engineering-independent-review-20261016.json',['source_pins','current_governance_and_extra_pins'],113),('t179pd-narrative-interface-policy-engineering-design-20261016.json',['source_pins'],21),('t179pd-narrative-interface-policy-engineering-independent-review-20261016.json',['source_evidence.author_historical_pins','source_evidence.candidate_pins'],42)]
oldreceiptp=R/(rec+'t179pd-hot-custody-red2-owned-prepared-recovery-20261016.json');old=json.loads(oldreceiptp.read_text());oldpaths={r['path'] for r in old['files']};assert len(oldpaths)==1250
required=set();scope_rows=[]
for index,(name,keys,count) in enumerate(specs):
 raw=git('show',head+':'+rec+name);data=json.loads(raw);paths=set()
 for key in keys:
  v=data
  for part in key.split('.'):v=v[part]
  paths.update(r['path'] for r in v)
 row=dict(report=name,sha256=sha(raw),count=len(paths),paths=sorted(paths));assert row==receipt['dependencyScopes'][index] and len(paths)==count and paths<=pathset and paths<=set(tree)
 required.update(paths);scope_rows.append(dict(**row,old_recovery_present=len(paths&oldpaths),old_recovery_missing=sorted(paths-oldpaths),current_present=len(paths)))
assert len(required)==145 and sorted(required)==receipt['concreteRequiredPaths'];assert len(required-oldpaths)==66
assert scope_rows[1]['old_recovery_present']==52 and scope_rows[2]['old_recovery_present']==57 and scope_rows[3]['old_recovery_present']==6
readerprofile=rec+'t179pd-observed-reader-final-fresh-source-recovery-20261016.json';reader=json.loads(git('show',head+':'+readerprofile));delta=set(git('diff','--name-only','d13aab44fd3487d95bf6005bb8296dbda54846bb',head).decode().splitlines());wanted={r['path'] for r in reader['files']}|delta|required
assert wanted<=pathset
critical=['BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.QtePhysicalInjuryConsumerHeldPublication.cs','BookOfEternityClient/Core/SessionOperationContext.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs']
critical_missing=[p for p in critical if p not in pathset]
assert critical_missing==['BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs']
later_reports=[('t179pd-hot-custody-finite-causal-fixture-design-20261016.json','source_pins'),('t179pd-hot-custody-red2-source-independent-review-20261016.json','pins'),('t179pd-hot-custody-red2-owned-source-independent-review-20261016.json','pins')]
later=[];later_required=set()
for name,key in later_reports:
 data=json.loads(git('show',head+':'+rec+name)); selected={row['path'] for row in data[key]};later_required.update(selected);later.append(dict(report=name,pin=pin(rec+name),count=len(selected),missing=sorted(selected-pathset)))
assert [len(row['missing']) for row in later]==[16,4,2] and len(later_required-pathset)==17
helper=rec+'t179pd-fresh-github-owner-policy-scope-restore-20261016.py';oldhelper=rec+'t179pd-fresh-github-automatic-policy-scope-restore-20261016.py';diffs=[]
for name,args in [('source-continuity',['diff','--binary',source,head,'--',*scopes]),('helper',['diff','--binary',base,head,'--',helper]),('status',['diff','--binary',base,head,'--','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md'])]:diffs.append(dict(**save(T/(stem+'-'+name+'-20261016.diff'),git(*args)),argv=['git',*args]))
helpers=[]
for kind,oldname,newname in [('wrapper','t179pd-run-hot-custody-red1-absence-singleton.py','t179pd-run-hot-custody-red2-owned-singleton.py'),('late-inspector','t179pd-inspect-hot-custody-red1-absence-first-late.py','t179pd-inspect-hot-custody-red2-owned-first-late.py'),('binding-inspector','t179pd-inspect-hot-custody-red1-absence-late-root-bindings.py','t179pd-inspect-hot-custody-red2-owned-late-root-bindings.py')]:
 a=git('show',head+':'+rec+oldname).decode();b=git('show',head+':'+rec+newname).decode();normal=a.replace('hot-custody-red1-absence','hot-custody-red2-owned').replace('47364c47f5788427fa6360d99c4babdce8f2d717',source)
 if kind=='wrapper':normal=normal.replace("'fr063-hot-joint-attempt-ownership'","'fr063-hot-joint-ready-custody-drift'").replace("print('\\n'.join(p.with_suffix('.log').read_text().splitlines()[-28:]));",'')
 assert normal==b
 d=''.join(difflib.unified_diff(a.splitlines(True),b.splitlines(True),fromfile=rec+oldname,tofile=rec+newname));diffs.append(dict(**save(T/(stem+'-'+kind+'-20261016.diff'),d.encode()),comparison=dict(old=pin(rec+oldname),new=pin(rec+newname)),normalization='prefix/source/category mapping; wrapper additionally omits last28lines console echo, full log retained'))
 helpers.extend([rec+oldname,rec+newname])
mp=rec+'t179pd-hot-custody-red2-owned-prep-binaries-20261016.json';mb=git('show',head+':'+mp);assert mb==git('show',base+':'+mp);manifest=json.loads(mb);assert manifest['source_sha']==source and len(manifest['binaries'])==4
for row in manifest['binaries']:assert sha((R/row['path']).read_bytes())==row['sha256']
prior=[]
for ext,h in [('md','6fb681405761e82fa7fd44971a17fa7b064342d94a9b81ced94cd8a3a54bc4e2'),('json','17f28e83864ab8796265942e0a401a1d54965f517391cf7400d745e58cd109ed')]:
 p=rec+'t179pd-hot-custody-red2-owned-source-independent-review-20261016.'+ext;b=git('show',head+':'+p);assert b==(T/Path(p).name).read_bytes() and sha(b)==h;prior.append(pin(p))
prep=[]
for phase in ['catalog','plan']:
 ip=rec+'t179pd-hot-custody-red2-owned-prep-'+phase+'-20261016-artifact-index.json';ib=git('show',head+':'+ip);assert ib==git('show',base+':'+ip);idx=json.loads(ib)
 for row in idx['artifacts']:
  p=rec+row['path'];b=git('show',head+':'+p);assert b==git('show',base+':'+p) and sha(b)==row['stored_sha256'];prep.append(dict(path=p,git_blob=bid(b),sha256=sha(b),unchanged=True))
assert len(prep)==12
extra=['AGENTS.md','docs/development-workflow.md','tests/categories.json','tests/selection.json','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md',helper,oldhelper,readerprofile,mp,rec+'t179pd-inspect-hot-custody-red2-owned-controls.py']+critical+[rec+s[0] for s in specs]+helpers
projections=[]
for first,last in [(8,23),(24,56),(57,85)]:
 text=''.join(git('show',head+':'+helper).decode().splitlines(keepends=True)[first-1:last]);projections.append(dict(path=helper,commit=head,start=first,end=last,text=text,sha256=sha(text.encode())))
proj=save(T/(stem+'-projections-20261016.json'),(json.dumps(projections,indent=2)+'\n').encode())
out=dict(base=base,compiled_source=source,candidate=head,source_continuity_empty=True,helper=pin(helper),scopes=scope_rows,required_paths=sorted(required),required_count=145,added_concrete_dependencies=sorted(required-oldpaths),old_count=1250,current_count=1348,critical_paths=critical,critical_missing=critical_missing,later_reports=later,omitted_concrete_paths=sorted(later_required-pathset),expanded_required_count=len(required|later_required),recovery=dict(receipt_path=str(receiptp),receipt_sha256=sha(receiptp.read_bytes()),directory=str(clone),head=head,clean=True,no_alternates=True,origin=origin,remote_readback_claim=receipt['remoteReadback'],reviewer_network='not used; GitHub-only transport and remote state attributed to coordinator receipt',files=restored,wanted_path_count=len(wanted),wanted_paths_present=True),pins=[pin(p) for p in dict.fromkeys(extra+[rec+name for name,key in later_reports])],diffs=diffs,projections=dict(**proj,count=3),binaries=manifest,prior_accepted_pair=prior,prep_chains_unchanged=prep,helper_execution_by_reviewer=False,actual_tests=0)
f=save(T/(stem+'-inspection-20261016.json'),(json.dumps(out,indent=2)+'\n').encode());print(json.dumps(dict(inspection=f,pins=len(out['pins']),diffs=len(diffs),required=145,recovered=1348,added_concrete=66,scopes=[(s['count'],s['old_recovery_present'],s['current_present']) for s in scope_rows],DLLs=4,unchanged_prep_chains=12,omitted_concrete_paths=len(later_required-pathset)),indent=2))
