from pathlib import Path
import gzip, hashlib, json, subprocess, sys
repo=Path(__file__).resolve().parents[3]; root=repo/'specs/1553-portable-local-storage/recovery/evidence'
def git(*args):return subprocess.check_output(['git',*args],cwd=repo)
def sha(b):return hashlib.sha256(b).hexdigest()
out={'Scope':'T042-INPUT-TRANSACTION only; historical repeated executions are not distinct coverage','Manifests':[],'Inputs':0,'Artifacts':0,'Gzip':0,'Summaries':[]}
for p in sorted(root.glob('t042-*/manifest.json')):
 m=json.loads(p.read_text()); source=m['SourceCommit']; inputs=0
 for x in m['Inputs']:
  b=git('show',source+':'+x['Path']); assert sha(b)==x['Sha256'],(p,x['Path']);assert git('rev-parse',source+':'+x['Path']).decode().strip()==x['GitBlob'];inputs+=1
 for x in m['Artifacts']:
  f=p.parent/x['Path'];b=f.read_bytes();assert len(b)==x['Bytes'];assert sha(b)==x['Sha256'],f
  if 'DecompressedSha256' in x:assert sha(gzip.decompress(b))==x['DecompressedSha256'];out['Gzip']+=1
  if f.name=='summary.json':
   s=json.loads(b); assert s['Revision']['Head']==source,(f,s['Revision']);assert s['Revision']['ChangedFiles']==0 or ((s['DiscoveryOnly'] or s['PlanOnly']) and s['Tests']['Executed']==0 and len(m.get('MetadataOnlyDirtyPaths',[]))==s['Revision']['ChangedFiles']);assert not s.get('TimedOut');assert s['OwnedTreeCleanupSucceeded'];assert not s.get('DuplicateTests');
   out['Summaries'].append({'Path':str(f.relative_to(repo)),'Source':source,'ExitCode':s['ExitCode'],'Tests':s.get('Tests'),'Selection':s.get('Selection'),'OwnedCleanup':s['OwnedTreeCleanupSucceeded']})
 out['Inputs']+=inputs;out['Artifacts']+=len(m['Artifacts']);out['Manifests'].append({'Path':str(p.relative_to(repo)),'Source':source,'Inputs':inputs,'Artifacts':len(m['Artifacts'])})
import xml.etree.ElementTree as ET
bridge=root/'t042-core-green/final-green/gm-bridge-prompt-operation-unit-001.trx'
daemon=list((root/'t042-terminal-final-green/terminal-final-green').glob('*.trx'))
def cases(path):return {e.attrib['testName'] for e in ET.parse(path).getroot().iter() if e.tag.endswith('UnitTestResult')}
bnames=cases(bridge);dnames=set().union(*(cases(p) for p in daemon))
assert len(bnames)==46 and len(dnames)==37 and not bnames.intersection(dnames)
out['LatestDistinctCoverage']={'Bridge':46,'DaemonAndExactIntegration':37,'Total':83,'Names':sorted(bnames|dnames),'Evidence':'core bridge TRX at773f5e9e + final daemon TRX atdb5864c2; unchanged bridge source reused'}
out['HistoricalExecuted']=sum((s.get('Tests') or {}).get('Executed',0) for s in out['Summaries']);out['HistoricalPassed']=sum((s.get('Tests') or {}).get('Passed',0) for s in out['Summaries']);out['HistoricalFailed']=sum((s.get('Tests') or {}).get('Failed',0) for s in out['Summaries']);out['Valid']=True
Path(sys.argv[1]).write_text(json.dumps(out,indent=2)+'\n');print(json.dumps({k:v for k,v in out.items() if k not in ['Manifests','Summaries']}))
