from pathlib import Path
import json,gzip,hashlib,subprocess
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery';T=Path('/tmp');S='8c098487398830c20bd3705261c2a2c507bce5d8';A='9bbcc7af7a3ed711df1676a1e1340a6bfc13aa62';C='eac4e7e0dd695770d86f776d89f90ae9a2d7f822';PREP='e79e6c239f3461f6c7542986279ff6dd99796062';P='t179pd-observed-green-default-fixture-first-late-20261016'
h=lambda b:hashlib.sha256(b).hexdigest()
def g(*a,cwd=R):return subprocess.check_output(['git',*a],cwd=cwd)
def pin(p,rev=C,current=True):
 b=g('show',rev+':'+p)
 if current:assert b==(R/p).read_bytes(),p
 return {'path':p,'revision':rev,'blob':g('rev-parse',rev+':'+p).decode().strip(),'bytes':len(b),'sha256':h(b)}
assert g('rev-parse','HEAD').decode().strip()==C and not g('status','--porcelain')
scope=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityClient.TestSupport','BookOfEternityGMBridge','tests','scripts'];diffs=[]
for n,a,b in [('compiled-to-actual',S,A),('prep-to-actual',PREP,A),('actual-to-carrier',A,C),('compiled-to-carrier',S,C)]:
 argv=['git','diff','--no-ext-diff','--binary','--full-index',a,b,'--',*scope];raw=subprocess.check_output(argv,cwd=R);assert raw==b'';g('merge-base','--is-ancestor',a,b);p=T/('t179pd-observed-green-default-fixture-late-'+n+'.diff');assert not p.exists();p.write_bytes(raw);diffs.append({'argv':argv,'path':str(p),'bytes':0,'sha256':h(raw)})
source=json.loads((D/'t179pd-observed-green-default-fixture-source-review-20261016.json').read_text());assert source['verdict']=='ACCEPTED_FIRST_PROTECTED_LATE2_ONLY' and source['compiled_source']==S
raw=json.loads((T/'t179pd-observed-green-default-fixture-late-independent-inspection.json').read_text());bindings=json.loads((T/'t179pd-observed-green-default-fixture-late-independent-bindings.json').read_text());ct=raw['trxCounters'];assert ct['total']==ct['executed']==ct['passed']=='2' and all(v=='0' for k,v in ct.items() if k not in ['total','executed','passed'])
expected=source['selection']['descriptors'];assert raw['plan']['Descriptors']==[source['released_actual']['descriptor']]
assert raw['summary']['Categories']==['fr063-joint-late-completion'] and raw['summary']['WallTime']=='00:03:36.2110999' and raw['summary']['CategoryResults']==[{'Category':'fr063-joint-late-completion','Seconds':211.3661387,'BudgetMinutes':5,'Completed':True}]
assert not raw['summary']['DiscoveryOnly'] and not raw['summary']['PlanOnly']
sourcepins=[]
for row in source['source_pins']:
 q=pin(row['path'],row['revision'],current=not row['path'].startswith('specs/'));assert q['sha256']==row['sha256'] and q['blob']==row['blob']
 if not row['path'].startswith('specs/'):sourcepins.append(pin(row['path'],S))
for p in ['BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.QteStoryAppend.cs','BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs','BookOfEternityClient/Services/ProgressionScheduleService.MortalJointBasis.cs','BookOfEternityClient/Services/StoryService.MortalJointRepair.cs']:sourcepins.append(pin(p,S))
late='BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs';assert g('show','9aa339141ec8ebec301b18679aa021d018250b0f:'+late)==g('show',S+':'+late)
projections=[]
for p,start,end in [(late,15,43),(late,339,381),(late,411,434),(late,841,941),(late,972,1031),('BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs',131,142)]:
 text=''.join(g('show',S+':'+p).decode().splitlines(keepends=True)[start-1:end]);projections.append({'path':p,'revision':S,'first_line':start,'last_line':end,'sha256':h(text.encode()),'text':text})
