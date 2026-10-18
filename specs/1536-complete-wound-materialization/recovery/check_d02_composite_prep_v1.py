"""Read-only D02 original PREP evidence; no imported controller or native SDK."""
from pathlib import Path
import hashlib, json, re, stat, subprocess, zipfile

REPO=Path('/workspace/The-Book-of-Eternity-Reborn')
CLONE=Path('/workspace/1536-cloud-clean-restore')
REC=REPO/'specs/1536-complete-wound-materialization/recovery'
OUT=Path('/tmp/1536-cloud-tools/review')
CANDIDATE='74105ae6165259051810898e00a6466d4c2bc53e'
SOURCE='3a36b1f1b790205099f6c940b0e362e456fa0975'
CONTROLLER='42837634637f1e0ee5c3752147263bdd7f5388775abf741c033ff55f937e26b6'
BINDINGS='0565587f35dd6fca647e0235141ffe9f5cc98f0ac3485173d2f286ec239d33ff'
EXPECTED_RECORDS={
 'catalog':'6b80d4932680e8a18e427dd66af326a2a609a830c94425c7be51f7e5d46412d4',
 'plan':'eb1a7764c0d5e04382ab6c24d429977e6d83c13cd7dfecc4dcd9e2d764525de9',
 'prepare':'72aa1ee64d8d4227b0e4e5dba5ab99d08ff2bcd7306b499c1776c3639b2aac57'}
checks=[]
def check(name,condition,detail=None):
 row={'name':name,'passed':bool(condition)}
 if detail is not None:row['detail']=detail
 checks.append(row)
def sha(data):return hashlib.sha256(data).hexdigest()
def read(p):return Path(p).read_bytes()
def obj(p):return json.loads(read(p))
def git(*args,cwd=REPO):return subprocess.check_output(['git','-C',str(cwd),*args])
def match(row,path=None):
 p=Path(path or row['path'])
 return p.is_file() and not p.is_symlink() and len(read(p))==row['bytes'] and sha(read(p))==row['sha256']
def image_tree(root):
 result=[]
 for p in sorted(root.rglob('*')):
  if p.is_symlink():raise AssertionError('Unexpected symlink '+str(p))
  if p.is_file():result.append({'path':str(p.relative_to(root)),'bytes':p.stat().st_size,'sha256':sha(read(p)),'mode':stat.S_IMODE(p.stat().st_mode)})
 return result

index_path=REC/'l01-delivery-d02-composite-prep-20261018.json'
index=obj(index_path);bindings=obj(REC/'l01-delivery-d02/bindings.json');root=Path(bindings['root'])
control=obj(REC/'l01-delivery-d02/control.json');packet=obj(REC/'l01-delivery-d01-source-packet-20261018.json')
receipt_path=Path('/tmp/1536-cloud-tools/d01-carrier-recovery-74105ae6.json');receipt=obj(receipt_path)
check('current and independent restored HEAD exact/clean',all(git('rev-parse','HEAD',cwd=p).decode().strip()==CANDIDATE and not git('status','--porcelain',cwd=p).strip() for p in [REPO,CLONE]))
check('GitHub recovery receipt exact remote, full clone,149pins,153paths, no production change',receipt['sha']==CANDIDATE and receipt['remoteShaVerified'] and receipt['clean'] and not receipt['localWriterObjectsCopied'] and receipt['currentPinsVerified']==149 and receipt['changedPaths']==153 and not receipt['productionChanges'])
changed=git('diff','--name-only',SOURCE,CANDIDATE).decode().splitlines()
check('only four new PREP evidence/control/handoff paths',set(changed)=={str(p.relative_to(REPO)) for p in [index_path,REPO/index['archive']['path'],REC/'l01-delivery-d02/control.json',REC.parent/'plan.md']} )
check('new carrier bytes exact Git and independent clone',all(read(REPO/p)==read(CLONE/p)==git('show',CANDIDATE+':'+p) for p in changed))
check('accepted controller and bindings unchanged',sha(read(REC/'l01-delivery-d02/phase.py'))==CONTROLLER and sha(read(REC/'l01-delivery-d02/bindings.json'))==BINDINGS and git('show',SOURCE+':'+str((REC/'l01-delivery-d02/phase.py').relative_to(REPO)))==read(REC/'l01-delivery-d02/phase.py'))
check('149 source pins current/restored exact',len(packet['sourcePins'])==149 and all(match(x,REPO/x['path']) and match(x,CLONE/x['path']) for x in packet['sourcePins']))
check('pin packet unchanged from admitted source',read(REC/'l01-delivery-d01-source-packet-20261018.json')==git('show',SOURCE+':'+str((REC/'l01-delivery-d01-source-packet-20261018.json').relative_to(REPO))))
check('SOURCE acceptance exact and gate consumed; synthetic held',sha(read(REPO/control['source']['reviewPath']))==control['source']['reviewSha256'] and obj(REPO/control['source']['reviewPath'])['verdict']=='ACCEPT_D02_COMPOSITE_SOURCE_ZERO_ONLY' and control['source']['status']=='SOURCE_ACCEPTED_ZERO_PERMITS_CONSUMED_PREP_PENDING' and control['synthetic']['status']=='HOLD_PREP_REQUIRED')
check('gameplay and crash5 NONE',control['gameplayRelease']==control['crash5Release']==index['gameplayRelease']==index['crash5Release']=='NONE')
check('all376 bound SDK/package/helper inputs exact',len(bindings['inputs'])==376 and all(match(x) for x in bindings['inputs']))
inventory=obj(REC/'l01-managed-stack-capability-inventory-20261018.json')
check('existing172 tool/reference inputs exact',len(inventory['availableFiles'])==9 and len(inventory['net8ReferenceInputs'])==163 and all(match(x) for x in inventory['availableFiles']+inventory['net8ReferenceInputs']))
archive=REPO/index['archive']['path'];rawzip=read(archive)
check('archive exact hash/size/Git/restored',len(rawzip)==1128968==index['archive']['bytes'] and sha(rawzip)==index['archive']['sha256']=='86901d62a702c3e02c0ea1b58863b7838bd2159e89463db06790929b61971169' and rawzip==read(CLONE/index['archive']['path'])==git('show',CANDIDATE+':'+index['archive']['path']))
z=zipfile.ZipFile(archive);members=index['archive']['members']
check('exact50 unique archived names, no TRX',len(members)==len(z.namelist())==len(set(z.namelist()))==50 and set(z.namelist())=={x['member'] for x in members} and not any(x.endswith('.trx') for x in z.namelist()))
for row in members:
 data=z.read(row['member'])
 check('raw chain '+row['member'],len(data)==row['bytes'] and sha(data)==row['sha256'] and data==read(row['originalPath']))
