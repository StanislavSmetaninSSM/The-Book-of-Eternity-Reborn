"""T024 inert default Spectre console probe. Never answers terminal queries."""
import codecs, errno, fcntl, json, os, pathlib, pty, re, select, struct, subprocess, sys, termios, time

# Small read-only screen projection for the cursor operations Spectre renders.
# Unsupported sequences fail explicitly; this is not an input/DSR responder.
class Screen:
    def __init__(self):
        self.rows = [[' '] * 100 for _ in range(25)]
        self.x = self.y = 0
        self.pending = ''
        self.invalid = False
        self.decoder = codecs.getincrementaldecoder('utf-8')()
    def feed(self, raw):
        if self.invalid: raise ValueError('Screen projection already invalid')
        try: self._feed(raw)
        except BaseException:
            self.invalid = True
            raise
    def finish(self):
        try:
            self.pending += self.decoder.decode(b'', final=True)
            self._feed(b'')
            if self.pending: raise ValueError('Incomplete terminal escape at EOF')
        except BaseException:
            self.invalid = True
            raise
    def _feed(self, raw):
        self.pending += self.decoder.decode(raw)
        while self.pending:
            if self.pending.startswith('\x1b['):
                match = re.match(r'\x1b\[([0-9;?]*)([ -/]*)([@-~])', self.pending)
                if not match:
                    if len(self.pending) < 64: return
                    raise ValueError('Unrecognized CSI')
                args, intermediate, final = match.groups()
                self.pending = self.pending[match.end():]
                if intermediate: raise ValueError('Unsupported CSI intermediate')
                if final in ('h','l'):
                    if args != '?25': raise ValueError('Unsupported terminal mode')
                    continue
                if '?' in args: raise ValueError('Unsupported private CSI')
                vals = [int(n or 0) for n in args.lstrip('?').split(';')]
                if len(vals) > (2 if final in ('H','f') else (16 if final == 'm' else 1)):
                    raise ValueError('Unexpected CSI parameter count')
                n = vals[0] or 1
                if final == 'A': self.y = max(0, self.y-n)
                elif final == 'B': self.y = min(24, self.y+n)
                elif final == 'C': self.x = min(99, self.x+n)
                elif final == 'D': self.x = max(0, self.x-n)
                elif final == 'G': self.x = min(99, n-1)
                elif final in ('H','f'):
                    self.y = min(24, n-1); self.x = min(99, ((vals[1] or 1) if len(vals)>1 else 1)-1)
                elif final == 'K':
                    mode=vals[0]
                    if mode not in (0,1,2):raise ValueError('Unsupported erase mode')
                    lo,hi=(0,100) if mode==2 else ((0,self.x+1) if mode==1 else (self.x,100))
                    self.rows[self.y][lo:hi]=[' ']*(hi-lo)
                elif final == 'J' and vals[0] in (2,3): self.rows=[[' ']*100 for _ in range(25)]
                elif final == 'm':
                    # Non-concealing decorations only; NO_COLOR forbids needing a colour model.
                    if any(v not in (0,1,2,3,4,9,22,23,24,29) for v in vals):
                        raise ValueError('Unsupported or concealing style')
                elif final == 'n': raise RuntimeError('Terminal query requested; no fabricated DSR response')
                else: raise ValueError('Unsupported CSI '+repr(match.group(0)))
                continue
            if self.pending[0]=='\x1b':
                if len(self.pending)==1:return
                raise ValueError('Unsupported escape '+repr(self.pending[:20]))
            c,self.pending=self.pending[0],self.pending[1:]
            if c=='\r':self.x=0
            elif c=='\n':
                self.y+=1
                if self.y==25:self.rows.pop(0);self.rows.append([' ']*100);self.y=24
            elif c=='\b':self.x=max(0,self.x-1)
            elif c >= ' ':
                self.rows[self.y][self.x]=c;self.x=min(99,self.x+1)
    def lines(self):return [''.join(row).rstrip() for row in self.rows]
    def selected(self, choice):
        if self.invalid or self.pending or self.decoder.getstate()[0]: return False
        lines=self.lines()
        a=[line for line in lines if 'SMOKE_ALPHA' in line]
        b=[line for line in lines if 'SMOKE_BETA' in line]
        if len(a)!=1 or len(b)!=1:return False
        chosen,other=(a[0],b[0]) if choice=='SMOKE_ALPHA' else (b[0],a[0])
        prefix=chosen.split(choice)[0].strip()
        other_label='SMOKE_BETA' if choice=='SMOKE_ALPHA' else 'SMOKE_ALPHA'
        return bool(prefix) and not other.split(other_label)[0].strip()