ep=[]
for row in source['evidence_pins']:
 q=pin(row['path']);assert q['sha256']==row['sha256'];ep.append(q)
for n,sha in [('t179pd-observed-green-default-fixture-source-review-20261016.md','14edb73a646665be155403b8575245ec0c57d6ad157ad93bfd5aecbfc69cc5b5'),('t179pd-observed-green-default-fixture-source-review-20261016.json','30def04f7fc39f157e9473178bbd8c0cbee26d5cac81a135586a98f1c61e2df0')]:
 p=D/n;assert h(p.read_bytes())==sha and p.read_bytes()==(T/n).read_bytes();ep.append(pin(str(p.relative_to(R))))
for p in sorted(D.glob(P+'*')):ep.append(pin(str(p.relative_to(R))))
extra=json.loads((D/'t179pd-observed-green-default-fixture-first-late-extra-index-20261016.json').read_text());extraChains=[]
for row in extra:
 p=D/row['path'];b=p.read_bytes();v=gzip.decompress(b) if p.suffix=='.gz' else b;assert len(v)==row['raw_bytes'] and h(v)==row['raw_sha256']
 if 'stored_sha256'in row:assert h(b)==row['stored_sha256']
 ep.append(pin(str(p.relative_to(R))));extraChains.append({**row,'committed_verified':True})
ep.append(pin(str((D/'t179pd-observed-green-default-fixture-first-late-extra-index-20261016.json').relative_to(R))))
bi=source['binaries'];assert bi['source_sha']==S
for row in bi['binaries']['binaries'] if False else bi['binaries']:assert h((R/row['path']).read_bytes())==row['sha256']
na=json.loads((D/(P+'-native-exit.json')).read_text());assert na['argv']==source['released_actual']['argv'] and na['native_exit']==0 and na['actual_head']==A and na['compiled_source']==S and na['category']=='fr063-joint-late-completion'
assert na['post_git_clean'] and na['post_head_unchanged'] and na['binaries_unchanged'] and all(v==[] for v in na['new_scoped_temp_residue'].values())
for c in raw['cases']:assert not Path(c['cleanup']['CleanupOwnedRoot']).exists()
assert {c['nonzeroProgression'] for c in raw['cases']}=={False,True} and all(c['manifestSnapshotHashesVerified']==66 and c['cleanupPathsVerified']==142 and c['inertRepeatImages']==84 and c['storyReconstructedBytes']==691 for c in raw['cases'])
assert all(b['sourceOriginalEnvelopeAndRollbackHashesVerified']==66 for b in bindings)
# Historical semantic acceptance is reused with original compilation and execution identity; bytes are revalidated.
hp=[]
for row in source['audit_continuity']['historical_evidence_pins_reverified']:
 q=pin(row['path']);assert q['sha256']==row['sha256'] and q['blob']==row['blob'];hp.append(q)
retained=source['retained_original_provenance'];rch=[]
for old in retained:
 prefix=old['prefix'];idx=json.loads((D/(prefix+'-artifact-index.json')).read_text());n=json.loads((D/(prefix+'-native-exit.json')).read_text());pl=json.loads((D/(prefix+'-plan.json')).read_text());assert idx['source_sha']==old['actual'] and n['actual_head']==old['actual'] and n['compiled_source']==old['compiled_source'] and n['native_exit']==0 and idx['summary']['Tests']==old['tests']
 cat=idx['summary']['Categories'][0];oldD=pl['Descriptors'];ds=[d for d in expected if d['Category']==cat];assert [{k:v for k,v in d.items() if k!='Name'} for d in oldD]==[{k:v for k,v in d.items() if k!='Name'} for d in ds]
 for row in idx['artifacts']:
  p=D/row['path'];b=p.read_bytes();v=gzip.decompress(b) if p.suffix=='.gz' else b;local=Path(row['raw_path']);local=local if local.is_absolute() else R/local
  assert len(v)==row['raw_bytes'] and h(v)==row['raw_sha256'] and h(b)==row['stored_sha256'] and v==local.read_bytes();q=pin(str(p.relative_to(R)));rch.append({**row,'blob':q['blob'],'original_compiled_source':old['compiled_source'],'original_actual':old['actual']})
