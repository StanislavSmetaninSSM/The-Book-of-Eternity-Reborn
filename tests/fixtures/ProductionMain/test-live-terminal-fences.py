"""Actual driver AST DATA only: never imports or starts the live driver."""
import ast,pathlib,sys,unittest,tempfile,json,hashlib,os,stat,types,copy
from unittest.mock import patch
import relay_phase_evidence as evidence
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'SelectionPrompt'))
source=pathlib.Path(__file__).with_name('live-mortal-phase.py').read_text()
tree=ast.parse(source)
node=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='clean_save_player_interval')
namespace={};exec(compile(ast.Module(body=[node],type_ignores=[]),'<actual-driver-function>','exec'),namespace)
clean=namespace['clean_save_player_interval']
class Fences(unittest.TestCase):
    def test_exact_success_before_actual_clear(self):
        self.assertTrue(clean('✅ Игра сохранена\r\n'.encode()+b'\x1b[2J\x1b[Hplayer'))
    def test_warning_before_clear_even_when_final_cells_clean(self):
        for warning in ('Требуется служебная проверка хранилища сохранений.','unknown outcome','Требуется\x1b[1m служебная проверка'):
            self.assertFalse(clean(('✅ Игра сохранена\r\n'+warning+'\r\n').encode()+b'\x1b[2Jplayer'))
    def test_missing_success_or_clear_refuses(self):
        for raw in (b'\x1b[2Jplayer','✅ Игра сохранена\r\n'.encode(),b'unknown\x1b[2J'):
            self.assertFalse(clean(raw))
    def test_no_unconditional_client_control_c(self):
        terminal=next(n for n in tree.body if isinstance(n,ast.ClassDef) and n.name=='Terminal')
        send=next(n for n in terminal.body if isinstance(n,ast.FunctionDef) and n.name=='send')
        self.assertEqual(send.args.kwonlyargs[0].arg,'cleanup')
        self.assertIs(send.args.kw_defaults[0].value,False)
        for n in ast.walk(tree):
            if isinstance(n,ast.Call) and isinstance(n.func,ast.Attribute) and isinstance(n.func.value,ast.Name) and n.func.value.id=='client' and n.func.attr=='send':
                self.assertGreaterEqual(len(n.args),3)
                if isinstance(n.args[0],ast.Constant):self.assertNotEqual(n.args[0].value,b'\x03')
    def test_actual_constructor_rolls_back_each_owned_acquisition(self):
        terminal=next(n for n in tree.body if isinstance(n,ast.ClassDef) and n.name=='Terminal')
        init=next(n for n in terminal.body if isinstance(n,ast.FunctionDef) and n.name=='__init__')
        for stage in ('ioctl','baseline','preflight','sidecar','popen','evidence','journal','abort-proof'):
            events=[];primary=ValueError('injected '+stage);peers=[]
            def step(name,value=None):
                events.append(name)
                if stage==name or (stage=='abort-proof' and name=='journal'):raise primary
                return value
            attrs=[0,0,0,3,0,0,[]]
            child=types.SimpleNamespace(pid=123,stdout=None,poll=lambda:None,
                terminate=lambda:events.append('terminate'),wait=lambda **kw:events.append('wait'),kill=lambda:events.append('kill'))
            def abort():
                events.append('abort')
                if stage=='journal':raise RuntimeError('injected cleanup failure')
                return {'Joined':stage!='abort-proof','EOF':True,'Errors':['unjoined'] if stage=='abort-proof' else []}
            console=types.SimpleNamespace(abort=abort)
            fakeos=types.SimpleNamespace(set_blocking=lambda *a:None,close=lambda fd:events.append(('close',fd)))
            class Path:
                def __truediv__(self,other):return self
                def mkdir(self,**kw):pass
                def __str__(self):return 'inert-path'
            root,base,out,trace,marker,trace_profile,trace_env=self.trace_case()
            ns=dict(phase_evidence=evidence,base=base,trace_profile=trace_profile,pty=types.SimpleNamespace(openpty=lambda:(10,11)),fcntl=types.SimpleNamespace(ioctl=lambda *a:step('ioctl')),
                termios=types.SimpleNamespace(TIOCSWINSZ=1,TCSANOW=2,ECHO=1,ECHONL=2,
                    tcgetattr=lambda fd:step('baseline',attrs if 'configured' not in events else [0,0,0,0,0,0,[]]),
                    tcsetattr=lambda *a:events.append('configured')),
                struct=types.SimpleNamespace(pack=lambda *a:b''),copy=copy,os=fakeos,
                terminal_fixture_admission=lambda:step('preflight',(Path(),Path(),Path())),
                sys=types.SimpleNamespace(path=[],dont_write_bytecode=False),out=out,start=0,work_seconds=660,client_cleanup_seconds=680,
                console_routes=types.SimpleNamespace(client_environment=lambda env:env),env=trace_env,ship='inert',
                subprocess=types.SimpleNamespace(Popen=lambda *a,**kw:step('popen',child),TimeoutExpired=TimeoutError),
                evidence=types.SimpleNamespace(raw=lambda *a:step('evidence')),observed_processes=[],
                process_observer=types.SimpleNamespace(register=lambda pid:pid),peers=peers,
                journal=types.SimpleNamespace(append=lambda *a:step('journal')),elapsed=lambda:0,result={})
            module=types.ModuleType('client_session');module.ClientSession=lambda *a:step('sidecar',console)
            node=ast.ClassDef(name='Terminal',bases=[],keywords=[],body=[init],decorator_list=[])
            with patch.dict(sys.modules,{'client_session':module}):
                exec(compile(ast.fix_missing_locations(ast.Module(body=[node],type_ignores=[])),'<actual-init>','exec'),ns)
                with self.assertRaises(ValueError) as caught:ns['Terminal']('client',['never executed'])
            self.assertIs(caught.exception,primary)
            detail=ns['result']['TerminalInitializationFailures'][0]
            self.assertEqual(detail['PrimaryFailure'],str(type(primary).__name__)+': '+str(primary))
            self.assertFalse(detail['NormalExit'])
            if stage=='journal':self.assertIn('injected cleanup failure',' '.join(detail['CleanupFailures']))
            if stage=='abort-proof':
                self.assertFalse(detail['SidecarAbort']['Joined']);self.assertTrue(detail['CleanupFailures'])
            if stage in ('popen','evidence'):self.assertEqual(detail['SidecarAbort'],{'Joined':True,'EOF':True,'Errors':[]})
            self.assertIn(('close',10),events);self.assertIn(('close',11),events);self.assertEqual(peers,[])
            if stage in ('popen','evidence','journal'):self.assertIn('abort',events)
            if stage in ('evidence','journal'):
                self.assertIn('terminate',events);self.assertIn('wait',events)

    def trace_case(self):
        temporary=tempfile.TemporaryDirectory();self.addCleanup(temporary.cleanup)
        root=pathlib.Path(temporary.name).resolve();base=root/'play';base.mkdir();out=root/'p01';out.mkdir()
        trace=out/'physical-trace';trace.mkdir(mode=0o700);nonce='a'*32
        marker=trace/'owner-nonce';marker.write_bytes(nonce.encode());marker.chmod(0o600)
        profile={'StopNonce':nonce,'PhysicalTrace':{'Route':'external','Directory':str(trace)}}
        env={'BOE_MAIN_STOP_DIAGNOSTIC_NONCE':nonce,'BOE_TEST_CANONICAL_LEASE_TRACE_ROUTE':'external','BOE_TEST_CANONICAL_LEASE_TRACE_DIR':str(trace)}
        return root,base,out,trace,marker,profile,env
    def trace_admission(self):
        fn=getattr(evidence,'require_physical_trace_admission',None)
        self.assertTrue(callable(fn),'Missing physical trace admission; downstream matrix UNEXECUTED')
        return fn
    def test_physical_trace_real_namespace_and_pinned_environment(self):
        fn=self.trace_admission()
        root,base,out,trace,marker,profile,env=self.trace_case()
        path=out/'launch-profile.json';raw=json.dumps(profile).encode();path.write_bytes(raw)
        env['BOE_TEST_PHYSICAL_TRACE_PROFILE_SHA256']=hashlib.sha256(raw).hexdigest()
        self.assertEqual(evidence.load_physical_trace_profile(out,env),profile)
        for invalid in (None,'','0'*64):
            changed=dict(env)
            if invalid is None:changed.pop('BOE_TEST_PHYSICAL_TRACE_PROFILE_SHA256')
            else:changed['BOE_TEST_PHYSICAL_TRACE_PROFILE_SHA256']=invalid
            with self.assertRaises(ValueError):evidence.load_physical_trace_profile(out,changed)
        path.write_bytes(raw+b' ')
        with self.assertRaises(ValueError):evidence.load_physical_trace_profile(out,env)
        path.unlink();path.symlink_to(root/'other-profile');(root/'other-profile').write_bytes(raw)
        with self.assertRaises(ValueError):evidence.load_physical_trace_profile(out,env)
        cases=('valid','missing-route','empty-route','stderr','unknown','missing-path','empty-path','other-path','other-nonce','under-base','equal-base','ancestor','linked-parent','linked-marker','fifo-marker','public-dir','public-marker','wrong-marker','short-marker','missing-marker','missing-dir','relative','missing-stop','malformed-stop','long-marker','dotdot','trailing-separator','fifo-directory')
        for case in cases:
            with self.subTest(case=case):
                root,base,out,trace,marker,profile,env=self.trace_case()
                if case=='missing-stop':profile.pop('StopNonce')
                elif case=='malformed-stop':profile['StopNonce']='not-a-nonce';env['BOE_MAIN_STOP_DIAGNOSTIC_NONCE']='not-a-nonce'
                elif case=='long-marker':marker.write_bytes(b'a'*33)
                elif case in ('dotdot','trailing-separator'):
                    path=str(out)+'/../p01/physical-trace' if case=='dotdot' else str(trace)+'/'
                    profile['PhysicalTrace']['Directory']=path;env['BOE_TEST_CANONICAL_LEASE_TRACE_DIR']=path
                elif case=='fifo-directory':marker.unlink();trace.rmdir();os.mkfifo(trace,0o600)
                elif case=='missing-route':env.pop('BOE_TEST_CANONICAL_LEASE_TRACE_ROUTE')
                elif case in ('empty-route','stderr','unknown'):env['BOE_TEST_CANONICAL_LEASE_TRACE_ROUTE']={'empty-route':'','stderr':'stderr','unknown':'other'}[case]
                elif case=='missing-path':env.pop('BOE_TEST_CANONICAL_LEASE_TRACE_DIR')
                elif case=='empty-path':env['BOE_TEST_CANONICAL_LEASE_TRACE_DIR']=''
                elif case=='other-path':
                    other=root/'other';other.mkdir(mode=0o700);(other/'owner-nonce').write_bytes(b'a'*32);(other/'owner-nonce').chmod(0o600);env['BOE_TEST_CANONICAL_LEASE_TRACE_DIR']=str(other)
                elif case=='other-nonce':env['BOE_MAIN_STOP_DIAGNOSTIC_NONCE']='b'*32;marker.write_bytes(b'b'*32)
                elif case in ('under-base','equal-base','ancestor','relative'):
                    target=base/'trace' if case=='under-base' else base if case=='equal-base' else root if case=='ancestor' else pathlib.Path('relative')
                    if case=='under-base':target.mkdir(mode=0o700);(target/'owner-nonce').write_bytes(b'a'*32);(target/'owner-nonce').chmod(0o600)
                    profile['PhysicalTrace']['Directory']=str(target);env['BOE_TEST_CANONICAL_LEASE_TRACE_DIR']=str(target)
                elif case=='linked-parent':
                    link=root/'linked';link.symlink_to(out,target_is_directory=True);profile['PhysicalTrace']['Directory']=str(link/'physical-trace');env['BOE_TEST_CANONICAL_LEASE_TRACE_DIR']=str(link/'physical-trace')
                elif case=='linked-marker':marker.unlink();marker.symlink_to(root/'real-marker');(root/'real-marker').write_bytes(b'a'*32)
                elif case=='fifo-marker':marker.unlink();os.mkfifo(marker,0o600)
                elif case=='public-dir':trace.chmod(0o755)
                elif case=='public-marker':marker.chmod(0o644)
                elif case=='wrong-marker':marker.write_bytes(b'b'*32)
                elif case=='short-marker':marker.write_bytes(b'a'*31)
                elif case=='missing-marker':marker.unlink()
                elif case=='missing-dir':marker.unlink();trace.rmdir()
                def snapshot():
                    return {str(p.relative_to(root)):(p.lstat().st_mode,os.readlink(p) if p.is_symlink() else p.read_bytes() if stat.S_ISREG(p.lstat().st_mode) else None) for p in root.rglob('*')}
                before=snapshot()
                if case=='valid':fn(base,out,profile,env)
                else:
                    with self.assertRaises(ValueError):fn(base,out,profile,env)
                self.assertEqual(snapshot(),before,'Admission mutated assets')
    def test_actual_direct_constructor_trace_refusal_precedes_acquisition(self):
        fn=self.trace_admission()
        for cls,names in [('Terminal',('client','bridge','daemon')),('ShippedRpc',('rpc',))]:
            node=next(n for n in tree.body if isinstance(n,ast.ClassDef) and n.name==cls)
            init=next(n for n in node.body if isinstance(n,ast.FunctionDef) and n.name=='__init__')
            for name in names:
                for valid in (True,False):
                    root,base,out,trace,marker,profile,env=self.trace_case()
                    if not valid:env['BOE_MAIN_STOP_DIAGNOSTIC_NONCE']='b'*32
                    acquired=[];sentinel=RuntimeError('First inert acquisition')
                    def stop(*a,**kw):acquired.append('acquire');raise sentinel
                    ns=dict(phase_evidence=evidence,base=base,out=out,trace_profile=profile,env=env,peers=[],result={},
                        pty=types.SimpleNamespace(openpty=stop),rpc_helper_arguments=lambda *a:[],ship=root,session=base/'game_session',__file__='inert',
                        console_routes=types.SimpleNamespace(client_environment=lambda value:dict(value)),subprocess=types.SimpleNamespace(Popen=stop,PIPE=1))
                    exec(compile(ast.Module(body=[init],type_ignores=[]),'<actual-init>','exec'),ns)
                    with self.assertRaises(RuntimeError if valid else ValueError) as caught:ns['__init__'](types.SimpleNamespace(),name,[]) if cls=='Terminal' else ns['__init__'](types.SimpleNamespace())
                    if valid:self.assertIs(caught.exception,sentinel)
                    self.assertEqual(acquired,['acquire'] if valid else [])
                    if not valid:self.assertEqual(list(out.glob('rpc-helper.*.log')),[])
    def test_actual_final_client_environment_is_checked_before_popen(self):
        fn=self.trace_admission()
        terminal=next(n for n in tree.body if isinstance(n,ast.ClassDef) and n.name=='Terminal');init=next(n for n in terminal.body if isinstance(n,ast.FunctionDef) and n.name=='__init__')
        for drift in (False,True):
            root,base,out,trace,marker,profile,env=self.trace_case();calls=[];sentinel=RuntimeError('First inert PTY acquisition')
            def stop(*a,**kw):calls.append('openpty');raise sentinel
            def mutate(value):return dict(value,BOE_MAIN_STOP_DIAGNOSTIC_NONCE='b'*32) if drift else dict(value)
            ns=dict(phase_evidence=evidence,base=base,out=out,trace_profile=profile,env=env,console_routes=types.SimpleNamespace(client_environment=mutate),peers=[],result={},pty=types.SimpleNamespace(openpty=stop))
            exec(compile(ast.Module(body=[init],type_ignores=[]),'<actual-full-client-init>','exec'),ns)
            with self.assertRaises(ValueError if drift else RuntimeError) as caught:ns['__init__'](types.SimpleNamespace(),'client',[])
            if not drift:self.assertIs(caught.exception,sentinel)
            self.assertEqual(calls,[] if drift else ['openpty'])
            self.assertFalse((out/'client-terminal').exists())
    def test_actual_receipt_trace_refusal_precedes_run(self):
        fn=self.trace_admission();root,base,out,trace,marker,profile,env=self.trace_case()
        matches=[]
        for parent in ast.walk(tree):
            for _,body in ast.iter_fields(parent):
                if not isinstance(body,list):continue
                for i,n in enumerate(body):
                    if isinstance(n,ast.Assign) and isinstance(n.value,ast.Call) and isinstance(n.value.func,ast.Attribute) and n.value.func.attr=='run' and any(isinstance(t,ast.Name) and t.id=='helper' for t in n.targets):
                        start=i
                        while start and not (isinstance(body[start-1],ast.Expr) and isinstance(body[start-1].value,ast.Call) and isinstance(body[start-1].value.func,ast.Attribute) and body[start-1].value.func.attr=='receipt_package_proved'):start-=1
                        matches.append(body[max(0,start-1):i+1])
        self.assertEqual(len(matches),1);calls=[]
        sentinel=RuntimeError('Run reached')
        def run(*a,**kw):calls.append(kw);raise sentinel
        pe=types.SimpleNamespace(require_physical_trace_admission=fn,receipt_package_proved=lambda *a:None)
        ns=dict(phase_evidence=pe,base=base,out=out,trace_profile=profile,env=env,receipt_helper=root/'helper.dll',package_manifest={'ReceiptHelperPins':{}},subprocess=types.SimpleNamespace(run=run),baseline=root,session=base/'game_session',receipt_check_budget=lambda *a:1,start=0,work_seconds=660,time=types.SimpleNamespace(monotonic=lambda:0))
        for valid in (True,False):
            calls.clear();env['BOE_TEST_CANONICAL_LEASE_TRACE_ROUTE']='external' if valid else ''
            with self.assertRaises(RuntimeError if valid else ValueError) as caught:exec(compile(ast.Module(body=matches[0],type_ignores=[]),'<actual-receipt-prelaunch>','exec'),ns)
            if valid:self.assertIs(caught.exception,sentinel)
            self.assertEqual(len(calls),1 if valid else 0)

    def package(self):
        temporary=tempfile.TemporaryDirectory();self.addCleanup(temporary.cleanup)
        root=pathlib.Path(temporary.name);source=root/'terminal-fixture';deps=root/'terminal-deps'
        for name in evidence.TERMINAL_SOURCE_FILES:
            p=source/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text('authored DATA')
        expected={}
        for index in range(17):
            name='node_modules/@xterm/authored/'+str(index);p=deps/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(str(index));expected[name]=evidence.sha(p.read_bytes())
        provenance={'@xterm/headless':{'version':'6.0.0','files':expected},'@xterm/addon-unicode11':{'version':'0.9.0','files':{}}}
        (source/'SelectionPrompt/terminal/provenance.json').write_text(json.dumps(provenance))
        def pins(folder):return {str(p.relative_to(folder)):evidence.sha(p.read_bytes()) for p in folder.rglob('*') if p.is_file()}
        def modes(folder):return {str(p.relative_to(folder)):stat.S_IMODE(p.stat().st_mode) for p in folder.rglob('*') if p.is_file()}
        manifest={'TerminalFixturePins':pins(source),'TerminalFixtureModes':modes(source),'TerminalDependencyPins':pins(deps),'TerminalDependencyModes':modes(deps),'TerminalNodePath':'/opt/codex/runtimes/codex-primary-runtime/dependencies/node/bin/node','TerminalNodeSHA256':evidence.TERMINAL_NODE_SHA256,'ConsoleRouteModuleSHA256':pins(source)['ProductionMain/mortal_console_routes.py']}
        return root,manifest
    def test_package_exact_join_and_changed_source_refusal(self):
        root,m=self.package();evidence.terminal_package_preflight(root,m)
        (root/'terminal-fixture/SelectionPrompt/terminal/client_ui.py').write_text('changed')
        with self.assertRaises(ValueError):evidence.terminal_package_preflight(root,m)
    def test_package_extra_link_or_mode_refused(self):
        for change in ('extra','link','mode'):
            root,m=self.package();p=root/'terminal-deps/node_modules/@xterm/authored/0'
            if change=='extra':(p.parent/'extra').write_text('unexpected')
            elif change=='link':(p.parent/'linked-dir').symlink_to(p.parent,target_is_directory=True)
            else:p.chmod(stat.S_IMODE(p.stat().st_mode)^stat.S_IXUSR)
            with self.assertRaises(ValueError):evidence.terminal_package_preflight(root,m)

