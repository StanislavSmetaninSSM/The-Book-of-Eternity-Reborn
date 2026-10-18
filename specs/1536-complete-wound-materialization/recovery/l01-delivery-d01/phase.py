"""D01 ZERO preparation / explicitly gated own synthetic; no game actual entrypoint."""
from contextlib import contextmanager
from pathlib import Path
import hashlib
import json
import os
import runpy
import subprocess
import sys
import time
import uuid

HERE=Path(__file__).resolve().parent
REPO=HERE.parents[3]
RECOVERY=HERE.parent
ROOT=Path('/tmp/l01d01-76df8b42f6ad4ea2')
PACKET=RECOVERY/'l01-delivery-d01-source-packet-20261018.json'
CONTROL=RECOVERY/'l01-delivery-d01-control-20261018.json'
DOTNET='/tmp/1536-cloud-tools/dotnet/dotnet'
sha=lambda b:hashlib.sha256(b).hexdigest()


def verify_installed_inputs(inventory):
    rows=inventory['availableFiles']+inventory['net8ReferenceInputs']
    for row in rows:
        data=Path(row['path']).read_bytes()
        assert len(data)==row['bytes'] and sha(data)==row['sha256'],row['path']
    return dict(count=len(rows),sha256=sha(json.dumps(rows,sort_keys=True,separators=(',',':')).encode()))


def emit_summary(record):
    payload=(json.dumps({k:record[k] for k in ('status','nativeExit','observerErrors','durationSeconds')})+'\n').encode()
    # Keep all reporting errors inside FINALIZE; no buffered shutdown flush.
    if os.write(1,payload)!=len(payload):raise OSError('Incomplete summary write')


