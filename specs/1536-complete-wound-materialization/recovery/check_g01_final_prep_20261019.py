#!/usr/bin/env python3
"""Read-only G01 completed PREP evidence; no project-code imports, SDK or procfs actions."""
from pathlib import Path
import json,hashlib,subprocess,re,stat,struct,uuid,zipfile,datetime,collections
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');O=Path('/tmp/1536-cloud-tools/review')
B='specs/1536-complete-wound-materialization/recovery';T=B+'/l01-game-capture-g01';E=B+'/l01-game-capture-g01-prep-first-20261019';V='5093c3970df18a42ee3445486ef6befa548f9bdc';S='d293b3bf796289fb6662a778ca408fae76ab2736';ROOT=Path('/tmp/g-a91f6d2c7b30')
checks=[];facts={}
def h(b):return hashlib.sha256(b).hexdigest()
def j(p):return json.loads(Path(p).read_text())
def git(*a,root=R):return subprocess.check_output(['git','-C',str(root),*a])
def ck(n,b,d=None):
 checks.append(dict(check=n,passed=bool(b),detail=d));assert b,n

def exact(p,row):
 b=Path(p).read_bytes();return len(b)==row['bytes'] and h(b)==row['sha256']
def blobs(rev,paths):
 data=subprocess.run(['git','-C',str(R),'cat-file','--batch'],input=''.join(rev+':'+p+'\n' for p in paths).encode(),stdout=subprocess.PIPE,check=True).stdout;pos=0;out={}
 for name in paths:
  end=data.index(b'\n',pos);a=data[pos:end].split();assert a[1]==b'blob';size=int(a[2]);pos=end+1;out[name]=data[pos:pos+size];pos+=size+1
 assert pos==len(data);return out
m=j(R/E/'manifest.json');r=j(R/E/'phase/record.json');cfg=j(R/T/'bindings.json');packet=j(R/T/'source-packet.json');control=j(R/T/'control.json')
receipt=j('/tmp/1536-cloud-tools/g01-recovery-5093c397.json')
ck('carrier and clean independent recovery exact',receipt['sha']==V and receipt['remoteShaVerified'] and receipt['clean'] and receipt['localWriterObjectsCopied'] is False and receipt['changedPaths']==319 and receipt['currentPinsVerified']==163)
ck('both checkouts exact and clean',all(git('rev-parse','HEAD',root=q).decode().strip()==V and git('status','--porcelain',root=q)==b'' for q in [R,C]))
rawpaths=[E+'/'+x['path'] for x in m['files']]+[E+'/manifest.json'];vb=blobs(V,rawpaths);chains=[]
for x in m['files']:
 p=E+'/'+x['path'];b=(R/p).read_bytes();ok=len(b)==x['bytes'] and h(b)==x['sha256'] and b==(C/p).read_bytes()==vb[p]==Path(x['originalPath']).read_bytes();chains.append(dict(path=x['path'],sha256=x['sha256'],bytes=x['bytes'],exact=ok))
