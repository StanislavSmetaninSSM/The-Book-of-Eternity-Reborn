from pathlib import Path
from collections import Counter
import json,gzip,hashlib,subprocess,xml.etree.ElementTree as ET,base64
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery';T=Path('/tmp');C='e4d438e2ebc9b2ee63c863e864c805e6087d0f56';S='8c098487398830c20bd3705261c2a2c507bce5d8';B='e28914befdadaa5ce20bffffc8b3c885a4206142';P='00aa1c2d70e539805bd6ea72941f4c09c0c00c31';h=lambda b:hashlib.sha256(b).hexdigest()
def git(*a,cwd=R):return subprocess.check_output(['git',*a],cwd=cwd)
def pin(p,rev=C,current=True):
 b=git('show',rev+':'+p)
 if current:assert b==(R/p).read_bytes(),p
 return {'path':p,'revision':rev,'blob':git('rev-parse',rev+':'+p).decode().strip(),'bytes':len(b),'sha256':h(b)}
def chains(prefix,carrier):
 ip=D/(prefix+'-artifact-index.json');idx=json.loads(ip.read_text());out=[]
 for row in idx['artifacts']:
  p=D/row['path'];stored=p.read_bytes();raw=gzip.decompress(stored) if p.suffix=='.gz' else stored;local=Path(row['raw_path']);local=local if local.is_absolute() else R/local
  assert h(stored)==row['stored_sha256'] and h(raw)==row['raw_sha256'] and len(raw)==row['raw_bytes'] and local.read_bytes()==raw
  cp=pin(str(p.relative_to(R)),carrier);fp=pin(str(p.relative_to(R)));assert cp['blob']==fp['blob'];out.append({**row,'original_carrier':carrier,'original_blob':cp['blob'],'final_blob':fp['blob']})
 return idx,out
