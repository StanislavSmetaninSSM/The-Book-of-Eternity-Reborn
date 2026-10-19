"""G01 tools/ZERO PREP, exactly one gated game case, and offline mapping. No retry."""
from pathlib import Path
import hashlib
import json
import os
import runpy
import shutil
import subprocess
import sys
import time
import uuid

HERE=Path(__file__).resolve().parent
REC=HERE.parent
REPO=HERE.parents[3]
ROOT=Path('/tmp/g-a91f6d2c7b30')
DOTNET='/tmp/1536-cloud-tools/dotnet/dotnet'
PW='/tmp/1536-cloud-tools/pwsh/pwsh'
METHOD='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss'
H=lambda b:hashlib.sha256(b).hexdigest()
def read(p):return json.loads(Path(p).read_text())
def git(*a):return subprocess.check_output(['git','-C',str(REPO),*a]).decode().strip()
def image(p):
    p=Path(p);b=p.read_bytes();return dict(path=str(p),bytes=len(b),sha256=H(b))
def verify(r):
    b=Path(r['path']).read_bytes();assert len(b)==r['bytes'] and H(b)==r['sha256'],r['path']
def unit_tree(rev):
    rows=subprocess.check_output(['git','-C',str(REPO),'ls-tree','-r','--full-tree',rev]).splitlines()
    def included(row):
        p=row.split(b'\t',1)[1]
        return not p.startswith((b'specs/1536-complete-wound-materialization/',b'BookOfEternityClient.IntegrationTests/')) and p not in (b'tests/categories.json',b'tests/selection.json')
    rows=[r for r in rows if included(r)]
    return dict(count=len(rows),sha256=H(b'\n'.join(rows)+b'\n'))

