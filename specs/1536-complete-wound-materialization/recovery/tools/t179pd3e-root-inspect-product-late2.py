from pathlib import Path
import json,sys,hashlib,gzip,base64,xml.etree.ElementTree as E,subprocess,datetime,os
R=Path('/workspace/The-Book-of-Eternity-Reborn');rp=Path(sys.argv[1]);AD=Path(sys.argv[2]);command=json.loads(rp.read_text());packet=json.loads((AD/'archive.json').read_text());sha=lambda b:hashlib.sha256(b).hexdigest();A=command['actualSource'];S=command['compiledSource']
assert command['kind']=='FIRST_PD3E_PRODUCT_RAW_NOT_EVIDENCE_ACCEPTANCE' and command['mode']=='protected-late2' and command['nativeExit']==0
prep=json.loads(Path(command['prepRecord']).read_text());assert sha(Path(command['prepRecord']).read_bytes())==command['prepRecordSha256'];assert prep['compiledSource']==S and prep['nativeExit']==0 and prep['actualTestsExecuted']==0
assert command['headAfter']==A and not command['statusAfter']
assert command['argv']==['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category','fr063-joint-late-completion','-NoBuild']
assert command['binaryBefore']==command['binaryAfter'] and len(command['binaryAfter'])==9
for f in command['binaryAfter']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and sha(b)==f['sha256']
assert command['namespaceBefore']==command['namespaceAfter']=={'ownedRoots':[]}
assert all(p.startswith('specs/1536-complete-wound-materialization/')for p in subprocess.check_output(['git','diff','--name-only',S,A],cwd=R,text=True).splitlines())
raws={};chains=[];expected={str(rp),command['stdoutPath'],command['processRecord'],command['firstSentinel'],str(rp.parent/'source-binding.json')}
for d in command['resultDirectories']:expected.update(str(p)for p in Path(d).rglob('*')if p.is_file())
assert expected=={f['rawPath']for f in packet['files']}
for f in packet['files']:
 b=Path(f['rawPath']).read_bytes();z=(AD/f['storedPath']).read_bytes();assert len(b)==f['bytes'] and sha(b)==f['sha256'];assert len(z)==f['storedBytes'] and sha(z)==f['storedSha256'];assert gzip.decompress(z)==b;assert Path(f['rawPath']).name not in raws;raws[Path(f['rawPath']).name]=b;chains.append(f)
assert json.loads(raws['record.json'])==command
origin=Path('/tmp/a2-writer-inspect-late.py');old=origin.read_text();helpers=old[old.index('def h('):old.index('packet=json.loads(')];body=old[old.index("s=json.loads(raws['summary.json'])"):old.index("result=dict(kind='A2_FIRST_PROTECTED_LATE2_WRITER_INSPECTION'")]
for forbidden in ['write_text(','write_bytes(','subprocess.','exec(','eval(','unlink(','mkdir(','rmtree(','open(']:assert forbidden not in helpers+body
scope=dict(Path=Path,json=json,gzip=gzip,hashlib=hashlib,base64=base64,E=E,R=R,A=A,S=S,raws=raws)
exec(compile(helpers, str(origin)+':unchanged-value-helpers','exec'),scope)
exec(compile(body,str(origin)+':retained-semantic-predicates-on-NEW-RAW','exec'),scope)
cases=scope['cases'];assert len(cases)==2
for c in cases:assert not os.path.lexists(c['cleanup']['CleanupOwnedRoot'])
out=dict(kind='ROOT_FIRST_NEW_PRODUCT_PROTECTED_LATE2_LITERAL_AND_RETAINED_PREDICATE_READBACK_NOT_INDEPENDENT_VERDICT',actualSource=A,compiledSource=S,nativeExit=0,wallSeconds=command['wallSeconds'],summary=scope['s'],plan=scope['plan'],rawChains=len(chains),archiveSha256=sha((AD/'archive.json').read_bytes()),nineBeforeAfterLiveBinaryPinsExact=True,namespaceEmptyBeforeAfter=True,cases=cases,retainedSemanticPredicateOrigin=str(origin),retainedOriginSha256=sha(origin.read_bytes()),retainedSemanticPredicateSha256=sha(body.encode()),semanticProof='Unchanged retained value predicates evaluated ONLY against NEW first actual raw: genuine source42/original43, confirmed held/III→II repair, physical66snapshots, commonCREATE/TREAT/damage/charge, frozen0/1progression/report transactions, exactstoryreconstruction/cleanup/inertrepeat; no oldraw replay.',scope='Independent EVIDENCE and publication/recovery still required; no ordinary45, fullHOT/history/F1/platform qualification. Final computed raw body remains absent; only source/milestone bodies and final digest/inert equality where retained.',helperSource=Path(__file__).read_text(),utc=datetime.datetime.now(datetime.timezone.utc).isoformat());p=Path('/tmp/t179pd3e-root-first-product-late2-inspection-'+A[:8]+'-20261017.json');assert not p.exists();p.write_text(json.dumps(out,indent=2)+'\n');print(p,sha(p.read_bytes()))
