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

def inert_geometry_selected(raw, cut, menu):
    """Reuse maintained raw grammar; only this literal inert descriptor skips size admission."""
    sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'ProductionMain'))
    import mortal_console_routes as routes
    choices=('s | Ход 43 | Хозяйственный двор у нижней лестницы | 18.11.2026 18:00 | 99999KB','↩️ Назад')
    if menu.get('header')!='Выберите сохранение:' or tuple(menu.get('choices',()))!=choices or menu.get('page_size')!=15:
        raise ValueError('Not the exact inert geometry descriptor')
    descriptor={'header':'Выберите сохранение:','choices':choices,'page_size':15}
    frames=routes.decode_menu_frames(raw,cut,descriptor,menu)
    return frames[-1] if frames else None

def inert_initial_pty_cut(raw, keyboard):
    """Only the observed startup keyboard-mode pair, at byte zero."""
    prefix=b'\x1b[?1h\x1b='
    if not raw.startswith(prefix) or keyboard!={'cursor':True,'keypad':True}:return None
    return len(prefix)

def inert_wrapped_load(raw, cut, menu):
    """Exact negative raw artifact, not a render decoder or input authorization."""
    try:text=raw[cut:].decode('utf-8')
    except UnicodeError:return False
    row,back=menu['choices']
    if not row.endswith('99999KB') or menu.get('previous_height')!=3 or menu.get('load_clear') is not True:return False
    nl=r'\r?\n'
    close='\r'+('\x1b[2K\x1b[1A'*3)+'\x1b[2K\x1b[?25h'
    clear='\x1b[2J\x1b[3J\x1b[1;1H'
    prefix=re.escape(close+clear)+r'[^\x1b\r\n]*📂 Загрузка игры[^\x1b\r\n]*'+nl+nl+re.escape('\x1b[?25l')
    body=(r'Выберите сохранение: *'+nl+r'(?: *'+nl+r')*> '+re.escape(row[:-7])+r' *'+nl+
          r'99999KB *'+nl+r'  '+re.escape(back)+r' *')
    return re.fullmatch(prefix+body+r'(?:'+re.escape('\r\x1b[4A')+body+r')*',text) is not None

def noecho_baseline(attributes):
    """Owned pre-spawn configuration only; preserve every non-echo attribute."""
    import copy
    configured=copy.deepcopy(attributes)
    configured[3] &= ~(termios.ECHO|termios.ECHONL)
    return configured

def require_noecho(attributes):
    if attributes[3] & (termios.ECHO|termios.ECHONL):
        raise ValueError('Owned terminal echo re-enabled; input refused')

def termios_data(attributes):
    return [v if not isinstance(v,list) else
            [({'Hex':x.hex()} if isinstance(x,bytes) else x) for x in v]
            for v in attributes]

def measured_load_profile(measured, size, attributes):
    """Fixture measurement, not process ownership or live input authority."""
    if (size!=[25,100,0,0] or any(type(measured.get(k)) is not int or measured[k]!=v for k,v in
        (('ConsoleWidth',100),('SpectreWidth',100),('ConsoleHeight',25),('SpectreHeight',25)))
        or any(measured.get(k) is not False for k in ('InputRedirected','OutputRedirected','ErrorRedirected'))):
        raise ValueError('Measured terminal does not match fixed owned PTY profile')
    require_noecho(attributes)
    from mortal_console_routes import OWNED_PTY_LOAD_PROFILE
    return OWNED_PTY_LOAD_PROFILE

