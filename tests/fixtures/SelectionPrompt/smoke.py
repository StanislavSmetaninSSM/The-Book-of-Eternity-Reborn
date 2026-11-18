"""T024 inert default Spectre console probe. Never answers terminal queries."""
import codecs, errno, fcntl, json, os, pathlib, pty, re, select, struct, subprocess, sys, termios, time

# Small read-only screen projection for the cursor operations Spectre renders.
# Unsupported sequences fail explicitly; this is not an input/DSR responder.
class Screen:
    def __init__(self, keyboard=None):
        self.rows = [[' '] * 100 for _ in range(25)]
        self.x = self.y = 0
        self.keyboard = keyboard if keyboard is not None else {'cursor':False,'keypad':False}
        self.plain = ''
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
                    if args == '?1': self.keyboard['cursor'] = final == 'h'
                    elif args != '?25': raise ValueError('Unsupported terminal mode')
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
                if self.pending[1] in ('=','>'):
                    self.keyboard['keypad'] = self.pending[1] == '='
                    self.pending = self.pending[2:]
                    continue
                raise ValueError('Unsupported escape '+repr(self.pending[:20]))
            c,self.pending=self.pending[0],self.pending[1:]
            self.plain += c
            if c=='\r':self.x=0
            elif c=='\n':
                self.y+=1
                if self.y==25:self.rows.pop(0);self.rows.append([' ']*100);self.y=24
            elif c=='\b':self.x=max(0,self.x-1)
            elif c >= ' ':
                self.rows[self.y][self.x]=c;self.x=min(99,self.x+1)
    def down(self):
        if self.invalid or self.pending or self.decoder.getstate()[0]:
            raise ValueError('Incomplete keyboard mode')
        return b'\x1bOB' if self.keyboard['cursor'] else b'\x1b[B'
    def frame(self, choice, after):
        # Captured PIPE has no screen. Only the latest complete emitted prompt frame
        # after the original input cut may authorize the next input.
        if self.invalid or self.pending or self.decoder.getstate()[0]: return False
        text=self.plain[after:]
        at=text.rfind('T024 inert selection')
        if at<0:return False
        match=re.fullmatch(r'T024 inert selection\r?\n[ \r\n]*([ >]*SMOKE_ALPHA *)\r?\n([ >]*SMOKE_BETA *)',text[at:])
        if not match:return False
        a,b=match.groups()
        return (a.lstrip().startswith('> ') and not b.lstrip().startswith('> ')) if choice=='SMOKE_ALPHA' else (
            b.lstrip().startswith('> ') and not a.lstrip().startswith('> '))
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

def expected_dumb_refusal(result):
    receipt=result.get('Choice') or {}
    failure=receipt.get('Failure')
    expected=('System.NotSupportedException: Cannot show selection prompt since '
              'the current terminal does not support ANSI escape sequences.')
    return (result.get('Mode')=='pipe-dumb' and result.get('ExitCode')==1
            and result.get('Inputs')==[] and receipt.get('Selected') is None
            and isinstance(failure,str) and failure.partition('\n')[0]==expected
            and all(result.get(k) is True for k in
                    ('EOF','TermiosRestored','Joined','CleanupDrained'))
            and result.get('CleanupFailures')==[])