assert len(rch)==24 and sum(p['tests']['Passed'] for p in retained)==46
order=[]
for cat,count in [('fr063-observed-original-default',67),('portable-ordinary-readers',16)]:
 ds=[d for d in expected if d['Category']==cat];assert sum(d['EstimatedCases'] for d in ds)==count;order.append({'category':cat,'cases':count,'descriptors':ds,'argv':['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category',cat,'-NoBuild']})
assert 2+46+sum(p['cases'] for p in order)==131
rp=T/'t179pd-observed-green-default-fixture-first-late-selected-recovery-20261016.json';re=json.loads(rp.read_text());rr=Path(re['directory']);assert re['sha']==C and re['count']==len(re['files'])==923
assert g('rev-parse','HEAD',cwd=rr).decode().strip()==C and not g('status','--porcelain',cwd=rr);gd=Path(g('rev-parse','--absolute-git-dir',cwd=rr).decode().strip());assert not (gd/'objects/info/alternates').exists()
for row in re['files']:
 b=(rr/row['path']).read_bytes();assert len(b)==row['bytes'] and h(b)==row['sha256'];assert b==g('show',C+':'+row['path'])==g('show',C+':'+row['path'],cwd=rr)
historical=json.loads((D/'t179pd-observed-green-default-fixture-source-accepted-selected-recovery-20261016.json').read_text());assert historical['sha']==A and historical['count']==len(historical['files'])==909
for row in historical['files']:
 b=g('show',A+':'+row['path']);assert len(b)==row['bytes'] and h(b)==row['sha256']
outputs=[]
for n in ['t179pd-inspect-observed-green-default-fixture-late-independent.py','t179pd-inspect-observed-green-default-fixture-late-binding-independent.py','t179pd-observed-green-default-fixture-late-independent-inspection.json','t179pd-observed-green-default-fixture-late-independent-bindings.json']:
 p=T/n;outputs.append({'path':str(p),'bytes':p.stat().st_size,'sha256':h(p.read_bytes())})
out={'compiled_source':S,'prep_carrier':PREP,'actual':A,'carrier':C,'observed_head':C,'clean':True,'source_continuity_diffs':diffs,'source_pins':sourcepins,'source_projections':projections,'evidence_pins':ep,'native_receipt':na,'binaries':bi,'all_four_current_dlls_verified':True,'prior_source_report_immutable':True,'unchanged_late_fixture_from_9aa':True,'exact_executed_descriptor_matches_source_release':True,'trx_counters_all_expected':True,'semantic_outputs':outputs,'root_extra_chains':extraChains,'ordered_conditional_release':order,'retained_original_pass_packets':retained,'historical_pins':hp,'retained_24_raw_chains':rch,'previous_failed_packets':source['audit_continuity']['prior_failures_retained'],'recovery':{'receipt':str(rp),'sha256':h(rp.read_bytes()),'revision':C,'count_verified':923,'file_pins':re['files'],'clean':True,'no_alternates':True,'transport_attestation':re['source'],'remote_readback_receipt':re['remoteReadback'],'network_repeated_by_reviewer':False,'restored_runtime':'NOT_RUN'},'historical_source_accepted_restore':{'revision':A,'file_count':909,'file_hashes_verified_against_git':True,'live_clone_not_claimed_at_this_historical_revision':True}}
p=T/'t179pd-observed-green-default-fixture-late-continuity-independent.json';assert not p.exists();p.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps({'output':str(p),'sha256':h(p.read_bytes()),'source_pins':len(sourcepins),'evidence_pins':len(ep),'historical_pins':len(hp),'retained_raw_chains':len(rch),'recovery':923,'ordered_cases':[o['cases'] for o in order],'all_checks_passed':True},indent=2))
