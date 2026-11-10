"""Focused static correction/readback; consumes independent pure fake result only."""
import ast,hashlib,json,pathlib,subprocess
R=pathlib.Path('/workspace/The-Book-of-Eternity-Reborn');C=pathlib.Path('/workspace/1536-cloud-clean-restore');D=pathlib.Path('/tmp/1536-cloud-tools/review');Q=pathlib.Path('specs/1536-complete-wound-materialization/recovery');P=Q/'l01-delivery-d02';BASE='96f08c8bb2d534c00bcbca4ff5a279554445d58d';SHA='5efddae52f18d723b8e8365127ef88aac6b04bfb'
def h(b):return hashlib.sha256(b).hexdigest()
def git(*a):return subprocess.check_output(['git','-C',str(R),*a])
def read(p):return json.loads(p.read_bytes())
checks=[]
def ck(n,v):checks.append(dict(name=n,passed=bool(v)))
old=git('show',BASE+':'+str(P/'phase.py'));new=(R/P/'phase.py').read_bytes();bindings=(R/P/'bindings.json').read_bytes()
ck('exact accepted correction bytes',h(new)=='fc8a9f070f20528b3942584ca2248cb1b25f4ca3168cd5ba9414791d84c5986e')
oldline="    lib['write'](phase/(name+'.record.json'),result)\n";newlines="    # Use the accepted M01/S01 publication path; observer faults preserve native.\n    lib['FINALIZE'](result,lambda value:lib['write'](phase/(name+'.record.json'),value),lambda value:None)\n"
ck('exact inverse restores entire original controller',new.decode().replace(newlines,oldline)==old.decode() and new.decode().count(newlines)==1)
a={n.name:ast.dump(n,include_attributes=False) for n in ast.parse(old).body if isinstance(n,ast.FunctionDef)};b={n.name:ast.dump(n,include_attributes=False) for n in ast.parse(new).body if isinstance(n,ast.FunctionDef)}
ck('only run_owned function changed',set(a)==set(b) and {k for k in a if a[k]!=b[k]}=={'run_owned'})
ck('bindings unchanged',h(bindings)=='a7d62aae8152516210aec579d12ac0d01bf990880b1650f6cd4ba8445a98e12e' and bindings==git('show',BASE+':'+str(P/'bindings.json')))
packet=read(R/Q/'l01-delivery-d01-source-packet-20261018.json');changes=git('diff','--name-only',BASE,SHA).decode().splitlines()
ck('149 pins and source packet untouched',len(packet['sourcePins'])==149 and not({x['path'] for x in packet['sourcePins']}&set(changes)) and (R/Q/'l01-delivery-d01-source-packet-20261018.json').read_bytes()==git('show',BASE+':'+str(Q/'l01-delivery-d01-source-packet-20261018.json')))
for name,sha in [('l01-delivery-d02-source-original-independent-review-20261018.json','51c4b4afb23d13f6ef6d4dfd8ac72307d0a6e02c37a6943a77ff582992ef8079'),('l01-delivery-d02-source-original-independent-review-20261018.md','2d44bd4deb338f6e84e5ca45461de4009013792f9e5832d37e2e779a91ef980c'),('l01-delivery-d02-source-original-checks-20261018.json','bcd8a79b4579f90b65eb071d0252814fb31b9af47b1e85b52fc00dbf07eb28dd'),('check_d02_original_source.py','ec405adb65ca8d585f0d74d73decca93f2fa4e5d2bf8202f0b5c61690940917a')]:ck('preserved original '+name,h((R/Q/name).read_bytes())==sha and (R/Q/name).read_bytes()==(D/name).read_bytes())
reg=read(D/'d02-corrected-native-accounting-independent-20261018.json');green=read(R/P/'native-accounting-green.json');red=read(R/P/'native-accounting-red.json')
ck('independent fake replay exactly matches saved green',reg==green and reg['passed']==12 and reg['failed']==[] and reg['nativeActions']==0)
ck('original red eight successes/four persistence failures retained',red['controllerSha256']==h(old) and red['passed']==8 and len(red['failed'])==4 and all(x['fault']=='record-persist' for x in red['failed']))
m01row=next(x for x in packet['sourcePins'] if x['path'].endswith('/t148-m01-run-phase-20261018.py'))
ck('actual pinned M01 finalizer used',reg['finalizerSourceSha256']==m01row['sha256']==h((R/m01row['path']).read_bytes()) and "name=='finalize'" in (R/P/'native-accounting-check.py').read_text())
ck('known nonzero native priority all faults',all(x['returnedNative']==x['raw']==x['native']==x['finalizerExit'] for x in reg['cases'] if x['native']!=0))
ck('native zero observer failure holds',all(x['returnedNative']==x['raw']==0 and x['finalizerExit']==2 and x['observerErrors'] for x in reg['cases'] if x['native']==0 and x['fault']!='none'))
ck('negative raw signal preserved',all(x['signal']==9 for x in reg['cases'] if x['native']==-9) and 'return 128-result if result<0 else result' in new.decode())
ck('zero gates still closed until saved acceptance',read(R/P/'control.json')['source']['status']=='HOLD_SOURCE_REQUIRED' and read(R/P/'control.json')['synthetic']['status']=='HOLD_PREP_REQUIRED')
receiptpath=pathlib.Path('/tmp/1536-cloud-tools/d01-carrier-recovery-5efddae5.json');receipt=read(receiptpath)
ck('corrected recovery exact141paths149pins',receipt['sha']==SHA and receipt['remoteShaVerified'] and receipt['clean'] and receipt['changedPaths']==141 and receipt['currentPinsVerified']==149 and not receipt['productionChanges'])
ck('changed files exact saved/GitHub restore',all((R/p).read_bytes()==git('show',SHA+':'+p)==(C/p).read_bytes() for p in changes))
ck('writer and restored HEAD clean',git('rev-parse','HEAD').decode().strip()==SHA and not git('status','--porcelain').strip() and subprocess.check_output(['git','-C',str(C),'rev-parse','HEAD']).decode().strip()==SHA and not subprocess.check_output(['git','-C',str(C),'status','--porcelain']).strip())
result=dict(kind='D02_FOCUSED_PUBLICATION_CORRECTION_CHECKS',candidate=SHA,originalCandidate=BASE,checks=checks,passed=sum(x['passed'] for x in checks),failed=[x for x in checks if not x['passed']],pureRegressionCases=12,nativeActions=0,controllerSha256=h(new),bindingsSha256=h(bindings),recoveryReceiptSha256=h(receiptpath.read_bytes()),checkerSha256=h(pathlib.Path(__file__).read_bytes()))
p=D/'l01-delivery-d02-source-focused-checks-20261018.json';p.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['passed','failed','pureRegressionCases','controllerSha256','bindingsSha256']}));print('sha256',h(p.read_bytes()))
