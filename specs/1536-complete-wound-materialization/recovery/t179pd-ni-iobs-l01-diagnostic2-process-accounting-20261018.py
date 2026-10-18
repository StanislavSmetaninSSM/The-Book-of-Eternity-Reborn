"""Bounded read-only OS accounting for one L01 diagnostic controller; no process intervention."""
from pathlib import Path
import datetime as dt, hashlib, json, os, time

ROLES = {'engine-joint-interface-original-crash', 'engine-joint-interface-resume'}

def utc(): return dt.datetime.now(dt.timezone.utc).isoformat()
def sha(raw): return hashlib.sha256(raw).hexdigest()

def parse_stat(raw, expected_pid):
    text = raw.decode('utf-8', errors='strict'); end = text.rfind(')')
    assert int(text.split(' ', 1)[0]) == expected_pid and end > 0
    fields = text[end + 2:].split(); assert len(fields) >= 22
    return dict(pid=expected_pid, state=fields[0], ppid=int(fields[1]),
                utime=int(fields[11]), stime=int(fields[12]), startTick=int(fields[19]),
                threads=int(fields[17]), vsize=int(fields[20]), rssPages=int(fields[21]), raw=text)

def argv_bytes(raw): return [part.decode('utf-8', errors='strict') for part in raw.split(b'\0') if part]

def target(argv, mutable_root, dotnet_root, repo):
    # Positively select only these genuine new fixture children, not arbitrary managed processes.
    if len(argv) != 13 or argv[0] != str(dotnet_root / 'dotnet') or argv[1:3] != ['exec', '--runtimeconfig'] or argv[4] != '--depsfile' or argv[7] not in ROLES:
        return None
    output_dir = repo / 'BookOfEternityClient.IntegrationTests/bin/Debug/net8.0'
    assert argv[3] == str(output_dir / 'BookOfEternityClient.IntegrationTests.runtimeconfig.json')
    assert argv[5] == str(output_dir / 'BookOfEternityClient.IntegrationTests.deps.json')
    assert argv[6] == str(output_dir / 'BookOfEternityClient.TestSupport.dll')
    assert argv[8] == str(output_dir / 'BookOfEternityClient.IntegrationTests.dll')
    own, root = Path(argv[12]), Path(argv[9])
    if not own.is_relative_to(mutable_root): return None
    assert own.parent == mutable_root and own.name.startswith('boe-joint-interface-crash-')
    assert argv[11] == '1'
    assert own == own.resolve(strict=True) and root == root.resolve(strict=True), 'fixture paths must be canonical and symlink-free'
    assert root == own if argv[7].endswith('original-crash') else root.is_relative_to(own / 'tmp')
    assert argv[10] == str(own / ('cut.json' if argv[7].endswith('original-crash') else 'resume.json'))
    parent_raw = (own / 'parent.json').read_bytes(); parent = json.loads(parent_raw)
    assert parent['Cut'] == 1 and parent['Fault'] is None and parent['Diagnostic'] is True
    nonce = parent['Nonce']; assert len(nonce) == 32 and all(c in '0123456789abcdef' for c in nonce)
    return dict(role=argv[7], root=str(root), own=str(own), nonce=nonce,
                parentPid=parent['ParentPid'], parentSha256=sha(parent_raw), argv=argv)