def actual_case(lib,phase,prep,expected_exit):
    case=phase/('exit'+str(expected_exit));case.mkdir(mode=0o700)
    fixture=case/'fixture';fixture.mkdir(mode=0o700)
    env=lib['environment'](case)
    nonce=uuid.uuid4().hex
    base=[DOTNET,'exec','--runtimeconfig',str(prep/'D01Live.runtimeconfig.json'),str(prep/'D01Live.dll')]
    record=dict(expectedTargetNativeExit=expected_exit,nonce=nonce,root=str(fixture),target=None,reader=None,targetLaunches=0,readerLaunches=0,
                observerErrors=[],primaryFailure=None,passed=False)
    target=reader=tfd=rfd=None;handles=[]
    lib['write'](case/'started.json',record)
    def own(process,command):
        identity=lib['identity'](process.pid)
        assert identity['parentPid']==os.getpid() and identity['uid']==os.geteuid()
        assert Path('/proc',str(process.pid),'exe').resolve()==Path(DOTNET).resolve()
        assert Path('/proc',str(process.pid),'cmdline').read_bytes().split(b'\0')[:-1]==[x.encode() for x in command]
        fd=lib['open_owned'](process.pid,identity)
        return dict(command=command,identity=identity,nativeExit=None,joined=False),fd
    try:
        target_err=(case/'target.stderr').open('wb');handles.append(target_err)
        target_command=base+['--child',str(fixture),nonce,str(expected_exit)]
        target=subprocess.Popen(target_command,cwd=case,env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=target_err)
        record['targetLaunches']=1;record['targetLaunchPid']=target.pid
        record['target'],tfd=own(target,target_command)
        lib['write'](case/'target-identity.json',record['target'])
        manifest=case/'reader-manifest.json'
        lib['write'](manifest,dict(Pid=target.pid,BirthTick=record['target']['identity']['birthTick'],Root=str(fixture),Nonce=nonce))
        reader_out=(case/'reader.stdout').open('wb');reader_err=(case/'reader.stderr').open('wb');handles.extend([reader_out,reader_err])
        reader_command=base+['read',str(manifest)]
        # Supervisor never reads target stdout. Exactly one .NET StreamReader owns that inherited pipe.
        reader=subprocess.Popen(reader_command,cwd=case,env=env,stdin=target.stdout,stdout=reader_out,stderr=reader_err)
        record['readerLaunches']=1;record['readerLaunchPid']=reader.pid
        record['reader'],rfd=own(reader,reader_command)
        target.stdout.close()
        lib['write'](case/'reader-identity.json',record['reader'])
        deadline=time.monotonic()+10
        pulse=None
        while time.monotonic()<deadline:
            data=(case/'reader.stdout').read_bytes()
            complete=[line for line in data.splitlines(keepends=True) if line.endswith(b'\n')]
            messages=[json.loads(line) for line in complete]
            pulse=next((m for m in messages if m.get('Kind')=='D01_LIVE_PULSE'),None)
            if pulse is not None:break
            if target.poll() is not None or reader.poll() is not None:break
            time.sleep(.01)
        assert pulse is not None,'No complete live pulse before bounded observation deadline.'
        e=pulse['Event'];health=pulse['Health']
        assert pulse['ReaderPid']==reader.pid and e['Pid']==target.pid and e['BirthTick']==record['target']['identity']['birthTick']
        assert e['Root']==str(fixture) and e['Nonce']==nonce and e['Cut']==1 and e['Control']=='CUSTODY_RETURN'
        assert pulse['RawPending'] and not health['Eof'] and target.poll() is None and reader.poll() is None
        assert (not health['Uncertain']) if expected_exit==0 else (health['Uncertain'] and health['Malformed']==1)
        record.update(livePulse=pulse,childAliveBeforeStop=True,pulseReceivedMonotonic=time.monotonic(),sourceToReceiveLatency='UNKNOWN',capturePhase='UNKNOWN')
        target.stdin.write(('STOP:'+nonce+'\n').encode());target.stdin.flush();target.stdin.close()
        record['target']['nativeExit']=target.wait(timeout=5);record['target']['joined']=True
        record['reader']['nativeExit']=reader.wait(timeout=5);record['reader']['joined']=True
        messages=[json.loads(line) for line in (case/'reader.stdout').read_text().splitlines()]
        terminal=next(m for m in messages if m.get('Kind')=='D01_READER_TERMINAL')
        child=json.loads((case/'target.stderr').read_text())
        assert child['Pid']==target.pid and child['BirthTick']==record['target']['identity']['birthTick'] and child['Root']==str(fixture) and child['Nonce']==nonce
        assert terminal['ReaderPid']==reader.pid and terminal['Failure'] is None and terminal['Health']['Eof'] and not terminal['Health']['RawReadFault']
        assert terminal['Runtime']=='.NET 8.0.32' and terminal['RawStdoutSha256']==child['RawStdoutSha256']==sha(terminal['RawStdout'].encode())
        assert record['target']['nativeExit']==expected_exit and record['reader']['nativeExit']==0
        assert not (case/'reader.stderr').read_bytes()
        record.update(terminal=terminal,childSelfReport=child,rawStdoutChildHashMatches=True,joinedEof=True,passed=True)
    except BaseException as error:
        record['primaryFailure']=repr(error)
    finally:
        # All handles refer to direct children just spawned here; never global PID/name cleanup.
        for name,process,fd in [('reader',reader,rfd),('target',target,tfd)]:
            if process is None:continue
            slot=record[name]
            try:
                if process.poll() is None:
                    if name=='target' and process.stdin is not None and not process.stdin.closed:
                        try:process.stdin.write(('STOP:'+nonce+'\n').encode());process.stdin.flush();process.stdin.close();process.wait(timeout=2)
                        except BaseException as error:record['observerErrors'].append('Cooperative own cleanup: '+repr(error))
                    if process.poll() is None:
                        assert fd is not None,'Unverified process retained; no signal by PID.'
                        lib['terminate'](fd)
                        if slot is not None:slot['cleanupExit']=process.wait(timeout=5)
                if slot is not None:
                    if slot['nativeExit'] is None and 'cleanupExit' not in slot:slot['nativeExit']=process.returncode
                    slot['joined']=process.returncode is not None
            except BaseException as error:record['observerErrors'].append(name+' cleanup: '+repr(error))
            if fd is not None:lib['close_observed'](record,name+' pidfd close',lambda fd=fd:os.close(fd))
            for stream in [process.stdin,process.stdout,process.stderr]:
                if stream is not None:lib['close_observed'](record,name+' stream close',stream.close)
        for handle in handles:lib['close_observed'](record,'artifact close',handle.close)
    record['passed']=record['passed'] and not record['observerErrors'] and record['primaryFailure'] is None
    try:lib['write'](case/'record.json',record)
    except BaseException as error:
        record['observerErrors'].append('Case record publication: '+repr(error));record['passed']=False
    return record


