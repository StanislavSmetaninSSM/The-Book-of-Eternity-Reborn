"""Focused source acceptance checks and affected inert callback proofs; no native SDK/API work."""
from pathlib import Path
import json,hashlib,subprocess,runpy,io,contextlib,ast
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');P='specs/1536-complete-wound-materialization/recovery/';G=P+'l01-game-capture-g01/'
REV='e520663f7918e536c2ae0675cb79a774be637655';OLD='223a5e09a3998e528e2d6e514ecc91589b5c57c8';H=lambda b:hashlib.sha256(b).hexdigest()
def git(*a,root=R):return subprocess.check_output(['git','-C',str(root),*a])
def at(p,rev=REV):return git('show',rev+':'+p)
def rd(p):return json.loads(at(p))
checks=[]
def ck(n,b):checks.append(dict(name=n,passed=bool(b)));assert b,n
ck('Exact clean writer and independent restore',all(git('rev-parse','HEAD',root=r).decode().strip()==REV and not git('status','--porcelain',root=r) for r in [R,C]))
receipt=Path('/tmp/1536-cloud-tools/g01-recovery-e520663f.json');re=json.loads(receipt.read_text());ck('Exact recovery222paths163pins',re['sha']==REV and re['remoteShaVerified'] and re['clean'] and re['changedPaths']==222 and re['currentPinsVerified']==163)
p=rd(G+'source-packet.json');cfg=rd(G+'bindings.json');oldp=json.loads(at(G+'source-packet.json',OLD));oldcfg=json.loads(at(G+'bindings.json',OLD))
ck('Current163pin canonical digest',len(p['sourcePins'])==163 and H(json.dumps(p['sourcePins'],sort_keys=True,separators=(',',':')).encode())==p['sourcePinDigest']=='a6c2675674dd6e3218e16917fd9abfcffb44d638195c367c3efcb2d98780b026')
ck('Currentpins exact Git clone',all(len(at(x['path']))==x['bytes'] and H(at(x['path']))==x['sha256'] and (C/x['path']).read_bytes()==at(x['path']) for x in p['sourcePins']))
ck('Exact controller binding',H(at(G+'phase.py'))=='63969901c241f7007030af78b757bba0618c72dae457ae75c11804de45bc9980')
ck('Exact bindings hash',H(at(G+'bindings.json'))=='50e5bffdc10799c05b8757b8606642c0a5e6329fc4a0b91a764851eca4a679b2')
ck('Historical explicit current deltas unchanged',p['explicitD01Deltas']==oldp['explicitD01Deltas'] and p['historicalPins']==oldp['historicalPins'])
ck('No CSharp implementation mapping assertions selection production changes',not git('diff','--name-only',OLD,REV,'--','BookOfEternityClient.IntegrationTests','BookOfEternityClient','BookOfEternityClient.TestSupport','tests/categories.json','tests/selection.json',G+'MapFrames.cs',G+'ParentProbe.cs'))
ck('Unit closure metadata root identity and graph retained',all(cfg[k]==oldcfg[k] for k in cfg if k not in ['inputs','requestInputs']))
for name in ['inputs','requestInputs']:
 a={x['path']:x for x in oldcfg[name]};b={x['path']:x for x in cfg[name]};delta={k for k in a.keys()|b.keys() if a.get(k)!=b.get(k)}
 expected={str(R/G/'watchdog.py'),str(R/G/'Collect.ps1')} | ({str(R/G/'check_health.ps1')} if name=='inputs' else set())
 ck(name+' only explicitly affected hashes changed',delta==expected)
 for k in delta:ck('Affected input currenthash '+k,len(Path(k).read_bytes())==b[k]['bytes'] and H(Path(k).read_bytes())==b[k]['sha256'])
for name in ['l01-game-capture-g01-affected-source-review-223a-20261019.json','l01-game-capture-g01-affected-source-review-223a-20261019.md','l01-game-capture-g01-affected-source-checks-223a-20261019.json','check_g01_affected_source_223a_20261019.py']:
 ck('Original affected HOLD copy '+name,at(P+name)==(D/name).read_bytes())