def main():
    root, helper, mode = pathlib.Path(sys.argv[1]),sys.argv[2],sys.argv[3]
    assert mode in ('pty','pipe-dumb','pipe-xterm','pipe-inventory','pipe-load')
    inventory = mode == 'pipe-inventory'
    load = mode == 'pipe-load'
    fixed_render = inventory or load
    if fixed_render:
        sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'ProductionMain'))
        from mortal_console_routes import selected_frame, fixed_menu, local_load_menu, after_menu, entry_menu
        menu=fixed_menu("🎒 📦 Инвентарь  (⚖ 1.8kg/45.0kg кг)  (выберите для просмотра / управления)",["💰 Деньги: 500","← Назад"],20)
        if load:load_menu=local_load_menu('Хозяйственный двор у кладовой совета',43,99999)
        first_label,footer=menu['choices']
    root.mkdir(parents=True,exist_ok=False)
    master,slave=pty.openpty()
    fcntl.ioctl(slave,termios.TIOCSWINSZ,struct.pack('HHHH',25,100,0,0))
    initial=termios.tcgetattr(slave)
    def own():
        os.setsid();fcntl.ioctl(slave,termios.TIOCSCTTY,0);os.tcsetpgrp(slave,os.getpid())
    env=os.environ.copy();env['TERM']='dumb' if mode=='pipe-dumb' else 'xterm-256color'
    result={'Mode':mode,'Success':False,'EOF':False,'CleanupFailures':[],'Inputs':[]}
    process=None;screen=Screen();control=Screen(screen.keyboard);closed=set();streams={};raws={}
    frame_cut=0; captured=bytearray(); raw_cut=0
    start=time.monotonic();stage=0
    try:
        args=['dotnet',helper,'spectre-load-smoke' if load else 'spectre-inventory-smoke' if inventory else 'spectre-selection-smoke',str(root),'--plain-output']
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
                if mode=='pty' or streams[fd]=='captured':
                    captured.extend(data);screen.feed(data)
                else:control.feed(data)
            ready=lambda choice: (selected_frame(bytes(captured),raw_cut,menu) ==
                (first_label if choice=='SMOKE_ALPHA' else footer)) if fixed_render else (
                screen.selected(choice) if mode=='pty' else screen.frame(choice,frame_cut))
            if stage==0 and ready('SMOKE_ALPHA'):
                if fixed_render:
                    actual=bytes(captured[raw_cut:])
                    assert not actual.endswith(b'\r') and actual.rstrip(b' ').endswith(footer.encode())
                    result['InitialCompleteFooterWithoutCR']=True
                result['InitialRender']=screen.lines() if mode=='pty' else screen.plain[frame_cut:]
                arrow=screen.down()
                result['KeyboardAtDown']=dict(screen.keyboard)
                assert not control.pending and not control.invalid
                assert os.write(master,arrow)==len(arrow)
                result['Inputs'].append({'Key':'Down','Hex':arrow.hex()})
                frame_cut=len(screen.plain);raw_cut=len(captured);stage=1
            elif stage==1 and ready('SMOKE_BETA'):
                if fixed_render:
                    actual=bytes(captured[raw_cut:])
                    assert not actual.endswith(b'\r') and actual.rstrip(b' ').endswith(footer.encode())
                    result['MovedCompleteFooterWithoutCR']=True
                result['MovedRender']=screen.lines() if mode=='pty' else screen.plain[frame_cut:]
                if load:
                    previous=menu;raw_cut=len(captured)
                    menu=entry_menu(after_menu(load_menu,previous),load_clear=True)
                    first_label,footer=menu['choices']
                assert os.write(master,b'\r')==1
                result['Inputs'].append('Enter');stage=2
            elif load and stage==2 and ready('SMOKE_ALPHA'):
                actual=bytes(captured[raw_cut:])
                assert b'\x1b[2K' in actual and b'\x1b[?25h' in actual
                assert b'\x1b[2J\x1b[3J\x1b[1;1H' in actual and '📂 Загрузка игры'.encode() in actual
                assert not actual.endswith(b'\r') and actual.rstrip(b' ').endswith(footer.encode())
                result['TransitionCompleteFooterWithoutCR']=True
                result['TransitionRawHex']=actual.hex()
                raw_cut=len(captured);frame_cut=len(screen.plain)
                assert os.write(master,b'\x1b[B')==3
                result['Inputs'].append({'Key':'Down','Hex':'1b5b42'});stage=3
            elif load and stage==3 and ready('SMOKE_BETA'):
                actual=bytes(captured[raw_cut:])
                assert not actual.endswith(b'\r') and actual.rstrip(b' ').endswith(footer.encode())
                result['LoadMovedCompleteFooterWithoutCR']=True
                assert os.write(master,b'\r')==1
                result['Inputs'].append('Enter');stage=4
            if process.poll() is not None and slave>=0:
                os.close(slave);slave=-1
            if len(closed)==len(streams):break
        result['EOF']=len(closed)==len(streams)
        if result['EOF']:screen.finish();control.finish()
        if not result['EOF']:raise TimeoutError('Actual selection/exit/EOF not observed within 12s')
        result['ExitCode']=process.wait(timeout=1)
        result['TermiosRestored']=termios.tcgetattr(master)==initial
        receipt=json.loads((root/'choice.json').read_text())
        result['Choice']=receipt
        assert result['TermiosRestored'] and not receipt['InputRedirected']
        assert receipt['Term']==env['TERM'] and receipt['NoColor']=='1' and receipt['PlainOutput']
        if mode!='pipe-dumb':
            assert stage==(4 if load else 2) and result['ExitCode']==0 and receipt['Selected']==(footer if fixed_render else 'SMOKE_BETA') and receipt['Failure'] is None
        if mode=='pty':
            assert not receipt['OutputRedirected'] and not receipt['ErrorRedirected']
        else:
            assert receipt['OutputRedirected'] and receipt['ErrorRedirected']
        result['Success']=mode!='pipe-dumb'
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
        if mode=='pipe-dumb' and 'Failure' not in result:
            result['ExpectedAnsiRefusal']=expected_dumb_refusal(result)
            result['Success']=result['ExpectedAnsiRefusal']
            if not result['Success']:result['Failure']='Expected ANSI refusal/complete cleanup not proved'
        result['ElapsedSeconds']=time.monotonic()-start
        (root/'result.json').write_text(json.dumps(result,indent=2))
    return 0 if result['Success'] and not result['CleanupFailures'] else 1

if __name__ == "__main__":
    sys.exit(main())
