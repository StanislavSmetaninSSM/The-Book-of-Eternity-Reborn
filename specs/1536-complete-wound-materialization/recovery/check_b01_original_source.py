from pathlib import Path
import subprocess,json,hashlib,zipfile,ast,xml.etree.ElementTree as ET
R=Path('/workspace/The-Book-of-Eternity-Reborn');CL=Path('/workspace/1536-cloud-clean-restore');O=Path('/tmp/1536-cloud-tools/review');P='specs/1536-complete-wound-materialization/';D=P+'recovery/';C='5f82a65318a3845418ddb1966d00095c78f511e3';B='be99971f7fcce34aeca46d85f5b4a3a893ccc147';h=lambda b:hashlib.sha256(b).hexdigest()
def git(*a,repo=R):return subprocess.check_output(['git','-C',str(repo),*a])
def ref(p):
 p=Path(p);d=p.read_bytes();return dict(path=str(p),bytes=len(d),sha256=h(d))
checks=[]
def ck(n,v):
 checks.append(dict(check=n,passed=bool(v)))
 if not v:raise AssertionError(n)
srcpath=D+'l01-build-tooling-unit-once-20261018.py';src=git('show',C+':'+srcpath);ck('original runner exact hash',h(src)=='5ce605559a4cd11e87c0dce92c5284561cd60b0909785c6ed72584019b784ba4');ast.parse(src)
(O/'b01-original-unit-once-source-5f82a653.py').write_bytes(src)
x=json.loads(git('show',C+':'+D+'l01-build-tooling-observations-20261018.json'));q=x['readonlyQueries'];zraw=git('show',C+':'+q['artifact']);ck('query archive hash',h(zraw)==q['sha256'] and len(zraw)==q['bytes'])
import io
z=zipfile.ZipFile(io.BytesIO(zraw));ck('two original query members',sorted(z.namelist())==sorted(a['member'] for a in q['members']))
for a in q['members']:
 d=z.read(a['member']);ck('original query '+a['member'],h(d)==a['sha256'] and len(d)==a['bytes']);v=json.loads(d);ck('query native0 '+a['member'],v['nativeExit']==0)
v=json.loads(z.read('query.json'));ck('exact evaluated property/item record',json.loads(v['stdout'])==x['compilerConfiguration']['propertyEvaluation'])
for a in x['sourceBindings']:
 d=git('show',C+':'+a['path']);ck('source input '+a['path'],h(d)==a['sha256'] and len(d)==a['bytes'])
for node in x['projectGraphs']['unitBuildProjectReferenceClosure']:
 tree=ET.fromstring(git('show',C+':'+node['path']));refs=[]
 for e in tree.iter('ProjectReference'):refs.append(str((R/node['path']).parent.joinpath(e.attrib['Include'].replace('\\','/')).resolve().relative_to(R)))
 ck('full build graph '+node['path'],sorted(refs)==sorted(node['references']))
ck('five build nodes',len(x['projectGraphs']['unitBuildProjectReferenceClosure'])==5)
assets=json.loads((R/'BookOfEternityClient.Tests/obj/project.assets.json').read_text());packages=[key for key,row in assets['libraries'].items() if row['type']=='package'];ck('61 cached unit packages',len(packages)==61)
for key in packages:
 name,version=key.lower().split('/');ck('cached nupkg '+key,Path('/tmp/1536-cloud-tools/nuget',name,version,name+'.'+version+'.nupkg').is_file())
packet=json.loads(git('show',C+':'+D+'l01-delivery-d01-source-packet-20261018.json'));ck('149 D01 pins retained',len(packet['sourcePins'])==149 and git('show',B+':'+D+'l01-delivery-d01-source-packet-20261018.json')==git('show',C+':'+D+'l01-delivery-d01-source-packet-20261018.json'))
changes=git('diff','--name-only',B,C).decode().splitlines();ck('six new plan/evidence/script paths only',len(changes)==6 and all(p.startswith(P) for p in changes));ck('no existing source pins changed',not set(changes).intersection(a['path'] for a in packet['sourcePins']))
smoke=D+'l01-managed-stack-s01/smoke.py';ck('binding gap proven outside149',smoke not in [a['path'] for a in packet['sourcePins']] and b"d121731cff4867acf993d034962c9f5591fdcb496307a5fe0e9642b8392da333" not in src);ck('original helper currently accepted exact bytes',h(git('show',C+':'+smoke))=='d121731cff4867acf993d034962c9f5591fdcb496307a5fe0e9642b8392da333')
ck('sampling/guard unresolved FNF and resource errors visible in source',b'except FileNotFoundError:pass' in src and b'files[name]=type(error).__name__' in src);ck('comm-only initial census',b"comm not in ('dotnet','pwsh')" in src);ck('raw direct wait not signal-normalized',b"record['nativeExit']=child.wait(timeout=2)" in src and b'return lib[\'FINALIZE\']' in src)
ck('single pure build no test launch',b"build=[DOTNET,'build'" in src and b"command=['/usr/bin/timeout','--verbose','--signal=TERM','--kill-after=10s','600s',*build]" in src and b'start_new_session=True' in src and b'--artifacts-path' in src and b'--disable-build-servers' in src)
receiptpath=Path('/tmp/1536-cloud-tools/d01-carrier-recovery-5f82a653.json');rec=json.loads(receiptpath.read_text());ck('original recovery scope',rec['sha']==C and rec['remoteShaVerified'] and rec['clean'] and rec['changedPaths']==112 and rec['currentPinsVerified']==149 and not rec['productionChanges'])
for p in changes:
 d=git('show',C+':'+p,repo=CL);a=next(row for row in rec['files'] if row['path']==p);ck('recovered original '+p,len(d)==a['bytes'] and h(d)==a['sha256'])
result=dict(kind='B01_ORIGINAL_STATIC_AND_SAVED_QUERY_CHECKS',candidate=C,passed=len(checks),checks=checks,admission='HOLD_SOURCE_CORRECTIONS_REQUIRED',scope='Read-only Git/XML/JSON/ZIP/cache-file metadata and AST parse only; no helper/runtime/compiler/build/test/timeout invocation. Separate permitted dated proc/resource observations are documented in review.',receipt=ref(receiptpath),originalRunner=ref(O/'b01-original-unit-once-source-5f82a653.py'),utilityHistory=['Initial rg used absent optional Directory.* globs and exited2, so following query read did not run; query was subsequently read once with Python. No native phase or source mutation resulted.'])
p=O/'l01-build-tooling-b01-original-checks-20261018.json';p.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(ref(p)));print('passed',len(checks))