assert git('rev-parse','HEAD').decode().strip()==C and not git('status','--porcelain')
plan=json.loads((D/'t179pd-observed-green-default-fixture-prep-plan-20261016-plan.json').read_text());expected=plan['Descriptors'];wanted=Counter();[wanted.update({m['Name']:m['Cases'] for m in d['ExpectedMethodCounts']}) for d in expected];assert len(expected)==8 and len(wanted)==59 and sum(wanted.values())==131
packets=[('fr063-joint-late-completion','t179pd-observed-green-default-fixture-first-late-20261016','eac4e7e0dd695770d86f776d89f90ae9a2d7f822',S),('fr063-observed-original-reader','t179pd-observed-green-orf02-reader-20261016','05a077e1cf20ecaee92640677eaa5f0e08d0d921','e522102735317eedc04e42ea1e0b892f73983871'),('fr063-observed-original-barrier','t179pd-observed-green-orf02-barrier-20261016','0bbc8524713ea6ce70231fc8185a91d6708d2d0e','e522102735317eedc04e42ea1e0b892f73983871'),('fr063-observed-original-conformance','t179pd-observed-green-admission-fixture-conformance-20261016','54e07796d5fca6bac5940b447ef65379727f2247','d50d1a9a98e087c8fc5a8908745dfd1e03aaac48'),('fr063-observed-original-browser','t179pd-observed-green-admission-fixture-browser-20261016','da3b8085845fd8cdebba7be7adce9e5416f998ac','d50d1a9a98e087c8fc5a8908745dfd1e03aaac48'),('fr063-observed-original-default','t179pd-observed-green-default-fixture-default-20261016','ba5650f710a9a6dcc96c07492bafadc2b823a1fb',S),('portable-ordinary-readers','t179pd-observed-green-default-fixture-ordinary-20261016','3db8f9b2f6e5f6c8f63e6d791feef5efc6f8c814',S)]
allcases=[];allchains=[];summaries=[];allroots=[];ids=set();execids=set();names=set();got=Counter();evidence=[];boundary_diagnostics=[]
for category,prefix,carrier,compiled in packets:
 idx,links=chains(prefix,carrier);allchains+=links;su=json.loads((D/(prefix+'-summary.json')).read_text());pl=json.loads((D/(prefix+'-plan.json')).read_text());na=json.loads((D/(prefix+'-native-exit.json')).read_text());actual=na['actual_head']
 assert idx['summary']==su and idx['source_sha']==actual and idx['native_exit_code']==na['native_exit']==su['ExitCode']==0 and na['compiled_source']==compiled
 assert na['argv']==['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category',category,'-NoBuild'] and na['category']==category
 assert na['binaries_unchanged'] and na['post_head_unchanged'] and na['post_git_clean'] and all(not v for v in na['new_scoped_temp_residue'].values())
 assert pl['Categories']==su['Categories']==[category] and pl['Revision']==su['Revision'] and su['Revision']['Head']==actual and su['Revision']['ChangedFiles']==0
 assert not su['TimedOut'] and not su['DuplicateTests'] and not su['DiscoveryOnly'] and not su['PlanOnly'] and su['OwnedTreeCleanupSucceeded'] and su['RuntimeCleanupSucceeded'] and su['Selection']['Complete'] and not su['Selection']['UnderfilledDescriptors']
 assert all(q['Completed'] and q['Seconds']<=q['BudgetMinutes']*60 for q in su['CategoryResults'])
 peers=[d for d in expected if d['Category']==category];assert len(peers)==len(pl['Descriptors']);cases=[];roots=[]
 for d in pl['Descriptors']:
  peer=next(p for p in peers if p['Project']==d['Project']);assert {k:v for k,v in peer.items() if k!='Name'}=={k:v for k,v in d.items() if k!='Name'}
  trx=D/(prefix+'-'+d['Name']+'.trx.gz');xml=ET.fromstring(gzip.decompress(trx.read_bytes()));ns={'t':xml.tag.split('}')[0][1:]};definitions={}
  for definition in xml.findall('t:TestDefinitions/t:UnitTest',ns):
   m=definition.find('t:TestMethod',ns);definitions[definition.get('id')]=m.get('className').split(',')[0]+'.'+m.get('name')
  results=xml.findall('t:Results/t:UnitTestResult',ns);ct=xml.find('t:ResultSummary/t:Counters',ns).attrib;assert all(int(v)==(len(results) if k in ['total','executed','passed'] else 0) for k,v in ct.items());counts=Counter()
  for result in results:
   tid,eid,name=result.get('testId'),result.get('executionId'),result.get('testName');assert tid not in ids and eid not in execids and name not in names and result.get('outcome')=='Passed';ids.add(tid);execids.add(eid);names.add(name);method=definitions[tid];counts[method]+=1
   stdout=result.findtext('t:Output/t:StdOut',namespaces=ns) or '';di=[]
   for line in stdout.splitlines():
    try:v=json.loads(line)
    except ValueError:continue
    if isinstance(v,dict):di.append(v)
   clean=[v for v in di if 'CleanupOwnedRoot'in v];strict=category!='fr063-observed-original-default' and (category!='portable-ordinary-readers' or method.startswith('BookOfEternityClient.Tests.PortableOrdinaryReadTests.'))
   assert len(clean)==(1 if strict else 0)
   for v in clean:assert v['OwnedFixtureRemoved'] and v['CleanupFailure'] is None and not Path(v['CleanupOwnedRoot']).exists();roots.append(v['CleanupOwnedRoot'])
   case={'category':category,'descriptor':d['Name'],'method':method,'name':name,'test_id':tid,'execution_id':eid,'outcome':'Passed','duration':result.get('duration'),'compiled_source':compiled,'actual':actual,'carrier':carrier,'stdout_sha256':h(stdout.encode()),'cleanup':clean,'cleanup_scope':'strict_per_case' if strict else 'runner_and_scoped_prefix_only'};cases.append(case)
   if category in ['fr063-observed-original-reader','fr063-observed-original-barrier']:boundary_diagnostics.append({'case':name,'rows':di})
  assert counts==Counter({m['Name']:m['Cases'] for m in d['ExpectedMethodCounts']}) and len(results)==d['EstimatedCases'];got.update(counts)
 n=len(cases);assert su['Tests']==dict(Total=n,Executed=n,Passed=n,Failed=0) and su['Selection']=={'PlannedDescriptors':len(peers),'CompletedDescriptors':len(peers),'PlannedCases':n,'CompletedCases':n,'UnderfilledDescriptors':[],'Complete':True}
 allcases+=cases;allroots+=roots;summaries.append({'category':category,'prefix':prefix,'carrier':carrier,'compiled_source':compiled,'actual':actual,'summary':su,'native':na,'descriptors':pl['Descriptors'],'cases':n,'strict_roots':len(roots),'chains':len(links)})
 for p in sorted(D.glob(prefix+'*')):evidence.append(pin(str(p.relative_to(R))))
