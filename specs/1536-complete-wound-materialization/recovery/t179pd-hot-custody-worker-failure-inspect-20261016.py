from pathlib import Path
import json,gzip,hashlib,subprocess,os,xml.etree.ElementTree as E,re,datetime,sys
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=Path('specs/1536-complete-wound-materialization/recovery');C='8ac70607652bcb728e6c2de453514c75f8af7192';A='8562db25b748186354d36a0a6d6dc8efc63de991';S='f9bc5cc824cbc374225e64e8fe6c2da4cd8f09c9';B='7acd4a960fa273e0df2dd83e4998baef79ba94d5';P='t179pd-hot-custody-implementation-green-worker2-20261016';OUT=Path('/tmp/t179pd-hot-custody-worker-failure-inspection-20261016.json');assert not OUT.exists();h=lambda b:hashlib.sha256(b).hexdigest();env=dict(os.environ,GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0');commands=[];pins=[];projections=[]
def git(*args,root=R):
 p=subprocess.run(['git',*args],cwd=root,env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE);commands.append(dict(argv=['git',*args],cwd=str(root),exit=p.returncode,stdoutBytes=len(p.stdout),stdoutSha256=h(p.stdout),stderr=p.stderr.decode()));assert p.returncode==0;return p.stdout
def pin(path,rev=C):
 b=git('show',rev+':'+str(path));pins.append(dict(revision=rev,path=str(path),blob=git('rev-parse',rev+':'+str(path)).decode().strip(),bytes=len(b),sha256=h(b)));return b
def proj(path,lo,hi,rev=S):
 b=pin(path,rev);t=''.join(b.decode().splitlines(keepends=True)[lo-1:hi]);projections.append(dict(revision=rev,path=path,start=lo,end=hi,sha256=h(t.encode()),text=t))
idx=json.loads(pin(D/(P+'-artifact-index.json')));assert idx['source_sha']==A and idx['native_exit_code']==1;chains=[]
for a in idx['artifacts']:
 st=pin(D/a['path']);raw=gzip.decompress(st) if a['path'].endswith('.gz') else st;assert st==(R/D/a['path']).read_bytes() and h(st)==a['stored_sha256'];assert raw==Path(a['raw_path']).read_bytes() and h(raw)==a['raw_sha256'] and len(raw)==a['raw_bytes'];chains.append(dict(**a,rawLocalStoredGitEqual=True,carrier=C))
assert len(chains)==6
summary=json.loads(pin(D/(P+'-summary.json')));native=json.loads(pin(D/(P+'-native-exit.json')));plan=json.loads(pin(D/(P+'-plan.json')))
assert summary==idx['summary'];assert summary['Tests']==dict(Total=2,Executed=2,Passed=0,Failed=2);assert summary['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=2,CompletedCases=2,UnderfilledDescriptors=[],Complete=True);assert not summary['TimedOut'] and not summary['DuplicateTests'] and summary['OwnedTreeCleanupSucceeded'] and summary['RuntimeCleanupSucceeded'];assert summary['Revision']['Head']==A and summary['Revision']['ChangedFiles']==0;assert summary['ExitCode']==1
assert native['native_exit']==1 and native['actual_head']==A and native['compiled_source']==S;assert native['binaries_unchanged'] and native['post_head_unchanged'] and native['post_git_clean'];assert native['argv']==['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category','fr063-hot-worker-ready-disposition','-NoBuild'];assert all(not v for k in ['scoped_temp_prefixes_before','scoped_temp_prefixes_after','new_scoped_temp_residue'] for v in native[k].values())
desc=plan['Descriptors'];assert len(desc)==1 and desc[0]['EstimatedCases']==2 and desc[0]['BudgetMinutes']==5 and desc[0]['Exclusive'] and not desc[0]['FrontendBuild'];assert summary['CategoryResults'][0]['Seconds']==89.6058263 and not summary['CategoryResults'][0]['Completed']
trx=next(a for a in idx['artifacts'] if '.trx' in a['path']);xml=E.fromstring(gzip.decompress(pin(D/trx['path'])));ns={'t':xml.tag.split('}')[0][1:]};results=xml.findall('.//t:UnitTestResult',ns);counter=xml.find('.//t:Counters',ns).attrib;assert int(counter['total'])==int(counter['executed'])==int(counter['failed'])==2;assert all(int(v)==0 for k,v in counter.items() if k not in ['total','executed','failed']);assert len(results)==2 and len({r.get('testId') for r in results})==2
cases=[]
for r in results:
 assert r.get('outcome')=='Failed';msg=r.findtext('t:Output/t:ErrorInfo/t:Message',namespaces=ns);stack=r.findtext('t:Output/t:ErrorInfo/t:StackTrace',namespaces=ns);assert msg=="System.TimeoutException : Actual worker repair Wait exceeded the fixture's protective 40s bound.";assert 'HotWorkerDisposition.cs:line 122' in stack and 'HotWorkerDisposition.cs:line 160' in stack
 stdout=r.findtext('t:Output/t:StdOut',namespaces=ns);rows=[]
 for line in stdout.splitlines():
  try:v=json.loads(line)
  except ValueError:continue
  if isinstance(v,dict):rows.append(v)
 obs=[v['Observation'] for v in rows if v.get('Boundary')=='fr063_hot_actual_worker_settled_before_oracles'];assert len(obs)==1;obs=obs[0];assert obs['guardFired'] and obs['WaitSettled'] and obs['GuardSettled'];assert obs['accepted']==False and obs['readyCuts']==0 and obs['appliedTaskAtCut'] is None and obs['Audit']==[];assert len(obs['Failures'])==1 and 'protective 40s bound' in obs['Failures'][0];assert not any(v.get('Boundary')=='fr063_hot_actual_worker_disposition' for v in rows)
 task=json.loads(obs['LatestTask']);assert task['schemaVersion']==1 and task['sessionGeneration']==obs['generation'] and re.fullmatch('[0-9a-f]{32}',task['sessionGeneration']);assert task['sourceTurn']==dict(sessionId='session_hot_actual_worker',requestId=obs['RequestId'],turnNumber=43);assert task['workerId']=='validation_repair_codex' and task['role']==task['taskType']=='validation-repair' and task['timeoutSeconds']==15;assert re.fullmatch('[a-z0-9_-]+',task['taskId']);assert task['allowedProposalPaths']==['game_state/world/weather.json'];assert task['validationIssues']==[dict(code='normalized_weather_missing_description',path='game_state/world/weather.json',message='Synthetic weather repair owner control.')];assert len(task['contextFiles'])==1 and task['contextFiles'][0]['path']=='game_state/world/weather.json' and re.fullmatch('[0-9a-f]{64}',task['contextFiles'][0]['sha256']);assert task['responseContract']=='worker-proposal-v1';assert all(task[k] for k in ['createdAtUtc','acceptanceCriteria','forbiddenActions','instructions']);assert all(k not in task for k in ['spiritualWoundContinuation','afterlifeContract','authoringRequest','draftRequest','guardianAbodeRequest','soulContentRequest'])
 clean=[v for v in rows if 'CleanupOwnedRoot' in v];assert len(clean)==1;clean=clean[0];assert clean['OwnedFixtureRemoved'] and clean['CleanupFailure'] is None and not Path(clean['CleanupOwnedRoot']).exists();cases.append(dict(identity=r.attrib,error=msg,stack=stack,stdoutSha256=h(stdout.encode()),settledDiagnostic=obs,actualTask=task,cleanup=clean,positiveOraclesReached=False))
assert {c['settledDiagnostic']['failReadyPublication'] for c in cases}=={False,True};assert len({c['cleanup']['CleanupOwnedRoot'] for c in cases})==2
manifest=json.loads(pin(D/'t179pd-hot-custody-implementation-workerfix-prep-binaries-20261016.json'));assert manifest['source_sha']==S and len(manifest['binaries'])==4
for b in manifest['binaries']:assert h((R/b['path']).read_bytes())==b['sha256']
diffs=[];scope=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityClient.TestSupport','tests','scripts','AGENTS.md','docs/development-workflow.md','docs/testing.md']
for a,b in [(S,A),(S,C),(A,C)]:
 argv=['diff','--binary','--full-index','--no-ext-diff',a,b,'--',*scope];v=git(*argv);assert not v;diffs.append(dict(argv=['git',*argv],bytes=len(v),sha256=h(v),text=v.decode()))
argv=['diff','--binary','--full-index','--no-ext-diff',A,C,'--','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md'];v=git(*argv);diffs.append(dict(argv=['git',*argv],bytes=len(v),sha256=h(v),text=v.decode()))
for p,lo,hi in [
 ('BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.HotWorkerDisposition.cs',14,194),
 ('BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs',10916,11005),
 ('BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SpiritualContinuation.cs',480,510),
 ('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',3330,3448),
 ('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',7878,7948),
 ('BookOfEternityClient/Services/GmWorkers/GmWorkerValidationRepairDelegator.cs',105,221),
 ('BookOfEternityClient/Services/GmWorkers/GmWorkerValidationRepairDelegator.cs',429,460),
 ('BookOfEternityClient/Services/GmWorkers/GmWorkerBridgePool.cs',240,305),
 ('BookOfEternityClient/Services/GmWorkers/GmWorkerBridgePool.cs',344,413),
 ('BookOfEternityClient/Services/GmWorkers/GmWorkerBridgePool.cs',529,592),
 ('BookOfEternityClient/Services/GmWorkers/GmWorkerBackendSelector.cs',1,47),
 ('BookOfEternityClient/Services/GmWorkers/GmWorkerContractValidator.cs',7,171),
 ('BookOfEternityClient/Services/GmWorkers/GmWorkerContractValidator.cs',481,504),
 ('BookOfEternityClient/Services/GmWorkers/GmWorkerBridgeProfileTemplates.cs',30,49),
 ('BookOfEternityClient.TestSupport/GmWorkerBridgeTestFixtures.cs',13,17),
 ('BookOfEternityClient.Tests/GmWorkerNativeAdmissionTests.cs',35,80),
 ('BookOfEternityClient.Tests/GmWorkerNativePoolTests.cs',81,114),
 ('BookOfEternityClient/Services/GmWorkers/GmWorkerNativePoolAdmission.cs',1,63),
 ('BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SpiritualWorkerFixture.cs',77,86),
 ('BookOfEternityClient.IntegrationTests/GmWorkerValidationRepairDelegatorTests.cs',1128,1149)]:proj(p,lo,hi)
# Retain preceding thirteen outcomes with original actual heads; this is count/provenance inspection, not their final semantic acceptance.
retained=[]
for stem,count in [('t179pd-hot-custody-implementation-first-late-20261016',2)]+[('t179pd-hot-custody-implementation-green-'+tag+'-20261016',n) for tag,n in [('ready3',3),('hs01',1),('live1',1),('active-tail3',3),('owner-refusal1',1),('browser2',2)]]:
 ix=json.loads(pin(D/(stem+'-artifact-index.json')));su=ix['summary'];assert ix['native_exit_code']==0 and su['Tests']==dict(Total=count,Executed=count,Passed=count,Failed=0) and su['Selection']['Complete'] and not su['TimedOut'];tr=next(a for a in ix['artifacts'] if '.trx' in a['path']);st=pin(D/tr['path']);raw=gzip.decompress(st);assert h(st)==tr['stored_sha256'] and h(raw)==tr['raw_sha256'] and raw==Path(tr['raw_path']).read_bytes();root=E.fromstring(raw);n={'t':root.tag.split('}')[0][1:]};rr=root.findall('.//t:UnitTestResult',n);assert len(rr)==count and all(r.get('outcome')=='Passed' for r in rr);retained.append(dict(prefix=stem,actualHead=su['Revision']['Head'],tests=su['Tests'],trx=tr,cases=[r.attrib for r in rr],qualification='Existing actual PASS retained with original provenance; final complete semantic stage review still required.'))
assert sum(x['tests']['Passed'] for x in retained)==13 and len({r['testId'] for x in retained for r in x['cases']})==13
rp=Path('/tmp/t179pd-hot-custody-implementation-first-worker-failure-delta-recovery-20261016.json');rb=rp.read_bytes();rec=json.loads(rb);assert rec['sha']==C and rec['base_full_tree']==B and rec['runtime']=='NOT_RUN';assert git('diff','--name-status',B,C).decode()==rec['name_status'] and git('diff','--summary',B,C).decode()==rec['mode_change_summary'];changed=git('diff','--name-only',B,C).decode().splitlines();assert len(changed)==rec['changed_count']==133 and len(rec['files'])==rec['count']==139 and set(changed)<={r['path'] for r in rec['files']};assert rec['deleted_paths']==git('diff','--name-only','--diff-filter=D',B,C).decode().splitlines()==[]
for f in rec['files']:
 b=pin(f['path']);p=Path(rec['directory'])/f['path'];assert b==p.read_bytes()==(R/f['path']).read_bytes() and h(b)==f['sha256'] and len(b)==f['bytes'];assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==f['git_blob'];assert git('ls-tree',C,'--',f['path']).split()[0].decode()==f['mode']==('100755' if p.stat().st_mode&0o111 else '100644')
base=Path(rec['base_full_tree_directory']);assert git('rev-parse','HEAD',root=base).decode().strip()==B and git('status','--porcelain=v1','--untracked-files=all','--ignored',root=base)==b''
for suffix in ['root-inspection.json','root-supports.json']:pin(D/(P+'-'+suffix))
for path in ['AGENTS.md','docs/development-workflow.md','docs/testing.md','tests/categories.json','tests/selection.json']:pin(path)
for p,lo,hi in [('specs/1536-complete-wound-materialization/plan.md',1,24),('specs/1536-complete-wound-materialization/tasks.md',44,58)]:proj(p,lo,hi,C)
assert git('rev-parse','HEAD').decode().strip()==C and not git('status','--porcelain=v1');residue=[str(p) for p in Path('/tmp').glob('boe-gameengine-turnlifecycle-*') if p.is_dir()];assert not residue
out=dict(carrier=C,actualHead=A,compiledSource=S,retainedBase=B,reviewHostPlatform=sys.platform,native=native,summary=summary,plan=plan,trxCounters=counter,cases=cases,verifiedFailureChains=chains,fourBinaries=manifest,sourceDiffs=diffs,gitPins=pins,projections=projections,retainedPriorPass=retained,retainedPassCount=13,recovery=dict(path=str(rp),sha256=h(rb),receipt=rec,allDeltaPathsModesRawGitWriterVerified=True,fullBaseCurrentCleanIncludingIgnored=True,fullBasePriorIndependentVerificationRetained=True),currentScopedResidue=residue,commands=commands,runtimeExecutedByReviewer=False,release=[],verdict='WORKER_POSITIVE_QUALIFICATION_BLOCKED_SOURCE_REACHABILITY_CONFIRMED')
OUT.write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n');print(json.dumps(dict(output=str(OUT),sha256=h(OUT.read_bytes()),pins=len(pins),projections=len(projections),diffs=len(diffs),failureChains=len(chains),failedCases=2,retainedPass=13,recoveryFiles=139)))