records={name:json.loads(z.read(name+'/phase/record.json')) for name in EXPECTED_RECORDS}
identities=[]
for name,record in records.items():
 check(name+' exact original record hash',sha(z.read(name+'/phase/record.json'))==EXPECTED_RECORDS[name]==index['phases'][name]['sha256']==control['source']['zeroRecords'][name])
 check(name+' source/native/observer/ZERO/provenance',record['source']==SOURCE and record['controllerSha256']==CONTROLLER and record['bindingsSha256']==BINDINGS and record['sourcePinDigest']==packet['sourcePinDigest'] and record['nativeExit']==0 and record['observerErrors']==[] and record['status']=='PREPARED_COMPOSITE_ZERO_ONLY' and all(record[k]==0 for k in ['gameProcesses','diagnosticRequests','testsExecuted','fixtureSubprocessTargets']) and record['compilationProvenance']==index['compilationProvenance'])
 check(name+' installed inputs and unchanged normal snapshots',record['verifiedInstalledInputs']=={'count':172,'sha256':'1ee529f929e54f79a2f3b2c7a516ef0afeb6cc0b32cfb9a73f174dd44b392cce'} and record['originalNormal899UnchangedBefore'] and record['originalNormal899UnchangedAfter'])
 check(name+' original dated process observations clean',record['afterLiveTools']==[] and record['beforeTools']==record['afterTools']==[{'comm':'dotnet','state':'Z','count':35}] and record['beforeVisibility']==record['afterVisibility']=={'executableFallbacks':5,'positivelyVanished':0})
 env=record['environment']
 check(name+' exact private artifact routing and phase environment',env['ArtifactsPath']==str(root/'artifacts') and env['TMPDIR']==str(root/name/'tmp') and env['XDG_RUNTIME_DIR']==str(root/name/'runtime') and env['MSBUILDDISABLENODEREUSE']=='1' and env['UseSharedCompilation']=='false')
 check(name+' old outcomes unmodified',record['originalCatalogNativeExit']==124 and record['originalUnitNativeExit']==137 and not record['oldOutcomeReplaced'])
 commands=record['commands'] if name!='prepare' else [record['compiler']]
 check(name+' expected exact command count',len(commands)=={'catalog':5,'plan':1,'prepare':1}[name])
 for n,command in enumerate(commands):
  ident=command['identity'];identities.append((ident['pid'],ident['birthTick']))
  check(name+' owned command '+str(n),command['nativeExit']==0 and command['joined'] and not command['observerErrors'] and ident['pid']>0 and ident['birthTick']>0 and ident['parentPid']>0 and ident['uid']==1000 and (name=='prepare' or (command['remainingSessionProcesses']==[] and command['rawWrapperReturnCode']==0 and command['terminationSignal'] is None and 0<float(command['command'][5][:-1])<=600)))
 if name!='prepare':
  summary=json.loads(z.read(name+'/runner/summary.json'));log=z.read(name+'/runner/dotnet-test.log').decode()
  check(name+' summary exact/native0/cleanup/ZERO/no build',summary==record['runnerSummary'] and summary['ExitCode']==0 and not summary['TimedOut'] and summary['OwnedTreeCleanupSucceeded'] and summary['RuntimeCleanupSucceeded'] and summary['Tests']=={'Total':0,'Executed':0,'Passed':0,'Failed':0} and not any(x['Name'].startswith('Build-') for x in summary['PhaseResults']) and summary['Revision']['Head']==SOURCE and summary['Revision']['ChangedFiles']==0)
  expected_paths=[str(root/'artifacts/bin'/p['name']/'debug'/(p['name']+'.dll')) for p in bindings['projects'] if name=='catalog' or p['key']=='integration']
  actual_paths=re.findall(r'^Test run for (.*?) \(\.NETCoreApp,Version=v8\.0\)$',log,re.M)
  check(name+' actual discovery assembly routing',actual_paths==expected_paths,actual_paths)
  check(name+' explicit NoBuild command and no warning/error output',record['commands'][-1]['command'][-1]=='-NoBuild' and not re.search(r'(?im)^.*(?:warning|error) [A-Z]+\d+\s*:',log) and 'Build succeeded' not in log)
