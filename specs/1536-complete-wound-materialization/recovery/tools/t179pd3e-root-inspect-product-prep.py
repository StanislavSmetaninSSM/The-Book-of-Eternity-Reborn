from pathlib import Path
import sys,json,hashlib,gzip,subprocess,datetime
R=Path('/workspace/The-Book-of-Eternity-Reborn');rp=Path(sys.argv[1]);AD=Path(sys.argv[2]);c=json.loads(rp.read_text());ar=json.loads((AD/'archive.json').read_text());h=lambda b:hashlib.sha256(b).hexdigest();S=c['compiledSource']
assert c['kind']=='FIRST_PD3E_PRODUCT_ZERO_EXECUTION_PREPARATION_NOT_RUNTIME_ACCEPTANCE' and c['nativeExit']==0 and c['actualTestsExecuted']==0
assert c['actualSource']==S==c['headAfter'] and not c['statusAfter']
assert c['argv']==['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-SelectionFile','tests/selection.json','-PlanOnly']
expected={str(rp),c['stdoutPath'],c['processRecord'],c['firstSentinel'],str(rp.parent/'source-binding.json')}
for d in c['resultDirectories']:expected.update(str(p) for p in Path(d).rglob('*') if p.is_file())
assert expected=={f['rawPath'] for f in ar['files']};raws={}
for f in ar['files']:
 b=Path(f['rawPath']).read_bytes();z=(AD/f['storedPath']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256'];assert len(z)==f['storedBytes'] and h(z)==f['storedSha256'];assert gzip.decompress(z)==b;assert Path(f['rawPath']).name not in raws;raws[Path(f['rawPath']).name]=b
assert json.loads(raws['record.json'])==c and h(raws['stdout.log'])==c['stdoutSha256'];binding=json.loads(raws['source-binding.json']);assert h(raws['source-binding.json'])==c['sourceBindingSha256']
assert len(binding['sourcePins'])==55 and h(json.dumps(binding['sourcePins'],ensure_ascii=False,separators=(',',':')).encode())==binding['sourcePinDigest']==c['sourcePinDigest']
for pin in binding['sourcePins']:
 b=(R/pin['path']).read_bytes();assert len(b)==pin['bytes'] and h(b)==pin['sha256'];assert b==subprocess.check_output(['git','show',S+':'+pin['path']],cwd=R)
assert len(c['binaryAfter'])==9
for f in c['binaryAfter']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
main=R/'BookOfEternityClient/bin/Debug/net8.0';managed=[dict(path=str(p.relative_to(main)),bytes=p.stat().st_size,sha256=h(p.read_bytes())) for p in sorted(main.rglob('*')) if p.is_file()];assert managed==c['managedOutputInventory']
assert c['namespaceBefore']==c['namespaceAfter']==dict(ownedRoots=[])
s=json.loads(raws['summary.json']);plan=json.loads(raws['plan.json']);assert s['Tests']['Executed']==0 and not any(n.endswith('.trx') for n in raws)
ledgerbytes=subprocess.check_output(['git','show',S+':'+binding['currentLedger']['path']],cwd=R);ledger=json.loads(ledgerbytes);assert h(ledgerbytes)==binding['currentLedger']['sha256'];methods={m['Name']:m['Cases'] for d in plan['Descriptors'] for m in d['ExpectedMethodCounts']};assert len(plan['Descriptors'])==6 and len(methods)==20 and sum(methods.values())==47 and methods=={m['fqn']:m['declaredRows'] for m in ledger['methods']}
H=subprocess.check_output(['git','rev-parse','HEAD'],cwd=R,text=True).strip();assert all(p.startswith('specs/1536-complete-wound-materialization/') for p in subprocess.check_output(['git','diff','--name-only',S,H],cwd=R,text=True).splitlines())
out=dict(kind='ROOT_NEW_PRODUCT_FRESH_PREPARATION_LITERAL_READBACK_NOT_SOURCE_VERDICT',compiledSource=S,currentCarrier=H,nativeExit=0,wallSeconds=c['wallSeconds'],actualTestsExecuted=0,sourcePinsExact=55,nineFreshCurrentExact=True,managedFilesCurrentExact=len(managed),namespaceEmpty=True,methods=methods,summary=s,plan=plan,rawChains=len(ar['files']),archiveSha256=h((AD/'archive.json').read_bytes()),scope='Fresh selected BOTH build and PlanOnly only; no runtime acceptance or replay. Full affected SOURCE still required before protectedLate2 actual, then separate evidence before ordinary45.',helperSource=Path(__file__).read_text(),utc=datetime.datetime.now(datetime.timezone.utc).isoformat());p=Path('/tmp/t179pd3e-root-product-preparation-readback-'+S[:8]+'-20261017.json');assert not p.exists();p.write_text(json.dumps(out,indent=2)+'\n');print(p,h(p.read_bytes()))
