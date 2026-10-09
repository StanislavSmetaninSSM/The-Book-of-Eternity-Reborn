from pathlib import Path
import json,base64,hashlib,struct,subprocess,sys,xml.etree.ElementTree as E
prefix,sha=sys.argv[1:];run,=Path('TestResults/test-categories').glob(prefix+'*');s=json.loads((run/'summary.json').read_text())
assert s['Revision']['Head']==sha and s['Revision']['ChangedFiles']==0
assert s['Tests']=={'Total':15,'Executed':15,'Passed':15,'Failed':0}
assert s['Selection']['Complete'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['TimedOut'] and not s['DuplicateTests']
n={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};rows=[];removed=[];assets=0;acks=0
cuts={'inbox_unknown','inbox_unknown_dispose','derived_audit_unknown','terminal_audit_unknown','timeout_audit_unknown','required_audit_unknown'}
early={'terminal_audit_unknown','timeout_audit_unknown','cancelled','known_failure','terminal_pending_reaper'}
digest=lambda b:hashlib.sha256(b).hexdigest()
b64=lambda b:base64.b64decode(b)
for trx in run.glob('*.trx'):
 for test in E.parse(trx).findall('.//t:UnitTestResult',n):
  assert test.get('outcome')=='Passed'
  for line in test.findtext('t:Output/t:StdOut',default='',namespaces=n).splitlines():
   try:d=json.loads(line)
   except:continue
   if 'CleanupOwnedRoot' in d:
    assert d['OwnedFixtureRemoved'] and d['CleanupFailure'] is None and not Path(d['CleanupOwnedRoot']).exists();removed.append(d['CleanupOwnedRoot'])
   if 'Scenario' not in d:continue
   sc=d['Scenario'];w=sc['WorkerStorage'];c=w['Cut'];mode=w['mode'];g=d['Guardian']
   assert sc['Success'] and 'Failure' not in sc and 'FixtureCleanupFailure' not in sc
   assert g['echild'] and g['driverExitCode']==0 and not g['deadline'] and not g['failures'] and not g['emergencySignals']
   assert w['DispatchSettled'] and w['physicalSettled'] and w['ownerOutputTasksSettled'] and w['OutputsSettled'] and not w['IsUncertain']
   assert w['releases']==1 and w['originalWorkspaceRetained'] and w['actualStop']['State']==0
   assert w['workerStarts']==(0 if mode in early else 1)
   assert w['originalCompletionSettled']==(None if mode in early else True)
   record=json.loads(b64(w['retiredBytes']).decode('utf-8-sig')) if w['retiredBytes'] else None
   if record:
    assert w['recordSource']=='retired' and w['retiredReference']['RecordSha256']==digest(b64(w['retiredBytes']))
    ri=dict(record['Identity']); assert ri.pop('Backend')=='LinuxNativeLineage' and ri.pop('Scope')=='OrdinarySamePidNamespace'
    wi=dict(w['Identity']); assert wi.pop('Backend')==2 and wi.pop('Scope')==2 and ri==wi
   else:assert w['recordSource']=='active'
   if w['publicationAcknowledged']:
    acks+=1;bundle=w['bundleAfter'];pb=b64(bundle['proposal.json']);p=json.loads(pb.decode('utf-8-sig'))
    assert p['taskId']==w['task']['TaskId'] and p['workerId']==w['task']['WorkerId']
    assert w['Progress']['Publication']['ProposalSha256']==digest(pb) and len(p['changedFiles'])==1
    member=p['changedFiles'][0]
    assert b64(bundle[member['path']])==b'{"fixture":"proposed-content-only"}\n'
   if mode in cuts:
    assert c['Cuts']==1 and c['Index']==0 and c['ActualUncertainty'];j=b64(c['JournalAtCut'])
    assert j==b64(c['RetainedJournal']) and digest(j).upper()==c['JournalSha256']
    md=json.loads(j[16:16+struct.unpack('<q',j[8:16])[0]] if j.startswith(b'BOELP2\r\n') else j)
    assert not md['Committed'] and md['Members'][0]['Path']==c['Target']
    assert digest(b64(c['PublishedBytes']))==md['Members'][0]['After']['Sha256'].lower()
    assert b64(c['RetainedTarget'])==b'foreign-worker-publication-image' and c['PriorAtCut']==c['AfterImages']
    assert not c['LaterReads'] and not c['LaterMutations'] and c['LaterLeases']==c['LaterRecovery']==c['LaterPublications']==0 and not c['PendingClosingLease']
    f=w['factsAtCut'];reserved=json.loads(b64(f['taskBytes']).decode('utf-8-sig'))
    assert f['actualGeneration']==w['generation']==reserved['sessionGeneration']
    assert f['task']['TaskId']==w['task']['TaskId'] and f['Identity']['TaskSha256']==digest(b64(f['taskBytes']))
    assert f['stop']['State']==0 and f['OutputsSettled']
    assert f['bundleAtCut']==w['bundleAfter'] and f['workspaceAtCut']==w['workspaceAfter']
    assert w['SameRetainedUncertainty'] and w['slotHeld'] and w['EntryCount']==w['OwnedCapacity']==1 and not w['RetirementAcknowledged'] and w['rootLeaseActive']
    assert w['receiptBytes'] is None and not w['acceptedAfter']
    if f['cleanupOwnerExists']:assert w['sameCleanupOwner']
    if mode=='required_audit_unknown':
     assert w['ResultReturned'] and w['Failure'] is None and w['acceptedBeforeReaper'] and not w['firstReaperAccepted']
     assert w['originalPublicationRecorded'] and w['publicationAcknowledged'] and not w['workspaceExists']
    else:assert w['SameOriginalUncertainty'] and not w['ResultReturned']
    if mode.startswith('inbox_unknown') or mode=='derived_audit_unknown':
     assert f['publicationAcknowledged'] and f['Progress']['Publication']['Committed'] and w['publicationAcknowledged'] and not w['originalPublicationRecorded'] and w['workspaceCalls']==0 and w['workspaceExists']
    if mode in early:assert w['workspaceCalls']==0 and w['workspaceExists']
    if mode=='terminal_audit_unknown':assert w['OriginalCauseRetained']
    if mode=='timeout_audit_unknown':assert w['TimeoutFactRetained']=='TimedOut'
    if mode=='inbox_unknown_dispose':assert w['disposeAttempts']>=2 and w['CleanupFailureRetained']
   else:
    assert c['Cuts']==0 and c['ActualUncertainty'] is None and c['RetainedJournal'] is None
    assert not w['slotHeld'] and w['EntryCount']==w['OwnedCapacity']==0 and w['RetirementAcknowledged'] and not w['rootLeaseActive']
    if mode=='cancelled':assert 'OperationCanceledException' in w['Failure'] and not w['ResultReturned']
    elif mode in {'known_failure','terminal_pending_reaper'}:assert w['Failure'] is None and w['Status']=='Failed' and w['knownHits']==1
    else:assert w['Failure'] is None and w['acceptedAfter'] and w['publicationAcknowledged'] and w['originalPublicationRecorded']
    if mode in {'inbox_known','audit_known'}:assert w['knownHits']>0
    if mode=='required_audit_unavailable':assert w['receiptBytes'] and w['receiptPath'] and w['knownHits']==1
    if mode in {'cleanup_audit_refused','reaper_audit_refused'}:
     assert len(w['RefusalWitnesses'])==(2 if mode=='cleanup_audit_refused' else 3)
     ev=[json.loads(x) for x in b64(w['AuditAfter']).decode('utf-8-sig').splitlines()]
     assert sum(e['eventType']=='process-tree-cleanup-confirmed' for e in ev)==1
     assert all(e['eventType'] not in {'process-tree-cleanup-unconfirmed','process-tree-cleanup-retry-failed','workspace-cleanup-deferred'} for e in ev)
    if mode=='terminal_pending_reaper':
     p=sc['PendingPhase'];assert p['originalRunIncomplete'] and p['physicalSettled'] and p['disposeAttempts']==2
     assert p['beforeWorkspace']==p['afterWorkspace'] and p['beforeWorkspace'] and p['beforeAudit']==p['afterAudit'] and p['workspaceExists'] and p['workspaceCalls']==0
     assert not p['RetirementAcknowledged'] and p['slotHeld'] and p['rootLeaseActive'] and p['EntryCount']==p['OwnedCapacity']==1
   folder=Path(d['FixtureFolder']);p=json.loads((folder/'build-provenance.json').read_text());assert len(p['assets'])==2
   for a in p['assets']:
    assert digest(subprocess.check_output(['git','show',sha+':'+a['source']]))==a['sourceSha256']
    assert digest((folder/a['binary']).read_bytes())==a['binarySha256'];assets+=1
   rows.append({'mode':mode,'cut':c['Cuts'],'sameOriginalCSP':w['SameOriginalUncertainty'],'retainedCSP':w['SameRetainedUncertainty'],'actualACK':w['publicationAcknowledged'],'recordedPublication':w['originalPublicationRecorded'],'retired':w['RetirementAcknowledged'],'physicalSettled':w['physicalSettled'],'acceptedBefore':w['acceptedBeforeReaper'],'acceptedAfter':w['acceptedAfter'],'rootLeaseActive':w['rootLeaseActive']})
assert len(rows)==len(removed)==15 and len({r['mode'] for r in rows})==15 and assets==30 and acks==10
receipt={'source':sha,'actual':run.name,'tests':s['Tests'],'rows':rows,'removedRoots':removed,'nativeSourceBinaryPairs':assets,'actualAcknowledgedBundles':acks,'scope':'6 authentic canonical cuts, 9 original controls, actual pending-terminal reaper phase, distinct ACK/publication/acceptance,15 physical/guardian/root cleanups. No native Windows/full Bridge or actual secondary lease-close guarantee.'}
Path('/tmp/boe-1553-durable-root-green.json').write_text(json.dumps(receipt,indent=2)+'\n')
print('Verified15 PASS /6 authentic cuts /9 controls /10 actual acknowledged bundles /15 original physical+guardian+root cleanups /30 native source-binary pairs')
