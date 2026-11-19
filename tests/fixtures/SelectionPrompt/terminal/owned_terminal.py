"""Fullroute-only owned sidecar transport. No screen emulation or coordinate synthesis."""
import base64, hashlib, json, os, pathlib, select, stat, subprocess, time

class OwnedTerminal:
    def __init__(self, node, prefix, evidence, deadline, checked_pty_writer):
        self.deadline=deadline; self.writer=checked_pty_writer; self.seq=0
        self.pending=False; self.failed=False; self.closed=False; self.buffer=b''
        self.process=None; self.stderr=None; self.audit=[]; self.evidence=pathlib.Path(evidence)
        here=pathlib.Path(__file__).resolve().parent
        pins=json.loads((here/'provenance.json').read_text())
        prefix=pathlib.Path(prefix)
        if prefix.is_symlink(): raise ValueError('Linked dependency root')
        expected={k:v for entry in pins.values() for k,v in entry['files'].items()}
        entries=list((prefix/'node_modules').rglob('*'))
        if any(p.is_symlink() or not (stat.S_ISREG(p.lstat().st_mode) or stat.S_ISDIR(p.lstat().st_mode)) for p in entries):
            raise ValueError('Linked or special dependency entry')
        actual={str(p.relative_to(prefix)) for p in entries if p.is_file()}
        if actual!=set(expected): raise ValueError('Dependency inventory mismatch')
        for name,digest in expected.items():
            p=prefix/name
            if any(x.is_symlink() for x in [p,*p.parents]) or hashlib.sha256(p.read_bytes()).hexdigest()!=digest:
                raise ValueError('Dependency hash/link mismatch')
        nodepath=pathlib.Path(node)
        if not nodepath.is_absolute() or nodepath.is_symlink() or hashlib.sha256(nodepath.read_bytes()).hexdigest()!='acbe4b8d9153b841ed0697cbfc4f93e767386620edb745ab9358306c0a6f31aa':
            raise ValueError('Pinned Node v24.22.3 binary mismatch')
        self.stderr=open(self.evidence/'terminal.stderr.raw','xb')
        try:
            self._time() # Admission after provenance work, before any child starts.
            self.process=subprocess.Popen([node,str(here/'sidecar.mjs'),str(prefix)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=self.stderr)
            os.set_blocking(self.process.stdin.fileno(),False)
            ready=self._read()
            if ready!={'op':'READY','headless':'6.0.0','unicodeAddon':'0.9.0','unicode':'11','node':'v24.22.3','cols':100,'rows':25}:
                raise ValueError('Terminal readiness mismatch')
        except BaseException:
            self.failed=True
            self.abort()
            raise

    def _time(self):
        if time.monotonic()>=self.deadline: raise TimeoutError('Original terminal deadline')
    def _read(self):
        while b'\n' not in self.buffer:
            self._time()
            if not select.select([self.process.stdout],[],[],min(.05,self.deadline-time.monotonic()))[0]:continue
            raw=os.read(self.process.stdout.fileno(),65536)
            if not raw: raise EOFError('Sidecar EOF before protocol result')
            with open(self.evidence/'terminal.stdout.raw','ab') as capture:capture.write(raw)
            self.buffer+=raw
            if len(self.buffer)>1048576: raise ValueError('Oversized sidecar output')
        line,self.buffer=self.buffer.split(b'\n',1); self._time()
        return json.loads(line)
    def _command(self,op,**fields):
        if self.failed or self.closed:raise ValueError('Terminal unavailable')
        value={'op':op,'seq':self.seq+1,**fields}
        try:
            self._time();self.seq+=1
            self._record({'command':value})
            data=(json.dumps(value)+'\n').encode()
            while data:
                self._time()
                if not select.select([], [self.process.stdin], [], min(.05,max(0,self.deadline-time.monotonic())))[1]:continue
                try:count=os.write(self.process.stdin.fileno(),data[:4096])
                except BlockingIOError:continue
                data=data[count:]
            reply=self._read()
            self._record({'reply':reply})
            if type(reply) is not dict or type(reply.get('seq')) is not int or reply['seq']!=self.seq:
                raise ValueError('Terminal sequence mismatch')
            self._validate_reply(op,reply)
            return reply
        except BaseException as error:
            self.failed=True;self._record({'failure':repr(error),'command':value});raise
    @staticmethod
    def _validate_reply(op,value):
        def cursor(state):
            return type(state) is dict and set(state)=={'x','y'} and all(type(state[k]) is int for k in state) and 0<=state['x']<=100 and 0<=state['y']<25
        def modes(value):
            boolean={'applicationCursorKeysMode','applicationKeypadMode','bracketedPasteMode','insertMode','originMode','reverseWraparoundMode','sendFocusMode','synchronizedOutputMode','wraparoundMode'}
            return type(value) is dict and set(value)==boolean|{'mouseTrackingMode'} and all(type(value[k]) is bool for k in boolean) and value['mouseTrackingMode']=='none'
        if op=='WRITE':
            if set(value)!={'op','seq','pending','replies','cursor','modes'} or value['op']!='WRITTEN' or type(value['pending']) is not bool or type(value['replies']) is not list or not cursor(value['cursor']) or not modes(value['modes']):
                raise ValueError('Malformed WRITTEN result')
            for response in value['replies']:
                if type(response) is not dict or set(response)!={'bytes','state'} or type(response['bytes']) is not str or not cursor(response['state']):raise ValueError('Malformed response')
                data=base64.b64decode(response['bytes'],validate=True)
                expected=('\x1b[%d;%dR'%(response['state']['y']+1,response['state']['x']+1)).encode()
                if data!=expected:raise ValueError('Reply/state mismatch')
        elif op=='SNAPSHOT':
            if set(value)!={'op','seq','cursor','lines'} or value['op']!='SNAPSHOT' or not cursor(value['cursor']) or type(value['lines']) is not list or len(value['lines'])!=25 or any(type(line) is not str for line in value['lines']):raise ValueError('Malformed SNAPSHOT result')
        elif op=='CLOSE':
            if set(value)!={'op','seq'} or value['op']!='CLOSED':raise ValueError('Malformed CLOSED result')
        else:raise ValueError('Unknown operation')
    def _record(self,value):
        self.audit.append(value)
        with open(self.evidence/'terminal.protocol.jsonl','a') as output:output.write(json.dumps(value)+'\n')
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
        try:
            if self.pending or self.failed or self.closed:raise ValueError('Unsettled terminal prefix')
            self._time();self.writer(data,'route-key:'+label,self.seq)
        except BaseException as error:
            self.failed=True;self._record({'keyFailure':repr(error),'label':label,'seq':self.seq});raise
    def snapshot(self):
        if self.pending:raise ValueError('Partial terminal prefix')
        result=self._command('SNAPSHOT')
        if result.get('op')!='SNAPSHOT':
            self.failed=True;raise ValueError('Snapshot mismatch')
        return result
    def close(self):
        try:
            result=self._command('CLOSE')
            if result.get('op')!='CLOSED':raise ValueError('Close not acknowledged')
            self.process.stdin.close()
            self.process.wait(timeout=max(.001,self.deadline-time.monotonic()))
            trailing=b''
            while True:
                self._time()
                if not select.select([self.process.stdout],[],[],min(.05,max(0,self.deadline-time.monotonic())))[0]:continue
                data=os.read(self.process.stdout.fileno(),65536)
                if not data:break
                trailing+=data
            self.process.stdout.close();self.stderr.close()
            if self.process.returncode!=0 or self.buffer or trailing:raise ValueError('Sidecar failed or undrained output')
            self.closed=True
            self._record({'NormalClose':True,'Joined':True,'EOF':True})
        except BaseException as error:
            self.failed=True;self._record({'closeFailure':repr(error)});raise

    def abort(self):
        """Bounded cleanup of this exact child; failure never becomes normal CLOSE."""
        self.failed=True
        proof={'NormalClose':False,'Joined':False,'EOF':False,'Errors':[]}
        try:
            if self.process is not None:
                if self.process.stdin and not self.process.stdin.closed:self.process.stdin.close()
                if self.process.poll() is None:self.process.terminate()
                try:self.process.wait(timeout=1)
                except subprocess.TimeoutExpired:
                    self.process.kill();self.process.wait(timeout=1)
                proof['Joined']=True;proof['ExitCode']=self.process.returncode
                end=time.monotonic()+1;tail=bytearray(self.buffer)
                while self.process.stdout and not self.process.stdout.closed and time.monotonic()<end:
                    if not select.select([self.process.stdout],[],[],.02)[0]:continue
                    raw=os.read(self.process.stdout.fileno(),65536)
                    if not raw:proof['EOF']=True;break
                    tail.extend(raw)
                (self.evidence/'terminal.abort.stdout.raw').write_bytes(tail)
        except BaseException as error:proof['Errors'].append(repr(error))
        finally:
            for stream in ([self.process.stdin,self.process.stdout] if self.process is not None else [])+[self.stderr]:
                if stream is not None:
                    try:stream.close()
                    except BaseException as error:proof['Errors'].append(repr(error))
            (self.evidence/'terminal.abort.json').write_text(json.dumps(proof,indent=2)+'\n')
        return proof