check('seven fresh distinct PID/birth identities',len(identities)==len(set(identities))==7,identities)
catalog=records['catalog'];plan=records['plan'];prepare=records['prepare']
check('complete810/11802 catalog inventory audit',json.loads(z.read('catalog/runner/catalog-audit.json'))==catalog['runnerSummary']['CatalogAudit']==index['catalogAudit']=={'Valid':True,'Methods':11802,'Categories':810,'Unmapped':[],'StaleSelectors':[]} and catalog['runnerSummary']['DiscoveryOnly'])
input_map={x['path']:x for x in bindings['inputs']}
for p,evaluated in zip(bindings['projects'],catalog['evaluatedBindings']):
 key=p['key'];objroot=root/'artifacts/obj'/p['name'];props=json.loads(z.read('catalog/phase/bind-'+key+'.stdout'))['Properties']
 check(key+' exact evaluated property record',evaluated['project']==key and evaluated['properties']==props and props['TargetPath']==str(root/'artifacts/bin'/p['name']/'debug'/(p['name']+'.dll')) and props['ProjectAssetsFile']==str(objroot/'project.assets.json') and Path(props['MSBuildProjectExtensionsPath'])==objroot and props['IsTestProject']=='true' and props['TargetFramework']=='net8.0' and props['Configuration']=='Debug' and props['AssemblyName']==p['name'])
 xml=z.read('catalog/phase/bind-'+key+'.project.xml').decode()
 paths={line.strip() for comment in re.findall(r'<!--(.*?)-->',xml,re.S) for line in comment.splitlines() if line.strip().startswith('/') and line.strip().endswith(('.props','.targets'))}
 check(key+' full actual preprocessed imports recorded',len(paths)=={'integration':129,'unit':127}[key] and paths=={x['path'] for x in evaluated['imports']})
 stage_names={str(objroot/(p['name']+'.csproj.nuget.g.'+suffix)) for suffix in ['props','targets']}
 check(key+' own staged metadata imports/no original obj',stage_names<=paths and not any(str(Path(p['metadataFiles'][0]['path']).parent)+'/' in x for x in paths))
 unbound=[]
 for row in evaluated['imports']:
  path=Path(row['path']);data=read(path)
  okay=match(row)
  if path.is_relative_to(objroot):okay=okay and any(x['sha256']==row['sha256'] and Path(x['path']).name==path.name for x in p['metadataFiles'])
  elif path.is_relative_to(REPO):okay=okay and data==git('show',SOURCE+':'+str(path.relative_to(REPO)))
  else:okay=okay and str(path) in input_map and input_map[str(path)]['sha256']==row['sha256']
  if not okay:unbound.append(str(path))
 check(key+' all actual imports current/exact authorized closure',not unbound,unbound)
 check(key+' retained and staged complete bins equal manifest',len(p['binFiles'])=={'integration':460,'unit':439}[key] and image_tree(Path(p['sourceBin']))==image_tree(root/'artifacts/bin'/p['name']/'debug')==p['binFiles'])
 check(key+' six total metadata exact original/staged/archive',len(p['metadataFiles'])==3 and all(match(x) and match(x,objroot/Path(x['path']).name) and z.read('staged-metadata/'+key+'/'+Path(x['path']).name)==read(x['path']) for x in p['metadataFiles']))
 # Runtime dependency files stay in their own independently compiled closure.
 deps=obj(root/'artifacts/bin'/p['name']/'debug'/(p['name']+'.deps.json'))
 declared=[rel for target in deps['targets'].values() for lib in target.values() for rel in lib.get('runtime',{})]
 check(key+' declared runtime assets present in own bin closure',all((root/'artifacts/bin'/p['name']/'debug'/Path(x).name).is_file() for x in declared),{'runtimeEntries':len(declared)})