def main():
    root, helper, mode = pathlib.Path(sys.argv[1]),sys.argv[2],sys.argv[3]
    assert mode in ('pty','pipe-dumb','pipe-xterm','pipe-inventory','pipe-load','pipe-inventory-cleanup','pipe-load-geometry','pty-load-100','pty-load-80','pty-fullroute')
    fullroute = mode == 'pty-fullroute'
    cleanup = mode == 'pipe-inventory-cleanup'
    inventory = mode in ('pipe-inventory','pipe-inventory-cleanup')
    geometry = mode in ('pipe-load-geometry','pty-load-100','pty-load-80')
    same_pty = mode in ('pty','pty-load-100','pty-load-80','pty-fullroute')
    wrong_width = mode == 'pty-load-80'
    load = mode == 'pipe-load' or geometry
    fixed_render = inventory or load
    if fixed_render:
        sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'ProductionMain'))
        from mortal_console_routes import selected_frame, fixed_menu, local_load_menu, after_menu, entry_menu
        menu=fixed_menu("🎒 📦 Инвентарь  (⚖ 1.8kg/45.0kg кг)  (выберите для просмотра / управления)",["💰 Деньги: 500","← Назад"],20)
        if load:load_menu=local_load_menu('Двор совета',43,99999)
        if geometry:
            # Inert observation only: defer the unchanged production admission guard
            # until real rendering, exact selection, normal exit and drain exist.
            load_menu=dict(load_menu,choices=('s | Ход 43 | Хозяйственный двор у нижней лестницы | 18.11.2026 18:00 | 99999KB','↩️ Назад'))
        if inventory:menu=fixed_menu(menu['header'],['💰 Деньги: 500','⚔ ⚔️ Основная рука: Древний меч','📦 Древний меч [Оружие]','📦 🔒 Кладовая материалов совета (1 пр.)','← Назад'],20)
        first_label,footer=menu['choices'][0],menu['choices'][-1]
    root.mkdir(parents=True,exist_ok=False)
    master,slave=pty.openpty()
    fcntl.ioctl(slave,termios.TIOCSWINSZ,struct.pack('HHHH',25,80 if wrong_width else 100,0,0))
    initial=termios.tcgetattr(slave)
    owned_noecho=same_pty and (geometry or fullroute)
    configured=noecho_baseline(initial) if owned_noecho else initial
    def own():
        os.setsid();fcntl.ioctl(slave,termios.TIOCSCTTY,0);os.tcsetpgrp(slave,os.getpid())
    env=os.environ.copy();env['TERM']='dumb' if mode=='pipe-dumb' else 'xterm-256color'
    result={'Mode':mode,'Success':False,'EOF':False,'CleanupFailures':[],'Inputs':[]}
    result['PtySizeReadback']=list(struct.unpack('HHHH',fcntl.ioctl(slave,termios.TIOCGWINSZ,b'\0'*8)))
    process=None;screen=Screen();control=Screen(screen.keyboard);closed=set();streams={};raws={}
    frame_cut=0; captured=bytearray(); raw_cut=0
    start=time.monotonic();stage=0
    def write_input(key):
        if owned_noecho:
            observed=termios.tcgetattr(master)
            result.setdefault('BeforeInputTermios',[]).append(termios_data(observed))
            require_noecho(observed) # snapshot only; raw grammar still rejects racing echo
            size=list(struct.unpack('HHHH',fcntl.ioctl(master,termios.TIOCGWINSZ,b'\0'*8)))
            assert size==result['PtySizeReadback'], 'Owned terminal geometry changed; input refused'
        assert os.write(master,key)==len(key)
    try:
        if owned_noecho:
            result['OriginalTermios']=termios_data(initial)
            result['ConfiguredTermios']=termios_data(configured)
            termios.tcsetattr(slave,termios.TCSANOW,configured)
            readback=termios.tcgetattr(slave)
            result['ConfiguredReadback']=termios_data(readback)
            assert readback==configured
        args=['dotnet',helper,'mortal-fullroute-smoke' if fullroute else 'spectre-inventory-cleanup-smoke' if cleanup else 'spectre-load-geometry-smoke' if geometry else 'spectre-load-smoke' if load else 'spectre-inventory-smoke' if inventory else 'spectre-selection-smoke',str(root),'--plain-output']
        process=subprocess.Popen(args,stdin=slave,stdout=slave if same_pty else subprocess.PIPE,
                                 stderr=slave if same_pty else subprocess.STDOUT,env=env,preexec_fn=own)
        result.update(OriginalPid=process.pid,Argv=args)
        streams={master:'terminal' if same_pty else 'echo'}
        if not same_pty:streams[process.stdout.fileno()]='captured'
        raws={fd:(root/(name+'.raw')).open('wb',buffering=0) for fd,name in streams.items()}
        if inventory:
            # Exercise the actual driver's finite selector, not another observer.
            import ast
            source=pathlib.Path(__file__).resolve().parents[1]/'ProductionMain/live-mortal-phase.py'
            nodes=[n for n in ast.parse(source.read_text()).body if isinstance(n,ast.FunctionDef) and n.name in ('finite_select','register_inventory_cleanup','settle_inventory_cleanup','current_player_prompt_at')]
            import mortal_console_routes
            class Peer:
                capture=captured
                def send(self,key,phase):
                    if cleanup and key==b'\r':
                        assert selected_frame(bytes(captured),0,menu)=='← Назад'
                        assert not captured.endswith(b'\r') and bytes(captured).rstrip(b' ').endswith(footer.encode())
                        result['CompleteFooterWithoutCR']=True
                    write_input(key)
                    result['Inputs'].append({'Hex':key.hex(),'Phase':phase,'CaptureCut':len(captured)})
            def wait(predicate,seconds,label):
                deadline=min(start+12,time.monotonic()+seconds)
                while not predicate():
                    if time.monotonic()>deadline:raise TimeoutError(label)
                    for fd in select.select([f for f in streams if f not in closed],[],[],.02)[0]:
                        try:data=os.read(fd,65536)
                        except OSError as ex:
                            if fd!=master or ex.errno!=errno.EIO:raise
                            data=b''
                        if not data:closed.add(fd);continue
                        raws[fd].write(data)
                        if streams[fd]=='captured':captured.extend(data);screen.feed(data)
                        else:control.feed(data)
            def fresh_player(peer,cut,seconds):wait(lambda:namespace['current_player_prompt_at'](re.sub(r'\x1b\[[0-9;? ]*[A-Za-z~]','',bytes(captured[cut:]).decode('utf-8')))>=0,seconds,'actual fresh player prompt')
            namespace={'client':Peer(),'console_routes':mortal_console_routes,'until':wait,'fresh_player':fresh_player,'inventory_context':None,'inventory_cleanup_required':False,'client_prompt':False}
            constant=next(n for n in ast.parse(source.read_text()).body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='PLAYER_PROMPT_MARKER' for t in n.targets))
            exec(compile(ast.Module(body=[constant]+nodes,type_ignores=[]),'actual finite selector','exec'),namespace)
            target=menu['choices'][3]
            if cleanup:
                namespace['register_inventory_cleanup'](menu,0)
                original_wait=namespace['until'];injected=False
                def fault_after_down(predicate,seconds,label):
                    nonlocal injected
                    if result['Inputs'] and not injected:
                        injected=True;raise RuntimeError('T030 injected failure after one confirmed Down')
                    return original_wait(predicate,seconds,label)
                namespace['until']=fault_after_down
                try:namespace['finite_select'](menu,target,0,5)
                except RuntimeError as error:
                    assert str(error)=='T030 injected failure after one confirmed Down';result['OriginalInjectedFailure']=str(error)
                else:raise AssertionError('Expected original injected failure')
                namespace['until']=original_wait
                assert namespace['settle_inventory_cleanup']() and namespace['client_prompt']
                assert [i['Hex'] for i in result['Inputs']]==['1b5b42']*4+['0d']
                assert namespace['inventory_context'] is None
                target='← Назад';namespace['client'].send(b'exit\r','inert normal exit after actual fresh prompt')
            else:
                namespace['finite_select'](menu,target,0,5)
                assert [i['Hex'] for i in result['Inputs']]==['1b5b42']*3+['0d']
            if not cleanup:
                assert selected_frame(bytes(captured),0,menu)==target
                assert not captured.endswith(b'\r') and bytes(captured).rstrip(b' ').endswith(footer.encode())
            result['ActualFiniteSelector']=True;result['Target']=target;stage=2
        if fullroute:
            # Same owned transport/cleanup as the qualified geometry modes. No new process scheme.
            from fullroute_inputs import run as run_fullroute
            def pump_fullroute():
                if process.poll() not in (None, 0):
                    report=root/'fullroute-result.json'
                    raise RuntimeError('Actual fullroute child failed: '+(report.read_text() if report.exists() else str(process.returncode)))
                for fd in select.select([f for f in streams if f not in closed],[],[],.02)[0]:
                    try:data=os.read(fd,65536)
                    except OSError as error:
                        if fd!=master or error.errno!=errno.EIO:raise
                        data=b''
                    if not data:closed.add(fd);continue
                    raws[fd].write(data);captured.extend(data);screen.feed(data)
            full_deadline=start+45
            while inert_initial_pty_cut(bytes(captured),screen.keyboard) is None:
                if time.monotonic()>=full_deadline:raise TimeoutError('Actual initial PTY mode')
                pump_fullroute()
            first_cut=inert_initial_pty_cut(bytes(captured),screen.keyboard)
            # Geometry is published atomically by this fixture before its first real prompt.
            while not (root/'geometry.json').exists():
                if time.monotonic()>=full_deadline:raise TimeoutError('Fullroute setup/geometry')
                pump_fullroute()
            measured=json.loads((root/'geometry.json').read_text())
            result['ActualGeometry']=measured
            profile=measured_load_profile(measured,result['PtySizeReadback'],termios.tcgetattr(master))
            result['QualifiedDataProfile']=profile
            def route_send(key,label):
                write_input(key)
                result['Inputs'].append({'Hex':key.hex(),'Phase':label,'CaptureCut':len(captured)})
            run_fullroute(root,captured,screen,pump_fullroute,route_send,result,full_deadline,first_cut,profile)
            while time.monotonic()<full_deadline:
                pump_fullroute()
                if process.poll() is not None and slave>=0:os.close(slave);slave=-1
                if len(closed)==len(streams):break
            stage=5
        while not fullroute and time.monotonic()-start<12:
            for fd in select.select([f for f in streams if f not in closed],[],[],.02)[0]:
                try:data=os.read(fd,65536)
                except OSError as ex:
                    if fd!=master or ex.errno!=errno.EIO:raise
                    data=b''
                if not data:closed.add(fd);continue
                raws[fd].write(data)
                if same_pty or streams[fd]=='captured':
                    captured.extend(data);screen.feed(data)
                else:control.feed(data)
            if same_pty and geometry and stage==0:
                initial_cut=inert_initial_pty_cut(bytes(captured),screen.keyboard)
                if initial_cut is None:continue
                raw_cut=initial_cut
            if wrong_width and stage==2 and inert_wrapped_load(bytes(captured),raw_cut,menu):
                assert len(result['Inputs'])==2 and result['Inputs'][1]=='Enter'
                result['ExpectedWrappedRefusal']=True
                result['WrappedRawHex']=bytes(captured[raw_cut:]).hex()
                result['NormalExitQualified']=False
                break
            ready=lambda choice: (inert_geometry_selected(bytes(captured),raw_cut,menu)==(first_label if choice=='SMOKE_ALPHA' else footer)) if geometry and mode!='pty-load-100' and stage>=2 else (selected_frame(bytes(captured),raw_cut,menu) ==
                (first_label if choice=='SMOKE_ALPHA' else footer)) if fixed_render else (
                screen.selected(choice) if same_pty else screen.frame(choice,frame_cut))
            if stage==0 and ready('SMOKE_ALPHA'):
                if geometry:
                    # The completed initial prompt follows the synchronous geometry
                    # write, so no partially-written file can be observed here.
                    measured=json.loads((root/'geometry.json').read_text())
                    result['ActualGeometry']=measured
                    if same_pty:
                        expected_width=80 if wrong_width else 100
                        assert measured['ConsoleWidth']==measured['SpectreWidth']==result['PtySizeReadback'][1]==expected_width
                        assert not measured['InputRedirected'] and not measured['OutputRedirected']
                        if mode=='pty-load-100':
                            profile=measured_load_profile(measured,result['PtySizeReadback'],termios.tcgetattr(master))
                            load_menu=local_load_menu('Хозяйственный двор у нижней лестницы',43,99999,profile=profile)
                            result['QualifiedDataProfile']=profile
                if fixed_render:
                    actual=bytes(captured[raw_cut:])
                    assert not actual.endswith(b'\r') and actual.rstrip(b' ').endswith(footer.encode())
                    result['InitialCompleteFooterWithoutCR']=True
                result['InitialRender']=screen.lines() if same_pty else screen.plain[frame_cut:]
                arrow=screen.down()
                result['KeyboardAtDown']=dict(screen.keyboard)
                assert not control.pending and not control.invalid
                write_input(arrow)
                result['Inputs'].append({'Key':'Down','Hex':arrow.hex()})
                frame_cut=len(screen.plain);raw_cut=len(captured);stage=1
            elif stage==1 and ready('SMOKE_BETA'):
                if fixed_render:
                    actual=bytes(captured[raw_cut:])
                    assert not actual.endswith(b'\r') and actual.rstrip(b' ').endswith(footer.encode())
                    result['MovedCompleteFooterWithoutCR']=True
                result['MovedRender']=screen.lines() if same_pty else screen.plain[frame_cut:]
                if load:
                    previous=menu;raw_cut=len(captured)
                    menu=entry_menu(after_menu(load_menu,previous),load_clear=True)
                    first_label,footer=menu['choices']
                write_input(b'\r')
                result['Inputs'].append('Enter');stage=2
            elif load and stage==2 and ready('SMOKE_ALPHA'):
                actual=bytes(captured[raw_cut:])
                assert b'\x1b[2K' in actual and b'\x1b[?25h' in actual
                assert b'\x1b[2J\x1b[3J\x1b[1;1H' in actual and '📂 Загрузка игры'.encode() in actual
                assert not actual.endswith(b'\r') and actual.rstrip(b' ').endswith(footer.encode())
                result['TransitionCompleteFooterWithoutCR']=True
                result['TransitionRawHex']=actual.hex()
                raw_cut=len(captured);frame_cut=len(screen.plain)
                arrow=screen.down()
                write_input(arrow)
                result['Inputs'].append({'Key':'Down','Hex':arrow.hex()});stage=3
            elif load and stage==3 and ready('SMOKE_BETA'):
                actual=bytes(captured[raw_cut:])
                assert not actual.endswith(b'\r') and actual.rstrip(b' ').endswith(footer.encode())
                result['LoadMovedCompleteFooterWithoutCR']=True
                write_input(b'\r')
                result['Inputs'].append('Enter');stage=4
            if process.poll() is not None and slave>=0:
                os.close(slave);slave=-1
            if len(closed)==len(streams):break
        result['EOF']=len(closed)==len(streams)
        if result['EOF']:screen.finish();control.finish()
        if result.get('ExpectedWrappedRefusal'):
            raise RuntimeError('Expected inert wrong-width refusal; owned cleanup required')
        if not result['EOF']:raise TimeoutError('Actual selection/exit/EOF not observed within original bounded deadline')
        result['ExitCode']=process.wait(timeout=1)
        result['ChildRestoredConfiguredBaseline']=termios.tcgetattr(master)==configured
        result['TermiosRestored']=result['ChildRestoredConfiguredBaseline']
        if fullroute:
            receipt=json.loads((root/'fullroute-result.json').read_text())
            result['FullRouteReceipt']=receipt
            assert result['ExitCode']==0 and receipt['Success'] and receipt['FixtureRootAbsent']
            assert receipt['CleanupFailures']==[] and result['ChildRestoredConfiguredBaseline']
            assert [entry['Stage'] for entry in receipt['Stages']]==['locked-denial','retrieve','save','deposit','load-focus-back']
            result['Success']=True
        else:
            receipt=json.loads((root/'choice.json').read_text())
            result['Choice']=receipt
            assert result['TermiosRestored'] and not receipt['InputRedirected']
            assert receipt['Term']==env['TERM'] and receipt['NoColor']=='1' and receipt['PlainOutput']
            if mode!='pipe-dumb':
                assert stage==(4 if load else 2) and result['ExitCode']==0 and receipt['Selected']==(target if inventory else footer if fixed_render else 'SMOKE_BETA') and receipt['Failure'] is None
            if same_pty:
                assert not receipt['OutputRedirected'] and not receipt['ErrorRedirected']
            else:
                assert receipt['OutputRedirected'] and receipt['ErrorRedirected']
            if geometry:
                result['ActualGeometry']=json.loads((root/'geometry.json').read_text())
                result['RenderedAndExitedBeforeGuard']=True
                # Default PIPE still refuses; measured fixed PTY profile has bounded DATA admission.
                local_load_menu('Хозяйственный двор у нижней лестницы',43,99999,profile=result.get('QualifiedDataProfile'))
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
        if owned_noecho:
            try:
                if process is not None and result.get('Joined') is not True:
                    raise RuntimeError('Cannot restore original terminal before child joined')
                termios.tcsetattr(master,termios.TCSANOW,initial)
                restored=termios.tcgetattr(master)
                result['OwnerRestoredOriginal']=restored==initial
                result['OriginalRestoreReadback']=termios_data(restored)
                if restored!=initial:raise RuntimeError('Owned original termios restoration mismatch')
            except BaseException as error:result['CleanupFailures'].append(str(error))
        for raw in raws.values():raw.close()
        for fd in (slave,master):
            if fd>=0:os.close(fd)
        if process is not None and process.stdout is not None:process.stdout.close()
        if mode=='pipe-dumb' and 'Failure' not in result:
            result['ExpectedAnsiRefusal']=expected_dumb_refusal(result)
            result['Success']=result['ExpectedAnsiRefusal']
            if not result['Success']:result['Failure']='Expected ANSI refusal/complete cleanup not proved'
        if wrong_width and result.get('ExpectedWrappedRefusal'):
            result['Success']=(result.get('Joined') is True and result.get('CleanupDrained') is True
                               and result['CleanupFailures']==[] and len(result['Inputs'])==2)
        result['ElapsedSeconds']=time.monotonic()-start
        (root/'result.json').write_text(json.dumps(result,indent=2))
    return 0 if result['Success'] and not result['CleanupFailures'] else 1

if __name__ == "__main__":
    sys.exit(main())
