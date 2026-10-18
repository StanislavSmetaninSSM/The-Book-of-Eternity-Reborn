from pathlib import Path
import json,hashlib,subprocess,gzip,ast,datetime,xml.etree.ElementTree as ET,collections,fnmatch
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=Path('/tmp/1536-cloud-tools/review');REC=R/'specs/1536-complete-wound-materialization/recovery'
def h(b):return hashlib.sha256(b).hexdigest()
def git(*args):return subprocess.check_output(['git',*args],cwd=R)
candidate='bb156d0d84d4f2911f356ad79205a3f2697c1cf9';prior='c4c0c62fbf37d2b0699b72e95794e4e3cef50535';head=git('rev-parse','HEAD').decode().strip()
p=json.loads((REC/'t179pd-ni-iobs-l01-source-packet-20261018.json').read_bytes());l=json.loads((R/p['ledger']['path']).read_bytes())
c=json.loads(git('show',candidate+':specs/1536-complete-wound-materialization/recovery/t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
assert len(p['sourcePins'])==98 and h(json.dumps(p['sourcePins'],sort_keys=True,separators=(',',':')).encode())==p['pinDigest']
assert h((R/p['ledger']['path']).read_bytes())==p['ledger']['sha256']
assert not git('diff','--name-only',prior,head,'--','.',':!specs/1536-complete-wound-materialization')
assert git('diff','--name-only',p['productBase'],head,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()==p['productDelta']
def tree(rev):
 out={}
 for row in git('ls-tree','-rz',rev).split(b'\0'):
  if not row:continue
  meta,path=row.split(b'\t',1);mode,kind,blob=meta.decode().split();out[path.decode()]=(mode,blob)
 return out
ct=tree(candidate);pt=tree(prior);ht=tree(head)
for x in p['sourcePins']:
 b=(R/x['path']).read_bytes();blob=hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
 assert len(b)==x['bytes'] and h(b)==x['sha256'] and blob==x['gitBlob'],x['path']
 assert ct[x['path']]==pt[x['path']]==ht[x['path']]==(x['mode'],blob),x['path']
for x in p['sourceAcceptance']['artifacts']:
 assert h((R/x['path']).read_bytes())==x['sha256'],x['path']
for x in p['verificationTools']:
 assert h((R/x['path']).read_bytes())==x['sha256'],x['path']
# Syntax and literals only, never invoke these controllers.
a=ast.parse((REC/'t179pd-ni-iobs-l01-process-first-run.py').read_text())
groups=ast.literal_eval(next(n.value for n in a.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='groups' for t in n.targets)))
selectors=json.loads((R/'tests/selection.json').read_bytes())['selections'];assert len(selectors)==16
assert (R/'tests/selection.json').read_bytes()==(R/'tests/selections/1536-joint-interface-lifecycle-stage.json').read_bytes()
catalog={x['id']:x for x in json.loads((R/'tests/categories.json').read_bytes())['categories']}
counts={r['method']:r['staticRows'] for r in l['rows']}
counts.update({
'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_CurrentCaptureStopPrecedesPreparationAndExpiresActualOwner':1,
'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_CurrentCaptureObservationPreservesOrdinaryCompletion':2,
'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_LiveSeverityRepairCompletesJointOnceWithZeroCycleProgression':1,
'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_LiveSeverityRepairConsumesMatchingNonzeroReportExactlyOnce':1,
'BookOfEternityClient.Tests.PromptDocumentationCoverageTests.MortalJointSeverityRepairGuidance_PreservesPublicTransportAndOnceOnlyClientOwnership':1})
whole={};bycat={}
for s in selectors:
 cat=catalog[s['category']];methods={}
 for sel in cat['selectors']:
  for pattern in sel['tests']:
   matches={m:n for m,n in counts.items() if fnmatch.fnmatchcase(m,pattern)};assert matches,(cat['id'],pattern)
   for m,n in matches.items():assert m not in methods;methods[m]=n
 for m,n in methods.items():assert m not in whole;whole[m]=n
 bycat[cat['id']]=methods
assert len(whole)==33 and sum(whole.values())==57
pending={}
for mode,(cats,n) in groups.items():
 actual={m:v for cat in cats for m,v in bycat[cat].items()};assert sum(actual.values())==n
 wanted=next(x for x in c['pending'] if x['mode']==mode)
 assert wanted['expectedCases']==n and {x['method']:x['cases'] for x in wanted['methods']}==actual
 assert not set(pending)&set(actual);pending.update(actual)
assert len(pending)==12 and sum(pending.values())==20 and sum(n for mode,(_,n) in groups.items() if mode!='crash5')==15
retained={m:n for m,n in whole.items() if m not in pending};assert len(retained)==21 and sum(retained.values())==37
archive_summary=[];chains=0;passmethods=collections.Counter();passrows=set();fails=0;execution=0
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
for item in c['restoration']['restoredArchives']:
 ap=R/item['archive']['path'];ab=ap.read_bytes();assert h(ab)==item['archive']['sha256'];ar=json.loads(ab)
 trx=[];records=[];summaries=[]
 for f in ar['files']:
  sb=(ap.parent/f['storedPath']).read_bytes();assert len(sb)==f['storedBytes'] and h(sb)==f['storedSha256']
  raw=gzip.decompress(sb);assert len(raw)==f['bytes'] and h(raw)==f['sha256'];chains+=1
  if f['rawPath'].endswith('/record.json'):records.append(json.loads(raw))
  elif f['rawPath'].endswith('/summary.json'):summaries.append(json.loads(raw))
  elif f['rawPath'].endswith('.trx'):
   tree_=ET.fromstring(raw)
   names={u.attrib['id']:u.find('t:TestMethod',ns).attrib for u in tree_.findall('t:TestDefinitions/t:UnitTest',ns)}
   for res in tree_.findall('t:Results/t:UnitTestResult',ns):
    method=names[res.attrib['testId']];full=method['className'].split(',')[0]+'.'+method['name'];execution+=1
    if res.attrib['outcome']=='Passed':
     key=(full,res.attrib['testName']);assert key not in passrows;passrows.add(key);passmethods[full]+=1
    else:fails+=1
 assert len(ar['files'])==ar['rawChainCount']==item['archive']['rawChains']
 assert len(records)==1 and records[0]['actualSource']==item['actualSource'] and records[0]['compiledSource']==item['compiledSource']
 assert len(summaries)==1 and summaries[0]['Tests']==item['tests']
 archive_summary.append({'scope':item['scope'],'chains':len(ar['files']),'actualSource':item['actualSource'],'compiledSource':item['compiledSource'],'tests':item['tests']})
assert chains==100 and execution==44 and fails==7 and dict(passmethods)==retained
out={'kind':'INDEPENDENT_CLOUD_SOURCE_RETENTION_SELECTION_ARCHIVE_CHECK','checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'candidate':candidate,'currentHeadObserved':head,'priorSourceAcceptance':prior,'sourcePinCount':98,'sourcePinDigest':p['pinDigest'],'ledgerSha256':p['ledger']['sha256'],'allPinsMatchWorkingTreeCandidatePriorAndCurrentGitBlobMode':True,'nonSpecChangesSincePriorSourceAcceptance':[],'productDeltaPaths':len(p['productDelta']),'whole':{'descriptors':16,'methods':len(whole),'cases':sum(whole.values()),'basis':'Static current selectors expanded against unchanged authored ledger plus five unchanged source methods/six rows; not fresh discovery'},'pending':{'descriptors':7,'methods':len(pending),'cases':sum(pending.values()),'groups':groups},'retained':{'methods':len(retained),'cases':sum(retained.values()),'methodCases':retained,'provenance':'26 compiled45cc +11 compiled4c6d; actualSource per archive preserved'},'archives':archive_summary,'verifiedOriginalGzipRawChains':chains,'originalActualCaseRows':execution,'originalPassRows':len(passrows),'originalFixtureFailures':fails,'noDuplicatePassedMethodCaseNames':True,'correctedOldCrash':'UNKNOWN_UNAVAILABLE_NOT_RECOVERED','freshPREP':'NOT_ACCEPTED','reviewerExecution':'No build/discovery/tests or game runtime; Python read-only evidence/hash checks only','findings':[]}
op=D/'t179pd-ni-iobs-l01-cloud-source-check-20261018.json';op.write_text(json.dumps(out,indent=2)+'\n');print(op);print(h(op.read_bytes()));print(json.dumps({k:out[k] for k in ['sourcePinCount','verifiedOriginalGzipRawChains','originalActualCaseRows','originalPassRows','originalFixtureFailures']}))