class Sampler:
    def __init__(self, mutable_root, dotnet_root, repo, out, proc=Path('/proc'), cgroup=Path('/sys/fs/cgroup')):
        self.mutable_root, self.dotnet_root, self.repo = map(Path, [mutable_root, dotnet_root, repo])
        self.proc, self.cgroup, self.out = Path(proc), Path(cgroup), Path(out)
        self.file = self.out.open('x'); self.clock = time.monotonic(); self.scans = 0; self.gaps = []
        self.identities = {}; self.retired = set(); self.sampler_cpu_start = time.process_time()
        self.emit(dict(kind='PROFILE_START', utc=utc(), clockTickHz=os.sysconf('SC_CLK_TCK'),
                       pageSize=os.sysconf('SC_PAGE_SIZE'), nominalPeriodSeconds=1,
                       mutableRoot=str(self.mutable_root), samplerPid=os.getpid(),
                       sourceSha256=sha(Path(__file__).read_bytes()),
                       limits='Dated observed samples only, no continuous coverage; leader state/wchan is not an all-thread wait oracle; cgroup counters include other processes.'))
        self.read_header()

    def emit(self, value):
        self.file.write(json.dumps(value, ensure_ascii=True, separators=(',', ':')) + '\n'); self.file.flush()

    def gap(self, rows, stage, error, pid=None, tid=None):
        value = dict(stage=stage, error=type(error).__name__ + ': ' + str(error), pid=pid, tid=tid)
        rows.append(value); self.gaps.append(dict(scan=self.scans, **value))

    def read_header(self):
        values = {}; gaps = []
        for key, path in [('bootId', self.proc/'sys/kernel/random/boot_id'), ('cpuMax', self.cgroup/'cpu.max'), ('memoryMax', self.cgroup/'memory.max')]:
            try: values[key] = path.read_text()
            except (OSError, ValueError) as error: self.gap(gaps, 'header-' + key, error)
        self.emit(dict(kind='PROFILE_ENVIRONMENT', utc=utc(), values=values, gaps=gaps))

    def missing(self, base, pid, stage, gaps, tid=None, prior_tick=None):
        """A missing file does not establish process/thread exit. Reprobe identity."""
        try:
            live=parse_stat((base/'stat').read_bytes(),tid if tid is not None else pid)
        except FileNotFoundError:
            return dict(observation='confirmed-absent-on-stat-reprobe',pid=pid,tid=tid,priorStartTick=prior_tick)
        except (OSError,ValueError,AssertionError) as error:
            self.gap(gaps,stage+'-reprobe-unreadable',error,pid,tid)
            return dict(observation='unknown-on-reprobe',pid=pid,tid=tid)
        self.gap(gaps,stage+'-still-live-or-reused',ValueError('missing read but stat reprobe remains present'),pid,tid)
        return dict(observation='live-or-reused-after-missing-read',pid=pid,tid=tid,stat=live,priorStartTick=prior_tick)

    def one(self, pid, gaps):
        base = self.proc / str(pid)
        first = parse_stat((base/'stat').read_bytes(), pid)
        command = (base/'cmdline').read_bytes(); args = argv_bytes(command)
        # Avoid opening fixture paths or validating dependencies for foreign roots.
        if len(args) != 13 or args[7] not in ROLES or not Path(args[12]).is_relative_to(self.mutable_root): return None
        binding = target(args, self.mutable_root, self.dotnet_root, self.repo)
        if binding is None: return None
        result = dict(identity=dict(pid=pid, startTick=first['startTick']), binding=binding, statBefore=first)
        for key, path in [('io', base/'io'), ('status', base/'status'), ('leaderWchan', base/'wchan')]:
            try:
                text = path.read_text()
                result[key] = '\n'.join(x for x in text.splitlines() if x.split(':',1)[0] in {'Name','State','Tgid','Pid','PPid','Threads','VmRSS','voluntary_ctxt_switches','nonvoluntary_ctxt_switches'}) if key=='status' else text
            except (OSError,ValueError) as error: self.gap(gaps, key, error, pid=pid)
        tasks=[]
        try:
            tids=sorted(int(x.name) for x in (base/'task').iterdir() if x.name.isdigit())
            if len(tids)>256: self.gap(gaps,'task-limit',ValueError(f'{len(tids)} tasks exceeds256'),pid=pid)
            for tid in tids[:256]:
                task=base/'task'/str(tid)
                try:
                    before=parse_stat((task/'stat').read_bytes(),tid); wchan=(task/'wchan').read_text(); after=parse_stat((task/'stat').read_bytes(),tid)
                    assert before['startTick']==after['startTick']
                    assert before['startTick']>=first['startTick'] and (tid!=pid or before['startTick']==first['startTick'])
                    tasks.append(dict(tid=tid,startTick=after['startTick'],statBefore=before,statAfter=after,wchan=wchan))
                except FileNotFoundError:
                    observation=self.missing(task,pid,'task-file-missing',gaps,tid)
                    tasks.append(observation)
                    self.gap(gaps,'incomplete-task-sample',ValueError(observation['observation']),pid,tid)
                except (OSError,ValueError,AssertionError) as error: self.gap(gaps,'task-sample',error,pid,tid)
        except (OSError,ValueError) as error: self.gap(gaps,'task-enumeration',error,pid)
        result['tasks']=tasks
        after=parse_stat((base/'stat').read_bytes(),pid); command_after=(base/'cmdline').read_bytes()
        assert first['startTick']==after['startTick'] and command==command_after, 'identity/argv changed during sample'
        result['statAfter']=after; result['argvSha256']=sha(command)
        key=(pid,after['startTick']); previous=self.identities.setdefault(key,binding)
        assert previous==binding,'same pid+birth binding changed'
        return result

    def capture(self, deadline):
        self.scans += 1; started=time.monotonic(); cpu_started=time.process_time(); gaps=[]; entries=[]; races=[]; cg={}; reconciled=[]
        try:
            for pid in sorted(int(x.name) for x in self.proc.iterdir() if x.name.isdigit()):
                try:
                    value=self.one(pid,gaps)
                    if value: entries.append(value)
                except FileNotFoundError:
                    races.append(self.missing(self.proc/str(pid),pid,'process-file-missing',gaps))
                except (OSError,ValueError,AssertionError) as error: self.gap(gaps,'process-sample',error,pid=pid)
        except (OSError,ValueError) as error: self.gap(gaps,'process-enumeration',error)
        observed={(x['identity']['pid'],x['identity']['startTick'])for x in entries}
        for identity in self.identities.keys()-self.retired-observed:
            pid,tick=identity
            try:
                live=parse_stat((self.proc/str(pid)/'stat').read_bytes(),pid)
                if live['startTick']!=tick:
                    observation=dict(observation='prior-identity-ended-pid-reused',pid=pid,priorStartTick=tick,currentStat=live)
                    self.retired.add(identity)
                elif live['state'] in {'Z','X','x'}:
                    observation=dict(observation='prior-identity-exited-nonexecuting',pid=pid,startTick=tick,stat=live)
                    self.retired.add(identity)
                else:
                    self.gap(gaps,'prior-identity-still-live-but-not-selected',ValueError('prior selected identity missing from successful samples'),pid)
                    observation=dict(observation='unknown-live-identity-not-selected',pid=pid,startTick=tick,stat=live)
            except FileNotFoundError:
                observation=self.missing(self.proc/str(pid),pid,'prior-identity-missing',gaps,prior_tick=tick)
                if observation['observation']=='confirmed-absent-on-stat-reprobe':self.retired.add(identity)
            except (OSError,ValueError,AssertionError) as error:
                self.gap(gaps,'prior-identity-reconciliation',error,pid)
                observation=dict(observation='unknown-prior-identity',pid=pid,startTick=tick)
            reconciled.append(observation)
        for key in ['cpu.stat','cpu.max','memory.current','memory.events','memory.max']:
            try: cg[key]=(self.cgroup/key).read_text()
            except (OSError,ValueError) as error: self.gap(gaps,'cgroup-'+key,error)
        finished=time.monotonic()
        self.emit(dict(kind='PROFILE_SCAN',scan=self.scans,utc=utc(),monotonicStart=started,
                       monotonicEnd=finished,elapsedSeconds=finished-self.clock,
                       scheduledMonotonic=deadline,lateBySeconds=max(0,started-deadline),
                       durationSeconds=finished-started,samplerCpuSeconds=time.process_time()-cpu_started,entries=entries,cgroup=cg,gaps=gaps,discoveryRaces=races,priorIdentityReconciliation=reconciled))

    def finish(self, native, error=None):
        if error is not None: self.gaps.append(dict(stage='sampler-loop',error=repr(error)))
        summary=dict(kind='PROFILE_FINISH',utc=utc(),nativeControllerExit=native,scans=self.scans,
                     elapsedSeconds=time.monotonic()-self.clock,samplerTotalCpuSeconds=time.process_time()-self.sampler_cpu_start,
                     cpuAccounting='Process ticks already include threads; never add task ticks to process ticks. Per-task stats locate observed activity only.',
                     identities=[dict(pid=k[0],startTick=k[1],**v)for k,v in self.identities.items()],
                     gaps=self.gaps,status='OBSERVATION_GAPS_FREEZE_NEXT_RELEASE' if self.gaps else 'OBSERVATIONS_SAVED_REQUIRES_INDEPENDENT_CAUSAL_REVIEW',
                     countsToward57=False,noNativeIntervention=True)
        self.emit(summary);self.file.close();return summary
