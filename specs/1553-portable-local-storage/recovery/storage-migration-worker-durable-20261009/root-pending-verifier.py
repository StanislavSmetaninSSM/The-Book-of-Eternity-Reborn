from pathlib import Path
import json,base64,hashlib,subprocess,xml.etree.ElementTree as E
sha='257075de765c788f17c8ba9e128b280dd8210f2d';run,=Path('TestResults/test-categories').glob('20261009-083207*');s=json.loads((run/'summary.json').read_text())
assert s['Revision']['Head']==sha and s['Revision']['ChangedFiles']==0 and s['Tests']=={'Total':1,'Executed':1,'Passed':0,'Failed':1}
assert s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and s['Selection']['Complete'] and not s['TimedOut'] and not s['DuplicateTests']
rows=[];clean=[];n={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
for trx in run.glob('*.trx'):
 for test in E.parse(trx).findall('.//t:UnitTestResult',n):
  assert test.get('outcome')=='Failed'
  for line in test.findtext('t:Output/t:StdOut',default='',namespaces=n).splitlines():
   try:d=json.loads(line)
   except:continue
   if 'CleanupOwnedRoot' in d:
    assert d['OwnedFixtureRemoved'] and d['CleanupFailure'] is None and not Path(d['CleanupOwnedRoot']).exists();clean.append(d['CleanupOwnedRoot'])
   if 'Scenario' not in d:continue
   sc=d['Scenario'];p=sc['PendingPhase'];w=sc['WorkerStorage'];g=d['Guardian'];assert w['mode']=='terminal_pending_reaper'
   assert g['echild'] and g['driverExitCode']==1 and not g['emergencySignals'] and not g['failures'] and not g['deadline'] and 'FixtureCleanupFailure' not in sc
   assert p['originalRunIncomplete'] and p['physicalSettled'] and p['disposeAttempts']==2
   assert p['beforeWorkspace'] and not p['afterWorkspace'] and not p['workspaceExists'] and p['workspaceCalls']==1
   assert p['RetirementAcknowledged'] and not p['slotHeld'] and not p['rootLeaseActive'] and p['EntryCount']==p['OwnedCapacity']==0
   assert p['beforeAudit']!=p['afterAudit'];events=[json.loads(x) for x in base64.b64decode(p['afterAudit']).decode('utf-8-sig').splitlines()]
   assert sum(e['eventType']=='process-tree-cleanup-confirmed' for e in events)==1
   assert 'reaper finalized or wrote diagnostics before original terminal decision settled' in w['Failure']
   assert w['DispatchSettled'] and w['physicalSettled'] and w['RetirementAcknowledged'] and w['Cut']['Cuts']==0
   folder=Path(d['FixtureFolder']);provenance=json.loads((folder/'build-provenance.json').read_text());assert len(provenance['assets'])==2
   for a in provenance['assets']:
    assert hashlib.sha256(subprocess.check_output(['git','show',sha+':'+a['source']])).hexdigest()==a['sourceSha256']
    assert hashlib.sha256((folder/a['binary']).read_bytes()).hexdigest()==a['binarySha256']
   rows.append({'mode':w['mode'],'pending':{k:v for k,v in p.items() if k not in ['beforeWorkspace','afterWorkspace','beforeAudit','afterAudit']},'guardian':g})
assert len(rows)==len(clean)==1
Path('/tmp/boe-1553-durable-root-pending.json').write_text(json.dumps({'source':sha,'actual':run.name,'tests':s['Tests'],'rows':rows,'removedRoots':clean,'scope':'Actual original pending-terminal operation was incomplete while real reaper deleted workspace, appended required audit and retired original capacity. Causal phase failure, no CSP assertion.'},indent=2)+'\n')
print('Verified actual pending-phase causal failure, physical settlement, required audit, retirement, guardian/root cleanup and2 native pins')
