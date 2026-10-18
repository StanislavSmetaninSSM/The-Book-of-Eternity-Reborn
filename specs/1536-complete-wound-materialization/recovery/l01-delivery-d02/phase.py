"""One new composite ZERO PREP and separately gated original D01 synthetic0/7."""
from pathlib import Path
import hashlib
import json
import os
import re
import runpy
import shutil
import stat
import subprocess
import sys
import time
import zipfile

HERE=Path(__file__).resolve().parent
REPO=HERE.parents[3]
RECOVERY=HERE.parent
ROOT=Path('/tmp/l01d02-cd3d584b5e27457f')
DOTNET='/tmp/1536-cloud-tools/dotnet/dotnet'
PW='/tmp/1536-cloud-tools/pwsh/pwsh'
sha=lambda b:hashlib.sha256(b).hexdigest()

def tree_image(root):
    assert root.is_dir() and not root.is_symlink(),str(root)
    rows=[]
    for p in sorted(root.rglob('*')):
        assert not p.is_symlink(),str(p)
        if p.is_file():
            data=p.read_bytes()
            rows.append(dict(path=str(p.relative_to(root)),bytes=len(data),sha256=hashlib.sha256(data).hexdigest(),mode=stat.S_IMODE(p.stat().st_mode)))
    return rows

def mirror(source,destination,rows):
    assert tree_image(source)==rows,'Retained complete closure changed: '+str(source)
    destination.parent.mkdir(parents=True,exist_ok=True)
    destination.mkdir(mode=0o700)
    for row in rows:
        target=destination/row['path'];target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(source/row['path'],target)
    assert tree_image(destination)==rows,'Copied closure differs.'

def tracked_tree(revision):
    raw=subprocess.check_output(['git','-C',str(REPO),'ls-tree','-r','--full-tree',revision]).splitlines()
    raw=[line for line in raw if not line.split(b'\t',1)[1].startswith(b'specs/1536-complete-wound-materialization/')]
    return dict(count=len(raw),sha256=sha(b'\n'.join(raw)+b'\n'))

def verify_file(row):
    p=Path(row['path']);assert p.is_file() and not p.is_symlink(),str(p)
    data=p.read_bytes();assert len(data)==row['bytes'] and sha(data)==row['sha256'],str(p)
    return data

def staged_bin(project):
    return ROOT/'artifacts/bin'/project['name']/'debug'

def verify_staged(bindings):
    for project in bindings['projects']:
        assert tree_image(staged_bin(project))==project['binFiles'],project['key']
        for row in project['metadataFiles']:
            data=(ROOT/'artifacts/obj'/project['name']/Path(row['path']).name).read_bytes()
            assert len(data)==row['bytes'] and sha(data)==row['sha256'],row['path']

def run_owned(command,phase,name,env,deadline,lib,census):
    remaining=deadline-time.monotonic();assert remaining>0,'Aggregate deadline exhausted.'
    command=['/usr/bin/timeout','--verbose','--signal=TERM','--kill-after=10s',str(remaining)+'s',*command]
    result=dict(command=command,nativeExit=None,observerErrors=[],joined=False)
    lib['write'](phase/(name+'.started.json'),result)
    start=time.monotonic();child=None
    try:
        with (phase/(name+'.stdout')).open('wb') as out,(phase/(name+'.stderr')).open('wb') as err:
            child=subprocess.Popen(command,cwd=REPO,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,start_new_session=True)
            result['identity']=lib['identity'](child.pid)
            assert result['identity']['parentPid']==os.getpid()
            result['nativeExit']=child.wait(timeout=max(1,deadline-time.monotonic()+15));result['joined']=True
    except BaseException as error:result['observerErrors'].append(repr(error))
    finally:
        if child is not None and result['nativeExit'] is None:
            try:result['nativeExit']=child.wait(timeout=max(1,deadline-time.monotonic()+15));result['joined']=True
            except BaseException as error:result['observerErrors'].append('Bounded owned-wrapper join: '+repr(error))
        result['durationSeconds']=time.monotonic()-start
        try:
            result['remainingSessionProcesses']=[] if child is None else [p for p in census(child.pid)[0] if p['state']!='Z']
            if result['remainingSessionProcesses']:result['observerErrors'].append('Own session remains live; retain and HOLD, no signals/retry.')
        except BaseException as error:result['observerErrors'].append('Owned-session observation: '+repr(error))
    result['rawWrapperReturnCode']=result['nativeExit']
    result['terminationSignal']=-result['nativeExit'] if result['nativeExit'] is not None and result['nativeExit']<0 else None
    # Use the accepted M01/S01 publication path; observer faults preserve native.
    lib['FINALIZE'](result,lambda value:lib['write'](phase/(name+'.record.json'),value),lambda value:None)
    return result

