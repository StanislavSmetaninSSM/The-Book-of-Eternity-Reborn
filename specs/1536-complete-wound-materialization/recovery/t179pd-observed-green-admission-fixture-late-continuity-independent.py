from pathlib import Path
import subprocess,json,hashlib,gzip
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery';T=Path('/tmp')
S='d50d1a9a98e087c8fc5a8908745dfd1e03aaac48';PREP='ec50e4cd6c6e17ab651711eaf0c9cbc957dd8842';A='befe8ae9d6046bad5701d00806ac3eee6f60bfec';C='a85a726229e74465f986af69ca00a8e0225c0dea';P='t179pd-observed-green-admission-fixture-first-late-20261016'
h=lambda b:hashlib.sha256(b).hexdigest()
def g(*a,cwd=R):return subprocess.check_output(['git',*a],cwd=cwd)
def pin(p,rev=C):
 b=g('show',rev+':'+p);assert b==(R/p).read_bytes();return {'path':p,'revision':rev,'blob':g('rev-parse',rev+':'+p).decode().strip(),'bytes':len(b),'sha256':h(b)}
H='e6796dc3b5454cceb1591ccc634b54dbb1021bd6'
assert g('rev-parse','HEAD').decode().strip()==H and not g('status','--porcelain')
scope=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityClient.TestSupport','BookOfEternityGMBridge','tests','scripts'];diffs=[]
for name,a,b in [('compiled-to-actual',S,A),('prep-to-actual',PREP,A),('actual-to-carrier',A,C),('compiled-to-carrier',S,C),('carrier-to-observed-head',C,H)]:
 argv=['git','diff','--no-ext-diff','--binary','--full-index',a,b,'--',*scope];raw=subprocess.check_output(argv,cwd=R);assert not raw;g('merge-base','--is-ancestor',a,b);p=T/('t179pd-observed-green-admission-fixture-late-'+name+'.diff');assert not p.exists();p.write_bytes(raw);diffs.append({'argv':argv,'path':str(p),'bytes':len(raw),'sha256':h(raw)})
source=json.loads((D/'t179pd-observed-green-admission-fixture-source-review-20261016.json').read_text());assert source['verdict']=='ACCEPTED_SOURCE_FIXTURE_SELECTION_PREPARATION_FIRST_PROTECTED_LATE2_ONLY';sv=source['inspection']
raw=json.loads((T/'t179pd-observed-green-admission-fixture-late-independent-inspection.json').read_text());bindings=json.loads((T/'t179pd-observed-green-admission-fixture-late-independent-bindings.json').read_text())
ct=raw['trxCounters'];assert ct['total']==ct['executed']==ct['passed']=='2' and all(v=='0' for k,v in ct.items() if k not in ['total','executed','passed'])
expected=sv['selection']['descriptors'];assert raw['plan']['Descriptors'][0]==next(d for d in expected if d['Category']=='fr063-joint-late-completion')
assert raw['summary']['Categories']==['fr063-joint-late-completion'] and raw['summary']['CategoryResults'][0]['Seconds']<300 and not raw['summary']['DiscoveryOnly'] and not raw['summary']['PlanOnly']
sourcepins=[]
for row in sv['source_pins']:
 b=g('show',row['revision']+':'+row['path']);assert h(b)==row['sha256'] and g('rev-parse',row['revision']+':'+row['path']).decode().strip()==row['blob']
 if not row['path'].startswith('specs/'):
  q=pin(row['path'],S);assert q['sha256']==row['sha256'];sourcepins.append(q)
for p in ['BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.QteStoryAppend.cs','BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs','BookOfEternityClient/Services/ProgressionScheduleService.MortalJointBasis.cs','BookOfEternityClient/Services/StoryService.MortalJointRepair.cs']:
 sourcepins.append(pin(p,S))
late='BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs'
assert g('show','9aa339141ec8ebec301b18679aa021d018250b0f:'+late)==g('show',S+':'+late)
ep=[]
for row in sv['evidence_pins']:
 q=pin(row['path']);assert q['sha256']==row['sha256'];ep.append(q)
for n,sha in [('t179pd-observed-green-admission-fixture-source-review-20261016.md','44fe41a4d9ba084c7ca64c99843909bf47be0c3619fdd44f36a0e04cce8724eb'),('t179pd-observed-green-admission-fixture-source-review-20261016.json','e824061d1bf500c49e5220ff762497b783db3922df2b9ef3f9ceedecbd7131bc')]:
 p=D/n;assert h(p.read_bytes())==sha and p.read_bytes()==(T/n).read_bytes();ep.append(pin(str(p.relative_to(R))))
