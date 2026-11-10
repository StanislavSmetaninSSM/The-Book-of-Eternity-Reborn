from pathlib import Path
import json,base64,hashlib,struct,sys,xml.etree.ElementTree as E
phase,prefix,sha=sys.argv[1:];run,=Path('TestResults/test-categories').glob(prefix+'*');s=json.loads((run/'summary.json').read_text());green=phase=='green'
assert s['Revision']['Head']==sha and s['Revision']['ChangedFiles']==0
assert s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and s['Selection']['Complete'] and not s['TimedOut'] and not s['DuplicateTests']
assert s['Tests']=={'Total':12,'Executed':12,'Passed':12 if green else 3,'Failed':0 if green else 9}
b64=base64.b64decode;hash=lambda b:hashlib.sha256(b).hexdigest();ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};rows=[];removed=[]
for trx in run.glob('*.trx'):
 for test in E.parse(trx).findall('.//t:UnitTestResult',ns):
  for line in test.findtext('t:Output/t:StdOut',default='',namespaces=ns).splitlines():
   try:d=json.loads(line)
   except:continue
   if not isinstance(d,dict):continue
   if 'CleanupOwnedRoot' in d:
    assert d['OwnedFixtureRemoved'] and not Path(d['CleanupOwnedRoot']).exists() and d['CleanupFailure'] is None;removed.append(d['CleanupOwnedRoot'])
   if 'Cut' not in d:continue
   mode=d['mode'];c=d['Cut'];assert d['generation']
   if d['unknown']:
    assert c['Cuts']==1 and c['Index']==0 and c['ActualUncertainty'];j=b64(c['JournalAtCut']);assert j==b64(c['RetainedJournal']) and hash(j).upper()==c['JournalSha256']
    m=json.loads(j[16:16+struct.unpack('<q',j[8:16])[0]] if j.startswith(b'BOELP2\r\n') else j);member=m['Members'][0]
    assert not m['Committed'] and member['Path']==c['Target']
    deletion='delete' in mode or mode=='cancel_request_unknown'
    if deletion:assert not member['After']['Exists'] and member['After']['Sha256'] is None and c['PublishedBytes'] is None
    else:assert member['After']['Exists'] and hash(b64(c['PublishedBytes']))==member['After']['Sha256'].lower()
    assert b64(c['RetainedTarget'])==b'foreign-cleanup-publication-image'
    assert d['PriorAtCut'] and d['PriorAtCut']==d['afterImages']
    assert c['LaterLeases']==c['LaterRecovery']==c['LaterPublications']==0 and not c['PendingClosingLease']
    if green:
     assert test.get('outcome')=='Passed' and d['SameOriginalUncertainty'] and d['result'] is None and d['failure'] and not c['LaterReads'] and not c['LaterMutations']
    else:
     assert test.get('outcome')=='Failed' and not d['SameOriginalUncertainty'] and d['failure'] is None
     assert d['result']==('RetryableHeld' if mode.startswith('reconcile') else 'Rejected')
     if mode=='dependent_checkpoint_unknown':assert len(c['LaterReads'])==2 and len(c['LaterMutations'])==1
     else:assert not c['LaterReads'] and not c['LaterMutations']
    if deletion and mode!='cancel_request_unknown':
     cp=json.loads(b64(d['checkpointAfter']));assert len(cp['checkpoint']['pendingSubmission']['dependentDraftProgress'])==1
    if mode=='dependent_checkpoint_unknown':
     cp=json.loads(b64(c['PublishedBytes']));assert len(cp['checkpoint']['pendingSubmission']['dependentDraftProgress'])==1
    if mode.startswith('reconcile'):assert d['a'] and d['b'] and d['a']!=d['b']
   else:
    assert test.get('outcome')=='Passed' and c['Cuts']==0 and not d['failure'] and c['RetainedJournal'] is None
    assert d['result']=='Rejected' and d['checkpointAfter'] and d['readyAfter'] is None and d['requestAfter'] is None
    if d['a']:
     assert any(i!=d['a'] for i in d['committedRequests'])
     cp=json.loads(b64(d['checkpointAfter']));assert len(cp['checkpoint']['pendingSubmission']['dependentDraftProgress'])==1
     if d['b']:assert d['b'] in d['committedRequests']
    else:assert len(d['committedRequests'])==1
   rows.append({'mode':mode,'outcome':test.get('outcome'),'cuts':c['Cuts'],'sameCSP':d['SameOriginalUncertainty'],'result':d['result'],'laterReads':len(c['LaterReads']),'laterMutations':len(c['LaterMutations']),'priorImages':len(d['PriorAtCut'] or {})})
assert len(rows)==12 and len(set(r['mode'] for r in rows))==12 and sum(r['cuts'] for r in rows)==9
assert len(removed)==len(set(removed))==24
receipt={'source':sha,'actual':run.name,'tests':s['Tests'],'wallTime':s['WallTime'],'rows':rows,'removedRoots':removed,'scope':'Original signed GameEngine entry/wait/reconciliation;9authentic cuts,3finite ordinaryEscape controls, actualA-to-B/priorcheckpoint retention. Root/method settlement; no exact keycount/injected secondaryleaseclose/fullacceptedturn/C4/B2/native claim.'}
Path('/tmp/boe-c2-engine-root-'+phase+'.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Verified engine '+phase+':12actual,9authentic cuts,3success,24distinct removedroots')
