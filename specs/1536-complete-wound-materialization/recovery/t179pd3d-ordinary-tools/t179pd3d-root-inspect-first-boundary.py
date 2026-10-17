from pathlib import Path
import json,hashlib,sys,xml.etree.ElementTree as ET,subprocess,datetime,os,re
R=Path('/workspace/The-Book-of-Eternity-Reborn');p=Path(sys.argv[1]);x=json.loads(p.read_text());h=lambda b:hashlib.sha256(b).hexdigest()
assert x['kind']=='FIRST_PD3D_ORDINARY_BOUNDARY2_RAW_NOT_CAUSAL_ACCEPTANCE'
assert x['nativeExit']==1 and x['category']=='fr063-ordinary-output-policy-boundary'
assert x['compiledSource']=='b501b2b77ed363e9135766082c47386e7562651a'
assert x['actualSource']==x['headAfter'] and not x['statusAfter']
assert x['binaryBefore']==x['binaryAfter'] and len(x['binaryAfter'])==9
for f in x['binaryAfter']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
assert x['namespaceBefore']==x['namespaceAfter']==dict(ownedRoots=[])
assert h(Path(x['stdoutPath']).read_bytes())==x['stdoutSha256']
assert len(x['resultDirectories'])==1;result=Path(x['resultDirectories'][0]);s=json.loads((result/'summary.json').read_text());assert s['ExitCode']==1 and not s['TimedOut'];assert s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'];assert s['Tests']==dict(Total=2,Executed=2,Passed=0,Failed=2);sel=s['Selection'];assert sel['PlannedDescriptors']==sel['CompletedDescriptors']==1 and sel['PlannedCases']==sel['CompletedCases']==2 and sel['Complete'] and not sel['UnderfilledDescriptors'];assert not s['DuplicateTests']
trxs=list(result.rglob('*.trx'));assert len(trxs)==1;doc=ET.parse(trxs[0]);ns={'t':doc.getroot().tag.split('}')[0].lstrip('{')};rows=doc.findall('.//t:UnitTestResult',ns);assert len(rows)==2
source=(R/'BookOfEternityClient.Tests/AcceptedTurnNarrativeInterfacePolicyBoundaryTests.cs').read_text();line=next(i for i,v in enumerate(source.splitlines(),1)if 'Assert.NotNull(prefix);'in v);owners=set();roots=[];rawrows=[]
for row in rows:
 assert row.attrib['outcome']=='Failed';name=row.attrib['testName'];assert 'PublicOrdinaryOwner_ConsumesSharedPolicyStages'in name
 message=row.findtext('t:Output/t:ErrorInfo/t:Message','',ns);stack=row.findtext('t:Output/t:ErrorInfo/t:StackTrace','',ns);out=row.findtext('t:Output/t:StdOut','',ns)
 assert 'Assert.NotNull' in message and 'AcceptedTurnNarrativeInterfacePolicyBoundaryTests.cs:line '+str(line) in stack
 markers=re.findall(r'REAL_ORDINARY_PREREQUISITES_COMPLETE owner=(Narrative|Interface)',out);assert len(markers)==1;owner=markers[0];owners.add(owner);assert 'owner: "'+owner+'"'in name
 marker='REAL_ORDINARY_PREREQUISITES_COMPLETE owner='+owner
 cleanup=[]
 for ln in out.splitlines():
  try: val=json.loads(ln)
  except json.JSONDecodeError: continue
  if isinstance(val,dict) and val.get('kind')=='PD3D_ORDINARY_STRICT_CLEANUP':cleanup.append(val)
 assert len(cleanup)==1;receipt=cleanup[0];root=receipt['root'];assert receipt['removed'] is True and root.startswith('/tmp/boe-pd3d-ordinary-') and not os.path.lexists(root);roots.append(root)
 rawrows.append(dict(name=name,owner=owner,testId=row.attrib['testId'],outcome=row.attrib['outcome'],message=message,stack=stack,prerequisiteMarker=marker,cleanup=receipt))
assert owners=={'Narrative','Interface'} and len(set(roots))==2
changes=subprocess.check_output(['git','diff','--name-only',x['compiledSource'],x['actualSource']],cwd=R,text=True).splitlines();assert all(a.startswith('specs/1536-complete-wound-materialization/')for a in changes)
obj=dict(kind='ROOT_FIRST_BOUNDARY2_LITERAL_RAW_INSPECTION_NOT_INDEPENDENT_EVIDENCE_VERDICT',actualSource=x['actualSource'],compiledSource=x['compiledSource'],recordSha256=h(p.read_bytes()),nativeExit=1,executed=2,passed=0,failed=2,selectionComplete=True,onlyFinalMissingPrefixAssertNotNullLine=line,rows=rawrows,strictDistinctPhysicallyAbsentRoots=roots,nineBinariesBeforeAfterCurrentExact=True,namespaceEmptyBeforeAfter=True,runnerCleanupComplete=True,trxPath=str(trxs[0]),trxSha256=h(trxs[0].read_bytes()),proofLimit='Actual source-bound public-owner success and seed-byte comparisons precede final structural missing-helper failure; no gameplay bug/HOT authority or retained per-case filesystem seed bodies are inferred. Frozen archive/publication/recovery and independent causal EVIDENCE still required before production.',utc=datetime.datetime.now(datetime.timezone.utc).isoformat());q=Path('/tmp/t179pd3d-root-first-boundary2-inspection-'+x['actualSource'][:8]+'-20261017.json');assert not q.exists();q.write_text(json.dumps(obj,indent=2)+'\n');print(q,h(q.read_bytes()))
