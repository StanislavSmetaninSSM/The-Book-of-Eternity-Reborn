"""One reviewed build-only observation; no test, fixture, game or attach mode."""
from pathlib import Path
import hashlib
import json
import os
import resource
import runpy
import shutil
import subprocess
import sys
import time

REPO=Path(__file__).resolve().parents[3]
RECOVERY=Path(__file__).resolve().parent
ROOT=Path('/tmp/l01bd-772d38df9b5b4cf8')
DOTNET='/tmp/1536-cloud-tools/dotnet/dotnet'
sha=lambda b:hashlib.sha256(b).hexdigest()

def proc_rows(session=None):
    rows=[];visibility=dict(executableFallbacks=0,positivelyVanished=0)
    for p in Path('/proc').iterdir():
        if not p.name.isdecimal():continue
        first_tick=None
        for attempt in range(2):
            try:
                if p.stat().st_uid!=os.geteuid():break
                raw=(p/'stat').read_text();v=raw[raw.rfind(')')+2:].split();comm=raw[raw.find('(')+1:raw.rfind(')')];tick=int(v[19])
                if first_tick is not None and first_tick!=tick:raise RuntimeError('PID reused during census: '+p.name)
                first_tick=tick;fallback=False;basis='same owned session'
                include=int(v[3])==session if session is not None else comm in ('dotnet','pwsh')
                if session is None and v[0]!='Z':
                    try:executable=os.readlink(p/'exe');basis='executable'
                    except PermissionError:
                        argv0=(p/'cmdline').read_bytes().split(b'\0',1)[0].decode()
                        if not argv0:raise RuntimeError('Live executable/argv identity unavailable: '+p.name)
                        executable=argv0;fallback=True;basis='exact argv0; executable unreadable'
                    include=include or executable in (DOTNET,'/tmp/1536-cloud-tools/pwsh/pwsh')
                check=(p/'stat').read_text();again=check[check.rfind(')')+2:].split()
                if int(again[19])!=tick:raise RuntimeError('PID identity changed during census: '+p.name)
                if fallback:visibility['executableFallbacks']+=1
                if include:rows.append(dict(pid=int(p.name),birthTick=tick,parentPid=int(v[1]),group=int(v[2]),session=int(v[3]),
                    state=v[0],comm=comm,cpuTicks=int(v[11])+int(v[12]),rssBytes=int(v[21])*os.sysconf('SC_PAGE_SIZE'),identityBasis=basis))
                break
            except FileNotFoundError:
                if not p.exists():visibility['positivelyVanished']+=1;break
                if attempt:raise RuntimeError('Still-present PID read unresolved: '+p.name)
    return rows,visibility

def resources_now():
    files={}
    for name in ('cpu.max','cpu.stat','memory.current','memory.max','memory.events'):
        files[name]=Path('/sys/fs/cgroup',name).read_text().strip()
    assert int(files['memory.current'])>=0 and (files['memory.max']=='max' or int(files['memory.max'])>0)
    quota,period=files['cpu.max'].split();assert (quota=='max' or int(quota)>0) and int(period)>0
    for name in ('cpu.stat','memory.events'):
        assert files[name] and all(int(line.split()[1])>=0 for line in files[name].splitlines())
    mount=next(line for line in Path('/proc/self/mountinfo').read_text().splitlines() if ' - cgroup2 ' in line and line.split()[4]=='/sys/fs/cgroup')
    node=Path('/sys/fs/cgroup').stat()
    return dict(monotonic=time.monotonic(),cgroup=files,cgroupMembership=Path('/proc/self/cgroup').read_text().strip(),
        cgroupMount=mount,mountNode=dict(device=node.st_dev,inode=node.st_ino),
        counterScope='Observed shared mount node; effective per-build/ancestor limits and exclusive attribution are unproved.',
        affinity=sorted(os.sched_getaffinity(0)),load=os.getloadavg(),tmpFreeBytes=shutil.disk_usage('/tmp').free)

def tool_summary(rows):
    return [dict(comm=comm,state=state,count=sum(p['comm']==comm and p['state']==state for p in rows))
        for comm,state in sorted({(p['comm'],p['state']) for p in rows})]