def main():
    root, helper, mode = pathlib.Path(sys.argv[1]),sys.argv[2],sys.argv[3]
    assert mode in ('pty','captured-pipe')
    root.mkdir(parents=True,exist_ok=False)
    master,slave=pty.openpty()
    fcntl.ioctl(slave,termios.TIOCSWINSZ,struct.pack('HHHH',25,100,0,0))
    initial=termios.tcgetattr(slave)
    def own():
        os.setsid();fcntl.ioctl(slave,termios.TIOCSCTTY,0);os.tcsetpgrp(slave,os.getpid())
    env=os.environ.copy();env['TERM']='xterm-256color'
    result={'Mode':mode,'Success':False,'EOF':False,'CleanupFailures':[],'Inputs':[]}
    process=None;screen=Screen();closed=set();streams={};raws={}
    start=time.monotonic();stage=0
    try:
        args=['dotnet',helper,'spectre-selection-smoke',str(root),'--plain-output']
        process=subprocess.Popen(args,stdin=slave,stdout=slave if mode=='pty' else subprocess.PIPE,
                                 stderr=slave if mode=='pty' else subprocess.STDOUT,env=env,preexec_fn=own)
        result.update(OriginalPid=process.pid,Argv=args)
        streams={master:'terminal' if mode=='pty' else 'echo'}
        if mode!='pty':streams[process.stdout.fileno()]='captured'
        raws={fd:(root/(name+'.raw')).open('wb',buffering=0) for fd,name in streams.items()}
        while time.monotonic()-start<12:
            for fd in select.select([f for f in streams if f not in closed],[],[],.02)[0]:
                try:data=os.read(fd,65536)
                except OSError as ex:
                    if fd!=master or ex.errno!=errno.EIO:raise
                    data=b''
                if not data:closed.add(fd);continue
                raws[fd].write(data)
                if mode=='pty':screen.feed(data)
            if mode=='pty' and stage==0 and screen.selected('SMOKE_ALPHA'):
                result['InitialScreen']=screen.lines()
                assert os.write(master,b'\x1b[B')==3
                result['Inputs'].append('Down');stage=1
            elif mode=='pty' and stage==1 and screen.selected('SMOKE_BETA'):
                result['MovedScreen']=screen.lines()
                assert os.write(master,b'\r')==1
                result['Inputs'].append('Enter');stage=2
            if process.poll() is not None and slave>=0:
                os.close(slave);slave=-1
            if len(closed)==len(streams):break
        result['EOF']=len(closed)==len(streams)
        if result['EOF'] and mode=='pty':screen.finish()
        if not result['EOF']:raise TimeoutError('Actual selection/exit/EOF not observed within 12s')
        result['ExitCode']=process.wait(timeout=1)
        result['TermiosRestored']=termios.tcgetattr(master)==initial
        receipt=json.loads((root/'choice.json').read_text())
        result['Choice']=receipt
        assert result['TermiosRestored'] and not receipt['InputRedirected']
        assert receipt['Term']=='xterm-256color' and receipt['NoColor']=='1' and receipt['PlainOutput']
        if mode=='pty':
            assert stage==2 and result['ExitCode']==0 and receipt['Selected']=='SMOKE_BETA' and receipt['Failure'] is None
            assert not receipt['OutputRedirected'] and not receipt['ErrorRedirected']
        else:
            assert result['ExitCode']!=0 and receipt['Selected'] is None and receipt['Failure']
            assert receipt['OutputRedirected'] and receipt['ErrorRedirected']
        result['Success']=True
    except BaseException as error:
        result['Failure']=type(error).__name__+': '+str(error)
    finally:
        # Only this exact spawned inert child, never a discovered/foreign PID.
        deadline=time.monotonic()+5
        if process is not None:
            try:
                if process.poll() is None:process.kill()
                process.wait(timeout=max(.001,deadline-time.monotonic()))
                result['Joined']=True
                if slave>=0:os.close(slave);slave=-1
                while len(closed)<len(streams) and time.monotonic()<deadline:
                    for fd in select.select([f for f in streams if f not in closed],[],[],.02)[0]:
                        try:data=os.read(fd,65536)
                        except OSError as error:
                            if fd!=master or error.errno!=errno.EIO:raise
                            data=b''
                        if data:raws[fd].write(data)
                        else:closed.add(fd)
                result['CleanupDrained']=len(closed)==len(streams)
                if not result['CleanupDrained']:result['CleanupFailures'].append('Owned stream drain incomplete')
            except BaseException as error:result['CleanupFailures'].append(str(error))
        for raw in raws.values():raw.close()
        for fd in (slave,master):
            if fd>=0:os.close(fd)
        if process is not None and process.stdout is not None:process.stdout.close()
        result['ElapsedSeconds']=time.monotonic()-start
        (root/'result.json').write_text(json.dumps(result,indent=2))
    return 0 if result['Success'] and not result['CleanupFailures'] else 1

if __name__ == "__main__":
    sys.exit(main())
