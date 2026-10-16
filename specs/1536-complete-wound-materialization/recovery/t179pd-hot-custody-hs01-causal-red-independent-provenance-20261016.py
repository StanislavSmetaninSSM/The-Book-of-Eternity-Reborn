from pathlib import Path
import os,subprocess,json,hashlib,gzip,base64,xml.etree.ElementTree as E,re,uuid,datetime
os.environ.update(GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
R=Path('/workspace/The-Book-of-Eternity-Reborn');T=Path('/tmp');S='47364c47f5788427fa6360d99c4babdce8f2d717';A='9c52f988e8daad0d6061e540b39db1e9bb9fb9b4';C='f13fba34c73c61eedff413c32723d2703d95fc55';L='bab89b7034951cac436ca692a3439dd2a7fecf51';REC='specs/1536-complete-wound-materialization/recovery/';STEM='t179pd-hot-custody-red1-absence-hs01-red-20261016-'
def git(*args,where=R):return subprocess.check_output(['git',*args],cwd=where)
def show(p,c=C):return git('show',c+':'+p)
def sha(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def pin(p,c=C):
 b=show(p,c);return dict(path=p,commit=c,bytes=len(b),sha256=sha(b),git_blob=git('rev-parse',c+':'+p).decode().strip())
def b64(v):return None if v is None else base64.b64decode(v,validate=True)
def jb(v):return json.loads(b64(v).decode('utf-8-sig'))
OBS=git('rev-parse','HEAD').decode().strip(); assert OBS=='4eab2a1ce9832306a44534c8e03f6c05cf7de5b4' and not git('status','--porcelain')
observed_changes=git('diff','--name-only',C,OBS).decode().splitlines(); assert all(p.startswith('specs/1536-complete-wound-materialization/') for p in observed_changes)
scope=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts'];diffs=[]
for label,l,r,paths in [('source-to-actual',S,A,scope),('actual-to-carrier',A,C,scope),('prior-evidence-to-carrier',L,C,scope),('status',L,C,['specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md'])]:
 argv=['git','diff','--binary',l,r,'--',*paths];b=git(*argv[1:]);assert not b if label!='status' else True
 p=T/f't179pd-hot-custody-hs01-causal-red-independent-{label}-20261016.diff';p.open('xb').write(b);diffs.append(dict(path=str(p),argv=argv,bytes=len(b),sha256=sha(b)))
assert all(p.startswith('specs/1536-complete-wound-materialization/') for p in git('diff','--name-only',S,C).decode().splitlines())
paths=[REC+STEM+'artifact-index.json'];idx=json.loads(show(paths[0]));assert idx['source_sha']==A and idx['native_exit_code']==1 and len(idx['artifacts'])==6
chains=[]
for item in idx['artifacts']:
 p=REC+item['path'];stored=show(p);raw=gzip.decompress(stored) if p.endswith('.gz') else stored
 assert stored==(R/p).read_bytes() and sha(stored)==item['stored_sha256'] and raw==Path(item['raw_path']).read_bytes() and sha(raw)==item['raw_sha256'] and len(raw)==item['raw_bytes']
 chains.append(dict(**item,committed_path=p,git_blob=blob(stored),raw_local_stored_git_equal=True));paths.append(p)
native=json.loads(show(REC+STEM+'native-exit.json'));summary=json.loads(show(REC+STEM+'summary.json'));plan=json.loads(show(REC+STEM+'plan.json'))
assert native['argv']==['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category','fr063-hot-joint-attempt-ownership','-NoBuild']
assert native['native_exit']==1 and native['actual_head']==A and native['compiled_source']==S and native['binaries_unchanged'] and native['post_head_unchanged'] and native['post_git_clean'] and all(not x for x in native['new_scoped_temp_residue'].values())
assert summary==idx['summary'] and summary['Revision']==plan['Revision'] and summary['Revision']['Head']==A and summary['Revision']['ChangedFiles']==0 and summary['ExitCode']==1
assert summary['Tests']==dict(Total=1,Executed=1,Passed=0,Failed=1) and not summary['TimedOut'] and not summary['DuplicateTests'] and summary['RuntimeCleanupSucceeded'] and summary['OwnedTreeCleanupSucceeded']
assert summary['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=1,CompletedCases=1,UnderfilledDescriptors=[],Complete=True)
assert len(summary['CategoryResults'])==1 and summary['CategoryResults'][0]['Completed'] is False and summary['CategoryResults'][0]['Seconds']==29.8403385 and summary['CategoryResults'][0]['BudgetMinutes']==5
assert not summary['PlanOnly'] and not summary['DiscoveryOnly'] and len(plan['Descriptors'])==1
prepplan=json.loads(show(REC+'t179pd-hot-custody-red1-absence-prep-plan-20261016-plan.json'));pd=next(x for x in prepplan['Descriptors'] if x['Category']=='fr063-hot-joint-attempt-ownership');d=plan['Descriptors'][0]
for k in ['Project','Category','Categories','Filter','Adapter','Exclusive','FrontendBuild','BudgetMinutes','EstimatedCases','ExpectedMethodCounts']:assert d[k]==pd[k]
trx=next(a for a in chains if a['path'].endswith('.trx.gz'));xml=E.fromstring(gzip.decompress(show(trx['committed_path'])));ns={'t':xml.tag.split('}')[0][1:]};cases=xml.findall('.//t:UnitTestResult',ns);assert len(cases)==1;c=cases[0]
assert c.attrib['testName']==d['ExpectedMethodCounts'][0]['Name'] and c.attrib['outcome']=='Failed'
counters=xml.find('.//t:Counters',ns).attrib
assert all(int(counters[k])==v for k,v in dict(total=1,executed=1,passed=0,failed=1,error=0,timeout=0,aborted=0,inconclusive=0,notExecuted=0,notRunnable=0).items())
assert len(c.findall('t:Output/t:ErrorInfo',ns))==1
error=c.findtext('t:Output/t:ErrorInfo/t:Message',namespaces=ns);stack=c.findtext('t:Output/t:ErrorInfo/t:StackTrace',namespaces=ns)
assert error=='Assert.NotNull() Failure: Value is null' and 'HotSubmissionCustody.cs:line 172' in stack and 'JointCompletion.cs:line 345' in stack
stdout=c.findtext('t:Output/t:StdOut',namespaces=ns);rows=[]
for line in stdout.splitlines():
 try:q=json.loads(line)
 except ValueError:continue
 if isinstance(q,dict):rows.append(q)
def one(k):
 found=[r['Observation'] for r in rows if r.get('Boundary')==k];assert len(found)==1;return found[0]
p=one('fr063_joint_completion_actual_prerequisites');f=one('fr063_hot_custody_genuine_post_original_submission');a=one('fr063_joint_active_tail_actual_refusal');q=one('fr063_hot_custody_existing_owner_oracles_before_missing_result')
assert set(f['AuthoredPublicPaths'])=={'game_state/player/status_changes.json','game_state/player/experience.json','game_state/core/player_status.json'} and len(f['AuthoredPublicPaths'])==3
for path in f['AuthoredPublicPaths']:assert f['OriginalInputs'][path]!=f['CurrentInputs'][path]
co=p['FullRequest']['coordinates'];pending=jb(a['After']['game_state/control/pending_wound_resolutions.json']);sr=pending['submittedTreatmentRequests'];assert len(sr)==1
assert sr[0]['operationKey']==co['operationKey'] and sr[0]['attemptId']==co['attemptId'] and sr[0]['request']==p['FullRequest']
command=jb(a['After']['game_state/wounds/wound_commands.json']);assert command==p['Command'] and len(command['commands'])==1
cmd=command['commands'][0];assert cmd['authority']['request']==p['FullRequest'] and cmd['operationKey']==co['operationKey'] and cmd['kind']=='accepted_transition' and cmd['transitionKind']=='treat'
assert a['Issues'][0]['FilePath']=='acceptedTurn.wounds'
# Reconstruct the complete strict diagnostic repair row, not only its high-level label.
ledger=b64(a['After']['game_state/control/gm_trajectory_ledger.jsonl']).decode('utf-8-sig');lines=ledger.split('\n');assert len(lines)==2 and lines[1]=='';row=json.loads(lines[0]);record=row['recordId'];created=row['createdAt']
assert record.startswith('gmtraj_') and uuid.UUID(hex=record[7:]).hex==record[7:]
assert re.fullmatch(r'\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z',created);datetime.datetime.fromisoformat(created.replace('Z','+00:00'))
expected={'recordId':record,'kind':'repair','sessionId':f['SessionId'],'turnId':f['RequestId'],'requestId':f['RequestId'],'turnNumber':43,'realm':'MortalWorld','realmResolution':{'realm':'MortalWorld','source':'pending_turn_snapshot.progressionControl.currentRealm','rawValue':'Mortal World','reason':''},'mode':'validation_repair','actionSummary':'Осмотреть ушиб.','contextPackPath':'game_state/control/gm_context_pack','templateVersions':{'turnOutput':'v1','validationRepair':'v1','progressionReport':'v1','actorReasoning':'v1','tempoAdvantage':'v1'},'outputFiles':[],'dispatch':{'attempts':0,'busyRetries':0,'timeout':False,'status':'client_observed_terminal'},'validation':{'status':'accepted','source':'обработки хода','acceptanceScope':'correlated_repair_ready','fullCanonicalStateAccepted':False,'issueKinds':['wound_severity_above_opportunity'],'repairPacketRefs':['wound_materialization_repair']},'repair':{'attempts':1,'status':'accepted'},'workerEvents':[],'rollbackEvents':[],'terminal':{'kind':'validation_repair_ready','signalPath':'game_state/control/validation_repair_ready.json'},'rubric':{'validTurn':True,'playerFacingOutputPresent':True,'implementationSourceRead':False,'rawWrongRealmWrite':False,'manualReasoningNeeded':False},'createdAt':created,'observedBy':'client'}
assert row==expected
clean=[r for r in rows if 'CleanupOwnedRoot' in r];assert len(clean)==1 and clean[0]['OwnedFixtureRemoved'] and clean[0]['CleanupFailure'] is None and not Path(clean[0]['CleanupOwnedRoot']).exists()
assert next(i for i,r in enumerate(rows) if r.get('Boundary')=='fr063_hot_custody_existing_owner_oracles_before_missing_result')<next(i for i,r in enumerate(rows) if 'CleanupOwnedRoot' in r)
assert not any(r.get('Boundary')=='fr063_hot_custody_actual_attempt_diagnostic' for r in rows)
# Four exact binaries and prior independent gates are unchanged.
mp=REC+'t179pd-hot-custody-red1-absence-prep-binaries-20261016.json';m=json.loads(show(mp));assert m['source_sha']==S and len(m['binaries'])==4
for b in m['binaries']:assert sha((R/b['path']).read_bytes())==b['sha256']
paths.append(mp);prior=[]
for stem,mdh,jh in [('t179pd-hot-custody-red1-absence-source-independent-review-20261016','c4887407270ab4470ac2768e9517d4642c4b5b31c66006d634295199e1234c48','f3745671b520b2ab627950bedf4cfe569c3c1e139110fe0fe767a8f0fd831a04'),('t179pd-hot-custody-red1-absence-late-evidence-independent-review-20261016','e9ecea482dab10d0f221d7e6ff065cb0a624f6be7611787f4e280df423c6c875','c9fad32e23c14ed56e91809d465297d1b4023c6eada9cbcd2d6fe16841e6d20c')]:
 for ext,expectedsha in [('md',mdh),('json',jh)]:
  path=REC+stem+'.'+ext;b=show(path);assert sha(b)==expectedsha and b==(T/(stem+'.'+ext)).read_bytes();prior.append(pin(path));paths.append(path)
previous_late=[]
idxpath=REC+'t179pd-hot-custody-red1-absence-first-late-20261016-artifact-index.json';prev=json.loads(show(idxpath));paths.append(idxpath)
for item in prev['artifacts']:
 path=REC+item['path'];b=show(path);assert b==show(path,L) and sha(b)==item['stored_sha256'];previous_late.append(dict(**item,git_blob=blob(b),unchanged_from=L));paths.append(path)
sourcepaths=['BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.HotSubmissionCustody.cs','BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs','BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletionActiveTail.cs','BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairMechanicalAdmission.cs','BookOfEternityClient/Services/PendingTurnSnapshotReader.cs','tests/categories.json','tests/selection.json','scripts/test-csharp.ps1']
for path in sourcepaths:assert show(path,S)==show(path,A)==show(path,C);paths.append(path)
assert show(sourcepaths[0],S).decode().splitlines()[171].strip()=='Assert.NotNull(export);'
proc=subprocess.run(['git','grep','-l','ExportMortalJointPublicationAttemptDiagnostic',S,'--','BookOfEternityClient'],cwd=R,stdout=subprocess.PIPE);assert proc.returncode==1
projections=[]
for path,first,last in [(sourcepaths[0],14,125),(sourcepaths[0],129,173),(sourcepaths[1],14,49),(sourcepaths[1],315,349),(sourcepaths[2],72,164),(sourcepaths[2],166,209),(sourcepaths[3],131,141),(sourcepaths[4],85,150)]:
 text=''.join(show(path,S).decode().splitlines(keepends=True)[first-1:last]);projections.append(dict(path=path,source=S,start=first,end=last,text=text,sha256=sha(text.encode())))
for path in [REC+'t179pd-inspect-hot-custody-red1-absence-hs01-red.py',REC+'t179pd-hot-custody-red1-absence-hs01-red-root-inspection-20261016.json',REC+'t179pd-run-hot-custody-red1-absence-singleton.py','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md',REC+'t179pd-hot-submitted-admission-engineering-design-20261016.md']:paths.append(path)
rp=T/'t179pd-hot-custody-red1-absence-hs01-red-recovery-20261016.json';receipt=json.loads(rp.read_text());clone=Path(receipt['directory']);assert receipt['sha']==C and receipt['count']==len(receipt['files'])==1124
assert git('rev-parse','HEAD',where=clone).decode().strip()==C and not git('status','--porcelain',where=clone)
alt=Path(git('rev-parse','--git-path','objects/info/alternates',where=clone).decode().strip());alt=alt if alt.is_absolute() else clone/alt;assert not alt.exists()
tree={}
for rowb in git('ls-tree','-r','-z',C).split(b'\0'):
 if rowb:
  meta,path=rowb.split(b'\t',1);tree[path.decode()]=meta.decode().split()[2]
files=[]; writer_metadata_differences=[]
for frow in receipt['files']:
 b=(clone/frow['path']).read_bytes();assert sha(b)==frow['sha256'] and len(b)==frow['bytes'] and blob(b)==tree[frow['path']]
 current=(R/frow['path']).read_bytes()
 if current!=b:
  assert frow['path'] in observed_changes and current==show(frow['path'],OBS); writer_metadata_differences.append(dict(path=frow['path'],frozen_sha256=sha(b),observed_sha256=sha(current),observed_commit=OBS))
 files.append(dict(**frow,git_blob=blob(b),writer_equal=current==b))
sem=json.loads((T/'t179pd-hot-custody-hs01-causal-red-independent-semantics-20261016.json').read_text());assert sem['actualHead']==A and sem['compiledSource']==S and sem['canonicalPreservationImagesVerified']==220
out=dict(source=S,actual=A,carrier=C,observed_head=OBS,observed_metadata_only_paths=observed_changes,writer_metadata_differences=writer_metadata_differences,native=native,summary=summary,plan=plan,trx_case=c.attrib,trx_counters=counters,error=error,stack=stack,stdout_sha256=sha(stdout.encode()),artifact_chains=chains,previous_late_artifacts_unchanged=previous_late,prior_independent_gates_unchanged=prior,binaries=m,pins=[pin(p) for p in dict.fromkeys(paths)],diffs=diffs,projections=projections,semantic_recomputation=sem,own_additional_checks=dict(exact_authored_public_inputs=sorted(f['AuthoredPublicPaths']),full_operation_attempt_command_request_binding=True,strict_repair_ledger_full_object=row,ledger_only_correlated_ready=True,preassert_boundary_precedes_cleanup=True,exact_single_assert_failure=True,production_member_absent=True),recovery=dict(receipt_path=str(rp),receipt_sha256=sha(rp.read_bytes()),directory=str(clone),head=C,count=1124,clean=True,no_alternates=True,files=files,network_provenance='Root GitHub-only receipt; reviewer independently verified local files/HEAD/clean/no-alternates, with no network/restored runtime.'),reviewer_runtime_count=0)
p=T/'t179pd-hot-custody-hs01-causal-red-independent-provenance-20261016.json';p.open('x').write(json.dumps(out,indent=2)+'\n');print(json.dumps({'path':str(p),'sha256':sha(p.read_bytes()),'pins':len(out['pins']),'raw_chains':6,'prior_late_chains_unchanged':6,'DLLs':4,'recovery':len(files),'case_count':len(cases),'exact_missing_export_red':True},indent=2))
