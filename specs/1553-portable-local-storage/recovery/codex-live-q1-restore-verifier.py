# Source/evidence-only restoration. No CLI or test execution. Adapted accepted desktop verifier.
from pathlib import Path
import subprocess,json,os,sys,hashlib
source=Path(sys.argv[4]) if len(sys.argv)>4 else Path('/workspace/native-1553'); restored=Path(sys.argv[2]); expected=sys.argv[1]
def git(where,*args): return subprocess.check_output(['git','-C',str(where),*args])
for r in [source,restored]:
 assert git(r,'rev-parse','HEAD').decode().strip()==expected
 assert not git(r,'status','--porcelain')
assert git(restored,'remote','get-url','origin').decode().strip()=='https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git'
assert git(restored,'rev-parse','--is-shallow-repository').decode().strip()=='false'
assert not (restored/'.git/objects/info/alternates').exists()
tree=git(restored,'rev-parse','HEAD^{tree}').decode().strip(); parent=git(restored,'rev-parse','HEAD^').decode().strip()
assert tree==git(source,'rev-parse','HEAD^{tree}').decode().strip()
assert parent==git(source,'rev-parse','HEAD^').decode().strip()
ancestors=['13cdd9aedcacaba62e76859c16940583d285c365','63e990097570f920dfe615e7fdf1f3542c7a6eb9','b9b3aeb3d8dddc45385aeea319301e6e21eb22c3','428127226308b98b630074e05a839200dad748c0','a8aa533fab866260d7a01a6d1499002c3d695c0a','65674ee83ddeac9c63e368334ea170cf9b700c54','04c9717a5290d001816fb394c276c3a2754f3dad','bd17aa1f5d3e6b55dca4a2abbc0f74552d400a49','213f41f935590da6978e4c420ede63d3ecb8234a','499ca652f367ec7dc5da5e85abb731793e5e47a5','cf787a8f7873e83e7c02e96504ad8e7ff2d9870c','113edbb00eeeb9172ee3a02e00585f7bde442243','d73e2cdf43d63cbda3e9cda033834d0e902ae734','953c48f81f06ef78010bf668133e2b974934e142','23a5b6695a34752005ffaff6f5d5aedc2ccca797','dc62a88a630ba20eacd65f7bdab138ba129123ff','4d456d5d5e12289d6fc97e5b2e7a0c523207bc08','fc49f271f5cd14ac3b24931cce2c98ef64a61068','14888663d608355098cc1a329d97ab61de0e6016','1bc9d67536dccbcc6672d8e7cd71ad885c2a946c','b5255b1fb96e405eb1ed195c6437b16ad5788246']
for a in ancestors: subprocess.run(['git','-C',str(restored),'merge-base','--is-ancestor',a,expected],check=True)
batch=subprocess.Popen(['git','-C',str(restored),'cat-file','--batch'],stdin=subprocess.PIPE,stdout=subprocess.PIPE); count=0
for entry in git(restored,'ls-tree','-rz','--full-tree','HEAD').split(b'\0'):
 if not entry: continue
 metadata,path=entry.split(b'\t',1); mode,kind,oid=metadata.split(); assert kind==b'blob'
 batch.stdin.write(oid+b'\n'); batch.stdin.flush(); returned,kind,size=batch.stdout.readline().split(); assert returned==oid and kind==b'blob'
 contents=batch.stdout.read(int(size)); assert batch.stdout.read(1)==b'\n'
 for checkout in [source,restored]:
  f=checkout/os.fsdecode(path); actual=os.fsencode(os.readlink(f)) if mode==b'120000' else f.read_bytes(); assert actual==contents, str(f)
 count+=1
batch.stdin.close(); assert batch.wait()==0
feature=restored/'specs/1553-portable-local-storage'; q=json.load(open(feature/'recovery/codex-live-q1-qualification.json'))
for pin in q['SourcePins']: assert hashlib.sha256((restored/pin['Path']).read_bytes()).hexdigest()==pin['SHA256']
for pin in q['Artifacts']:
 p=feature/pin['Path']; assert p.stat().st_size==pin['Bytes']; assert hashlib.sha256(p.read_bytes()).hexdigest()==pin['SHA256']
native=json.load(open(feature/'recovery/evidence/codex-q1/native-inputs.json'))
for pin in native['Pins']:
 p=restored/pin['Path']; assert p.stat().st_size==pin['Bytes']; assert hashlib.sha256(p.read_bytes()).hexdigest()==pin['SHA256']
for pin in q['SourcePins']:
 assert git(restored,'rev-parse',expected+':'+pin['Path']).decode().strip()==pin['GitBlob']
 assert (restored/pin['Path']).stat().st_size==pin['Bytes']
subprocess.run(['git','-C',str(restored),'fsck','--full','--no-dangling'],check=True)
remote=git(source,'ls-remote','origin','refs/heads/codex/1553-load-filesystem').split()[0].decode(); assert remote==expected
assert not git(source,'status','--porcelain') and not git(restored,'status','--porcelain')
proof={'Commit':expected,'Tree':tree,'Parent':parent,'RestoredDirectory':str(restored),'Transport':'fresh clone from GitHub HTTPS only; no local source/object/cache reuse','Origin':git(restored,'remote','get-url','origin').decode().strip(),'Clean':True,'TrackedFilesByteCompared':count,'Comparison':'restored and writer bytes each match independent GitHub-fetched blobs','FullFsck':True,'Shallow':False,'GitAlternatesPresent':False,'RemoteAfterRestoration':remote,'AcceptedAncestors':ancestors,'SourcePinsVerified':len(q['SourcePins']),'EvidenceArtifactsVerified':len(q['Artifacts']),'AcceptedNativeInputArtifactsVerified':len(native['Pins']),'QualificationScope':q['Verdict'],'TestsExecutedForRestoration':0,'CLIOrServicesStarted':0}
Path(sys.argv[3]).write_text(json.dumps(proof,indent=2)+'\n'); print(json.dumps(proof))
