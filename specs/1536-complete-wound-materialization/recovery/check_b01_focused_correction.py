from pathlib import Path
import ast,json,hashlib,subprocess
R=Path('/workspace/The-Book-of-Eternity-Reborn');CL=Path('/workspace/1536-cloud-clean-restore');O=Path('/tmp/1536-cloud-tools/review');P='specs/1536-complete-wound-materialization/';D=P+'recovery/';C='7f6bb5c6250b107b2f943a7531ea3fcfbd623966';B='5f82a65318a3845418ddb1966d00095c78f511e3';H='b8182fb480ad46452010e31f395e9d2deac63c11d5e1e90d0c08c273e8387e6f';h=lambda b:hashlib.sha256(b).hexdigest()
def git(*a,repo=R):return subprocess.check_output(['git','-C',str(repo),*a])
def ref(p):
 p=Path(p);d=p.read_bytes();return dict(path=str(p),bytes=len(d),sha256=h(d))
checks=[]
def ck(n,v):
 checks.append(dict(check=n,passed=bool(v)))
 if not v:raise AssertionError(n)
path=R/D/'l01-build-tooling-unit-once-20261018.py';src=path.read_bytes();text=src.decode();ck('corrected exact runner hash',h(src)==H);tree=ast.parse(src);main=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='main')
ck('helper guard before root/import',text.index("assert sha(smoke.read_bytes())")==text.index("assert sha(smoke.read_bytes())") and text.index("assert sha(smoke.read_bytes())")<text.index('ROOT.mkdir')<text.index('runpy.run_path(str(smoke))'))
ck('accepted helper hash exact',"d121731cff4867acf993d034962c9f5591fdcb496307a5fe0e9642b8392da333" in text and h((R/D/'l01-managed-stack-s01/smoke.py').read_bytes())=='d121731cff4867acf993d034962c9f5591fdcb496307a5fe0e9642b8392da333')
ck('resource guard before root/launch',text.index('before_resources=resources_now()')<text.index('ROOT.mkdir')<text.index('subprocess.Popen'))
ck('proc exact executable+fallback double identity',all(t in text for t in ["os.readlink(p/'exe')","(p/'cmdline').read_bytes()","if int(again[19])!=tick:raise RuntimeError","if first_tick is not None and first_tick!=tick","if not argv0:raise RuntimeError"]))
ck('unresolved PID absence is not skipped',"if not p.exists():visibility['positivelyVanished']+=1;break" in text and "if attempt:raise RuntimeError('Still-present PID read unresolved:" in text)
ck('resource errors propagate and numeric fields validated',"files[name]=Path('/sys/fs/cgroup',name).read_text().strip()" in text and 'files[name]=type(error).__name__' not in text and "assert int(files['memory.current'])>=0" in text and "int(period)>0" in text)
ck('raw mount membership/node limits recorded',all(t in text for t in ['cgroupMembership=','cgroupMount=mount','mountNode=dict(device=node.st_dev,inode=node.st_ino)','effective per-build/ancestor limits and exclusive attribution are unproved']))
ck('samples and terminal failures hold observer scope',"record['observerErrors'].append('Sample: '" in text and "record['observerErrors'].append('Terminal observation: '" in text and "record['nativeExit']==0 and not record['observerErrors']" in text)
ck('raw signal outcome retained',"record['rawWrapperReturnCode']=record['nativeExit']" in text and "record['wrapperTerminationSignal']=-record['nativeExit']" in text and 'return 128-result if result<0 else result' in text)
# Exact existing finalizer and exact corrected final return expression; fake records only.
ftree=ast.parse((R/D/'t148-m01-run-phase-20261018.py').read_bytes());fn=next(n for n in ftree.body if isinstance(n,ast.FunctionDef) and n.name=='finalize');scope={};exec(compile(ast.Module(body=[fn],type_ignores=[]),'accepted-finalizer','exec'),scope)
ret=main.body[-1];assert isinstance(ret,ast.Return);normalize=compile(ast.Expression(ret.value),'corrected-controller-return','eval');proof=[]
for native in [None,0,7,-9]:
 for error in [False,True]:
  record=dict(nativeExit=native,observerErrors=['fake-observer'] if error else []);result=scope['finalize'](record,lambda _:None,lambda _:None);controller=eval(normalize,{},dict(result=result));expected=(137 if native==-9 else native) if native not in (None,0) else (2 if native is None or error else 0)
  ck('raw/native/controller '+str(native)+'/'+str(error),record['nativeExit']==native and controller==expected);proof.append(dict(rawWrapperReturnCode=native,observerError=error,controllerExit=controller))
