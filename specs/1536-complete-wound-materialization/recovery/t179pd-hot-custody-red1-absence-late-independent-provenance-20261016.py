import os,json,gzip,hashlib,subprocess,xml.etree.ElementTree as ET,base64,re
from pathlib import Path
os.environ.update(GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
R=Path('/workspace/The-Book-of-Eternity-Reborn');T=Path('/tmp');C='bab89b7034951cac436ca692a3439dd2a7fecf51';A='1a5ec68f90fec0834c460e4519508abaa57017a2';S='47364c47f5788427fa6360d99c4babdce8f2d717';P='9186fedcffd1fe7d5d14760fc5a7f7827810a00c';REC='specs/1536-complete-wound-materialization/recovery/';STEM='t179pd-hot-custody-red1-absence-first-late-20261016-'
def git(*args,where=R):return subprocess.check_output(['git',*args],cwd=where)
def show(p,c=C):return git('show',c+':'+p)
def sha(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def pin(p,c=C):
 b=show(p,c);return dict(path=p,commit=c,bytes=len(b),sha256=sha(b),git_blob=git('rev-parse',c+':'+p).decode().strip())
def b64(s):return None if s is None else base64.b64decode(s,validate=True)
def jb(s):return json.loads(b64(s).decode('utf-8-sig'))
assert git('rev-parse','HEAD').decode().strip()==C and not git('status','--porcelain')
scopes=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts']
diffs=[]
for label,l,r,paths in [('compiled-to-actual',S,A,scopes),('actual-to-carrier',A,C,scopes),('accepted-source-to-carrier',P,C,scopes),('status',P,C,['specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md'])]:
 argv=['git','diff','--binary',l,r,'--',*paths];raw=git(*argv[1:]);assert not raw if label!='status' else True
 p=T/f't179pd-hot-custody-red1-absence-late-independent-{label}-20261016.diff';p.open('xb').write(raw);diffs.append(dict(path=str(p),argv=argv,bytes=len(raw),sha256=sha(raw)))
assert all(p.startswith('specs/1536-complete-wound-materialization/') for p in git('diff','--name-only',S,C).decode().splitlines())
paths=[REC+STEM+'artifact-index.json'];index=json.loads(show(paths[0]));assert index['source_sha']==A and index['native_exit_code']==0 and len(index['artifacts'])==6
chains=[]
for item in index['artifacts']:
 p=REC+item['path'];stored=show(p);raw=gzip.decompress(stored) if p.endswith('.gz') else stored
 assert stored==(R/p).read_bytes() and sha(stored)==item['stored_sha256']
 assert raw==Path(item['raw_path']).read_bytes() and len(raw)==item['raw_bytes'] and sha(raw)==item['raw_sha256']
 paths.append(p);chains.append(dict(**item,committed_path=p,git_blob=blob(stored),raw_local_stored_git_equal=True))
native=json.loads(show(REC+STEM+'native-exit.json'));summary=json.loads(show(REC+STEM+'summary.json'));plan=json.loads(show(REC+STEM+'plan.json'))
assert native['argv']==['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category','fr063-joint-late-completion','-NoBuild']
assert native['actual_head']==A and native['compiled_source']==S and native['native_exit']==0
assert native['binaries_unchanged'] and native['post_head_unchanged'] and native['post_git_clean']
assert all(not v for v in native['new_scoped_temp_residue'].values())
assert summary==index['summary'] and summary['Revision']==plan['Revision'] and summary['Revision']['Head']==A and summary['Revision']['ChangedFiles']==0
assert summary['Tests']==dict(Total=2,Executed=2,Passed=2,Failed=0) and summary['ExitCode']==0
assert not summary['TimedOut'] and not summary['DuplicateTests'] and not summary['DiscoveryOnly'] and not summary['PlanOnly']
assert summary['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=2,CompletedCases=2,UnderfilledDescriptors=[],Complete=True)
assert summary['RuntimeCleanupSucceeded'] and summary['OwnedTreeCleanupSucceeded'] and all(p['Completed'] for p in summary['PhaseResults'])
assert len(summary['CategoryResults'])==1 and summary['CategoryResults'][0]['Seconds']==198.6041064 and summary['CategoryResults'][0]['BudgetMinutes']==5
assert len(plan['Descriptors'])==1;desc=plan['Descriptors'][0]
prepared=json.loads(show(REC+'t179pd-hot-custody-red1-absence-prep-plan-20261016-plan.json'));old=next(d for d in prepared['Descriptors'] if d['Category']=='fr063-joint-late-completion')
for k in ['Project','Category','Categories','Filter','Adapter','Exclusive','FrontendBuild','BudgetMinutes','EstimatedCases','ExpectedMethodCounts']:assert desc[k]==old[k]
assert desc['EstimatedCases']==2 and desc['BudgetMinutes']==5 and desc['Exclusive'] and not desc['FrontendBuild']
trx=next(a for a in chains if a['path'].endswith('.trx.gz'));raw=gzip.decompress(show(trx['committed_path']));xml=ET.fromstring(raw);ns={'t':xml.tag.split('}')[0][1:]};cases=xml.findall('.//t:UnitTestResult',ns)
assert len(cases)==len({c.attrib['testId'] for c in cases})==len({c.attrib['executionId'] for c in cases})==2
assert {c.attrib['testName'] for c in cases}=={m['Name'] for m in desc['ExpectedMethodCounts']}
assert all(c.attrib['outcome']=='Passed' for c in cases)
counters=xml.find('.//t:Counters',ns).attrib
assert all(int(counters[k])==v for k,v in {'total':2,'executed':2,'passed':2,'failed':0,'error':0,'timeout':0,'aborted':0,'inconclusive':0,'notExecuted':0,'notRunnable':0}.items())
raw_case_checks=[]
for c in cases:
 stdout=c.findtext('t:Output/t:StdOut',namespaces=ns);rows=[]
 for line in stdout.splitlines():
  try:j=json.loads(line)
  except ValueError:continue
  if isinstance(j,dict):rows.append(j)
 def one(b):
  rr=[r['Observation'] for r in rows if r.get('Boundary')==b];assert len(rr)==1;return rr[0]
 u=one('fr063_joint_complete_union');p=one('fr063_joint_completion_actual_prerequisites');o=one('fr063_joint_completion_actual_owner_outcome');repeat=one('fr063_joint_completion_inert_repeat')
 images=o['Milestones'][0]['Images'];final=o['Milestones'][2]['Images']
 wh_before=jb(images['game_state/wounds/wound_history.json'])['transitions'];wh_after=jb(final['game_state/wounds/wound_history.json'])['transitions']
 rh_before=jb(images['game_state/resources/resource_history.json'])['entries'];rh_after=jb(final['game_state/resources/resource_history.json'])['entries']
 assert wh_after[:len(wh_before)]==wh_before and len(wh_after)==len(wh_before)+2
 assert rh_after[:len(rh_before)]==rh_before and len(rh_after)==len(rh_before)+2
 joint=u['Marker']['jointTreatment'];co=p['FullRequest']['coordinates'];assert joint['operationKey']==co['operationKey'] and joint['attemptId']==co['attemptId'] and joint['requestFingerprint']==p['FullRequest']['requestFingerprint'] and joint['treatmentHistoryOrdinal']==3
 assert u['Marker']['consumedCommands']==['game_state/effects/effect_commands.json','game_state/wounds/wound_commands.json','output/mortal_wound_decisions.json']
 assert u['Marker']['consumedPrivatePaths']==['game_state/control/pending_wound_resolutions.json']
 assert u['FullOwnerCanonicalValidation'] and repeat['FullBytesEqual'] and len(repeat['Before'])==len(repeat['After'])==84
 assert not any(r.get('Boundary','').startswith('fr063_hot_custody_') for r in rows)
 clean=[r for r in rows if 'CleanupOwnedRoot' in r];assert len(clean)==1 and clean[0]['OwnedFixtureRemoved'] and clean[0]['CleanupFailure'] is None and not Path(clean[0]['CleanupOwnedRoot']).exists()
 raw_case_checks.append({'case':c.attrib,'stdout_sha256':sha(stdout.encode()),'prior_wound_history_preserved':len(wh_before),'prior_resource_history_preserved':len(rh_before),'new_wound_transitions':2,'new_resource_transitions':2,'joint_treatment_matches_full_request':True,'exact_consumed_paths':True,'hot_hs01_not_executed':True,'cleanup':clean[0]})
assert len({x['cleanup']['CleanupOwnedRoot'] for x in raw_case_checks})==2
# Recheck preparation identity and every raw chain, without redoing discovery/build.
prepchains=[]
for kind in ['catalog','plan']:
 stem='t179pd-hot-custody-red1-absence-prep-'+kind+'-20261016-';idxpath=REC+stem+'artifact-index.json';idx=json.loads(show(idxpath));assert idx['source_sha']==S and idx['native_exit_code']==0;paths.append(idxpath)
 for item in idx['artifacts']:
  p=REC+item['path'];stored=show(p);raw=gzip.decompress(stored) if p.endswith('.gz') else stored
  assert raw==Path(item['raw_path']).read_bytes() and sha(raw)==item['raw_sha256'] and sha(stored)==item['stored_sha256'] and len(raw)==item['raw_bytes']
  assert show(p,P)==stored;prepchains.append(dict(**item,committed_path=p,git_blob=blob(stored),unchanged=True));paths.append(p)
assert len(prepchains)==12
manifestpath=REC+'t179pd-hot-custody-red1-absence-prep-binaries-20261016.json';manifest=json.loads(show(manifestpath));assert manifest['source_sha']==S and len(manifest['binaries'])==4
for b in manifest['binaries']:assert sha((R/b['path']).read_bytes())==b['sha256']
assert show(manifestpath,P)==show(manifestpath);paths.append(manifestpath)
sourcepair=[]
for ext,expected in [('md','c4887407270ab4470ac2768e9517d4642c4b5b31c66006d634295199e1234c48'),('json','f3745671b520b2ab627950bedf4cfe569c3c1e139110fe0fe767a8f0fd831a04')]:
 p=REC+'t179pd-hot-custody-red1-absence-source-independent-review-20261016.'+ext;b=show(p);assert sha(b)==expected and b==(T/Path(p).name).read_bytes();paths.append(p);sourcepair.append(pin(p))
# Exact selected clone integrity; transport provenance comes from receipt, no reviewer network.
rp=T/'t179pd-hot-custody-red1-absence-first-late-recovery-20261016.json';receipt=json.loads(rp.read_text());clone=Path(receipt['directory']);assert receipt['sha']==C and receipt['count']==len(receipt['files'])==1098
assert git('rev-parse','HEAD',where=clone).decode().strip()==C and not git('status','--porcelain',where=clone)
alt=Path(git('rev-parse','--git-path','objects/info/alternates',where=clone).decode().strip());alt=alt if alt.is_absolute() else clone/alt;assert not alt.exists()
tree={}
for row in git('ls-tree','-r','-z',C).split(b'\0'):
 if row:
  meta,p=row.split(b'\t',1);tree[p.decode()]=meta.decode().split()[2]
restored=[]
for row in receipt['files']:
 b=(clone/row['path']).read_bytes();assert b==(R/row['path']).read_bytes() and sha(b)==row['sha256'] and len(b)==row['bytes'] and blob(b)==tree[row['path']];restored.append(dict(**row,git_blob=blob(b)))
sourcepaths=['BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs','BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.HotSubmissionCustody.cs','BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletionActiveTail.cs','BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairMechanicalAdmission.cs','tests/categories.json','tests/selection.json','scripts/test-csharp.ps1']
for p in sourcepaths:assert show(p,S)==show(p,A)==show(p,C);paths.append(p)
new=show(sourcepaths[1],S).decode().splitlines();assert new[171].strip()=='Assert.NotNull(export);'
q=subprocess.run(['git','grep','-l','ExportMortalJointPublicationAttemptDiagnostic',S,'--','BookOfEternityClient'],cwd=R,stdout=subprocess.PIPE);assert q.returncode==1
projections=[]
for p,a,b in [(sourcepaths[0],14,49),(sourcepaths[0],303,360),(sourcepaths[0],431,452),(sourcepaths[0],455,575),(sourcepaths[0],578,649),(sourcepaths[0],860,958),(sourcepaths[0],991,1049),(sourcepaths[1],129,173),(sourcepaths[2],72,151),(sourcepaths[3],131,141)]:
 text=''.join(show(p,S).decode().splitlines(keepends=True)[a-1:b]);projections.append(dict(path=p,source=S,start=a,end=b,text=text,sha256=sha(text.encode())))
for p in [REC+'t179pd-run-hot-custody-red1-absence-singleton.py',REC+'t179pd-inspect-hot-custody-red1-absence-first-late.py',REC+'t179pd-inspect-hot-custody-red1-absence-late-root-bindings.py','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md']:paths.append(p)
sem=json.loads((T/'t179pd-hot-custody-red1-absence-late-independent-semantic-20261016.json').read_text());bindings=json.loads((T/'t179pd-hot-custody-red1-absence-late-independent-bindings-20261016.json').read_text())
assert sem['source']==S and sem['actualHead']==A and sem['carrier']==C and len(sem['cases'])==len(bindings)==2
out=dict(source=S,actual=A,carrier=C,clean_head=C,diffs=diffs,pins=[pin(p) for p in dict.fromkeys(paths)],native=native,summary=summary,plan=plan,raw_chains=chains,prep_chains=prepchains,binaries=manifest,source_review_pair=sourcepair,trx_counters=counters,own_additional_raw_checks=raw_case_checks,projections=projections,recovery=dict(receipt_path=str(rp),receipt_sha256=sha(rp.read_bytes()),directory=str(clone),head=C,count=1098,clean=True,no_alternates=True,selected_files=restored,transport='Root receipt; no reviewer network or restored runtime'),reviewed_helper_semantics=sem['cases'],reviewed_helper_bindings=bindings,expected_hs01_missing_export_line=172,production_member_absent=True,reviewer_runtime_count=0)
p=T/'t179pd-hot-custody-red1-absence-late-independent-provenance-20261016.json';p.open('x').write(json.dumps(out,indent=2)+'\n')
print(json.dumps({'path':str(p),'sha256':sha(p.read_bytes()),'pins':len(out['pins']),'raw_chains':len(chains),'prep_chains':len(prepchains),'recovery':len(restored),'cases':len(raw_case_checks),'DLLs':4,'scoped_source_diffs_empty':True},indent=2))
