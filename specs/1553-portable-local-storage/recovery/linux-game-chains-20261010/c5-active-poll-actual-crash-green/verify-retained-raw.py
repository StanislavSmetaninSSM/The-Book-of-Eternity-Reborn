from pathlib import Path
import json,hashlib,base64,sys,zipfile,io
own=Path(sys.argv[1]); report=Path(sys.argv[2]); result=json.loads((own/'result.json').read_text()); ack=result['CutEvidence']; cold=result['ColdEvidence']; graph=json.loads((own/'active-poll-task-graph.json').read_text()); guard=json.loads((own/'guardian.json').read_text())
assert result['Success'] and result['OriginalExitCode']==137 and result['OriginalEOF'] and result['OriginalPinUnresolved']
for k in ['LogicalUncertainRetained','ExactEvidenceRetained','ExactCutEvidenceRetained','ColdNativeAdmissionRefused','PhysicalCleanup','ChildrenSettled','StopTaskSettled','ServerSettled','DisposeTaskSettled']:assert result[k],k
assert result['Failure'] is None and result['CleanupFailure'] is None
assert guard['echild'] and not guard['deadline'] and guard['driverExitCode']==guard['failures']==guard['emergencySignals']==0
for k in ['QualifiedBoundary','ActiveInspectionLease','ActualWaitTaskGateReachable','PhysicalHandleOpen','ActiveRegistrationSameLease','OriginalAdmissionShared','PinIdentityOnlyNotObservedClose']:assert ack[k],k
assert not ack['CloseObserved'] and not ack['Processing'] and not ack['Cleanup'] and ack['StoryEntries']==ack['ModelCalls']==0
assert ack['PendingPhase']=='staged' and ack['ActionId']==ack['RequestId']
assert ack['OriginalRun']==ack['PinIdentity']['Identity'] and ack['Generation']==ack['OriginalRun']['GenerationId']
assert graph['OriginalOperationTaskId']==ack['OriginalOperationTaskId'] and graph['ActualWaitTaskId']==ack['ActualWaitTaskId']
assert graph['PollGraph']['ForwardReachable'] or graph['PollGraph']['ReverseReachable']
assert graph['PollGraph']['Operation'] != graph['PollGraph']['Gate']
assert graph['WaitTaskField'] and '<WaitForTerminalSignalWithParticipationAsync>' in graph['WaiterState']
assert cold['OperationStarted'] and cold['NativeAdmissionRefused'] and cold['CanonicalOpens']==cold['RecoveryCallbacks']==cold['Mutations']==0
assert 'ClassifyBrowserRecoveryAsync' in cold['Failure'] and 'GmMainOperationClient.OpenAsync' in cold['Failure']
assert json.loads((own/'pin-before-kill.json').read_text())['State']==1
assert json.loads((own/'pin-after-eof.json').read_text())['State']==4
inventory=ack['Inventory']; assert inventory==result['BeforeCold']==result['AfterCold']
bytes_checked=0; zip_members=[]
for label in ['cut-all','pre-cold-all','post-cold-all']:
 assert json.loads((own/label/'inventory.json').read_text())==inventory
 for rel,h in inventory.items():
  data=(own/label/(rel+'.bin')).read_bytes();assert hashlib.sha256(data).hexdigest().upper()==h,(label,rel);bytes_checked+=1
  if rel.endswith('.zip'):
   z=zipfile.ZipFile(io.BytesIO(data)); assert z.testzip() is None;zip_members.append({'Label':label,'Path':rel,'Bytes':len(data),'Members':len(z.namelist()),'SHA256':h})
raw=lambda rel:(own/'cut-all'/(rel+'.bin')).read_bytes()
pending=json.loads(raw('input/pending_player_action.json')); manifest=json.loads(raw('game_state/control/pending_turn_snapshot.json')); envelope=json.loads(raw('game_state/control/pending_turn_snapshot.authority.json')); payload_bytes=base64.b64decode(envelope['payloadJsonBase64']);assert hashlib.sha256(payload_bytes).hexdigest().upper()==envelope['payloadSha256'];payload=json.loads(payload_bytes)
assert envelope['formatVersion']==4 and envelope['integrityAlgorithm']=='SHA256-PAYLOAD-JSON'
assert payload['requestId']==manifest['requestId']==ack['RequestId'] and payload['sessionId']==manifest['sessionId']==ack['SessionId'] and payload['turnNumber']==manifest['turnNumber']==ack['TurnNumber']
assert payload['manifestPayloadHash']==manifest['manifestPayloadHash'] and payload['snapshotHashMode']==payload['rollbackHashMode']=='bytes'
assert payload['files']==manifest['files'] and payload['snapshotFileHashes']==manifest['snapshotFileHashes'] and payload['rollbackBackups']==manifest['rollbackBackups']
for table,hashes in [('files','snapshotFileHashes'),('rollbackBackups','rollbackBackupHashes')]:
 for logical,rel in payload[table].items():assert hashlib.sha256(raw(rel)).hexdigest().upper()==payload[hashes][logical],(table,logical,rel)
request=json.loads(raw('input/turn_request.json'));assert request['requestId']==ack['RequestId'] and request['sessionId']==ack['SessionId'] and request['turnNumber']==ack['TurnNumber'];assert pending['status']=='staged'
proof_json=pending['phaseProof'];assert hashlib.sha256(proof_json.encode()).hexdigest().upper()==pending['phaseProofHash'];proof=json.loads(proof_json);assert proof['requestJson']==raw('input/turn_request.json').decode('utf-8-sig') and proof['manifestJson']==raw('game_state/control/pending_turn_snapshot.json').decode('utf-8-sig') and proof['authorityJson']==raw('game_state/control/pending_turn_snapshot.authority.json').decode('utf-8-sig')
assert {k.lower():v for k,v in manifest['browserOriginalMainCondition']['activeIdentity'].items()}=={k.lower():v for k,v in ack['OriginalRun'].items()}
assert not any(k in inventory for k in ['ready/turn_complete.json','ready/turn_error.json'])
report.write_text(json.dumps({'OwnedRoot':str(own),'Result':'PASS actual retained bytes/tuple/task/pin/cold/physical checks','CanonicalInventoryCount':len(inventory),'PhysicalByteCopiesVerified':bytes_checked,'Snapshots':len(payload['files']),'RollbackBackups':len(payload['rollbackBackups']),'ZIPs':zip_members,'ActualOriginalPID':ack['Pid'],'ColdPID':cold['Pid'],'OriginalRun':ack['OriginalRun'],'PinIdentity':ack['PinIdentity'],'Limits':'Raw comparison, no new runtime/test or authority issuance; original portable integrity, not external authenticity.'},indent=2)+'\n')
print('raw qualification PASS',len(inventory),bytes_checked,len(payload['files']),len(payload['rollbackBackups']))