watch=at(G+'watchdog.py').decode();ps=at(G+'Collect.ps1').decode();phase=at(G+'phase.py').decode();proof=at(G+'check_health.ps1').decode()
ck('No .001 positive wait floor remains','max(.001' not in watch)
ck('All cleanup-action deadline guards present',all(x in watch for x in ['Initial cleanup census deadline exhausted','Direct collector cleanup deadline exhausted','Owned signal deadline exhausted','Collector join deadline exhausted','Post-collector census deadline exhausted','Late createdump signal deadline exhausted','Late createdump wait deadline exhausted']))
ck('Natural nonblocking poll still before forced action',watch.index('native = child.poll()',watch.index('finally:'))<watch.index('child.kill()') and 'code=child.poll()' in watch)
ck('Marker only positive absence exceptions; other errors throw','function Read-ParentCancellation' in ps and '[IO.File]::Exists' not in ps and '$stream = [IO.File]::Open' in ps and '$cause -is [IO.FileNotFoundException] -or $cause -is [IO.DirectoryNotFoundException]' in ps and 'throw # Unknown access/I/O state' in ps)
ck('Marker I/O before final health timestamp',ps.index('$parentCancelled = Read-ParentCancellation')<ps.index('$cancel = Test-AdmissionHealth $h $m ([Diagnostics.Stopwatch]::GetTimestamp())'))
ck('Health26 original23 plusprivate marker3 required',"health['Passed']==len(health['Cases'])==26" in phase and '-ProofRoot' in phase and all(t in proof for t in ['only missing marker means absent','private existing marker means cancelled','private marker I/O uncertainty refuses']))
ck('Existing parent6 map5 clock proofs retained',"parent['passed']==len(parent['cases'])==6" in phase and 'body-end' in phase and 'outside-body' in phase and 'insideObservedBracket=True' in phase)
# Both files were manually read: callbacks replace all OS-process/handle effects.
results=[]
for script,stored in [('check_watchdog.py','watchdog-fault-green-budget-guard.json'),('check_deadline.py','deadline-fault-green.json')]:
 stream=io.StringIO()
 with contextlib.redirect_stdout(stream):
  try:runpy.run_path(str(R/G/script),run_name='__main__')
  except SystemExit as e:assert e.code==0
 actual=json.loads(stream.getvalue());saved=rd(G+stored)
 ck('Current exact-source inert proof matches saved '+script,actual==saved)
 ck('Proof source bound/no native '+script,actual['sourceSha256']==H(at(G+'watchdog.py')) and actual['nativeProcesses']==actual['gameCases']==actual['apiRequests']==0)
 results.append(dict(script=script,passed=actual['passed'],savedSha256=H(at(G+stored)),result=actual))
ck('Lifecycle5 and deadline4 passing',[x['passed'] for x in results]==[5,4])
ck('Original deadline RED1of4 retained',rd(G+'deadline-fault-red.json')['passed']==1 and rd(G+'deadline-fault-red.json')['sourceSha256']==H(at(G+'watchdog.py',OLD)))
control=rd(G+'control.json');ck('All actual admissionstillclosed and allcounts0',control['sourceAdmission']['status']=='HOLD_WHOLE_SOURCE' and control['actualAdmission']['status']=='HOLD_PREP' and control['actualGameCasesExecuted']==control['diagnosticRequests']==control['dumpArtifactsCreated']==0)
ck('New root stillunused',not Path(cfg['root']).exists())
result=dict(kind='G01_FINAL_FOCUSED_SOURCE_CHECKS',candidate=REV,base=OLD,checks=checks,passed=len(checks),findings=[],proofs=results,sourcePinDigest=p['sourcePinDigest'],controllerSha256=H(at(G+'phase.py')),bindingsSha256=H(at(G+'bindings.json')),recovery=dict(path=str(receipt),sha256=H(receipt.read_bytes())),nativeSdkGameApiActions=0)
o=D/'l01-game-capture-g01-source-final-checks-e520-20261019.json';o.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(path=str(o),checks=len(checks),sha256=H(o.read_bytes()))))