with zipfile.ZipFile(REC/'l01-delivery-d01-first-prep-failure-originals-20261018.zip') as oldzip:old=json.loads(oldzip.read('phase/record.json'))
check('original normal899 files exact frozen hashes',len(old['binaryInventory'])==899 and all(match(x,REPO/x['path']) for x in old['binaryInventory']))
method='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss'
descriptor=plan['plan']['Descriptors'][0]
check('exact singleton PlanOnly1descriptor1method1case; no completion claim',json.loads(z.read('plan/runner/plan.json'))==plan['plan']==index['plan'] and len(plan['plan']['Descriptors'])==1 and descriptor['ExpectedMethodCounts']==[{'Name':method,'Cases':1}] and descriptor['Filter']=='FullyQualifiedName='+method and descriptor['EstimatedCases']==1 and descriptor['BudgetMinutes']==15 and plan['runnerSummary']['PlanOnly'] and plan['runnerSummary']['Selection']['PlannedDescriptors']==plan['runnerSummary']['Selection']['PlannedCases']==1 and plan['runnerSummary']['Selection']['CompletedCases']==0 and not plan['runnerSummary']['Selection']['Complete'])
check('PlanOnly binds exact new catalog record',plan['catalogRecordSha256']==EXPECTED_RECORDS['catalog'])
rsp=z.read('prepare/phase/compile.rsp').decode().splitlines()
expected_rsp=['-nostdlib+','-target:exe','-debug:portable','-optimize-','-out:'+str(root/'prepare/D01Live.dll')]+['-reference:'+x['path'] for x in inventory['net8ReferenceInputs']]+[str(REPO/'BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs'),str(REC/'l01-delivery-d01/Live.cs')]
check('directcompile exact163refs and unchanged helper/Live sources',rsp==expected_rsp and all(any(x['path']==str(Path(p).relative_to(REPO)) and match(x,REPO/x['path']) for x in packet['sourcePins']) for p in rsp[-2:]))
check('runtime exact net8.0/8.0.32 Disable',json.loads(z.read('prepare/phase/D01Live.runtimeconfig.json'))=={'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.32'},'rollForward':'Disable'}})
check('four directcompile outputs exact current/archived',len(prepare['outputs'])==4 and prepare['outputs']==index['liveOutputs'] and all(match(x) and z.read('prepare/phase/'+Path(x['path']).name)==read(x['path']) for x in prepare['outputs']))
check('new LiveDLL hash and compiler no diagnostics',prepare['outputs'][0]['sha256']=='ec59272038423c31a7c63959977c79136d6634b85b460c89f4955991f3094d7c' and z.read('prepare/phase/compile.stderr')==b'' and not re.search(rb'\b(?:error|warning) [A-Z]+\d+',z.read('prepare/phase/compile.stdout')))
check('old failure and immutable histories unmodified in candidate delta',all('first-binding-failure' not in x and 'first-prep-failure' not in x and 'source-packet' not in x for x in changed) and not index['firstBindingFailureReplaced'] and not index['originalCatalog124Replaced'] and not index['originalUnit137Replaced'] and index['old66529']=='UNKNOWN_UNAVAILABLE_NO_RECOVERY_CLAIM')
check('synthetic phase still unused',not (root/'synthetic').exists())
result={'kind':'D02_COMPOSITE_PREP_INDEPENDENT_READ_ONLY_CHECKS','candidate':CANDIDATE,'source':SOURCE,'controllerSha256':CONTROLLER,'bindingsSha256':BINDINGS,'prepRecords':EXPECTED_RECORDS,'passed':sum(x['passed'] for x in checks),'findings':[x for x in checks if not x['passed']],'checks':checks,'nativeActions':0,'procfsReads':0,'repositoryWrites':0,'recoveryReceiptSha256':sha(read(receipt_path)),'indexSha256':sha(read(index_path)),'checkerSha256':sha(read(__file__))}
target=OUT/'l01-delivery-d02-composite-prep-independent-checks-20261018.json'
target.write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'path':str(target),'sha256':sha(read(target)),'passed':result['passed'],'findings':result['findings']}))