def main():
    source,review_relative,review_hash=sys.argv[1:]
    git=lambda *args:subprocess.check_output(['git','-C',str(REPO),*args]).decode().strip()
    assert git('rev-parse','HEAD')==source and not git('status','--porcelain')
    review_path=REPO/review_relative;review=json.loads(review_path.read_text())
    assert sha(review_path.read_bytes())==review_hash
    assert review['buildOnlyAdmission']=='ACCEPT_ONE_ISOLATED_UNIT_BUILD_ONLY'
    assert review['buildScriptSha256']==sha(Path(__file__).read_bytes())
    packet=json.loads((RECOVERY/'l01-delivery-d01-source-packet-20261018.json').read_text())
    for pin in packet['sourcePins']:
        data=(REPO/pin['path']).read_bytes();assert len(data)==pin['bytes'] and sha(data)==pin['sha256'],pin['path']
    assert not any(Path('/proc',str(pid)).exists() for pid in (97377,97402))
    before_tools,before_visibility=proc_rows();assert not [p for p in before_tools if p['state']!='Z'],'Active dotnet/pwsh: no build launched.'
    assert shutil.disk_usage('/tmp').free>2*1024**3
    inventory=json.loads((RECOVERY/'l01-managed-stack-capability-inventory-20261018.json').read_text())
    for pin in inventory['availableFiles']+inventory['net8ReferenceInputs']:
        data=Path(pin['path']).read_bytes();assert len(data)==pin['bytes'] and sha(data)==pin['sha256'],pin['path']
    smoke=RECOVERY/'l01-managed-stack-s01/smoke.py'
    assert sha(smoke.read_bytes())=='d121731cff4867acf993d034962c9f5591fdcb496307a5fe0e9642b8392da333'
    before_resources=resources_now()
    os.umask(0o077);ROOT.mkdir(mode=0o700)
    lib=runpy.run_path(str(smoke))
    env=lib['environment'](ROOT)
    env.update(NUGET_PACKAGES='/tmp/1536-cloud-tools/nuget',NuGetAudit='false',DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
    build=[DOTNET,'build','BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj','--verbosity','minimal',
        '--artifacts-path',str(ROOT/'artifacts'),'--disable-build-servers','-p:GenerateDocumentationFile=true','-p:NoWarn=1591',
        '-p:NuGetAudit=false','-p:RestoreSources=/tmp/1536-cloud-tools/nuget','-clp:PerformanceSummary;Summary']
    command=['/usr/bin/timeout','--verbose','--signal=TERM','--kill-after=10s','600s',*build]
    record=dict(kind='D01_SEPARATE_UNIT_BUILD_DIAGNOSTIC_ONCE',source=source,sourcePinDigest=packet['sourcePinDigest'],
        root=str(ROOT),command=command,buildCommand=build,nativeExit=None,nativeExitScope='Raw Popen timeout-wrapper return; negative values encode signals. Normal0 passes through build success; other values never invent a separately known child build exit.',
        observerErrors=[],samples=[],beforeTools=tool_summary(before_tools),beforeVisibility=before_visibility,beforeResources=before_resources,testsExecuted=0,gameProcesses=0,
        fixtureTargets=0,syntheticRuns=0,diagnosticRequests=0,originalCatalogNativeExit=124,originalOutcomeReplaced=False,
        modeDifferences='Standalone unit5-project build; private outputs/CLI dirs/local cached feed; persistent build servers ignored. Result is not a controlled single-variable cause proof or full catalog/PREP qualification.')
    lib['write'](ROOT/'started.json',record)
    start=time.monotonic();child=None
    try:
        with (ROOT/'stdout.txt').open('wb') as out,(ROOT/'stderr.txt').open('wb') as err:
            child=subprocess.Popen(command,cwd=REPO,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,start_new_session=True)
            record['wrapperPid']=child.pid;record['wrapperIdentity']=lib['identity'](child.pid)
            assert record['wrapperIdentity']['parentPid']==os.getpid()
            while True:
                try:
                    processes,visibility=proc_rows(child.pid)
                    record['samples'].append(dict(elapsed=time.monotonic()-start,processes=processes,visibility=visibility,resources=resources_now()))
                except Exception as error:record['observerErrors'].append('Sample: '+repr(error))
                try:
                    record['nativeExit']=child.wait(timeout=2);break
                except subprocess.TimeoutExpired:pass
            record['joined']=True
    except BaseException as error:
        record['observerErrors'].append(repr(error))
    finally:
        if child is not None and record['nativeExit'] is None:
            # GNU timeout owns the new process group and600s/10s bound, independently
            # of this observer. Never issue a global/server/PID signal here.
            try:record['nativeExit']=child.wait(timeout=max(1,615-(time.monotonic()-start)));record['joined']=True
            except BaseException as error:record['observerErrors'].append('Bounded wrapper join: '+repr(error))
        record['durationSeconds']=time.monotonic()-start
        try:
            record['afterResources']=resources_now();after_tools,visibility=proc_rows();record['afterTools']=tool_summary(after_tools);record['afterVisibility']=visibility
            record['afterLiveTools']=[p for p in after_tools if p['state']!='Z']
            record['remainingSessionProcesses']=[] if child is None else [p for p in proc_rows(child.pid)[0] if p['state']!='Z']
            if record['remainingSessionProcesses']:record['observerErrors'].append('Live own-session process remains; retain root and stop.')
            if record['afterLiveTools']:record['observerErrors'].append('Active tool process after build; ownership unresolved, no signal or retry.')
            ru=resource.getrusage(resource.RUSAGE_CHILDREN)
            record['childrenRusage']=dict(userSeconds=ru.ru_utime,systemSeconds=ru.ru_stime,maxRssKiB=ru.ru_maxrss,
                limit='Cumulative waited children of this observer, including preflight Git queries; maxRSS is not simultaneous tree peak.')
        except BaseException as error:record['observerErrors'].append('Terminal observation: '+repr(error))
    record['rawWrapperReturnCode']=record['nativeExit']
    record['wrapperTerminationSignal']=-record['nativeExit'] if record['nativeExit'] is not None and record['nativeExit']<0 else None
    record['controllerExitConvention']='Negative Popen return -signal is preserved raw; external controller exit uses128+signal. Other exits follow native-priority FINALIZE.'
    record['status']='PASS_BUILD_ONLY_DIAGNOSTIC' if record['nativeExit']==0 and not record['observerErrors'] else 'FROZEN_NEW_BUILD_DIAGNOSTIC_OUTCOME'
    def emit(value):
        payload=(json.dumps({k:value[k] for k in ('status','nativeExit','observerErrors','durationSeconds')})+'\n').encode()
        if os.write(1,payload)!=len(payload):raise OSError('Incomplete summary write')
    result=lib['FINALIZE'](record,lambda value:lib['write'](ROOT/'record.json',value),emit)
    return 128-result if result<0 else result

if __name__=='__main__':sys.exit(main())