def main():
    mode,source=sys.argv[1:];assert mode in ('catalog','plan','prepare','synthetic')
    git=lambda *args:subprocess.check_output(['git','-C',str(REPO),*args]).decode().strip()
    assert git('rev-parse','HEAD')==source and not git('status','--porcelain')
    bindings=json.loads((HERE/'bindings.json').read_text());control=json.loads((HERE/'control.json').read_text())
    gate=control['synthetic'] if mode=='synthetic' else control['source']
    assert gate['status']==('PREP_ACCEPTED_SYNTHETIC_0_7_ONCE' if mode=='synthetic' else 'SOURCE_ACCEPTED_COMPOSITE_ZERO_ONLY')
    review_data=(REPO/gate['reviewPath']).read_bytes();assert sha(review_data)==gate['reviewSha256']
    review=json.loads(review_data)
    assert review['verdict']==('ACCEPT_D02_COMPOSITE_PREP_SYNTHETIC_0_7_ONCE' if mode=='synthetic' else 'ACCEPT_D02_COMPOSITE_SOURCE_ZERO_ONLY')
    assert review['controllerSha256']==sha(Path(__file__).read_bytes()) and review['bindingsSha256']==sha((HERE/'bindings.json').read_bytes())
    assert bindings['root']==str(ROOT) and bindings['oldOutcomeReplaced'] is False
    for row in bindings['inputs']:verify_file(row)
    # All executable Python helpers are hash-checked before import; no helper main.
    lib=runpy.run_path(str(RECOVERY/'l01-managed-stack-s01/smoke.py'))
    d01=runpy.run_path(str(RECOVERY/'l01-delivery-d01/phase.py'))
    m01=runpy.run_path(str(RECOVERY/'t148-m01-run-phase-20261018.py'))
    b01=runpy.run_path(str(RECOVERY/'l01-build-tooling-unit-once-20261018.py'))
    os.umask(0o077)
    if mode=='catalog':ROOT.mkdir(mode=0o700)
    assert ROOT.stat().st_uid==os.geteuid() and stat.S_IMODE(ROOT.stat().st_mode)==0o700 and not ROOT.is_symlink()
    phase=ROOT/mode;phase.mkdir(mode=0o700)
    record=dict(kind='D02_FIRST_'+mode.upper(),source=source,controllerSha256=review['controllerSha256'],bindingsSha256=review['bindingsSha256'],
        sourcePinDigest=bindings['sourcePinDigest'],compilationProvenance={p['key']:p['compiledSource'] for p in bindings['projects']},
        nativeExit=None,observerErrors=[],commands=[],gameProcesses=0,diagnosticRequests=0,testsExecuted=0,fixtureSubprocessTargets=0,
        originalCatalogNativeExit=124,originalUnitNativeExit=137,oldOutcomeReplaced=False,phaseRoot=str(phase),gameplayRelease='NONE')
    lib['write'](phase/'started.json',record);start=time.monotonic()
    def prior(name):
        raw=(ROOT/name/'record.json').read_bytes();p=json.loads(raw)
        assert p['nativeExit']==0 and not p['observerErrors'] and p['status']=='PREPARED_COMPOSITE_ZERO_ONLY'
        assert p['bindingsSha256']==record['bindingsSha256'] and p['controllerSha256']==record['controllerSha256']
        return raw,p
    try:
        packet=json.loads((RECOVERY/'l01-delivery-d01-source-packet-20261018.json').read_text())
        for row in packet['sourcePins']:verify_file(dict(row,path=str(REPO/row['path'])))
        assert packet['sourcePinDigest']==bindings['sourcePinDigest']
        assert tracked_tree(source)==bindings['trackedNonFeatureTree']
        for project in bindings['projects']:
            assert tracked_tree(project['compiledSource'])==bindings['trackedNonFeatureTree']
            assert tree_image(Path(project['sourceBin']))==project['binFiles']
            for row in project['metadataFiles']:verify_file(row)
        inventory=json.loads((RECOVERY/'l01-managed-stack-capability-inventory-20261018.json').read_text())
        record['verifiedInstalledInputs']=d01['verify_installed_inputs'](inventory)
        before,visibility=b01['proc_rows']();assert not [p for p in before if p['state']!='Z'],'Active exact tool match; no launch.'
        record.update(beforeTools=b01['tool_summary'](before),beforeVisibility=visibility)
        assert not any(Path('/proc',str(pid)).exists() for pid in (97377,97402,102017))
        with zipfile.ZipFile(RECOVERY/'l01-delivery-d01-first-prep-failure-originals-20261018.zip') as z:old=json.loads(z.read('phase/record.json'))
        assert m01['binaries']()==old['binaryInventory'];record['originalNormal899UnchangedBefore']=True
        env=lib['environment'](phase)
        env.update(PATH='/tmp/1536-cloud-tools/pwsh:/tmp/1536-cloud-tools/dotnet:/usr/bin:/bin',NUGET_PACKAGES='/tmp/1536-cloud-tools/nuget',
            NuGetAudit='false',DOTNET_GENERATE_ASPNET_CERTIFICATE='false',DOTNET_NOLOGO='1',ArtifactsPath=str(ROOT/'artifacts'),
            MSBUILDDISABLENODEREUSE='1',UseSharedCompilation='false')
        record['environment']=env
        if mode=='catalog':
            for project in bindings['projects']:
                mirror(Path(project['sourceBin']),staged_bin(project),project['binFiles'])
                obj=ROOT/'artifacts/obj'/project['name'];obj.mkdir(parents=True,mode=0o700)
                expected={project['name']+'.csproj.nuget.g.props',project['name']+'.csproj.nuget.g.targets','project.assets.json'}
                assert {Path(r['path']).name for r in project['metadataFiles']}==expected
                for row in project['metadataFiles']:(obj/Path(row['path']).name).write_bytes(verify_file(row))
            verify_staged(bindings);record['evaluatedBindings']=[]
            for project in bindings['projects']:
                props='TargetPath,ProjectAssetsFile,MSBuildProjectExtensionsPath,IsTestProject,TargetFramework,Configuration,AssemblyName,MSBuildAllProjects'
                command=[DOTNET,'msbuild',project['project'],'-nologo','-nodeReuse:false','-getProperty:'+props]
                result=run_owned(command,phase,'bind-'+project['key'],env,start+600,lib,b01['proc_rows']);record['commands'].append(result)
                if result['nativeExit']!=0:record['nativeExit']=result['nativeExit']
                assert result['nativeExit']==0 and not result['observerErrors'],'Property-only binding failed.'
                values=json.loads((phase/('bind-'+project['key']+'.stdout')).read_text())['Properties']
                assert Path(values['TargetPath'])==staged_bin(project)/(project['name']+'.dll')
                assert Path(values['ProjectAssetsFile'])==ROOT/'artifacts/obj'/project['name']/'project.assets.json'
                assert Path(values['MSBuildProjectExtensionsPath'])==ROOT/'artifacts/obj'/project['name']
                assert values['IsTestProject'].lower()=='true' and values['TargetFramework']=='net8.0' and values['Configuration']=='Debug'
                assert values['AssemblyName']==project['name'] and values['MSBuildAllProjects']
                preprocessed=phase/('bind-'+project['key']+'.project.xml')
                result=run_owned([DOTNET,'msbuild',project['project'],'-nologo','-nodeReuse:false','-preprocess:'+str(preprocessed)],
                    phase,'imports-'+project['key'],env,start+600,lib,b01['proc_rows']);record['commands'].append(result)
                if result['nativeExit']!=0:record['nativeExit']=result['nativeExit']
                assert result['nativeExit']==0 and not result['observerErrors'],'Property-only import binding failed.'
                imported=set()
                for comment in re.findall(r'<!--(.*?)-->',preprocessed.read_text(),re.S):
                    for line in comment.splitlines():
                        line=line.strip()
                        if line.startswith('/') and line.endswith(('.props','.targets')):imported.add(Path(line))
                obj=ROOT/'artifacts/obj'/project['name']
                assert {obj/(project['name']+'.csproj.nuget.g.props'),obj/(project['name']+'.csproj.nuget.g.targets')}<=imported
                input_map={r['path']:r for r in bindings['inputs']}
                import_images=[]
                for path in sorted(imported):
                    if path.is_relative_to(obj):
                        row=next(r for r in project['metadataFiles'] if Path(r['path']).name==path.name)
                        verify_file(dict(row,path=str(path)))
                    elif path.is_relative_to(REPO):
                        assert not path.is_relative_to(Path(project['metadataFiles'][0]['path']).parent),'Original normal obj import selected.'
                        assert path.read_bytes()==subprocess.check_output(['git','-C',str(REPO),'show',source+':'+str(path.relative_to(REPO))])
                    else:verify_file(input_map[str(path)])
                    import_images.append(lib['image'](path))
                record['evaluatedBindings'].append(dict(project=project['key'],properties=values,imports=import_images))
        elif mode=='plan':
            raw,_=prior('catalog');record['catalogRecordSha256']=sha(raw);verify_staged(bindings)
        elif mode=='synthetic':
            for name,expected in gate['prepRecords'].items():
                raw,p=prior(name);assert sha(raw)==expected
                if name=='prepare':
                    for row in p['outputs']:verify_file(row)
            verify_staged(bindings)
        if mode in ('catalog','plan'):
            command=[PW,'-NoLogo','-NoProfile','-NonInteractive','-File',str(REPO/'scripts/test-csharp.ps1')]
            command+=['-ValidateCatalog','-NoBuild'] if mode=='catalog' else ['-Category',packet['category'],'-PlanOnly','-NoBuild']
            results=REPO/'TestResults/test-categories';before=set(results.iterdir()) if results.exists() else set()
            result=run_owned(command,phase,'runner',env,start+600,lib,b01['proc_rows']);record['commands'].append(result)
            record.update(nativeExit=result['nativeExit'],observerErrors=list(result['observerErrors']))
            roots=set(results.iterdir())-before;record['resultRoots']=[str(p) for p in sorted(roots)]
            record['runnerArtifacts']=[lib['image'](p) for root in sorted(roots) for p in sorted(root.rglob('*')) if p.is_file()]
            assert result['nativeExit']==0 and not result['observerErrors'] and len(roots)==1
            root=next(iter(roots));summary=json.loads((root/'summary.json').read_text());record['runnerSummary']=summary
            assert summary['ExitCode']==0 and not summary['TimedOut'] and summary['OwnedTreeCleanupSucceeded'] and summary['RuntimeCleanupSucceeded']
            assert summary['Tests']['Executed']==0 and not [p for p in summary['PhaseResults'] if p['Name'].startswith('Build-')]
            if mode=='catalog':assert summary['CatalogAudit']['Valid'] and summary['DiscoveryOnly']
            else:
                plan=json.loads((root/'plan.json').read_text());record['plan']=plan
                expected='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss'
                assert summary['PlanOnly'] and summary['Selection']['PlannedDescriptors']==summary['Selection']['PlannedCases']==1
                assert len(plan['Descriptors'])==1 and plan['Descriptors'][0]['ExpectedMethodCounts']==[dict(Name=expected,Cases=1)]
            verify_staged(bindings)
        elif mode=='prepare':
            prior('catalog');prior('plan');verify_staged(bindings)
            lib['write'](phase/'D01Live.runtimeconfig.json',dict(runtimeOptions=dict(tfm='net8.0',framework=dict(name='Microsoft.NETCore.App',version='8.0.32'),rollForward='Disable')))
            args=['-nostdlib+','-target:exe','-debug:portable','-optimize-','-out:'+str(phase/'D01Live.dll')]
            args+=['-reference:'+p['path'] for p in inventory['net8ReferenceInputs']]
            args+=[str(REPO/'BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs'),str(RECOVERY/'l01-delivery-d01/Live.cs')]
            rsp=phase/'compile.rsp';rsp.write_text('\n'.join(args)+'\n')
            result=lib['run_owned']([DOTNET,'exec',lib['CSC'],'-noconfig','@'+str(rsp)],phase,'compile',env,60)
            record.update(compiler=result,nativeExit=result['nativeExit'],observerErrors=list(result['observerErrors']))
            record['outputs']=[lib['image'](phase/name) for name in ('D01Live.dll','D01Live.pdb','D01Live.runtimeconfig.json','compile.rsp') if (phase/name).is_file()]
            assert result['nativeExit']==0 and not result['observerErrors'] and len(record['outputs'])==4
        else:
            record['cases']=[]
            for expected in (0,7):
                case=d01['actual_case'](lib,phase,ROOT/'prepare',expected);record['cases'].append(case)
                if not case['passed']:break
            record['fixtureSubprocessTargets']=sum(c['targetLaunches'] for c in record['cases'])
            record['readerHarnessProcesses']=sum(c['readerLaunches'] for c in record['cases'])
            record['nativeExit']=0 if len(record['cases'])==2 and all(c['passed'] for c in record['cases']) else 2
            record['nativeExitScope']='Synthetic verification code; original target0/7 and reader native exits remain separate.'
        assert m01['binaries']()==old['binaryInventory'];record['originalNormal899UnchangedAfter']=True
        verify_staged(bindings)
    except BaseException as error:record['observerErrors'].append(repr(error))
    finally:
        record['durationSeconds']=time.monotonic()-start
        try:
            after,visibility=b01['proc_rows']();record.update(afterTools=b01['tool_summary'](after),afterVisibility=visibility,afterLiveTools=[p for p in after if p['state']!='Z'])
            if record['afterLiveTools']:record['observerErrors'].append('Active exact tool after phase; no signals/retry.')
        except BaseException as error:record['observerErrors'].append('Terminal census: '+repr(error))
    record['status']=('PASS_OWN_SYNTHETIC_DELIVERY2_ONLY' if mode=='synthetic' else 'PREPARED_COMPOSITE_ZERO_ONLY') if record['nativeExit']==0 and not record['observerErrors'] else 'FROZEN_FIRST_COMPOSITE_OUTCOME'
    result=lib['FINALIZE'](record,lambda r:lib['write'](phase/'record.json',r),d01['emit_summary'])
    return 128-result if result<0 else result

if __name__=='__main__':sys.exit(main())