ck('38 original local/writer/Git/independent-restore byte chains',len(chains)==38 and all(x['exact'] for x in chains))
ck('manifest exact and no published private binary claim',vb[E+'/manifest.json']==(R/E/'manifest.json').read_bytes()==(C/E/'manifest.json').read_bytes() and m['privateBinClosuresNotPublished'])
ck('record exact declared digest source and tools dependency',h((R/E/'phase/record.json').read_bytes())==m['recordSha256']=='cc1bdf563029fd4452ba5d7fb9b69ade452cf23c545af588c8b6f9bc9a4903f4' and r['source']==m['source']==S and r['toolsRecordSha256']=='410857f197e1da419f110f9f52eacbe751543c590994e81aa4498ea327e81d12')
review_name='l01-game-capture-g01-tools-partial-prep-review-20261019.json';checks_name='l01-game-capture-g01-tools-partial-prep-checks-20261019.json';prior=j(O/review_name)
ck('reuse exact independently reviewed43 tools checks, no replay',h((O/review_name).read_bytes())=='fc95db06a28b155e182141091e19531f5d5607a21a393a438f363c9a28b7b779' and (O/review_name).read_bytes()==(R/B/review_name).read_bytes()==(C/B/review_name).read_bytes() and h((O/checks_name).read_bytes())=='1a5c34834be98b03b7898e987d8041eaeedba1b89ef9647e15020013001f9870' and prior['checkCount']==43 and not prior['findings'])
pinpaths=[x['path'] for x in packet['sourcePins']];sb=blobs(S,pinpaths);cb=blobs(V,pinpaths)
ck('163 current pins unchanged from qualified source through fresh prepare/carrier',len(pinpaths)==163 and all(exact(R/x['path'],x) and (R/x['path']).read_bytes()==sb[x['path']]==cb[x['path']]==(C/x['path']).read_bytes() for x in packet['sourcePins']))
ck('exact accepted controller bindings digest',packet['sourcePinDigest']==r['sourcePinDigest']=='a6c2675674dd6e3218e16917fd9abfcffb44d638195c367c3efcb2d98780b026' and h((R/T/'phase.py').read_bytes())=='63969901c241f7007030af78b757bba0618c72dae457ae75c11804de45bc9980' and h((R/T/'bindings.json').read_bytes())=='50e5bffdc10799c05b8757b8606642c0a5e6329fc4a0b91a764851eca4a679b2')
ck('395 installed/external binding inputs unchanged',len(cfg['inputs'])==395 and all(exact(x['path'],x) for x in cfg['inputs']))
ck('fresh native0 under original600 aggregate, no observer errors',r['nativeExit']==0 and not r['observerErrors'] and r['status']=='PREPARED_G01_ZERO_ONLY' and 0<r['durationSeconds']<600)
ck('seven original command outcomes0 joined/empty owned sessions',len(r['commands'])==7 and all(x['nativeExit']==x['rawWrapperReturnCode']==0 and x['joined'] and not x['observerErrors'] and not x['remainingSessionProcesses'] and x['terminationSignal'] is None for x in r['commands']))
names=['fresh-integration-build','bind-integration','imports-integration','bind-unit','imports-unit','catalog','plan'];ck('seven individual command records exact aggregate',all(j(R/E/('phase/'+n+'.record.json'))==x for n,x in zip(names,r['commands'])))
rem=[float(x['command'][4][:-1]) for x in r['commands']];ck('shared remaining deadline only decreases',600>=rem[0]>0 and all(a>b>0 for a,b in zip(rem,rem[1:])))
ck('all original command stderr empty',all(not (R/E/('phase/'+n+'.stderr')).read_bytes() for n in names))
log=(R/E/'phase/fresh-integration-build.stdout').read_text();ck('fresh integration build success152warnings0errors', 'Build succeeded.' in log and '152 Warning(s)' in log and '0 Error(s)' in log)
built=re.findall(r'^\s+(\S+) -> (\S+\.dll)$',log,re.M);expected_projects=['BookOfEternityClient','BookOfEternityClient.TestSupport','BookOfEternityClient.IntegrationTests']
ck('only expected fresh three-project outputs private',built==[(n,str(ROOT/'artifacts/bin'/n/'debug'/(n+'.dll'))) for n in expected_projects] and '--no-restore' in r['commands'][0]['command'] and '--disable-build-servers' in r['commands'][0]['command'])
warnlines=[l for l in log.splitlines() if ': warning ' in l];facts['warningCodesLogOccurrences']=dict(collections.Counter(re.findall(r'warning (\w+)\s*:',log)));facts['currentG01Warnings']=sorted(set(l for l in warnlines if 'JointInterfaceManagedCapture' in l))
ck('current G01 warnings only two Linux-scope CA1416 sites',len(facts['currentG01Warnings'])==2 and all('warning CA1416:' in l and 'UnixFileMode' in l for l in facts['currentG01Warnings']))
ck('separate compiled source provenance',r['integrationCompiledSource']==S and r['unitCompiledSource']==cfg['unit']['compiledSource']=='d3a1ddcf7a6e2f06ae815877c46314ec81d9f0d7')
inputmap={x['path']:x for x in cfg['inputs']};importfacts=[]
for binding in r['evaluatedBindings']:
 key=binding['project'];name='BookOfEternityClient.IntegrationTests' if key=='integration' else 'BookOfEternityClient.Tests';props=binding['properties'];obj=ROOT/'artifacts/obj'/name
 ck(key+' original actual properties exact private paths',props==j(R/E/('phase/bind-'+key+'.stdout'))['Properties'] and props==dict(TargetPath=str(ROOT/'artifacts/bin'/name/'debug'/(name+'.dll')),ProjectAssetsFile=str(obj/'project.assets.json'),MSBuildProjectExtensionsPath=str(obj)+'/',IsTestProject='true',TargetFramework='net8.0',Configuration='Debug',AssemblyName=name))
 pp=(R/E/('phase/'+key+'.project.xml')).read_text();paths=set()
 for comment in re.findall(r'<!--(.*?)-->',pp,re.S):
  for line in comment.splitlines():
   line=line.strip()
   if line.startswith('/') and line.endswith(('.props','.targets')):paths.add(line)
 ck(key+' exact observed import set',paths=={x['path'] for x in binding['imports']} and len(paths)==(129 if key=='integration' else 127))
 metadata={Path(x['path']).name:x for q in cfg['metadata'] if q['name']==name for x in q['files']};classes=collections.Counter()
 for x in binding['imports']:
  p=Path(x['path']);assert exact(p,x)
  if p.is_relative_to(obj):expected=metadata[p.name];assert x['sha256']==expected['sha256'] and x['bytes']==expected['bytes'];classes['stagedMetadata']+=1
  elif p.is_relative_to(R):assert p.read_bytes()==git('show',S+':'+str(p.relative_to(R)));assert '/obj/' not in str(p);classes['currentSource']+=1
  else:assert x==inputmap[str(p)];classes['explicitInstalledPin']+=1
 ck(key+' all imports source or exact metadata or installed pins, no unknown/normalobj',classes['stagedMetadata']==2 and sum(classes.values())==len(paths),dict(classes));importfacts.append(dict(project=key,count=len(paths),classes=dict(classes)))
