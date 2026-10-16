import os, subprocess, pathlib, hashlib, json, gzip, base64, xml.etree.ElementTree as ET
os.environ.update(GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
REPO=pathlib.Path('/workspace/The-Book-of-Eternity-Reborn')
SOURCE='e182b6745817bd34ff5247c34f8d469bb71754f1'; CARRIER='e0ed69e2b96f41437e8465ae41182b111dfc1150'
ROOT='specs/1536-complete-wound-materialization/recovery/'
STEM='t179pd-observed-green-default-fixture-first-late-20261016'
OUT=pathlib.Path('/tmp/t179pd-hot-custody-red1-optional-absence-inspection-20261016.json')
def sha(b):return hashlib.sha256(b).hexdigest()
def git(*a):return subprocess.check_output(['git',*a],cwd=REPO)
def show(c,p):return git('show',c+':'+p)
def pin(c,p):
 b=show(c,p);return {'commit':c,'path':p,'git_blob':git('rev-parse',c+':'+p).decode().strip(),'bytes':len(b),'sha256':sha(b)}
def projection(c,p,a,b):
 lines=show(c,p).splitlines(keepends=True);raw=b''.join(lines[a-1:b]);return {'source':pin(c,p),'first_line':a,'last_line':b,'bytes':len(raw),'sha256':sha(raw),'text':raw.decode()}
reader='BookOfEternityClient/Services/PendingTurnSnapshotReader.cs'
test='BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.HotSubmissionCustody.cs'
design=ROOT+'t179pd-hot-submitted-admission-engineering-design-20261016.md'
projections=[projection(SOURCE,test,20,59),projection(SOURCE,test,196,208),projection(SOURCE,reader,8,50),projection(SOURCE,reader,152,166),projection(SOURCE,reader,452,472),projection(SOURCE,reader,475,516),projection(SOURCE,reader,641,704),projection(SOURCE,reader,1055,1104),projection(SOURCE,design,110,120),projection(SOURCE,design,152,162)]
index_path=ROOT+STEM+'-artifact-index.json';index=json.loads(show(CARRIER,index_path))
entry=next(e for e in index['artifacts'] if e['path'].endswith('.trx.gz'))
trx_path=ROOT+entry['path'];stored=show(CARRIER,trx_path);raw=gzip.decompress(stored)
assert sha(stored)==entry['stored_sha256'];assert sha(raw)==entry['raw_sha256'];assert len(raw)==entry['raw_bytes']
local=pathlib.Path(entry['raw_path']);assert local.read_bytes()==raw
historical='eac4e7e0dd695770d86f776d89f90ae9a2d7f822';assert show(historical,trx_path)==stored
x=ET.fromstring(raw);ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
path='game_state/player/status_changes.json'
input_paths=['game_state/player/status_changes.json','game_state/player/experience.json','game_state/core/player_status.json','game_state/meta/soul_state.json','game_state/misc/characteristics.json','game_state/player/stat_points.json']
cases=[]
for case in x.findall('.//t:UnitTestResult',ns):
 events=[]
 for out in case.findall('.//t:StdOut',ns):
  for line in (out.text or '').splitlines():
   if not line.startswith('{'):continue
   try:events.append(json.loads(line))
   except json.JSONDecodeError:continue
 inventory=[e['Observation'] for e in events if e.get('Boundary')=='fr063_joint_original_inventory_structural_absence_before_assert' and e['Observation']['Path']==path]
 assert len(inventory)==1
 assert all(inventory[0][k] is False for k in ['OriginalCovered','InManifestFiles','InSnapshotFileHashes','InRollbackBackups','InRollbackBaselineFiles'])
 owner=[e['Observation'] for e in events if e.get('Boundary')=='fr063_joint_completion_actual_owner_outcome'];assert len(owner)==1
 manifests=[]
 for mi,m in enumerate(owner[0]['Milestones']):
  key='game_state/control/pending_turn_snapshot.json'
  if key not in m['Images']:continue
  encoded=m['Images'][key];b=base64.b64decode(encoded,validate=True);j=json.loads(b.decode('utf-8-sig'))
  presence=j['originalPathPresenceV1'];assert len(presence)==22 and path not in presence
  for name in ['files','snapshotFileHashes','rollbackBackups','rollbackBaselineFiles']:
   assert path not in j.get(name,{})
  optional=[p for p in input_paths if p!='game_state/meta/soul_state.json']
  assert all(p not in presence for p in optional)
  manifests.append({'milestone_ordinal':mi,'milestone_path':m['Path'],'image_path':key,'bytes':len(b),'sha256':sha(b),'sessionId':j['sessionId'],'requestId':j['requestId'],'turnNumber':j['turnNumber'],'presence_keys':sorted(presence),'presence_count':len(presence),'status_presence_key':False,'input_coverage':{p:{'in_files':p in j['files'],'in_hashes':p in j['snapshotFileHashes'],'in_presence':p in presence,'presence_value':presence.get(p)} for p in input_paths}})
 assert len(manifests)==3 and len({m['sha256'] for m in manifests})==1
 cases.append({'test_name':case.attrib['testName'],'test_id':case.attrib['testId'],'execution_id':case.attrib['executionId'],'outcome':case.attrib['outcome'],'structural_inventory':inventory[0],'manifest_observations':manifests})
assert len(cases)==2 and all(c['outcome']=='Passed' for c in cases)
prior={}
for ext,expected in [('md','18886ecd532776f9658c3f77c0c05863ebbc3606161265bc3d785d3398155c31'),('json','8389138cb51f6b644ab7e120f4704c93d3beff5ad0ea12cfbcfbdd324735fc37')]:
 p=pathlib.Path('/tmp/t179pd-hot-custody-red1-source-independent-review-20261016.'+ext);b=p.read_bytes();assert sha(b)==expected;prior[ext]={'path':str(p),'bytes':len(b),'sha256':sha(b),'unchanged':True}
assert show(SOURCE,reader)==show(CARRIER,reader)
assert show(SOURCE,test)==show(CARRIER,test)
result={'schema':'independent-read-only-source-addendum-inspection/v1','source':SOURCE,'carrier':CARRIER,'historical_evidence_carrier':historical,'observed_head_only':git('rev-parse','HEAD').decode().strip(),'review_scope':'ROOT-HS01-F02 only; no current worktree correction acceptance','source_pins':[pin(SOURCE,p) for p in [test,reader,design,'BookOfEternityClient/Services/LiveTurnPreparationService.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairMechanicalAdmission.cs']],'projections':projections,'historical_index_pin':pin(CARRIER,index_path),'historical_trx_pin':pin(CARRIER,trx_path),'chain':{'index_entry':entry,'stored_matches_git_and_index':True,'raw_matches_local_and_index':True,'historical_carrier_bytes_unchanged':True,'raw_sha256':sha(raw),'raw_bytes':len(raw)},'historical_cases':cases,'prior_reports':prior,'first_failure':{'test_line':42,'reader_lines':[152,164,452,467],'issue_code':'pending_turn_snapshot_reader_required_paths_invalid','issue_actual_prefix':'observed optional path is outside originalPathPresenceV1: ','offending_optional_paths':[p for p in input_paths if p!='game_state/meta/soul_state.json'],'issue_count_from_static_control_flow':5,'before_manifest_read':True,'later_absence_unproven_branch_not_reached':True},'secondary_wrong_assertion':{'test_line':55,'assertion':'Assert.Contains(path, original.AbsentLogicalPaths)','reason':'Ordinary optional uncovered path grants neither bytes nor signed absence.'},'runtime_performed':False,'repository_mutated':False}
with OUT.open('x') as f:json.dump(result,f,indent=2);f.write('\n')
print(json.dumps({'inspection':str(OUT),'sha256':sha(OUT.read_bytes()),'source_pins':len(result['source_pins']),'projections':len(projections),'historical_cases':len(cases),'manifest_observations':sum(len(c['manifest_observations']) for c in cases),'first_failure':result['first_failure'],'prior_reports_unchanged':True},indent=2))
