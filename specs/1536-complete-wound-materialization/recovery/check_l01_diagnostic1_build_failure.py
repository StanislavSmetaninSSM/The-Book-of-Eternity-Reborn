#!/usr/bin/env python3
"""Frozen compiler failure only. Reads saved Git/archive bytes; performs no native work."""
import pathlib,subprocess,json,gzip,hashlib,re,datetime
R=pathlib.Path('/workspace/The-Book-of-Eternity-Reborn');C=pathlib.Path('/workspace/1536-cloud-clean-restore');D=pathlib.Path('/tmp/1536-cloud-tools/review')
SHA='c66d33592622c59261e08c2cb739a61fbf1955a9';ATTEMPT='9b1124a4706b47bdb198afb2444408d6cba2f0b0';REL='specs/1536-complete-wound-materialization/recovery/'
def h(b):return hashlib.sha256(b).hexdigest()
def get(path,rev=SHA,root=R):return subprocess.check_output(['git','-C',str(root),'show',rev+':'+path])
def ref(path):
 p=pathlib.Path(path);b=p.read_bytes();return {'path':str(p),'bytes':len(b),'sha256':h(b)}
cloud=json.loads(get(REL+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));attempt=cloud['crashDiagnostic1']['preparationAttempts'][-1]
archive_ref=attempt['archive'];ab=get(archive_ref['path']);assert len(ab)==archive_ref['bytes'] and h(ab)==archive_ref['sha256']=='e50b8656e8b82bdf0c7125e63541a803bf6ee8c3d6cf230164dab32a94f7edbf'
a=json.loads(ab);assert len(a['files'])==a['rawChainCount']==8 and a['nativeExits']==[1]
raw={}
for x in a['files']:
 p=str(pathlib.PurePosixPath(archive_ref['path']).parent/x['storedPath']);z=get(p);assert z==get(p,root=C) and len(z)==x['storedBytes'] and h(z)==x['storedSha256'];b=gzip.decompress(z);assert len(b)==x['bytes'] and h(b)==x['sha256'];assert x['rawPath']not in raw;raw[x['rawPath']]=b
r=json.loads(raw[attempt['recordPath']]);assert h(raw[attempt['recordPath']])==attempt['recordSha256']=='123724fa7b7e934ddb09882a8f511035abfb3216eb0e030117cd14991ca62963'
assert r['actualSource']==r['compiledSource']==r['headAfter']==ATTEMPT and not r['statusAfter'] and r['nativeExit']==1 and r['actualTestsExecuted']==0 and r['mode']=='catalog'
summary=json.loads(next(b for p,b in raw.items()if p.endswith('/summary.json')));assert summary['DiscoveryOnly']and summary['CatalogAudit']is None and summary['ExitCode']==1 and not summary['TimedOut'];assert summary['Tests']=={'Total':0,'Executed':0,'Passed':0,'Failed':0}
assert summary['OwnedTreeCleanupSucceeded']and summary['RuntimeCleanupSucceeded']and r['namespaceBefore']==r['namespaceAfter']=={'ownedRoots':[]}
assert not any(p.endswith('.trx')for p in raw) and not summary['AdapterResults']
log=next(b.decode()for p,b in raw.items()if p.endswith('/dotnet-test.log'));errors=sorted(set(x.strip()for x in log.splitlines()if ': error 'in x));assert len(errors)==1 and 'JointInterfaceCrash.cs(240,9): error CS0246:'in errors[0] and "'ILogger<>'"in errors[0]
assert any(x['Name']=='Build-integration'and not x['Completed']for x in summary['PhaseResults']) and not any(x['Name']=='Build-unit'for x in summary['PhaseResults'])
packet=json.loads(get(REL+'t179pd-ni-iobs-l01-source-packet-20261018.json'));assert len(packet['sourcePins'])==101 and len(packet['productDelta'])==23
for x in packet['sourcePins']:
 b=get(x['path']);assert b==get(x['path'],ATTEMPT)==get(x['path'],root=C) and len(b)==x['bytes']and h(b)==x['sha256']