ck('all12 staged NuGet metadata exact original binding',len(cfg['metadata'])==4 and all(exact(ROOT/'artifacts/obj'/q['name']/Path(x['path']).name,x) for q in cfg['metadata'] for x in q['files']))
cat=j(R/E/'catalog/summary.json');plan=j(R/E/'plan/summary.json');audit=j(R/E/'catalog/catalog-audit.json');selection=j(R/E/'plan/plan.json')
ck('complete810/11802 catalog and no unmapped/stale',audit==cat['CatalogAudit']=={'Valid':True,'Methods':11802,'Categories':810,'Unmapped':[],'StaleSelectors':[]} and cat['DiscoveryOnly'] and not cat['PlanOnly'])
for name,s in [('catalog',cat),('plan',plan)]:
 ck(name+' original summary matches aggregate ZERO and cleanup',s==r[name+'Summary'] and s['ExitCode']==0 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and s['Tests']==dict(Total=0,Executed=0,Passed=0,Failed=0) and s['Revision']['Head']==S and s['Revision']['ChangedFiles']==0 and not s['DuplicateTests'] and not s['AdapterResults'])
 ck(name+' no build phases',not any(x['Name'].startswith('Build-') for x in s['PhaseResults']))
 raw=(R/E/(name+'/dotnet-test.log')).read_text();target=str(ROOT/'artifacts/bin/BookOfEternityClient.IntegrationTests/debug/BookOfEternityClient.IntegrationTests.dll')
 ck(name+' discovery actually loaded private integration native0',('Test run for '+target+' (.NETCoreApp,Version=v8.0)') in raw and '===== integration-discovery: exit 0 =====' in raw)
 if name=='catalog':ck('catalog actually loaded separate private unit native0',('Test run for '+str(ROOT/'artifacts/bin/BookOfEternityClient.Tests/debug/BookOfEternityClient.Tests.dll')+' (.NETCoreApp,Version=v8.0)') in raw and '===== unit-discovery: exit 0 =====' in raw)