assert got==wanted and len(ids)==len(execids)==len(names)==len(allcases)==131 and len(allchains)==43 and len(allroots)==len(set(allroots))==63
assert sum(c['compiled_source']==S for c in allcases)==85 and sum(c['compiled_source']!=S for c in allcases)==46
assert sum(c['cleanup_scope']=='runner_and_scoped_prefix_only' for c in allcases)==68
assert not any('BrowserStagingBeforeRecorderAttachment' in m for m in got)
# Both causal RED packets were frozen before production success logic; keep them distinct from unexpected failures.
reds=[]
for prefix,carrier,compiled,expected_tests,expected_line in [('t179pd-observed-reader-causal-red-20261016','61e10b09122cdd0d3af4478214fdb3cef11864f4','db655665a0f109b39172b4d6806a680bb3cb76e6',{'Total':1,'Executed':1,'Passed':0,'Failed':1},80),('t179pd-observed-barrier-causal-red-20261016','e312c78b3f1a95766f535ef8372b049ee550138c','9aa339141ec8ebec301b18679aa021d018250b0f',{'Total':2,'Executed':2,'Passed':1,'Failed':1},36)]:
 idx,links=chains(prefix,carrier);su=json.loads((D/(prefix+'-summary.json')).read_text());na=json.loads((D/(prefix+'-native-exit.json')).read_text());assert su['Tests']==expected_tests and su['ExitCode']==na['native_exit']==1
 assert b'ReadObservedCurrent' not in git('show',compiled+':BookOfEternityClient/Services/PendingTurnSnapshotReader.cs');git('merge-base','--is-ancestor',carrier,P)
 trx=next(D.glob(prefix+'-*.trx.gz'));xml=ET.fromstring(gzip.decompress(trx.read_bytes()));ns={'t':xml.tag.split('}')[0][1:]};rr=xml.findall('.//t:UnitTestResult',ns);fail=[r for r in rr if r.get('outcome')=='Failed'];assert len(fail)==1;msg=fail[0].findtext('t:Output/t:ErrorInfo/t:Message',namespaces=ns);stack=fail[0].findtext('t:Output/t:ErrorInfo/t:StackTrace',namespaces=ns);assert 'line '+str(expected_line) in stack
 di=[]
 for r in rr:
  for line in (r.findtext('t:Output/t:StdOut',namespaces=ns)or'').splitlines():
   try:v=json.loads(line)
   except ValueError:continue
   if isinstance(v,dict):di.append(v)
 clean=[v for v in di if 'CleanupOwnedRoot'in v];assert len(clean)==expected_tests['Total'] and all(v['OwnedFixtureRemoved'] and v['CleanupFailure'] is None and not Path(v['CleanupOwnedRoot']).exists() for v in clean)
 if expected_line==80:assert 'Assert.NotNull' in msg and any(v.get('GenuineOriginalControl') and v.get('ReadOnlyTree') for v in di)
 else:assert 'completed original read must not issue authority' in msg
 reds.append({'prefix':prefix,'carrier':carrier,'compiled_source':compiled,'native':na,'tests':su['Tests'],'sole_failure':{'message':msg,'stack':stack},'diagnostics':di,'chains':links,'precedes_implementation':True})
 for p in sorted(D.glob(prefix+'*')):evidence.append(pin(str(p.relative_to(R))))
# Compare root accounting as a cross-check only after independent reconstruction.
rootpath=D/'t179pd-observed-reader-final-packet-audit-20261016.json';root=json.loads(rootpath.read_text());assert h(rootpath.read_bytes())=='21921482e788d09aa15a43d765b2aded17ad49fb306bc1dbbfa377422f643aae'
assert {(c['testId'],c['name'],c['compiled_source'],c['actual_head'],c['carrier'])for c in root['cases']}=={(c['test_id'],c['name'],c['compiled_source'],c['actual'],c['carrier'])for c in allcases};assert set(root['all_roots'])==set(allroots)
evidence.append(pin(str(rootpath.relative_to(R))))
scope=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityClient.TestSupport','BookOfEternityGMBridge','tests','scripts'];diffs=[]
for name,a,b,paths in [('complete-feature',B,S,scope),('implementation-and-corrections','35950488c3bd12241384dca9c13cd3166d738fb9',S,scope),('production-unchanged',P,S,['BookOfEternityClient']),('compiled-to-final',S,C,scope),('last-actual-to-final','ba5650f710a9a6dcc96c07492bafadc2b823a1fb',C,scope)]:
 argv=['git','diff','--no-ext-diff','--binary','--full-index',a,b,'--',*paths];v=subprocess.check_output(argv,cwd=R)
 if name in ['production-unchanged','compiled-to-final','last-actual-to-final']:assert not v
 p=T/('t179pd-observed-reader-final-'+name+'-20261016.diff');assert not p.exists();p.write_bytes(v);q=p.with_suffix('.diff.gz');assert not q.exists();q.write_bytes(gzip.compress(v,mtime=0));diffs.append({'argv':argv,'path':str(p),'bytes':len(v),'sha256':h(v),'gzip_path':str(q),'gzip_sha256':h(q.read_bytes())})
