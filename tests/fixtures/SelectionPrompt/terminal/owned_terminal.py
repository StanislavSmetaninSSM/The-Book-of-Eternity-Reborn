"""Fullroute-only owned sidecar transport. No screen emulation or coordinate synthesis."""
import base64, hashlib, json, os, pathlib, select, subprocess, time

class OwnedTerminal:
    def __init__(self, node, prefix, evidence, deadline, checked_pty_writer):
        self.deadline=deadline; self.writer=checked_pty_writer; self.seq=0
        self.pending=False; self.failed=False; self.closed=False; self.buffer=b''
        self.audit=[]; self.evidence=pathlib.Path(evidence)
        here=pathlib.Path(__file__).resolve().parent
        pins=json.loads((here/'provenance.json').read_text())
        prefix=pathlib.Path(prefix)
        if prefix.is_symlink(): raise ValueError('Linked dependency root')
        expected={k:v for entry in pins.values() for k,v in entry['files'].items()}
        actual={str(p.relative_to(prefix)) for p in (prefix/'node_modules').rglob('*') if p.is_file()}
        if actual!=set(expected): raise ValueError('Dependency inventory mismatch')
        for name,digest in expected.items():
            p=prefix/name
            if any(x.is_symlink() for x in [p,*p.parents]) or hashlib.sha256(p.read_bytes()).hexdigest()!=digest:
                raise ValueError('Dependency hash/link mismatch')
        self.stderr=open(self.evidence/'terminal.stderr.raw','xb')
        try:
            self.process=subprocess.Popen([node,str(here/'sidecar.mjs'),str(prefix)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=self.stderr)
            ready=self._read()
            if ready!={'op':'READY','headless':'6.0.0','unicodeAddon':'0.9.0','unicode':'11','cols':100,'rows':25}:
                raise ValueError('Terminal readiness mismatch')
        except BaseException:
            self.failed=True
            # Once started, the enclosing existing guardian retains ownership;
            # never pretend a failed constructor means the process was joined.
            self.stderr.close();raise

    def _time(self):
        if time.monotonic()>=self.deadline: raise TimeoutError('Original terminal deadline')
    def _read(self):
        while b'\n' not in self.buffer:
            self._time()
            if not select.select([self.process.stdout],[],[],min(.05,self.deadline-time.monotonic()))[0]:continue
            raw=os.read(self.process.stdout.fileno(),65536)
            if not raw: raise EOFError('Sidecar EOF before protocol result')
            self.buffer+=raw
            if len(self.buffer)>1048576: raise ValueError('Oversized sidecar output')
        line,self.buffer=self.buffer.split(b'\n',1); self._time()
        return json.loads(line)
    def _command(self,op,**fields):
        if self.failed or self.closed:raise ValueError('Terminal unavailable')
        self._time();self.seq+=1
        value={'op':op,'seq':self.seq,**fields}
        self.process.stdin.write((json.dumps(value)+'\n').encode());self.process.stdin.flush()
        reply=self._read()
        if reply.get('seq')!=self.seq:raise ValueError('Terminal sequence mismatch')
        self.audit.append({'command':value,'reply':reply})
        return reply
    def feed(self,raw):
        try:
            result=self._command('WRITE',bytes=base64.b64encode(raw).decode())
            if result.get('op')!='WRITTEN' or type(result.get('pending')) is not bool:raise ValueError('Invalid terminal result')
            self.pending=result['pending']
            # Buffered replies become eligible only after the upstream write callback.
            for response in result['replies']:
                self._time();data=base64.b64decode(response['bytes'],validate=True)
                self.writer(data,'terminal-reply',self.seq)
            return result
        except BaseException:self.failed=True;raise
    def key(self,data,label):
        # Caller must first drain current PTY output and bind its fresh-menu cut.
        if self.pending or self.failed or self.closed:raise ValueError('Unsettled terminal prefix')
        self._time();self.writer(data,'route-key:'+label,self.seq)
    def snapshot(self):
        if self.pending:raise ValueError('Partial terminal prefix')
        result=self._command('SNAPSHOT')
        if result.get('op')!='SNAPSHOT':raise ValueError('Snapshot mismatch')
        return result
    def close(self):
        result=self._command('CLOSE')
        if result.get('op')!='CLOSED':raise ValueError('Close not acknowledged')
        self.closed=True;self.process.stdin.close()
        self.process.wait(timeout=max(.001,self.deadline-time.monotonic()))
        trailing=self.process.stdout.read();self.process.stdout.close();self.stderr.close()
        (self.evidence/'terminal.protocol.json').write_text(json.dumps(self.audit,indent=2)+'\n')
        if self.process.returncode!=0 or self.buffer or trailing:raise ValueError('Sidecar failed or undrained output')