method='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss';desc=selection['Descriptors']
ck('exact original Fact one descriptor one method one case220/900 unchanged',len(desc)==1 and desc[0]['ExpectedMethodCounts']==[dict(Name=method,Cases=1)] and desc[0]['Filter']=='FullyQualifiedName='+method and desc[0]['EstimatedCases']==1 and desc[0]['BudgetMinutes']==15 and desc[0]['Exclusive'] and not desc[0]['FrontendBuild'])
ck('PlanOnly underfilled is honest ZERO not actual PASS',plan['PlanOnly'] and plan['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=0,PlannedCases=1,CompletedCases=0,UnderfilledDescriptors=[desc[0]['Name']],Complete=False))
closurefacts=[]
for name,rows in r['privateBinClosures'].items():
 base=ROOT/'artifacts/bin'/name/'debug';actual={str(p.relative_to(base)) for p in base.rglob('*') if p.is_file()}
 ck(name+' complete private file set and hashes/modes',len(rows)==(460 if 'IntegrationTests' in name else 439) and actual=={x['path'] for x in rows} and all(exact(base/x['path'],x) and stat.S_IMODE((base/x['path']).stat().st_mode)==x['mode'] for x in rows))
 closurefacts.append(dict(project=name,count=len(rows),manifestSha256=h(json.dumps(rows,sort_keys=True,separators=(',',':')).encode())))
ck('unit439 exact original separate closure retained',r['privateBinClosures']['BookOfEternityClient.Tests']==cfg['unit']['binFiles'])
tools=j(R/B/'l01-game-capture-g01-tools-first-20261019/record.json');ck('all8 local tools unchanged since reviewed tool originals',all(exact(x['path'],x) for x in tools['toolOutputs']))
# PE CodeView/portable PDB identity and module MVID, bytes only; no assembly loading.
def streams(b,md=0):
 assert b[md:md+4]==b'BSJB';p=(md+16+struct.unpack_from('<I',b,md+12)[0]+3)&~3;n=struct.unpack_from('<H',b,p+2)[0];p+=4;out={}
 for _ in range(n):
  off,size=struct.unpack_from('<II',b,p);end=b.index(0,p+8);out[b[p+8:end].decode()]=(md+off,size);p=(end+4)&~3
 return out

def pe_pdb(dll,pdb):
 b=dll.read_bytes();pb=pdb.read_bytes();u16=lambda p:struct.unpack_from('<H',b,p)[0];u32=lambda p:struct.unpack_from('<I',b,p)[0]
 pe=u32(0x3c);assert b[pe:pe+4]==b'PE\0\0';opt=pe+24;dd=opt+(112 if u16(opt)==0x20b else 96);ss=opt+u16(pe+20)
 def off(rva):
  for i in range(u16(pe+6)):
   p=ss+40*i;va=u32(p+12);sz=max(u32(p+8),u32(p+16))
   if va<=rva<va+sz:return u32(p+20)+rva-va
  raise ValueError('RVA')
 cli=off(u32(dd+14*8));st=streams(b,off(u32(cli+8)));tt=st['#~'][0];flags=b[tt+6];valid=struct.unpack_from('<Q',b,tt+8)[0];mod=tt+24+4*valid.bit_count();si=4 if flags&1 else 2;gi=4 if flags&2 else 2;gid=int.from_bytes(b[mod+2+si:mod+2+si+gi],'little');g=st['#GUID'][0]+16*(gid-1);mvid=str(uuid.UUID(bytes_le=b[g:g+16]));ps=streams(pb);pid=pb[ps['#Pdb'][0]:ps['#Pdb'][0]+20]
 de=off(u32(dd+6*8));entries=u32(dd+6*8+4)//28;codes=[]
 for i in range(entries):
  q=de+28*i
  if u32(q+12)==2:
   raw=u32(q+24);assert b[raw:raw+4]==b'RSDS';codes.append((b[raw+4:raw+20],u32(q+4),u32(raw+20)))
 assert len(codes)==1 and codes[0][0]==pid[:16] and codes[0][1]==int.from_bytes(pid[16:20],'little') and codes[0][2]==1
 return dict(module=dll.name,mvid=mvid,pdbId=pid.hex(),dllSha256=h(b),pdbSha256=h(pb))
modulefacts=[];ib=ROOT/'artifacts/bin/BookOfEternityClient.IntegrationTests/debug'
for n in expected_projects:
 modulefacts.append(pe_pdb(ib/(n+'.dll'),ib/(n+'.pdb')))
 ck(n+' fresh primary DLL/PDB copy agrees with integration runtime closure',all((ib/(n+x)).read_bytes()==(ROOT/'artifacts/bin'/n/'debug'/(n+x)).read_bytes() for x in ['.dll','.pdb']))
ck('three fresh game module PE/portablePDB identities bound',len(modulefacts)==3)
with zipfile.ZipFile(R/B/'l01-delivery-d01-first-prep-failure-originals-20261018.zip') as z:normal=json.loads(z.read('phase/record.json'))['binaryInventory']
ck('normal899 exact original files still untouched',len(normal)==899 and all(exact(R/x['path'],x) for x in normal) and r['normal899UnchangedBefore'] and r['normal899UnchangedAfter'])
ck('dated terminal observation no live tools, no continuous claim',r['afterLiveTools']==[] and r['beforeTools']==r['afterTools']==[dict(comm='dotnet',state='Z',count=35)] and r['beforeVisibility']['executableFallbacks']==r['afterVisibility']['executableFallbacks']==5)
ck('no actual/API yet and new actual directory absent',control['actualAdmission']['status']=='HOLD_PREP' and control['actualGameCasesExecuted']==control['diagnosticRequests']==control['dumpArtifactsCreated']==0 and not (ROOT/'actual').exists())
ck('planned run id and private retention/old holds retained',cfg['runId']=='2ca476b7cf58446fb3eb3c6218e850b8' and control['allOldOutcomesAndBroaderHoldsUnchanged'] and control['crash5Release']=='NONE')
facts.update(originalChains=chains,imports=importfacts,closures=closurefacts,modules=modulefacts,nativeDurationSeconds=r['durationSeconds'],buildDurationSeconds=r['commands'][0]['durationSeconds'],catalogDurationSeconds=r['commands'][5]['durationSeconds'],planDurationSeconds=r['commands'][6]['durationSeconds'],receiptSha256=h(Path('/tmp/1536-cloud-tools/g01-recovery-5093c397.json').read_bytes()),manifestSha256=h((R/E/'manifest.json').read_bytes()))
out=dict(kind='G01_FINAL_PREP_READONLY_CHECKS',reviewedCarrier=V,prepareSource=S,checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),checks=checks,checkCount=len(checks),findings=[],facts=facts,readerHistory=[dict(stage='exploratory initial read',exit=1,error='FileNotFoundError reading evidence/record.json instead of manifest-declared evidence/phase/record.json',correction='Read exact manifest path; no original, source or native changes')],limits=['Reuses completed tools43; does not rerun native proofs','Private binaries checked locally, not claimed recovered from Git','No SDK/procfs/native execution by reviewer'])
(O/'l01-game-capture-g01-final-prep-checks-20261019.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(dict(checks=len(checks),findings=[],modules=modulefacts)))
