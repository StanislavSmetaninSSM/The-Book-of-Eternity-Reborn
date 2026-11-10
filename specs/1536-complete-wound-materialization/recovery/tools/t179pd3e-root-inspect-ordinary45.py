from pathlib import Path
import json,sys,hashlib,gzip,xml.etree.ElementTree as E,subprocess,datetime,os,collections
R=Path('/workspace/The-Book-of-Eternity-Reborn');rp=Path(sys.argv[1]);AD=Path(sys.argv[2]);c=json.loads(rp.read_text());ar=json.loads((AD/'archive.json').read_text());h=lambda b:hashlib.sha256(b).hexdigest();assert c['kind']=='FIRST_PD3E_PRODUCT_RAW_NOT_EVIDENCE_ACCEPTANCE' and c['mode']=='ordinary45';A=c['actualSource'];S=c['compiledSource'];prep=json.loads(Path(c['prepRecord']).read_text());assert h(Path(c['prepRecord']).read_bytes())==c['prepRecordSha256'];assert prep['compiledSource']==S and prep['nativeExit']==0 and prep['actualTestsExecuted']==0;assert c['headAfter']==A and not c['statusAfter']
expected={str(rp),c['stdoutPath'],c['processRecord'],c['firstSentinel'],str(rp.parent/'source-binding.json')}
for d in c['resultDirectories']:expected.update(str(p)for p in Path(d).rglob('*')if p.is_file())
assert expected=={f['rawPath']for f in ar['files']};raws={}
for f in ar['files']:
 b=Path(f['rawPath']).read_bytes();z=(AD/f['storedPath']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256'];assert len(z)==f['storedBytes'] and h(z)==f['storedSha256'];assert gzip.decompress(z)==b;assert Path(f['rawPath']).name not in raws;raws[Path(f['rawPath']).name]=b
assert json.loads(raws['record.json'])==c and h(raws['stdout.log'])==c['stdoutSha256'];assert c['binaryBefore']==c['binaryAfter'] and len(c['binaryAfter'])==9
for f in c['binaryAfter']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
assert c['namespaceBefore']==c['namespaceAfter']==dict(ownedRoots=[]);assert all(p.startswith('specs/1536-complete-wound-materialization/')for p in subprocess.check_output(['git','diff','--name-only',S,A],cwd=R,text=True).splitlines());s=json.loads(raws['summary.json']);plan=json.loads(raws['plan.json']);expectedmethods={m['Name']:m['Cases']for d in c['expectedDescriptors']for m in d['ExpectedMethodCounts']};assert len(expectedmethods)==18 and sum(expectedmethods.values())==45
rows=[];ids=[];executions=[];actualmethods=collections.Counter();roots=[];failures=[]
for name,b in raws.items():
 if not name.endswith('.trx'):continue
 x=E.fromstring(b);ns={'t':x.tag.split('}')[0].lstrip('{')};defs={v.attrib['id']:v.find('t:TestMethod',ns)for v in x.findall('.//t:TestDefinitions/t:UnitTest',ns)}
 for v in x.findall('.//t:UnitTestResult',ns):
  tid=v.attrib['testId'];d=defs[tid];method=d.attrib['className'].split(',')[0]+'.'+d.attrib['name'];actualmethods[method]+=1;ids.append(tid);executions.append(v.attrib['executionId']);out=v.findtext('t:Output/t:StdOut','',ns);receipt=[]
  for ln in out.splitlines():
   try:j=json.loads(ln)
   except json.JSONDecodeError:continue
   if isinstance(j,dict)and j.get('kind')=='PD3E_REASONING_STRICT_CLEANUP':receipt.append(j)
  row=dict(name=v.attrib['testName'],method=method,outcome=v.attrib['outcome'],testId=tid,stdoutSha256=h(out.encode()),strictReceipts=receipt)
  if v.attrib['outcome']!='Passed':row.update(message=v.findtext('t:Output/t:ErrorInfo/t:Message','',ns),stack=v.findtext('t:Output/t:ErrorInfo/t:StackTrace','',ns));failures.append(row)
  for j in receipt:assert j['removed'] and j['root'].startswith('/tmp/boe-pd3e-reasoning-')and not os.path.lexists(j['root']);roots.append(j['root'])
  rows.append(row)
assert len(ids)==len(set(ids)) and len(executions)==len(set(executions))
if c['nativeExit']==0:
 assert s['Tests']==dict(Total=45,Executed=45,Passed=45,Failed=0);assert len(rows)==45 and not failures;assert dict(actualmethods)==expectedmethods;assert s['Selection']['Complete'] and s['Selection']['PlannedDescriptors']==s['Selection']['CompletedDescriptors']==5 and s['Selection']['PlannedCases']==s['Selection']['CompletedCases']==45 and not s['Selection']['UnderfilledDescriptors'];assert s['OwnedTreeCleanupSucceeded']and s['RuntimeCleanupSucceeded']and not s['TimedOut']and not s['DuplicateTests'];assert len(roots)==len(set(roots))==41
 for row in rows:
  wanted=row['method'].startswith('BookOfEternityClient.Tests.AcceptedTurnReasoningOrdinaryTests.')or row['method'].endswith('.PublicFullOwner_ConsumesSharedDocumentAndScopePrefix');assert len(row['strictReceipts'])==(1 if wanted else 0)
obj=dict(kind='ROOT_FIRST_ORDINARY45_LITERAL_NEW_RAW_INSPECTION_NOT_INDEPENDENT_EVIDENCE',actualSource=A,compiledSource=S,nativeExit=c['nativeExit'],wallSeconds=c['wallSeconds'],summary=s,plan=plan,rawChains=len(ar['files']),archiveSha256=h((AD/'archive.json').read_bytes()),expectedMethods=expectedmethods,actualMethodCounts=dict(actualmethods),rows=rows,failures=failures,distinctNewStrictPhysicallyAbsentRoots=roots,nineFreshBeforeAfterCurrentExact=True,namespaceEmptyBeforeAfter=True,scope='New45 only; independent final EVIDENCE/publication/recovery still required. Exactly41newfixture rows have emitted strict per-root cleanup. Legacy2Guardian integration best-effort rows have namespaceabsence, no invented per-case root identities. Source-only2 has nofixture root. Behavioral byte/mtime/fault assertions are source-bound reached assertions, deleted payload bodies were not separately frozen. ProtectedLate2 separate accepted proof, not counted among45. No oldWorld/HOT/fullF1/Windows claim.',helperSource=Path(__file__).read_text(),utc=datetime.datetime.now(datetime.timezone.utc).isoformat());p=Path('/tmp/t179pd3e-root-first-ordinary45-inspection-'+A[:8]+'-20261017.json');assert not p.exists();p.write_text(json.dumps(obj,indent=2)+'\n');print(p,h(p.read_bytes()))