ck('exact unchanged build command and timeout limits',all(t in text for t in ["--artifacts-path","--disable-build-servers","-p:GenerateDocumentationFile=true","-p:NoWarn=1591","-p:RestoreSources=/tmp/1536-cloud-tools/nuget","--kill-after=10s','600s'","start_new_session=True"]))
ck('no manual signal added',not any(isinstance(n,ast.Call) and isinstance(n.func,ast.Attribute) and n.func.attr in ['kill','terminate','killpg','pidfd_send_signal'] for n in ast.walk(tree)))
ck('one-shot root and review binding',"review['buildOnlyAdmission']=='ACCEPT_ONE_ISOLATED_UNIT_BUILD_ONLY'" in text and "review['buildScriptSha256']==sha(Path(__file__).read_bytes())" in text and 'ROOT.mkdir(mode=0o700)' in text)
probe=json.loads((R/D/'l01-build-tooling-corrected-preflight-probe-20261018.json').read_text());ck('saved source-bound read-only probe',probe['scriptSha256']==H and probe['buildLaunches']==0 and probe['processSummary']==[dict(comm='dotnet',state='Z',count=35)] and probe['visibility']['executableFallbacks']==5)
packet=R/D/'l01-delivery-d01-source-packet-20261018.json';p=json.loads(packet.read_text());ck('149 pins packet byte unchanged',len(p['sourcePins'])==149 and packet.read_bytes()==git('show',B+':'+D+'l01-delivery-d01-source-packet-20261018.json'))
changes=git('diff','--name-only',B,C).decode().splitlines();ck('only bounded ten paths',len(changes)==10 and all(z.startswith(P) for z in changes) and not set(changes).intersection(z['path'] for z in p['sourcePins']))
for name in ['l01-build-tooling-b01-original-independent-review-20261018.json','l01-build-tooling-b01-original-independent-review-20261018.md','l01-build-tooling-b01-original-checks-20261018.json','l01-build-tooling-b01-independent-observations-20261018.json','check_b01_original_source.py']:ck('original review copy '+name,(R/D/name).read_bytes()==(O/name).read_bytes())
receiptp=Path('/tmp/1536-cloud-tools/d01-carrier-recovery-7f6bb5c6.json');rec=json.loads(receiptp.read_text());ck('corrected carrier receipt',rec['sha']==C and rec['remoteShaVerified'] and rec['clean'] and rec['changedPaths']==118 and rec['currentPinsVerified']==149 and rec['historicalLedgersUnchanged'] and not rec['productionChanges'])
for path2 in changes:
 d=(R/path2).read_bytes();row=next(z for z in rec['files'] if z['path']==path2);ck('recovered '+path2,d==(CL/path2).read_bytes() and len(d)==row['bytes'] and h(d)==row['sha256'])
ck('writer and restore clean exact',git('rev-parse','HEAD').decode().strip()==git('rev-parse','HEAD',repo=CL).decode().strip()==C and not git('status','--porcelain') and not git('status','--porcelain',repo=CL))
ck('one-shot diagnostic root not consumed',not Path('/tmp/l01bd-772d38df9b5b4cf8').exists())
result=dict(kind='B01_FOCUSED_CORRECTED_SOURCE_CHECKS',candidate=C,buildScriptSha256=H,passed=len(checks),checks=checks,findings=[],rawExitProof=proof,scope='Static source/hash/Git/recovery checks plus AST-exact finalizer/return expression with fake records. No main, new census, build, runtime target, timeout or signals executed.',receipt=ref(receiptp),probe=ref(R/D/'l01-build-tooling-corrected-preflight-probe-20261018.json'))
p=O/'l01-build-tooling-b01-focused-checks-20261018.json';p.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(ref(p)));print('passed',len(checks))