changed=git('diff','--name-only',B,S,'--',*scope).decode().splitlines();assert len(changed)==13
paths=changed+['BookOfEternityClient.Tests/PendingTurnSnapshotAuthorityTests.cs','BookOfEternityClient.IntegrationTests/FileSystemManagerTests.cs','BookOfEternityClient.TestSupport/TrustedLocalPublicationCrashFixture.cs','BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs','BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs','BookOfEternityClient/Services/LiveTurnPreparationService.cs','BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs','BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs','BookOfEternityClient/Core/StrictJsonAuthority.cs','scripts/test-csharp.ps1','AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md','specs/1536-complete-wound-materialization/spec.md','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md','specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md'];sourcepins=[pin(p)for p in paths]
# Relevant prior reports are immutable context; retain full source/evidence verdicts without upgrading their original scope.
prior=[]
for n in ['t179pd-observed-green-source-review-20261016','t179pd-observed-green-corrected-source-review-20261016','t179pd-observed-green-orf02-source-review-20261016','t179pd-observed-green-admission-fixture-source-review-20261016','t179pd-selected-negative-fixture-admission-audit-20261016','t179pd-observed-green-default-fixture-source-review-20261016','t179pd-observed-green-default-fixture-late-evidence-review-20261016']:
 for ext in ['md','json']:
  p=D/(n+'.'+ext);q=T/(n+'.'+ext);assert p.read_bytes()==q.read_bytes();prior.append(pin(str(p.relative_to(R))))
lastreview=json.loads((D/'t179pd-observed-green-default-fixture-late-evidence-review-20261016.json').read_text());assert lastreview['verdict']=='ACCEPTED_CONDITIONAL_DEFAULT67_THEN_ORDINARY16_ONLY'
for row in lastreview['historical_pins']:assert pin(row['path'])['sha256']==row['sha256']
for row in lastreview['independent_artifacts']:assert h(Path(row['path']).read_bytes())==row['sha256']
bi=json.loads((D/'t179pd-observed-green-default-fixture-prep-binaries-20261016.json').read_text());assert bi['source_sha']==S
for row in bi['binaries']:assert h((R/row['path']).read_bytes())==row['sha256']
evidence.append(pin(str((D/'t179pd-observed-green-default-fixture-prep-binaries-20261016.json').relative_to(R))))
# Execution order and isolated source retention.
assert summaries[-1]['actual']==summaries[-2]['carrier'];git('merge-base','--is-ancestor','c1094b47e609f62c4de4f4cb8d0acc2435b6d595',summaries[-2]['actual'])
for row in summaries:
 assert not git('diff','--name-only',row['compiled_source'],row['actual'],'--',*scope)
 assert not git('diff','--name-only',row['actual'],row['carrier'],'--',*scope)
# Current recovery, including exact final carrier and historical file scopes.
rp=T/'t179pd-observed-reader-final-review-selected-recovery-20261016.json';rec=json.loads(rp.read_text());rr=Path(rec['directory']);assert rec['sha']==C and rec['count']==len(rec['files'])==962
assert git('rev-parse','HEAD',cwd=rr).decode().strip()==C and not git('status','--porcelain',cwd=rr);gd=Path(git('rev-parse','--absolute-git-dir',cwd=rr).decode().strip());assert not (gd/'objects/info/alternates').exists()
for row in rec['files']:
 v=(rr/row['path']).read_bytes();assert len(v)==row['bytes'] and h(v)==row['sha256'];assert v==git('show',C+':'+row['path'])==git('show',C+':'+row['path'],cwd=rr)
out={'carrier':C,'compiled_source':S,'feature_base':B,'production_source':P,'clean':True,'source_pins':sourcepins,'evidence_pins':evidence,'prior_report_pins':prior,'diffs':diffs,'selection':{'plan_pin':pin(str((D/'t179pd-observed-green-default-fixture-prep-plan-20261016-plan.json').relative_to(R))),'descriptors':expected,'methods':dict(wanted),'unique_methods':59,'unique_cases':131,'categories':7,'current_source_cases':85,'retained_original_source_cases':46},'packets':summaries,'successful_raw_chains':allchains,'cases':allcases,'strict_absent_roots':allroots,'narrow_cleanup_cases':68,'reader_barrier_diagnostics':boundary_diagnostics,'causal_reds':reds,'historical_failed_packets':lastreview['previous_failed_packets'],'root_accounting_matches_independent_union':True,'binaries':bi,'prior_late_semantic_evidence_retained':lastreview['independent_artifacts'],'recovery':{'receipt':str(rp),'sha256':h(rp.read_bytes()),'revision':C,'files_verified':962,'file_pins':rec['files'],'clean':True,'no_alternates':True,'transport_attestation':rec['source'],'remote_readback_receipt':rec['remoteReadback'],'network_repeated_by_reviewer':False,'restored_runtime':'NOT_RUN'}}
q=T/'t179pd-observed-reader-final-independent-inspection-20261016.json';assert not q.exists();q.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'inspection':str(q),'sha256':h(q.read_bytes()),'source_pins':len(sourcepins),'evidence_pins':len(evidence),'prior_reports':len(prior),'successful_chains':len(allchains),'causal_red_chains':sum(len(r['chains'])for r in reds),'cases':len(allcases),'strict_roots':len(allroots),'recovery_files':962},indent=2))
