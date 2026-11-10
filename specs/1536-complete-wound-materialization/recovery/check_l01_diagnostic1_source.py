#!/usr/bin/env python3
"""Static, read-only SOURCE review. No controllers, observers or native work are invoked."""
import ast,hashlib,json,pathlib,re,subprocess,sys,datetime
R=pathlib.Path('/workspace/The-Book-of-Eternity-Reborn');C=pathlib.Path('/workspace/1536-cloud-clean-restore');D=pathlib.Path('/tmp/1536-cloud-tools/review')
REL='specs/1536-complete-wound-materialization/recovery/'
BASE='6a3f88fd4aa5d155fd971ba144cc823adc06941b'
SHA=sys.argv[1];receipt_path=pathlib.Path(sys.argv[2]);checks=[];findings=[]
def h(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def git(*args):return subprocess.check_output(['git','-C',str(R),*args])
def obj(sha,path):return git('show',sha+':'+path)
def check(value,label):
 checks.append({'check':label,'passed':bool(value)})
 if not value:findings.append(label)
def file_ref(path):
 p=pathlib.Path(path);b=p.read_bytes();return {'path':str(p),'bytes':len(b),'sha256':h(b)}
p=json.loads(obj(SHA,REL+'t179pd-ni-iobs-l01-source-packet-20261018.json'));old=json.loads(obj(BASE,REL+'t179pd-ni-iobs-l01-source-packet-20261018.json'))
check(p['sourcePinCount']==len(p['sourcePins'])==101,'101 exact pins')
check(h(json.dumps(p['sourcePins'],sort_keys=True,separators=(',',':')).encode())==p['pinDigest'],'canonical pin digest')
tree={}
for line in git('ls-tree','-r',SHA).decode().splitlines():
 meta,path=line.split('\t');mode,kind,oid=meta.split();tree[path]=(mode,oid)
for x in p['sourcePins']:
 b=obj(SHA,x['path']);check(len(b)==x['bytes'] and h(b)==x['sha256'] and blob(b)==x['gitBlob'] and tree[x['path']]==(x['mode'],x['gitBlob']) and (R/x['path']).read_bytes()==b and (C/x['path']).read_bytes()==b,'pin '+x['path'])
oldpins={x['path']:x for x in old['sourcePins']};newpins={x['path']:x for x in p['sourcePins']}
newpaths=sorted(set(newpins)-set(oldpins));changed=sorted(x for x in set(oldpins)&set(newpins) if oldpins[x]['sha256']!=newpins[x]['sha256'])
crash='BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrash.cs';diag='BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrashDiagnostic.cs';sel='tests/selections/1536-joint-interface-crash-diagnostic1.json'
check(newpaths==sorted([diag,sel]) and not(set(oldpins)-set(newpins)),'two added closure files; no removed pins')
check(changed==sorted([crash,'tests/categories.json',REL+'t179pd-ni-iobs-l01-process-preparation-run.py',REL+'t179pd-ni-iobs-l01-process-first-run.py']),'only four existing pinned contents changed')
product=git('diff','--name-only',p['productBase'],SHA,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()
check(product==p['productDelta']==old['productDelta'] and len(product)==23,'product23 unchanged')
check(not git('diff','--name-only',BASE,SHA,'--','BookOfEternityClient','BookOfEternityGMBridge').strip(),'no production change')
for path in ['tests/selection.json','tests/selections/1536-joint-interface-lifecycle-stage.json',p['ledger']['path'],'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SpiritualContinuation.cs']:
 check(obj(BASE,path)==obj(SHA,path),'unchanged '+path)
check(h(obj(SHA,p['ledger']['path']))==p['ledger']['sha256']=='85e23e3546315a04c0f3317433fb3662ea8c893d65850b59e71a9d61e5f418cc','fixed authored method-row ledger')
before=obj(BASE,crash).decode();after=obj(SHA,crash).decode()
inverse=after.replace('string? fault = null, bool diagnostic = false)','string? fault = null)')
inverse=inverse.replace(', Fault = fault, Diagnostic = diagnostic }',', Fault = fault }')
inverse=inverse.replace('        var diagnostic = new JointInterfaceResumeDiagnostic(parent["Diagnostic"]?.GetValue<bool>() == true,\n            cut, root, parent["Nonce"]!.GetValue<string>());\n','')
inverse=''.join(line for line in inverse.splitlines(keepends=True) if not any(t in line for t in ['diagnostic.Observe(', 'ILogger<GameEngine> engineLogger =', 'if (diagnostic) Assert.Null(']))
inverse=inverse.replace('fileSystem: files, logger: engineLogger,','fileSystem: files, logger: log,').replace('IntegrityGuardEntries = integrityGuardEntries, DiagnosticOutputFailure = diagnostic.OutputFailure,','IntegrityGuardEntries = integrityGuardEntries,')
check(inverse==before,'exact inverse restores original entire Crash.cs bytes')
assert_count=lambda s:len(re.findall(r'\bAssert\.\w+\(',s))
check(assert_count(after)==assert_count(before)+1,'all original Assert invocations retained plus1 diagnostic-output check')
check('WaitAsync(TimeSpan.FromSeconds(220))' in after,'unchanged220-second native child bound')
ds=obj(SHA,diag).decode();method='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss'
check(ds.count('[Fact]')==1 and '=> RunJointInterfaceActualProcessLossAsync(1, diagnostic: true);' in ds,'single diagnostic Fact fixed cut1')
check(all(t in ds for t in ['Console.Out.Flush();','ReadJointInterfaceLinuxBirthTick(Environment.ProcessId)','Stopwatch.StartNew()','DateTimeOffset.UtcNow','Cut = cut, Root = root, Nonce = nonce','catch (Exception error) when (error is IOException or ObjectDisposedException)','OutputFailure ??=','retained.Log(logLevel, eventId, state, exception, (_, _) => message);']),'scalar flushed identity diagnostics and primary-output failure preservation')
check(not any(t in ds for t in ['AcquireCanonical','ReadFile','WriteFile','Recover','Kill(','Environment.Exit','Task.Delay','Task.Run','SetPrivateField']), 'no new game filesystem/authority/process intervention in diagnostic implementation')
check(ds.count('formatter(state, exception)')==1 and 'retained.IsEnabled(logLevel)' in ds,'adapter retains original formatting once and logger filter')
cats=json.loads(obj(SHA,'tests/categories.json'));oldcats=json.loads(obj(BASE,'tests/categories.json'));cid='fr063-joint-interface-crash-diagnostic';newcat=[c for c in cats['categories'] if c['id']==cid]
check(len(newcat)==1 and [c for c in cats['categories'] if c['id']!=cid]==oldcats['categories'],'only one dedicated catalog owner added')
check(newcat[0]['timeoutMinutes']==15,'diagnostic preserves agreed900-second outer ceiling')
check(newcat[0]['selectors']==[{'project':'integration','tests':[method]}],'exact diagnostic method selector')
selection=json.loads(obj(SHA,sel));whole=json.loads(obj(SHA,'tests/selection.json'))
check(len(selection['selections'])==1 and selection['selections'][0]['category']==cid and len(whole['selections'])==16 and cid not in {x['category'] for x in whole['selections']},'separate diagnostic1 excluded from whole57')
tools={}
for which in ['preparation','first']:
 path=REL+'t179pd-ni-iobs-l01-process-'+which+'-run.py';src=obj(SHA,path).decode();ast.parse(src);tools[which]=src
 check("len(binding['sourcePins'])==101 and len(binding['productDelta'])==23" in src and "==binding['pinDigest']" in src,'controller101/23/digest '+which)
 check("(out/'record.json').write_text" in src and src.index("(out/'record.json').write_text")<src.index('# Original outcome' if which=='preparation' else '# Semantic methods'),'original record before classification '+which)
check("mode in {'catalog','plan','diagnostic-plan'}" in tools['preparation'] and 'expected_descriptors,expected_methods,expected_cases=16,33,57' in tools['preparation'] and 'expected_descriptors,expected_methods,expected_cases=1,1,1' in tools['preparation'],'ZERO3 exact modes/counts')
check("groups={'diagnostic1':(['fr063-joint-interface-crash-diagnostic'],1)}" in tools['first'] and "prep['mode']=='diagnostic-plan'" in tools['first'] and "'-NoBuild'" in tools['first'],'FIRST only diagnostic1 from its fresh NoBuild plan')
check("all_before==prep['allOutputInventory']" in tools['first'] and "before==prep['binaryAfter']" in tools['first'] and "with sentinel.open('x')" in tools['first'],'inventory continuity and exclusive first sentinel')
orchestration=p['diagnosticOrchestration'];ws=obj(SHA,orchestration['path']);ast.parse(ws.decode());envref=orchestration['environmentShell'];env=obj(SHA,envref['path']).decode()
check(h(ws)==orchestration['sha256'] and len(ws)==orchestration['bytes'],'wrapper separate exact hash binding')
check(h(env.encode())==envref['sha256'] and len(env.encode())==envref['bytes'],'environment shell exact hash binding')
exports={}
for line in env.splitlines():
 if line.startswith('export '):key,value=line[7:].split('=',1);exports[key]=value
cloud=json.loads(obj(SHA,REL+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));ns=cloud['crashDiagnostic1']['runNamespace'];root=ns['tmpdir']
check(exports['TMPDIR']==root and exports['XDG_RUNTIME_DIR']==root+'/runtime' and all(exports[x].startswith(root+'/tooling/') for x in ['DOTNET_CLI_HOME','XDG_CACHE_HOME','XDG_CONFIG_HOME','XDG_DATA_HOME']) and 'HOME' not in exports,'effective new private mutable namespace, HOME unchanged')
check(ns['samePhysicalCloud'] and len(ns['frozenRootsOutsideMutableNamespace'])==2 and all(not pathlib.Path(x).is_relative_to(root) and not pathlib.Path(root).is_relative_to(x) for x in ns['frozenRootsOutsideMutableNamespace']),'frozen/new namespaces disjoint; same physical cloud recorded')
check(all(t in ws.decode() for t in ["os.environ['TMPDIR']==root",'actual_files==set(files)',"hashlib.sha256(b).hexdigest()==entry['sha256']","assert census('before')==0",'finally:after=census(\'after\')',"mode in {'catalog','plan','diagnostic-plan'}","kind=='actual' and mode=='diagnostic1'"]),'wrapper one-phase guard frozen set/bytes and dated pre/post global census')
check(p['mandatorySupplement']['sha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f','unchanged mandatory v2 observer')
oldcloud=json.loads(obj(BASE,REL+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
check(cloud['qualifiedCases']==oldcloud['qualifiedCases'] and cloud['qualifiedCases']['totalDistinctTargets']==52 and cloud['actuals']==oldcloud['actuals'],'qualified52 and all original actual outcomes unchanged')
for suffix,oldpath in [('source-packet-pre-diagnostic1-source-20261018.json','source-packet-20261018.json'),('cloud-qualification-pre-diagnostic1-source-20261018.json','cloud-qualification-20261018.json')]:
 check(obj(SHA,REL+'t179pd-ni-iobs-l01-'+suffix)==obj(BASE,REL+'t179pd-ni-iobs-l01-'+oldpath),'exact prior snapshot '+suffix)
receipt=json.loads(receipt_path.read_bytes());check(receipt['sha']==SHA and receipt['remoteShaVerified'] and receipt['clean'] and not receipt['localSourceOrObjectReuse'] and receipt['sourcePinsVerified']==101,'saved exact remote/full independent clean clone receipt')
for x in receipt['files']:
 b=(C/x['path']).read_bytes();check(len(b)==x['bytes'] and h(b)==x['sha256'] and blob(b)==x['gitBlob'] and b==obj(SHA,x['path'])== (R/x['path']).read_bytes(),'recovered '+x['path'])
check(len(receipt['files'])==receipt['changedPathsVerified'],'recovery complete path inventory')
result={'kind':'INDEPENDENT_DIAGNOSTIC1_STATIC_SOURCE_CHECK','candidate':SHA,'base':BASE,'sourcePins':101,'pinDigest':p['pinDigest'],'productDelta':23,'newPinnedPaths':newpaths,'changedExistingPinnedPaths':changed,'exactCrashInverse':inverse==before,'originalAssertCalls':assert_count(before),'newAssertCalls':assert_count(after),'whole':[16,33,57],'diagnostic':[1,1,1],'diagnosticCountsToward57':False,'categorySeconds':newcat[0]['timeoutMinutes']*60,'childSeconds':220,'recovery':file_ref(receipt_path),'recoveryChangedPaths':len(receipt['files']),'remoteScope':'Remote verification attested by saved receipt; restored bytes independently checked here.','orchestration':orchestration,'qualifiedTargets':52,'nativeExecutedByReviewer':False,'checksPassed':sum(x['passed'] for x in checks),'findings':findings,'metadataAcceptanceConditions':['Current causal/correction handoff must point to frozen crash5 and diagnostic1; retain old objects as explicitly historical.','Prior selected-routing SOURCE/PREP/next-contract fields must be historical, with current101/product23 ZERO3 and NONE actual release clear.','After review, save/push/exactremote/recover/read back only named catalog/whole-plan/diagnostic-plan ZERO3 labels before any ZERO; actual needs independent fresh PREP and a separate one-mode release.']}
dest=D/f't179pd-ni-iobs-l01-diagnostic1-source-independent-check-{SHA[:8]}-20261018.json';dest.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({'report':str(dest),'sha256':h(dest.read_bytes()),'checksPassed':result['checksPassed'],'findings':findings}));sys.exit(bool(findings))
