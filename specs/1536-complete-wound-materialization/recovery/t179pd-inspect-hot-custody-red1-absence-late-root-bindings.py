from pathlib import Path
import json,gzip,base64,hashlib,xml.etree.ElementTree as E
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery';P='t179pd-hot-custody-red1-absence-first-late-20261016'
def b(v):return None if v is None else base64.b64decode(v,validate=True)
def j(v):return json.loads(b(v).decode('utf-8-sig'))
def h(raw):return hashlib.sha256(raw).hexdigest().upper()
x=E.fromstring(gzip.decompress(next(D.glob(P+'-*.trx.gz')).read_bytes()));ns={'t':x.tag.split('}')[0][1:]};out=[]
for c in x.findall('.//t:UnitTestResult',ns):
 rows=[]
 for line in c.findtext('t:Output/t:StdOut',namespaces=ns).splitlines():
  try:v=json.loads(line)
  except ValueError:continue
  if isinstance(v,dict):rows.append(v)
 def one(k):
  found=[v['Observation'] for v in rows if v.get('Boundary')==k];assert len(found)==1;return found[0]
 p=one('fr063_joint_completion_actual_prerequisites');u=one('fr063_joint_complete_union');o=one('fr063_joint_completion_actual_owner_outcome');a=one('fr063_joint_progression_phase_report_arithmetic')
 raw=p['FullRequest'];co=raw['coordinates'];command=p['Command'];mi=o['Milestones'][0]['Images'];fi=o['Milestones'][2]['Images'];original=p['Original']
 for lower,upper in [('sessionId','SessionId'),('requestId','RequestId'),('snapshotToken','SnapshotToken')]:assert co[lower]==command[lower]==original[upper]
 assert co['sessionGeneration']==p['generation'] and co['turn']==43 and co['realm']==original['Realm']=='mortal_world'
 assert len(command['commands'])==1;cmd=command['commands'][0]
 assert cmd['kind']=='accepted_transition' and cmd['transitionKind']=='treat' and cmd['authority']['request']==raw
 assert raw['requestFingerprint']==u['RequestFingerprint']==u['Treat']['SourceFingerprint']
 assert u['Treat']['TreatmentResult']['requestAuthority']==raw
 pending=j(mi['game_state/control/pending_wound_resolutions.json']); submitted=pending['submittedTreatmentRequests']; assert len(submitted)==1
 assert submitted[0]['request']==raw and submitted[0]['requestFingerprint']==raw['requestFingerprint']
 assert submitted[0]['operationKey']==co['operationKey']==cmd['operationKey']==u['Treat']['OperationKey']
 assert submitted[0]['attemptId']==co['attemptId']==u['Treat']['AttemptId']
 assert co['woundId']==u['Treat']['WoundId']
 manifest=j(mi['game_state/control/pending_turn_snapshot.json']);auth=j(mi['game_state/control/pending_turn_snapshot.authority.json']);payload=b(auth['payloadJsonBase64']); assert h(payload)==auth['payloadSha256']
 ap=json.loads(payload)
 assert ap['sessionId']==original['SessionId'] and ap['requestId']==original['RequestId'] and ap['turnNumber']==43
 assert ap['manifestPayloadHash']==manifest['manifestPayloadHash']==original['SnapshotToken']
 for field in ['files','snapshotFileHashes','clientOwnedValidationHashes','rollbackBackups','rollbackBaselineFiles','sourceLabel']:assert ap[field]==manifest[field]
 for target,path in ap['rollbackBackups'].items():assert h(b(mi[path]))==ap['rollbackBackupHashes'][target]
 binding=u['Marker']['outputBinding']; assert binding['woundId']==u['Create']['WoundId'] and binding['transitionId']==u['Create']['TransitionId'] and binding['localWoundRef']==p['Draft']['woundRef']
 assert u['ExactStorySuffix']['narrative']==j(binding['narrativeBase64'])['response']==p['Scene']
 history=j(fi['game_state/wounds/wound_history.json']); entries=history.get('transitions',history.get('entries'));treat=next(e for e in entries if e['transitionId']==u['Treat']['TransitionId'])
 assert treat['transitionResult']['requestAuthority']==raw
 baseline=[]
 if o['ReportRemovalHistory']:
  tx=o['ReportRemovalHistory'][0];assert tx['Role']=='original_baseline_restore' and len(tx['Members'])==8
  for member in tx['Members']:
   path=member['Path'].split('/game_session/',1)[1]
   if path in manifest['files']:
    assert member['After']['Bytes']==mi[manifest['files'][path]]
    baseline.append(path)
   elif path=='game_state/control/pending_wound_resolutions.json':
    after=j(member['After']['Bytes']);assert after['submittedTreatmentRequests']==submitted
   else:assert path in ['game_state/control/progression_report.json','output/mortal_wound_decisions.json'] and member['After']['Bytes'] is None
  assert len(baseline)==5
 ctrl=a['ActualOriginalControl'];before=j(a['SignedOriginalScheduleBefore']);after=j(a['ExactScheduleAfter'])
 out.append({'test':c.get('testName'),'treatmentRequestFullValueEquality':True,'sourceOriginalEnvelopeAndRollbackHashesVerified':len(ap['rollbackBackups']),'outputCreateBinding':True,'nonzeroBaselineRestoredOriginalPaths':baseline,'requestFingerprint':raw['requestFingerprint'],'originalTuple':original,'generation':p['generation'],'progressionArithmetic':{'nonzero':p['nonzeroProgression'],'originalSchedule':before,'actualControl':ctrl,'afterSchedule':after}})
p=Path('/tmp/t179pd-hot-custody-red1-absence-late-root-bindings.json');p.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'cases':len(out),'fullBindingChecksPassed':True,'backupHashesEach':[a['sourceOriginalEnvelopeAndRollbackHashesVerified'] for a in out]}))