crash='BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrash.cs';diag='BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrashDiagnostic.cs';cs=get(crash).decode();ds=get(diag).decode()
assert 'using Microsoft.Extensions.Logging.Abstractions;'in cs and 'using Microsoft.Extensions.Logging;'not in cs and 'ILogger<GameEngine> engineLogger ='in cs and 'using Microsoft.Extensions.Logging;'in ds
fix=cs.replace('using Microsoft.Extensions.Logging.Abstractions;','using Microsoft.Extensions.Logging;\nusing Microsoft.Extensions.Logging.Abstractions;',1);assert fix.replace('using Microsoft.Extensions.Logging;\n','',1)==cs
receipt_path=pathlib.Path('/tmp/1536-cloud-tools/carrier-recovery-c66d3359.json');receipt=json.loads(receipt_path.read_bytes());assert receipt['sha']==SHA and receipt['sourcePinsVerified']==101 and receipt['remoteShaVerified']and receipt['clean']and not receipt['localSourceOrObjectReuse']and receipt['changedPathsVerified']==len(receipt['files'])==19
for x in receipt['files']:
 b=get(x['path']);assert b==get(x['path'],root=C)and len(b)==x['bytes']and h(b)==x['sha256']
boundaries=[];frozen=None
for x in attempt['evidence']:
 b=get(x['path']);assert b==get(x['path'],root=C)and len(b)==x['bytes']and h(b)==x['sha256']
 if not x['path'].endswith('.json'):continue
 j=json.loads(b)
 if j.get('kind')=='L01_SUPPLEMENTAL_ARGV_STAT_PROCESS_ROOT_BOUNDARY':
  assert j['cleanBoundary']and not any(j[k]for k in ['activeTaskGameTestProcesses','unknown','ownedRoots']);assert j['observerSourceSha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f'and j['fixtureTemp']==cloud['crashDiagnostic1']['runNamespace']['tmpdir'];boundaries.append({'path':x['path'],'sha256':x['sha256'],'phase':j['phase'],'startedUtc':j['startedUtc'],'finishedUtc':j['finishedUtc'],'entries':len(j['entries']),'clean':True})
 if j.get('kind')=='READ_ONLY_FROZEN_CURRENT_CLOUD_ROOTS_SEPARATION':assert j['unchanged'];frozen={'path':x['path'],'sha256':x['sha256'],'verifiedFiles':j['verifiedFiles'],'checkedUtc':j['checkedUtc']}
assert len(boundaries)==2 and frozen is not None and cloud['qualifiedCases']['totalDistinctTargets']==52 and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN'
out={'kind':'INDEPENDENT_DIAGNOSTIC1_BUILD_FAILURE_CAUSAL_CHECK','carrier':SHA,'attemptSource':ATTEMPT,'archive':archive_ref,'recordSha256':attempt['recordSha256'],'nativeExit':1,'actualTestsExecuted':0,'wallSeconds':r['wallSeconds'],'compilerErrors':errors,'summary':summary,'sourcePinsVerified':101,'productDelta':23,'sourceFault':'Unqualified ILogger<GameEngine> in Crash.cs lacks using Microsoft.Extensions.Logging; another partial file imports it, but normal using directives do not extend to sibling source files.','oneLineImportInverseRestoresExactSource':True,'recovery':ref(receipt_path),'recoveredPathsVerified':19,'remoteScope':'Saved parent exact-remote/full GitHub recovery receipt; frozen Git bytes independently compared in both repositories.','boundaries':boundaries,'frozenRootsBeforePhase':frozen,'qualified':52,'old66529':'UNKNOWN/unavailable unchanged','findings':[],'scope':'No tests/build/discovery/observer/process or live frozen-root action, no repo writes. Build attempt source is not a successful compiled baseline.'}
p=D/'t179pd-ni-iobs-l01-diagnostic1-build-causal-independent-check-c66d3359-20261018.json';p.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(ref(p)))
