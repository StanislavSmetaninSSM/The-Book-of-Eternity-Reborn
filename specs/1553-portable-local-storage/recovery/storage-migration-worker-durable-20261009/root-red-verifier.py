from pathlib import Path
import json,base64,hashlib,struct,subprocess,sys,xml.etree.ElementTree as E
phase,prefix,sha=sys.argv[1:]; root=Path.cwd();run,=(root/'TestResults/test-categories').glob(prefix+'*')
s=json.loads((run/'summary.json').read_text());assert s['Revision']['Head']==sha and s['Revision']['ChangedFiles']==0
assert s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['TimedOut'] and not s['DuplicateTests'] and s['Selection']['Complete']
assert s['Tests']=={'Total':14,'Executed':14,'Passed':6,'Failed':8}
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};rows=[];removed=[];assets=0
unreached={'required_audit_unknown','cleanup_audit_refused','reaper_audit_refused'}
cutmodes={'inbox_unknown','inbox_unknown_dispose','derived_audit_unknown','terminal_audit_unknown','timeout_audit_unknown'}
def digest(b):return hashlib.sha256(b).hexdigest()
def b64(b):return base64.b64decode(b)
for f in run.glob('*.trx'):
 for t in E.parse(f).findall('.//t:UnitTestResult',ns):
  for line in t.findtext('t:Output/t:StdOut',default='',namespaces=ns).splitlines():
   try:d=json.loads(line)
   except:continue
   if 'CleanupOwnedRoot' in d:
    assert d['OwnedFixtureRemoved'] and d['CleanupFailure'] is None and not Path(d['CleanupOwnedRoot']).exists();removed.append(d['CleanupOwnedRoot'])
   if 'Scenario' not in d:continue
   sc=d['Scenario'];w=sc['WorkerStorage'];c=w['Cut'];g=d['Guardian'];mode=w['mode']
   assert 'FixtureCleanupFailure' not in sc and g['echild'] and not g['deadline'] and not g['failures'] and not g['emergencySignals']
   assert w['DispatchSettled'] and w['physicalSettled'] and w['ownerOutputTasksSettled'] and w['OutputsSettled'] and not w['IsUncertain']
   assert w['releases']==1 and w['originalWorkspaceRetained'] and w['actualStop']['State']==0
   assert (t.get('outcome')=='Failed')==(mode in cutmodes|unreached)
   assert g['driverExitCode']==(1 if mode in cutmodes|unreached else 0)
   if mode in cutmodes:
    assert c['Cuts']==1 and c['Index']==0 and c['ActualUncertainty'];j=b64(c['JournalAtCut'])
    assert j==b64(c['RetainedJournal']) and digest(j).upper()==c['JournalSha256']
    md=json.loads(j[16:16+struct.unpack('<q',j[8:16])[0]] if j.startswith(b'BOELP2\r\n') else j)
    assert not md['Committed'] and md['Members'][0]['Path']==c['Target']
    assert digest(b64(c['PublishedBytes'])).lower()==md['Members'][0]['After']['Sha256'].lower()
    assert b64(c['RetainedTarget'])==b'foreign-worker-publication-image' and c['PriorAtCut']==c['AfterImages']
    facts=w['factsAtCut'];assert facts['actualGeneration']==w['generation'] and facts['task']['TaskId']==w['task']['TaskId']
    reserved=json.loads(b64(facts['taskBytes']).decode('utf-8-sig'));assert reserved['sessionGeneration']==w['generation']
    assert facts['Identity']['TaskSha256']==digest(b64(facts['taskBytes']))
    assert facts['stop']['State']==0 and facts['OutputsSettled']
    if mode.startswith('inbox') or mode=='derived_audit_unknown':assert facts['publicationAcknowledged'] and facts['Progress']['Publication']['Committed']
   else:assert c['Cuts']==0 and c['ActualUncertainty'] is None
   if mode in {'cleanup_audit_refused','reaper_audit_refused'}:
    assert not w['RetirementAcknowledged'] and w['EntryCount']==w['OwnedCapacity']==1 and w['rootLeaseActive']
    assert any('AppendRequiredEventOnceIfCurrentSessionAsync' in v['Failure'] and 'MainAdmission.AcquireAsync' in v['Failure'] for v in w['RefusalWitnesses'])
   folder=Path(d['FixtureFolder']);p=json.loads((folder/'build-provenance.json').read_text())
   for a in p['assets']:
    assert digest(subprocess.check_output(['git','show',sha+':'+a['source']]))==a['sourceSha256']
    assert digest((folder/a['binary']).read_bytes())==a['binarySha256'];assets+=1
   rows.append({'mode':mode,'outcome':t.get('outcome'),'cuts':c['Cuts'],'failure':sc.get('Failure'),'laterReads':len(c['LaterReads']),'laterMutations':len(c['LaterMutations']),'laterLeases':c['LaterLeases'],'retired':w['RetirementAcknowledged'],'sameCSP':w['SameOriginalUncertainty'],'retainedCSP':w['SameRetainedUncertainty']})
assert len(rows)==len(removed)==14 and assets==28
receipt={'source':sha,'actual':run.name,'tests':s['Tests'],'rows':rows,'removedRoots':removed,'nativeSourceBinaryPairs':assets,'classification':'5 authentic canonical publication failures; 2 real required-audit main-admission/retirement failures; 1 required-audit Unknown missed cut at same main-admission boundary; 6 ordinary controls pass.'}
Path('/tmp/boe-1553-durable-root-'+phase+'.json').write_text(json.dumps(receipt,indent=2)+'\n')
print('Verified14 complete /5 authentic cuts /2 required-audit admission failures /1 blocked cut /6 controls /14 guardians+root cleanup /28 source+binary pins')