for p in sorted(D.glob(P+'*')):ep.append(pin(str(p.relative_to(R))))
bi=sv['binaries'];assert bi['source_sha']==S
for row in bi['binaries']:assert h((R/row['path']).read_bytes())==row['sha256']
na=json.loads((D/(P+'-native-exit.json')).read_text());assert na['argv']==source['release']['argv'] and na['native_exit']==0 and na['actual_head']==A and na['compiled_source']==S
assert na['post_git_clean'] and na['post_head_unchanged'] and na['binaries_unchanged'] and all(v==[] for v in na['new_scoped_temp_residue'].values())
for case in raw['cases']:assert not Path(case['cleanup']['CleanupOwnedRoot']).exists()
stdout=gzip.decompress((D/(P+'-stdout.log.gz')).read_bytes()).decode();assert 'Failed:     0' in stdout or 'Passed:' in stdout
order=['fr063-observed-original-conformance','fr063-observed-original-browser','fr063-observed-original-default','portable-ordinary-readers'];counts=[35,8,67,16];released=[]
for cat,count in zip(order,counts):
 ds=[d for d in expected if d['Category']==cat];assert sum(d['EstimatedCases'] for d in ds)==count;released.append({'category':cat,'cases':count,'descriptors':ds,'argv':['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category',cat,'-NoBuild']})
assert sum(r['cases'] for r in released)==126 and len(expected)==8
# Revalidate immutable historical reader/barrier PASS and unexpected conformance packet bytes;
# retain earlier independently parsed semantic evidence with original e522 execution provenance.
hist=sv['history']['inspection'];hp=[]
for row in hist['evidence_pins']:
 q=pin(row['path']);assert q['sha256']==row['sha256'];hp.append(q)
retained=[]
for packet in hist['packets']:
 for row in packet['chains']:
  p=D/row['path'];b=p.read_bytes();v=gzip.decompress(b) if p.suffix=='.gz' else b
  assert h(b)==row['stored_sha256'] and len(v)==row['raw_bytes'] and h(v)==row['raw_sha256'] and v==Path(row['raw_path']).read_bytes()
 if packet['category']=='fr063-observed-original-conformance':
  assert packet['summary']['Tests']==dict(Total=35,Executed=35,Passed=34,Failed=1);continue
 assert packet['summary']['ExitCode']==0 and packet['summary']['Tests']['Failed']==0
 assert packet['native']['compiled_source']=='e522102735317eedc04e42ea1e0b892f73983871'
 ds=next(d for d in expected if d['Category']==packet['category']);old=packet['exact_preparation_descriptor'];assert {k:v for k,v in ds.items() if k!='Name'}=={k:v for k,v in old.items() if k!='Name'}
 for c in packet['cases']:
  assert c['outcome']=='Passed'
  for q in c['diagnostics']:
   if 'CleanupOwnedRoot' in q:assert not Path(q['CleanupOwnedRoot']).exists()
 retained.append(packet)
assert sum(p['summary']['Tests']['Passed'] for p in retained)==3
rp=T/'t179pd-observed-green-admission-fixture-late-selected-recovery-20261016.json';re=json.loads(rp.read_text());rr=Path(re['directory']);assert re['sha']==C and re['count']==len(re['files'])==804
assert g('rev-parse','HEAD',cwd=rr).decode().strip()==C and not g('status','--porcelain',cwd=rr);gd=Path(g('rev-parse','--absolute-git-dir',cwd=rr).decode().strip());assert not (gd/'objects/info/alternates').exists()
for row in re['files']:
 b=(rr/row['path']).read_bytes();assert len(b)==row['bytes'] and h(b)==row['sha256'];assert b==g('show',C+':'+row['path'])==g('show',C+':'+row['path'],cwd=rr)
outputs=[]
for n in ['t179pd-inspect-observed-green-admission-fixture-late-independent.py','t179pd-inspect-observed-green-admission-fixture-late-binding-independent.py','t179pd-observed-green-admission-fixture-late-independent-inspection.json','t179pd-observed-green-admission-fixture-late-independent-bindings.json']:
 p=T/n;outputs.append({'path':str(p),'bytes':p.stat().st_size,'sha256':h(p.read_bytes())})
out={'compiled_source':S,'prep_carrier':PREP,'actual':A,'carrier':C,'observed_head':H,'clean':True,'source_continuity_diffs':diffs,'source_pins':sourcepins,'evidence_pins':ep,'native_receipt':na,'binaries':bi,'all_four_current_dlls_verified':True,'prior_source_report_immutable':True,'unchanged_late_fixture_from_9aa':True,'exact_executed_descriptor_matches_source_release':True,'trx_counters_all_expected':True,'semantic_outputs':outputs,'source_release_order':released,'retained_historical_pass_packets':retained,'historical_pins':hp,'historical_18_chains_revalidated':True,'failed_conformance_packet_preserved':True,'recovery':{'receipt':str(rp),'sha256':h(rp.read_bytes()),'revision':C,'count_verified':804,'file_pins':re['files'],'clean':True,'no_alternates':True,'transport_attestation':re['source'],'remote_readback_receipt':re['remoteReadback'],'network_repeated_by_reviewer':False,'restored_runtime':'NOT_RUN'}}
p=T/'t179pd-observed-green-admission-fixture-late-continuity-independent.json';assert not p.exists();p.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps({'output':str(p),'sha256':h(p.read_bytes()),'source_pins':len(sourcepins),'evidence_pins':len(ep),'recovery':804,'ordered_cases':[r['cases'] for r in released],'all_checks_passed':True},indent=2))