def main():
    mode,source=sys.argv[1:];assert mode in ('tools','prepare','actual','map')
    assert git('rev-parse','HEAD')==source and not git('status','--porcelain')
    cfg=read(HERE/'bindings.json');packet=read(HERE/'source-packet.json');control=read(HERE/'control.json')
    gate=control['sourceAdmission'] if mode in ('tools','prepare') else control['actualAdmission']
    expected='SOURCE_ACCEPTED_G01_ZERO_ONLY' if mode in ('tools','prepare') else 'PREP_ACCEPTED_G01_ONE_ACTUAL_CASE' if mode=='actual' else 'CONSUMED_G01_FIRST_OUTCOME'
    assert gate['status']==expected
    review_raw=(REPO/gate['reviewPath']).read_bytes();assert H(review_raw)==gate['reviewSha256'];review=json.loads(review_raw)
    assert review['sourcePinDigest']==packet['sourcePinDigest'] and review['controllerSha256']==H(Path(__file__).read_bytes())
    assert review['verdict']==('ACCEPT_G01_WHOLE_SOURCE_ZERO_ONLY' if mode in ('tools','prepare') else 'ACCEPT_G01_PREP_ONE_ACTUAL')
    if mode in ('actual','map'):assert gate['runId']==cfg['runId']==review['plannedRunId']
    if mode=='actual':assert gate['lease']=='CONSUMED_FOR_NAMED_RUN_NO_RETRY'
    for row in packet['sourcePins']:verify(dict(row,path=str(REPO/row['path'])))
    for row in cfg['inputs']:verify(row)
    assert cfg['root']==str(ROOT) and unit_tree(source)==cfg['unchangedUnitTree']==unit_tree(cfg['unit']['compiledSource'])
    # Every imported executable helper is already in packet or input hashes.
    lib=runpy.run_path(str(REC/'l01-managed-stack-s01/smoke.py'))
    d02=runpy.run_path(str(REC/'l01-delivery-d02/phase.py'))
    b01=runpy.run_path(str(REC/'l01-build-tooling-unit-once-20261018.py'))
    m01=runpy.run_path(str(REC/'t148-m01-run-phase-20261018.py'))
    summary=runpy.run_path(str(REC/'l01-delivery-d01/phase.py'))['emit_summary']
    installed=read(REC/'l01-managed-stack-capability-inventory-20261018.json')
    for row in installed['availableFiles']+installed['net8ReferenceInputs']:verify(row)
    assert d02['tree_image'](Path(cfg['unit']['sourceBin']))==cfg['unit']['binFiles']
    before,visibility=b01['proc_rows']();assert not [p for p in before if p['state']!='Z'],'Active exact tool; no launch.'
    os.umask(0o077)
    if mode=='tools':ROOT.mkdir(mode=0o700)
    assert ROOT.is_dir() and not ROOT.is_symlink() and ROOT.stat().st_uid==os.geteuid() and ROOT.stat().st_mode&0o777==0o700
    phase=ROOT/mode;phase.mkdir(mode=0o700)
    env=lib['environment'](phase);env.update(PATH='/tmp/1536-cloud-tools/pwsh:/tmp/1536-cloud-tools/dotnet:/usr/bin:/bin',
        NUGET_PACKAGES='/tmp/1536-cloud-tools/nuget',NuGetAudit='false',DOTNET_GENERATE_ASPNET_CERTIFICATE='false',
        DOTNET_NOLOGO='1',MSBUILDDISABLENODEREUSE='1',UseSharedCompilation='false',ArtifactsPath=str(ROOT/'artifacts'))
    record=dict(kind='G01_FIRST_'+mode.upper(),source=source,sourcePinDigest=packet['sourcePinDigest'],nativeExit=None,
        observerErrors=[],commands=[],gameCasesPlanned=1 if mode=='actual' else 0,diagnosticRequestUpperBound=1 if mode=='actual' else 0,
        capturePhase='UNKNOWN',suspensionDuration='UNKNOWN',beforeTools=b01['tool_summary'](before),beforeVisibility=visibility)
    # Frozen normal output baseline is retained inside original raw archive, not newly qualified.
    import zipfile
    with zipfile.ZipFile(REC/'l01-delivery-d01-first-prep-failure-originals-20261018.zip') as z: normal=json.loads(z.read('phase/record.json'))['binaryInventory']
    assert m01['binaries']()==normal;record['normal899UnchangedBefore']=True
    start=time.monotonic();lib['write'](phase/'started.json',record)
    def run(command,name,deadline):
        result=d02['run_owned'](command,phase,name,env,deadline,lib,b01['proc_rows']);record['commands'].append(result)
        record['nativeExit']=result['nativeExit'];record['observerErrors'].extend(result['observerErrors'])
        return result
    def zero(command,name,deadline):
        result=run(command,name,deadline);assert result['nativeExit']==0 and not result['observerErrors'],name+' first native/observer failure';return result
    def prior(name):
        p=ROOT/name/'record.json';r=read(p);assert r['status']=='PREPARED_G01_ZERO_ONLY' and r['nativeExit']==0 and not r['observerErrors'] and r['sourcePinDigest']==packet['sourcePinDigest'];return p,r
    def binaries():
        return {name:d02['tree_image'](ROOT/'artifacts/bin'/name/'debug') for name in ('BookOfEternityClient.IntegrationTests','BookOfEternityClient.Tests')}
    def check_prep():
        for name,expected_hash in gate['prepRecords'].items():
            p,r=prior(name);assert H(p.read_bytes())==expected_hash
            for row in r.get('toolOutputs',[]):verify(row)
            if name=='prepare':assert binaries()==r['privateBinClosures']
    try:
        if mode=='tools':
            global_usings=phase/'GlobalUsings.cs';global_usings.write_text('global using System; global using System.IO; global using System.Linq; global using System.Collections.Generic; global using System.Threading; global using System.Threading.Tasks;\n')
            def compile_tool(name,sources):
                lib['write'](phase/(name+'.runtimeconfig.json'),dict(runtimeOptions=dict(tfm='net8.0',framework=dict(name='Microsoft.NETCore.App',version='8.0.32'),rollForward='Disable')))
                rsp=phase/(name+'.rsp');args=['-nostdlib+','-target:exe','-debug:portable','-optimize-','-out:'+str(phase/(name+'.dll'))]
                args+=['-reference:'+r['path'] for r in installed['net8ReferenceInputs']]+list(map(str,sources))
                rsp.write_text('\n'.join(args)+'\n');zero([DOTNET,'exec',lib['CSC'],'-noconfig','@'+str(rsp)],'compile-'+name,time.monotonic()+60)
            compile_tool('ParentProbe',[global_usings,REPO/'BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs',
                REPO/'BookOfEternityClient.IntegrationTests/JointInterfaceManagedCapturePolicy.cs',REPO/'BookOfEternityClient.IntegrationTests/JointInterfaceManagedCapture.cs',HERE/'ParentProbe.cs'])
            compile_tool('MapFrames',[HERE/'MapFrames.cs'])
            record['toolOutputs']=[image(phase/(n+ext)) for n in ('ParentProbe','MapFrames') for ext in ('.dll','.pdb','.runtimeconfig.json','.rsp')]
            short=ROOT/'t';short.mkdir(mode=0o700);env['TMPDIR']=str(short)
            before_ns=time.monotonic_ns();zero([DOTNET,str(phase/'ParentProbe.dll'),str(phase/'parent-proof')],'parent-proof',time.monotonic()+30);after_ns=time.monotonic_ns()
            parent=read(phase/'parent-proof.stdout');assert parent['passed']==len(parent['cases'])==6 and all(c['passed'] for c in parent['cases']) and parent['requests']==parent['gameProcesses']==parent['fixtureChildren']==0
            before_ps=time.monotonic_ns();zero([PW,'-NoLogo','-NoProfile','-NonInteractive','-File',str(HERE/'check_health.ps1'),'-Collector',str(HERE/'Collect.ps1')],'collector-health-proof',time.monotonic()+30);after_ps=time.monotonic_ns()
            health=read(phase/'collector-health-proof.stdout');assert health['Passed']==len(health['Cases'])==22 and all(c['Passed'] for c in health['Cases']) and health['Requests']==health['GameProcesses']==0
            record['clockProof']=[]
            for name,stamp,freq,lo,hi in [('net8',parent['clockStamp'],parent['clockFrequency'],before_ns,after_ns),('powershell',health['ClockStamp'],health['ClockFrequency'],before_ps,after_ps)]:
                normalized=stamp*10**9//freq;assert lo<=normalized<=hi
                record['clockProof'].append(dict(runtime=name,stamp=stamp,frequency=freq,beforeMonotonicNs=lo,afterMonotonicNs=hi,insideObservedBracket=True))
            assert parent['clockFrequency']==health['ClockFrequency']
            manifest=dict(SourceRoot=str(REPO),MapModules=[dict(Dll=str(phase/'ParentProbe.dll'),DllSha256=H((phase/'ParentProbe.dll').read_bytes()),Pdb=str(phase/'ParentProbe.pdb'),PdbSha256=H((phase/'ParentProbe.pdb').read_bytes()))])
            lib['write'](phase/'map-proof-modules.json',manifest);record['mappingProof']=[]
            for name,guid,token in [('valid',parent['moduleMvid'],parent['methodToken']),('wrong-mvid',str(uuid.UUID(int=0)),parent['methodToken']),('zero-token',parent['moduleMvid'],0)]:
                report=dict(payload=dict(threads=[dict(native_thread_id='0xb',stack_frames=[dict(is_managed=True,filename='ParentProbe.dll',guid=guid,token=hex(token),il_offset='0x0')])]))
                lib['write'](phase/('map-'+name+'.input.json'),report)
                zero([DOTNET,str(phase/'MapFrames.dll'),str(phase/('map-'+name+'.input.json')),str(phase/'map-proof-modules.json'),str(phase/('map-'+name+'.json'))],'map-'+name,time.monotonic()+30)
                mapped=read(phase/('map-'+name+'.json'))
                if name=='valid':
                    assert len(mapped['mappedFrames'])==1
                    f=mapped['mappedFrames'][0];assert f['method']=='SourceAnchor' and f['sourceFile'].endswith('/ParentProbe.cs') and f['sourceLine']>0 and f['moduleMvid']==parent['moduleMvid']
                else:assert not mapped['mappedFrames']
                record['mappingProof'].append(dict(case=name,passed=True,output=image(phase/('map-'+name+'.json'))))
            record.update(parentProof=parent,collectorHealthProof=health)
        elif mode=='prepare':
            p,tools=prior('tools');record['toolsRecordSha256']=H(p.read_bytes())
            for row in tools['toolOutputs']:verify(row)
            d02['mirror'](Path(cfg['unit']['sourceBin']),ROOT/'artifacts/bin/BookOfEternityClient.Tests/debug',cfg['unit']['binFiles'])
            for project in cfg['metadata']:
                obj=ROOT/'artifacts/obj'/project['name'];obj.mkdir(mode=0o700,parents=True)
                for row in project['files']:
                    verify(row);shutil.copy2(row['path'],obj/Path(row['path']).name)
            deadline=start+600
            build=[DOTNET,'build','BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj','--no-restore','--disable-build-servers',
                '--verbosity','minimal','--artifacts-path',str(ROOT/'artifacts'),'-p:GenerateDocumentationFile=true','-p:NoWarn=1591','-p:NuGetAudit=false','-clp:PerformanceSummary;Summary']
            zero(build,'fresh-integration-build',deadline)
            record['evaluatedBindings']=[]
            for key,name,project in [('integration','BookOfEternityClient.IntegrationTests','BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj'),('unit','BookOfEternityClient.Tests','BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj')]:
                props='TargetPath,ProjectAssetsFile,MSBuildProjectExtensionsPath,IsTestProject,TargetFramework,Configuration,AssemblyName'
                zero([DOTNET,'msbuild',project,'-nologo','-nodeReuse:false','-getProperty:'+props],'bind-'+key,deadline)
                values=read(phase/('bind-'+key+'.stdout'))['Properties']
                assert values['TargetPath']==str(ROOT/'artifacts/bin'/name/'debug'/(name+'.dll'))
                assert values['ProjectAssetsFile']==str(ROOT/'artifacts/obj'/name/'project.assets.json')
                assert Path(values['MSBuildProjectExtensionsPath'])==ROOT/'artifacts/obj'/name
                assert values['IsTestProject'].lower()=='true' and values['TargetFramework']=='net8.0' and values['Configuration']=='Debug' and values['AssemblyName']==name
                pp=phase/(key+'.project.xml');zero([DOTNET,'msbuild',project,'-nologo','-nodeReuse:false','-preprocess:'+str(pp)],'imports-'+key,deadline)
                import re
                paths=set()
                for comment in re.findall(r'<!--(.*?)-->',pp.read_text(),re.S):
                    for line in comment.splitlines():
                        line=line.strip()
                        if line.startswith('/') and line.endswith(('.props','.targets')):paths.add(Path(line))
                input_map={r['path']:r for r in cfg['inputs']}
                for path in paths:
                    if path.is_relative_to(ROOT/'artifacts/obj'/name):
                        original=next(r for group in cfg['metadata'] if group['name']==name for r in group['files'] if Path(r['path']).name==path.name)
                        verify(dict(original,path=str(path)))
                    elif path.is_relative_to(REPO):assert path.read_bytes()==subprocess.check_output(['git','-C',str(REPO),'show',source+':'+str(path.relative_to(REPO))])
                    else:verify(input_map[str(path)])
                record['evaluatedBindings'].append(dict(project=key,properties=values,imports=[image(p) for p in sorted(paths)]))
            record['runnerRoots']=[]
            for name,args in [('catalog',['-ValidateCatalog','-NoBuild']),('plan',['-Category','fr063-joint-interface-crash-diagnostic','-PlanOnly','-NoBuild'])]:
                results=REPO/'TestResults/test-categories';before_roots=set(results.iterdir()) if results.exists() else set()
                zero([PW,'-NoLogo','-NoProfile','-NonInteractive','-File',str(REPO/'scripts/test-csharp.ps1'),*args],name,deadline)
                roots=set(results.iterdir())-before_roots;assert len(roots)==1;rroot=next(iter(roots));s=read(rroot/'summary.json')
                assert s['ExitCode']==0 and s['Tests']['Executed']==0 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded']
                assert not [p for p in s['PhaseResults'] if p['Name'].startswith('Build-')]
                if name=='catalog':assert s['CatalogAudit']['Valid'] and s['DiscoveryOnly']
                else:
                    plan=read(rroot/'plan.json');assert len(plan['Descriptors'])==1 and plan['Descriptors'][0]['ExpectedMethodCounts']==[dict(Name=METHOD,Cases=1)]
                    assert s['PlanOnly'] and s['Selection']['PlannedDescriptors']==s['Selection']['PlannedCases']==1
                record[name+'Summary']=s;record['runnerRoots'].append(str(rroot))
            record['privateBinClosures']=binaries();record['integrationCompiledSource']=source;record['unitCompiledSource']=cfg['unit']['compiledSource']
            assert record['privateBinClosures']['BookOfEternityClient.Tests']==cfg['unit']['binFiles']
        elif mode=='actual':
            check_prep()
            bin=ROOT/'artifacts/bin/BookOfEternityClient.IntegrationTests/debug'
            request_inputs=cfg['requestInputs']+[image(bin/n) for n in ('BookOfEternityClient.IntegrationTests.dll','BookOfEternityClient.TestSupport.dll','BookOfEternityClient.dll','BookOfEternityClient.IntegrationTests.runtimeconfig.json','BookOfEternityClient.IntegrationTests.deps.json','System.Collections.Immutable.dll')]
            settings=dict(Contract='EXACT_RECEIPT_FRESH_STICKY_HEALTH_CAPTURE_PHASE_UNKNOWN_ALLOWED',RunId=gate['runId'],ActualRoot=str(phase),FixtureParent=env['TMPDIR'],
                Python=cfg['python'],Watchdog=str(HERE/'watchdog.py'),Dotnet=DOTNET,Pwsh=PW,Collector=str(HERE/'Collect.ps1'),Createdump=cfg['createdump'],
                Runtimeconfig=str(bin/'BookOfEternityClient.IntegrationTests.runtimeconfig.json'),Depsfile=str(bin/'BookOfEternityClient.IntegrationTests.deps.json'),
                SupportAssembly=str(bin/'BookOfEternityClient.TestSupport.dll'),IntegrationAssembly=str(bin/'BookOfEternityClient.IntegrationTests.dll'),
                RequestInputs=request_inputs,LoadedFiles=cfg['loadedFiles'],ClientAssembly=cfg['clientAssembly'],BinaryImages=request_inputs[-6:])
            worst=Path(env['TMPDIR'])/('g-'+'9'*12)/'tmp'/('dotnet-diagnostic-'+'9'*10+'-'+'9'*20+'-socket');assert len(str(worst).encode())<=107
            lib['write'](phase/'settings.json',settings);env.update(L01_G01_SETTINGS=str(phase/'settings.json'),L01_G01_SETTINGS_SHA256=H((phase/'settings.json').read_bytes()))
            record.update(runId=settings['RunId'],settingsSha256=env['L01_G01_SETTINGS_SHA256'],fixtureParent=env['TMPDIR'])
            results=REPO/'TestResults/test-categories';before_roots=set(results.iterdir()) if results.exists() else set()
            run([PW,'-NoLogo','-NoProfile','-NonInteractive','-File',str(REPO/'scripts/test-csharp.ps1'),'-Category','fr063-joint-interface-crash-diagnostic','-NoBuild'],'one-game-case',start+900)
            roots=set(results.iterdir())-before_roots;record['privateRunnerRoots']=[str(p) for p in roots];assert len(roots)==1
            rr=next(iter(roots));s=read(rr/'summary.json');record['privateRunnerSummaryPath']=str(rr/'summary.json')
            record['testCounts']=s['Tests'];record['runnerCleanup']={k:s[k] for k in ('TimedOut','OwnedTreeCleanupSucceeded','RuntimeCleanupSucceeded')}
            plan=read(rr/'plan.json');assert len(plan['Descriptors'])==1 and plan['Descriptors'][0]['ExpectedMethodCounts']==[dict(Name=METHOD,Cases=1)]
            assert s['Selection']['PlannedDescriptors']==s['Selection']['PlannedCases']==1
            if (phase/'fixture.json').exists():record['privateFixtureMarker']=read(phase/'fixture.json')
            # No raw TRX/test stdout, dump or report is copied or published by this controller.
        else:
            check_prep();actual=read(ROOT/'actual/record.json');own=Path(actual['privateFixtureMarker']['Own']);report=own/'capture/game-normal.core.crashreport.json'
            record['privateOwn']=str(own)
            if report.exists():
                bin=ROOT/'artifacts/bin/BookOfEternityClient.IntegrationTests/debug';modules=[]
                for n in ('BookOfEternityClient','BookOfEternityClient.IntegrationTests','BookOfEternityClient.TestSupport'):
                    dll=bin/(n+'.dll');pdb=bin/(n+'.pdb');modules.append(dict(Dll=str(dll),DllSha256=H(dll.read_bytes()),Pdb=str(pdb),PdbSha256=H(pdb.read_bytes())))
                lib['write'](phase/'modules.json',dict(SourceRoot=str(REPO),MapModules=modules))
                zero([DOTNET,str(ROOT/'tools/MapFrames.dll'),str(report),str(phase/'modules.json'),str(own/'capture/mapped.json')],'offline-map',start+60)
                record['privateMappedPath']=str(own/'capture/mapped.json');record['mappedSummary']=read(own/'capture/mapped.json')
            else:record.update(nativeExit=0,mapping='NOT_CAPTURED_NO_REPORT')
        assert m01['binaries']()==normal;record['normal899UnchangedAfter']=True
        if mode in ('actual','map'):check_prep()
    except BaseException as error:record['observerErrors'].append(repr(error))
    finally:
        record['durationSeconds']=time.monotonic()-start
        try:
            after,visibility=b01['proc_rows']();record.update(afterTools=b01['tool_summary'](after),afterVisibility=visibility,afterLiveTools=[p for p in after if p['state']!='Z'])
            if record['afterLiveTools']:record['observerErrors'].append('Live exact tool remains; retain roots/HOLD; no global signals.')
        except BaseException as error:record['observerErrors'].append('Terminal observation: '+repr(error))
    record['status']='PREPARED_G01_ZERO_ONLY' if mode in ('tools','prepare') and record['nativeExit']==0 and not record['observerErrors'] else 'FIRST_GAME_OUTCOME_RETAINED_NO_RETRY' if mode=='actual' else 'OFFLINE_MAPPING_COMPLETE' if mode=='map' and record['nativeExit']==0 and not record['observerErrors'] else 'FROZEN_FIRST_G01_OUTCOME'
    result=lib['FINALIZE'](record,lambda r:lib['write'](phase/'record.json',r),summary)
    return 128-result if result<0 else result

if __name__=='__main__':sys.exit(main())
