from pathlib import Path
import json,base64,hashlib,struct,sys,xml.etree.ElementTree as E
phase,prefix,sha=sys.argv[1:];run,=Path('TestResults/test-categories').glob(prefix+'*');s=json.loads((run/'summary.json').read_text());green=phase=='green'
assert s['Revision']['Head']==sha and s['Revision']['ChangedFiles']==0
assert s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and s['Selection']['Complete'] and not s['TimedOut'] and not s['DuplicateTests']
assert s['Tests']==({'Total':26,'Executed':26,'Passed':26,'Failed':0} if green else {'Total':11,'Executed':11,'Passed':4,'Failed':7})
b64=lambda b:base64.b64decode(b);hash=lambda b:hashlib.sha256(b).hexdigest();ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};rows=[];removed=[];outcomes=[]
for trx in run.glob('*.trx'):
 for test in E.parse(trx).findall('.//t:UnitTestResult',ns):
  outcomes.append(test.get('outcome'))
  for line in test.findtext('t:Output/t:StdOut',default='',namespaces=ns).splitlines():
   try:d=json.loads(line)
   except:continue
   if 'CleanupOwnedRoot' in d:
    assert d['OwnedFixtureRemoved'] and not Path(d['CleanupOwnedRoot']).exists() and d['CleanupFailure'] is None;removed.append(d['CleanupOwnedRoot'])
   if 'Cut' not in d:continue
   mode=d['mode'];c=d['Cut'];unknown=d['uncertain'];assert d['closeFailure'] is None
   assert not d['captureCurrentFinal']
   if unknown:
    assert c['Cuts']==1 and c['Index']==0 and c['ActualUncertainty'];j=b64(c['JournalAtCut']);assert j==b64(c['RetainedJournal']) and hash(j).upper()==c['JournalSha256']
    md=json.loads(j[16:16+struct.unpack('<q',j[8:16])[0]] if j.startswith(b'BOELP2\r\n') else j)
    assert not md['Committed'] and md['Members'][0]['Path']==c['Target'] and hash(b64(c['PublishedBytes']))==md['Members'][0]['After']['Sha256'].lower()
    assert b64(c['RetainedTarget'])==b'foreign-cleanup-publication-image'
    assert d['PriorAtCut']==d['afterImages'] and d['PriorAtCut'] and d['atCut']['generation']
    assert not c['LaterMutations'] and c['LaterLeases']==c['LaterRecovery']==c['LaterPublications']==0 and not c['PendingClosingLease']
    if mode=='submission_unknown':assert d['captureCurrentAfter'] is False
    if 'pending' not in mode and not mode.startswith('repair'):
     checkpoint=json.loads(b64(c['PublishedBytes']))['checkpoint'];assert checkpoint
     if mode=='submission_unknown':assert checkpoint['pendingSubmission'] and checkpoint['committedAdvance']==0
     if mode=='saved_checkpoint_unknown':assert checkpoint['committedAdvance']==1
    if green:assert test.get('outcome')=='Passed' and d['SameOriginalUncertainty'] and d['disposition'] is None and d['failure'] and not c['LaterReads']
    else:assert test.get('outcome')=='Failed' and not d['SameOriginalUncertainty'] and d['failure'] is None and d['disposition'] in ['blocked','repair_required'] and c['LaterReads']
   else:
    assert test.get('outcome')=='Passed' and c['Cuts']==0 and c['ActualUncertainty'] is None and d['failure'] is None and c['RetainedJournal'] is None
    assert d['disposition']==('repaired' if mode=='repair_success' else 'committed')
    if mode=='dependent_success':assert d['nextContinuation'] and d['originalContinuation']!=d['nextContinuation']
   rows.append({'mode':mode,'outcome':test.get('outcome'),'cut':c['Cuts'],'sameCSP':d['SameOriginalUncertainty'],'disposition':d['disposition'],'laterReads':len(c['LaterReads']),'retainedPriorImages':len(d['PriorAtCut'] or {}),'closeFailure':d['closeFailure']})
assert len(rows)==len(removed)==len(set(removed))==11 and sum(r['cut'] for r in rows)==7 and len(outcomes)==(26 if green else 11)
receipt={'source':sha,'actual':run.name,'tests':s['Tests'],'rows':rows,'removedRoots':removed,'scope':'Original services only; seven authentic retained journal/foreign cuts and exact previous committed images;4success, original capture/borrowedlease/root settlement. Known15 GREEN if included; no full engine/native/secondaryclose.'}
Path('/tmp/boe-c2-root-'+phase+'.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Verified '+phase+' actual complete,7 authentic cuts,4success,11 original close/root settlements')