class OrdinarySave(unittest.TestCase):
    """#1515 short path: actual AST and authored DATA, never the live top module."""
    def actual(self,name,ns=None):
        nodes=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name==name]
        self.assertEqual(len(nodes),1,'Missing actual '+name+'; downstream controls UNEXECUTED')
        ns={} if ns is None else ns
        exec(compile(ast.Module(body=nodes,type_ignores=[]),'<actual-'+name+'>','exec'),ns)
        return ns[name]
    def helper(self,name):
        fn=getattr(evidence,name,None)
        self.assertTrue(callable(fn),'Missing '+name+'; downstream controls UNEXECUTED')
        return fn
    def test_closed_route_and_continuation_binding(self):
        import mortal_console_routes as routes,re
        route={'Route':'ordinary-save'}
        try:observed=routes.checked_route(route)
        except ValueError as ex:self.fail('ordinary-save route missing: '+str(ex))
        self.assertEqual(observed,route)
        for bad in ({'Route':'ordinary-save','ItemId':'x'},{'Route':'ordinary-save','StorageId':'x'},{'Route':'ordinary-save-extra'}):
            with self.assertRaises(ValueError):routes.checked_route(bad)
        p=pathlib.Path(__file__).resolve().parents[3]/'specs/1515-mortal-transport-storage/manual-mm21/prepare-mm21-profile.py'
        profile=ast.parse(p.read_text());node=next(n for n in profile.body if isinstance(n,ast.FunctionDef) and n.name=='checked_continuation_binding')
        ns={'re':re};exec(compile(ast.Module(body=[node],type_ignores=[]),'<actual-binding>','exec'),ns)
        binding=dict(Phase='p02',PreviousSHA256='a'*64,GuardianSHA256='b'*64,SeedSHA256='c'*64,PhasePackageSHA256='d'*64,SourcePins={},Action='Ask, do not take.',ConsoleRoute=route)
        self.assertEqual(ns['checked_continuation_binding'](binding),binding)
        for field,value in [('PreviousSHA256',None),('Phase','p01'),('ConsoleRoute',{'Route':'ordinary-save','ItemId':'x'})]:
            bad=copy.deepcopy(binding);bad[field]=value
            with self.assertRaises(ValueError):ns['checked_continuation_binding'](bad)
        # Execute the actual guarded target lookup only. No top-level profile work.
        target=[n for n in ast.walk(profile) if isinstance(n,ast.Expr) and isinstance(n.value,ast.Call) and isinstance(n.value.func,ast.Attribute) and n.value.func.attr=='storage_target']
        self.assertEqual(len(target),1)
        parents={id(c):n for n in ast.walk(profile) for c in ast.iter_child_nodes(n)}
        guard=parents[id(target[0])]
        self.assertIsInstance(guard,ast.If,'ordinary continuation must not read inventory target')
        calls=[];ns={'route':route,'routes':types.SimpleNamespace(storage_target=lambda *a:calls.append(a))}
        exec(compile(ast.Module(body=[guard],type_ignores=[]),'<actual-profile-target-guard>','exec'),ns)
        self.assertEqual(calls,[])
    def test_actual_inventory_preflight_skips_ordinary_only(self):
        candidates=[n for n in tree.body if isinstance(n,ast.If) and any(isinstance(c,ast.Call) and isinstance(c.func,ast.Name) and c.func.id=='current_inventory_menu' for c in ast.walk(n))]
        self.assertEqual(len(candidates),1);node=candidates[0]
        sentinel=RuntimeError('inert inventory reached')
        def inventory():raise sentinel
        for route in ('ordinary-save','birth','permission-with-denial','storage-local-proof'):
            ns={'route':{'Route':route},'current_inventory_menu':inventory}
            if route in ('birth','ordinary-save'):
                try:exec(compile(ast.Module(body=[node],type_ignores=[]),'<actual-preflight>','exec'),ns)
                except RuntimeError as ex:
                    if ex is sentinel:self.fail('ordinary-save incorrectly enters inventory preflight')
                    raise
            else:
                with self.assertRaises(RuntimeError) as caught:exec(compile(ast.Module(body=[node],type_ignores=[]),'<actual-preflight>','exec'),ns)
                self.assertIs(caught.exception,sentinel)
    def save_scope(self,turn=43,uncertain=False,warning=False,multiple=False,prior_change=None):
        import io,zipfile
        tmp=tempfile.TemporaryDirectory();self.addCleanup(tmp.cleanup);session=pathlib.Path(tmp.name)
        saves=session/'saves/manual_saves';saves.mkdir(parents=True)
        (saves/'prior.zip').write_bytes(b'prior archive unchanged')
        capture=bytearray();calls=[];primary=RuntimeError('uncertain ACK')
        view=types.SimpleNamespace(prompt=lambda text,cut:text.encode() in capture[cut:])
        def options(*args):calls.append('options');capture.extend('Название сохранения:'.encode());return 0
        def send(data,label,predicate):
            self.assertTrue(predicate());cut=len(capture);calls.append(label)
            if 'Save name' in label:capture.extend('Описание (необязательно):'.encode())
            elif 'Save description' in label:
                capture.extend('✅ Игра сохранена\r\n'.encode())
                if prior_change=='overwrite':(saves/'prior.zip').write_bytes(b'overwritten')
                if prior_change=='delete':(saves/'prior.zip').unlink()
                for name in (('new.zip','extra.zip') if multiple else ('new.zip',)):
                    with zipfile.ZipFile(saves/name,'w') as z:z.writestr('save_metadata.json',json.dumps({'saveName':'s','turnNumber':turn}))
            elif 'acknowledge' in label:
                if uncertain:raise primary
                if warning:capture.extend('Требуется служебная проверка хранилища сохранений.\r\n'.encode())
                capture.extend(b'\x1b[2J\x1b[Hplayer')
            return cut
        def until(predicate,*args):self.assertTrue(predicate())
        ns=dict(session=session,client=types.SimpleNamespace(capture=capture,console=types.SimpleNamespace(view=view),send=send),
            options_choice=options,until=until,fresh_player=lambda *a:calls.append('fresh-player'),result={},
            io=io,zipfile=zipfile,strict_settings=evidence.strict_settings,sha=evidence.sha,
            clean_save_player_interval=clean,client_prompt=True,inventory_cleanup_required=False)
        return ns,calls,primary,saves
    def test_actual_shared_save_ui_current_turn_and_local_previous(self):
        # Same actual helper, not an authored implementation of Save.
        for expected in (43,42):
            ns,calls,_,saves=self.save_scope(expected)
            fn=self.actual('save_current_turn',ns);value=fn(expected)
            self.assertEqual(value['Metadata']['turnNumber'],expected)
            self.assertTrue(value['CleanOutcomeBeforeFirstPlayerClear'])
            self.assertEqual((saves/'prior.zip').read_bytes(),b'prior archive unchanged')
            self.assertEqual(sum('acknowledge' in c for c in calls),1);self.assertEqual(calls[-1],'fresh-player')
        # Keep the actual existing local caller's -1; ordinary must use current turn.
        local=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='storage_local_route')
        uses=[n for n in ast.walk(local) if isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=='save_current_turn']
        self.assertEqual(len(uses),1);self.assertEqual(ast.unparse(uses[0].args[0]),'expected_turn - 1')
    def test_actual_save_uncertain_ack_warning_multiple_and_wrong_turn(self):
        for mode in ('uncertain','warning','multiple','wrong-turn','overwrite','delete'):
            ns,calls,primary,_=self.save_scope(42 if mode=='wrong-turn' else 43,uncertain=mode=='uncertain',warning=mode=='warning',multiple=mode=='multiple',prior_change=mode)
            fn=self.actual('save_current_turn',ns)
            with self.assertRaises((RuntimeError,ValueError)) as caught:fn(43)
            if mode=='uncertain':self.assertIs(caught.exception,primary);self.assertNotIn('fresh-player',calls)
            self.assertLessEqual(sum('acknowledge' in c for c in calls),1)
            self.assertNotIn('OrdinarySaveProof',ns['result'])
    def data_case(self):
        tmp=tempfile.TemporaryDirectory();self.addCleanup(tmp.cleanup);s=pathlib.Path(tmp.name);q=s/'queue';q.mkdir()
        paths=('game_state/meta/soul_state.json','game_state/core/player_status.json','game_state/world/world_time.json',
            'game_state/control/progression_schedule.json','game_state/world/world_map.json','game_state/world/location_identity_index.json',
            'game_state/world/current_location.json','game_state/inventory/items.json','game_state/inventory/item_identity_index.json',
            'game_state/misc/vehicles.json','game_state/npcs/npc_core.json','game_state/npcs/npc_journals.json','game_state/npcs/item_journals.json')
        for name in paths:
            p=s/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(json.dumps({'scoped':name}).encode())
        story=s/'stories/mortal_life_1.jsonl';story.parent.mkdir();story.write_bytes(b'{"turn":43,"narrative":"current"}\n')
        result={'AcceptedTurnNumber':43,'AcceptedGameTurns':1,'GenuineActionsSent':1,'StoryPath':str(story),'StoryAfterSHA256':evidence.sha(story.read_bytes())}
        return s,q,story,result
    def test_scoped_archive_bytes_history_aliases_and_operational_stability(self):
        import io,zipfile,warnings
        capture=self.helper('ordinary_save_snapshot');verify=self.helper('verify_ordinary_save_archive')
        s,q,story,result=self.data_case()
        authority=s/'game_state/resources/resource_owner_authority.json';authority.parent.mkdir();authority.write_bytes(b'old authority')
        snap=capture(s,story,q,result)
        self.assertEqual(snap['Files']['game_state/world/mortal_living_world_history.json'],None)
        self.assertNotIn('game_state/resources/resource_owner_authority.json',snap['Files'])
        self.assertIn('game_state/control/progression_schedule.json',snap['Files'])
        def archive(change=None):
            b=io.BytesIO()
            with warnings.catch_warnings():
                warnings.simplefilter('ignore',UserWarning)
                with zipfile.ZipFile(b,'w') as z:
                    z.writestr('save_metadata.json',json.dumps({'saveName':'s','turnNumber':42 if change=='turn' else 43}))
                    for path,raw in snap['Files'].items():
                        if raw is not None:z.writestr(path,b'changed' if change==path else bytes.fromhex(raw))
                    z.writestr('game_state/resources/resource_owner_authority.json',b'new recomposed authority')
                    if change=='duplicate':z.writestr('save_metadata.json',b'{}')
                    if change=='duplicate-data':z.writestr('game_state/world/world_time.json',bytes.fromhex(snap['Files']['game_state/world/world_time.json']))
                    if change=='alias':z.writestr('game_state/world/../world/world_time.json',b'alias')
                    if change=='history':z.writestr('game_state/world/mortal_living_world_history.json',b'fabricated')
            return b.getvalue()
        self.assertTrue(verify(archive(),snap,43))
        for bad in ('turn','duplicate','duplicate-data','alias','history','game_state/world/world_time.json',story.relative_to(s).as_posix()):
            with self.subTest(bad=bad),self.assertRaises(ValueError):verify(archive(bad),snap,43)
        history=s/'game_state/world/mortal_living_world_history.json';history.write_bytes(b'{"actual":"history"}')
        snap=capture(s,story,q,result);self.assertTrue(verify(archive(),snap,43))
        for name in ('input/turn_request.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json','ready/turn_complete.json','ready/turn_error.json'):
            p=s/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b'{}')
            with self.subTest(name=name),self.assertRaises(ValueError):capture(s,story,q,result)
            p.unlink()
        pending=s/'game_state/control/pending_turn_snapshot';pending.mkdir()
        with self.assertRaises(ValueError):capture(s,story,q,result)
        pending.rmdir()
        for key in ('AcceptedTurnNumber','AcceptedGameTurns','GenuineActionsSent'):
            bad=dict(result);bad[key]=0
            with self.subTest(key=key),self.assertRaises(ValueError):capture(s,story,q,bad)
        (q/'unsettled').write_bytes(b'extra');self.assertNotEqual(capture(s,story,q,result),snap)
    def test_actual_postaccept_gate_precedes_save_without_renewing_deadline(self):
        for mode in ('ok','refused','late','state-drift','queue-drift'):
            events=[];now=[200.];primary=RuntimeError('consumer refusal');result={'AcceptedGameTurns':1,'AcceptedTurnNumber':43}
            snapshot_value={'Files':{'stories/mortal_life_1.jsonl':b'accepted'.hex()},'Turn':43,'Queue':{},'StoryPath':'stories/mortal_life_1.jsonl'}
            def gate(deadline):
                events.append(('consumer',deadline))
                if mode=='refused':raise primary
                if mode=='late':now[0]=761.
            def snapshot(*args):events.append('snapshot');return copy.deepcopy(snapshot_value)
            def save(turn):
                events.append(('save',turn))
                if mode=='state-drift':snapshot_value['Files']['stories/mortal_life_1.jsonl']=b'changed'.hex()
                if mode=='queue-drift':snapshot_value['Queue']['unexpected']='changed'
                return {'ArchiveBytes':b'archive','Metadata':{'turnNumber':turn},'CleanOutcomeBeforeFirstPlayerClear':True}
            def verify(*args):events.append('verify');return True
            ns=dict(route={'Route':'ordinary-save'},result=result,start=100.,work_seconds=660,time=types.SimpleNamespace(monotonic=lambda:now[0]),
                wait_daemon_consumer_returned=gate,save_current_turn=save,session='data',story='story',relay_queue='queue',expected_turn=43,
                phase_evidence=types.SimpleNamespace(ordinary_save_snapshot=snapshot,verify_ordinary_save_archive=verify),sha=evidence.sha)
            fn=self.actual('ordinary_save_after_acceptance',ns)
            if mode=='ok':
                fn();self.assertEqual(events,[('consumer',760.),'snapshot',('save',43),'verify','snapshot'])
                self.assertEqual(result['OrdinarySaveProof']['Before'],result['OrdinarySaveProof']['After'])
            else:
                with self.assertRaises((RuntimeError,TimeoutError,ValueError)) as caught:fn()
                if mode=='refused':self.assertIs(caught.exception,primary)
                if mode in ('late','refused'):self.assertEqual(events,[('consumer',760.)])
                else:self.assertEqual(events,[('consumer',760.),'snapshot',('save',43),'verify','snapshot'])
                self.assertNotIn('OrdinarySaveProof',result)
        # This helper must be invoked by actual postaccept orchestration, not tests only.
        calls=[n for n in ast.walk(tree) if isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=='ordinary_save_after_acceptance']
        self.assertEqual(len(calls),1)
    def test_actual_consumer_gate_uses_minimum_original_deadline(self):
        node=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='wait_daemon_consumer_returned')
        self.assertTrue(node.args.defaults,'pre-Save original bound parameter missing; matrix UNEXECUTED')
        for requested in (50.,900.):
            clock=[0.];proof={};result={'ActualRequest':{'sessionId':'s','requestId':'r','turnNumber':43},'ActualRequestSHA256':'a'*64,'DaemonSettlement':{'OriginalStatusRawHex':json.dumps({'pid':1}).encode().hex()}}
            limit=min(680.,requested)
            def parse(*args):clock[0]=limit;return proof
            ns=dict(route={'Route':'ordinary-save'},result=result,daemon_return_cut=0,json=json,env={},daemon_owner='owner',original={},start=0,client_cleanup_seconds=680,
                time=types.SimpleNamespace(monotonic=lambda:clock[0]),phase_evidence=types.SimpleNamespace(daemon_consumer_returned=parse),daemon=types.SimpleNamespace(capture=b'',process=types.SimpleNamespace(poll=lambda:None)),pump=lambda:None)
            fn=self.actual('wait_daemon_consumer_returned',ns)
            with self.assertRaises(TimeoutError):fn(requested)
            self.assertNotIn('DaemonConsumerReturned',result)
    def test_actual_final_success_requires_exact_save_proof(self):
        assignments=[n for n in ast.walk(tree) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Subscript) and isinstance(t.value,ast.Name) and t.value.id=='result' and isinstance(t.slice,ast.Constant) and t.slice.value=='Success' for t in n.targets)]
        self.assertEqual(len(assignments),1)
        ns=dict(result={'AcceptedGameTurns':1,'AcceptedTurnNumber':43},route={'Route':'ordinary-save'},client=None,phase_cleanup_proved=lambda r:True,phase_evidence=evidence)
        exec(compile(ast.Module(body=assignments,type_ignores=[]),'<actual-final-success>','exec'),ns)
        self.assertFalse(ns['result']['Success'],'accepted+clean without Save must not qualify ordinary-save')
        valid=self.helper('ordinary_save_proved')
        snapshot={'Turn':43,'StoryPath':'stories/mortal_life_1.jsonl','Files':{'stories/mortal_life_1.jsonl':b'accepted'.hex()},'Queue':{},'AcceptedGameTurns':1,'GenuineActionsSent':1}
        proof={'Turn':43,'Before':snapshot,'After':copy.deepcopy(snapshot),'ArchiveSHA256':evidence.sha(b'archive'),'ArchiveVerified':True,'CleanOutcomeBeforeFirstPlayerClear':True,'StorySHA256':evidence.sha(b'accepted')}
        r=dict(AcceptedTurnNumber=43,AcceptedGameTurns=1,GenuineActionsSent=1,StoryAfterSHA256=evidence.sha(b'accepted'),OrdinarySaveProof=proof)
        self.assertTrue(valid(r))
        for key,value in [('Turn',42),('ArchiveVerified',False),('ArchiveSHA256',''),('StorySHA256','0'*64),('After',{})]:
            bad=copy.deepcopy(r);bad['OrdinarySaveProof'][key]=value;self.assertFalse(valid(bad))
        ns['result']=r;exec(compile(ast.Module(body=assignments,type_ignores=[]),'<actual-final-success>','exec'),ns);self.assertTrue(r['Success'])

if __name__=='__main__':
    selected=OrdinarySave if sys.argv[1:]==['ordinary-save'] else Fences
    expected=8 if selected is OrdinarySave else 11
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(selected);assert suite.countTestCases()==expected
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() and result.testsRun==expected and not result.skipped else 1)