def main():
    mode,source=sys.argv[1:]
    assert mode in ('catalog','plan','prepare','synthetic')
    git=lambda *args:subprocess.check_output(['git','-C',str(REPO),*args]).decode().strip()
    assert git('rev-parse','HEAD')==source and not git('status','--porcelain')
    packet=json.loads(PACKET.read_text());control=json.loads(CONTROL.read_text())
    for p in packet['sourcePins']:
        b=(REPO/p['path']).read_bytes();assert len(b)==p['bytes'] and sha(b)==p['sha256'],p['path']
    assert packet['sourcePinDigest']==control['sourcePinDigest']
    gate=control['synthetic'] if mode=='synthetic' else control['source']
    assert gate['status']==('SOURCE_PREP_ACCEPTED_ONCE' if mode=='synthetic' else 'SOURCE_ACCEPTED_ZERO_ONLY')
    assert sha((REPO/gate['reviewPath']).read_bytes())==gate['reviewSha256']
    s01=RECOVERY/'l01-managed-stack-s01/smoke.py'
    assert sha(s01.read_bytes())=='d121731cff4867acf993d034962c9f5591fdcb496307a5fe0e9642b8392da333'
    lib=runpy.run_path(str(s01));m01=runpy.run_path(str(RECOVERY/'t148-m01-run-phase-20261018.py'))
    os.umask(0o077)
    assert ROOT.stat().st_uid==os.geteuid() and ROOT.stat().st_mode & 0o777==0o700
    phase=ROOT/('d01-'+mode);phase.mkdir(mode=0o700)
    env=lib['environment'](phase)
    env['PATH']='/tmp/1536-cloud-tools/pwsh:/tmp/1536-cloud-tools/dotnet:/usr/bin:/bin'
    env['NUGET_PACKAGES']='/tmp/1536-cloud-tools/nuget'
    env['NuGetAudit']='false'
    record=dict(kind='D01_FIRST_'+mode.upper(),source=source,sourcePinDigest=packet['sourcePinDigest'],nativeExit=None,
        observerErrors=[],gameProcesses=0,diagnosticRequests=0,fixtureSubprocessTargets=0,phaseRoot=str(phase))
    lib['write'](phase/'started.json',record)
    start=time.monotonic()
    try:
        inventory=json.loads((RECOVERY/'l01-managed-stack-capability-inventory-20261018.json').read_text())
        # Current packet pins the inventory itself. Recheck its installed inputs
        # for every phase, including after compilation and before synthetic launch.
        record['verifiedInstalledInputs']=verify_installed_inputs(inventory)
        if mode in ('catalog','plan'):
            if mode=='plan':
                cat=json.loads((ROOT/'d01-catalog/record.json').read_text())
                assert cat['nativeExit']==0 and not cat['observerErrors'] and cat['sourcePinDigest']==packet['sourcePinDigest']
                assert m01['binaries']()==cat['binaryInventory']
                record['compiledSource']=cat['source']
            command=['/tmp/1536-cloud-tools/pwsh/pwsh','-NoLogo','-NoProfile','-NonInteractive','-File','scripts/test-csharp.ps1']
            command+=['-ValidateCatalog'] if mode=='catalog' else ['-Category',packet['category'],'-PlanOnly','-NoBuild']
            record['command']=command
            results=REPO/'TestResults/test-categories';before=set(results.iterdir()) if results.exists() else set()
            @contextmanager
            def outputs():
                with (phase/'stdout.txt').open('wb') as out,(phase/'stderr.txt').open('wb') as err:yield out,err
            m01['capture_native'](record,outputs,lambda out,err:subprocess.run(command,cwd=REPO,env=env,stdout=out,stderr=err).returncode)
            after=set(results.iterdir()) if results.exists() else set()
            record['resultRoots']=[str(p) for p in sorted(after-before)]
            record['runnerArtifacts']=[lib['image'](p) for root in after-before for p in sorted(root.rglob('*')) if p.is_file()]
            record['binaryInventory']=m01['binaries']()
            if mode=='catalog':record['compiledSource']=source
            else:assert record['binaryInventory']==cat['binaryInventory']
        elif mode=='prepare':
            lib['write'](phase/'D01Live.runtimeconfig.json',dict(runtimeOptions=dict(tfm='net8.0',framework=dict(name='Microsoft.NETCore.App',version='8.0.32'),rollForward='Disable')))
            args=['-nostdlib+','-target:exe','-debug:portable','-optimize-','-out:'+str(phase/'D01Live.dll')]
            args+=['-reference:'+p['path'] for p in inventory['net8ReferenceInputs']]
            args+=[str(REPO/'BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs'),str(HERE/'Live.cs')]
            rsp=phase/'compile.rsp';rsp.write_text('\n'.join(args)+'\n')
            command=[DOTNET,'exec',lib['CSC'],'-noconfig','@'+str(rsp)]
            result=lib['run_owned'](command,phase,'compile',env,60)
            record.update(compiler=result,nativeExit=result['nativeExit'],observerErrors=result['observerErrors'])
            record['outputs']=[lib['image'](p) for p in sorted(phase.iterdir()) if p.is_file()]
        else:
            for name,expected_hash in gate['prepRecords'].items():
                p=ROOT/('d01-'+name)/'record.json';assert sha(p.read_bytes())==expected_hash
                prior=json.loads(p.read_text());assert prior['nativeExit']==0 and not prior['observerErrors'] and prior['sourcePinDigest']==packet['sourcePinDigest']
            prepared=ROOT/'d01-prepare';prior=json.loads((prepared/'record.json').read_text())
            for p in prior['outputs']:assert lib['image'](p['path'])==p
            record['cases']=[];record['fixtureSubprocessTargetsPlanned']=2
            for expected in (0,7):
                case=actual_case(lib,phase,prepared,expected);record['cases'].append(case)
                if not case['passed']:break
            record['fixtureSubprocessTargets']=sum(c['targetLaunches'] for c in record['cases'])
            record['readerHarnessProcesses']=sum(c['readerLaunches'] for c in record['cases'])
            record['nativeExit']=0 if len(record['cases'])==2 and all(c['passed'] for c in record['cases']) else 2
            record['nativeExitScope']='Synthetic selection verification result; actual target native0/7 and reader exits retained separately unchanged.'
        record['status']=('PASS_OWN_SYNTHETIC_DELIVERY2_ONLY' if mode=='synthetic' else 'PREPARED_ZERO_FIXTURE_TARGETS') if record['nativeExit']==0 and not record['observerErrors'] else 'FROZEN_ORIGINAL_FAILURE'
    except BaseException as error:
        record['observerErrors'].append(repr(error));record['status']='FROZEN_ORIGINAL_FAILURE'
    finally:
        record['durationSeconds']=time.monotonic()-start
    return lib['FINALIZE'](record,lambda r:lib['write'](phase/'record.json',r),emit_summary)

if __name__=='__main__':sys.exit(main())
