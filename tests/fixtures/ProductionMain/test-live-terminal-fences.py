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
            ns=dict(pty=types.SimpleNamespace(openpty=lambda:(10,11)),fcntl=types.SimpleNamespace(ioctl=lambda *a:step('ioctl')),
                termios=types.SimpleNamespace(TIOCSWINSZ=1,TCSANOW=2,ECHO=1,ECHONL=2,
                    tcgetattr=lambda fd:step('baseline',attrs if 'configured' not in events else [0,0,0,0,0,0,[]]),
                    tcsetattr=lambda *a:events.append('configured')),
                struct=types.SimpleNamespace(pack=lambda *a:b''),copy=copy,os=fakeos,
                terminal_fixture_admission=lambda:step('preflight',(Path(),Path(),Path())),
                sys=types.SimpleNamespace(path=[],dont_write_bytecode=False),out=Path(),start=0,work_seconds=660,client_cleanup_seconds=680,
                console_routes=types.SimpleNamespace(client_environment=lambda env:env),env={},ship='inert',
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
        cases=('valid','missing-route','empty-route','stderr','unknown','missing-path','empty-path','other-path','other-nonce','under-base','equal-base','ancestor','linked-parent','linked-marker','fifo-marker','public-dir','public-marker','wrong-marker','short-marker','missing-marker','missing-dir','relative')
        for case in cases:
            with self.subTest(case=case):
                root,base,out,trace,marker,profile,env=self.trace_case()
                if case=='missing-route':env.pop('BOE_TEST_CANONICAL_LEASE_TRACE_ROUTE')
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
                root,base,out,trace,marker,profile,env=self.trace_case();env['BOE_MAIN_STOP_DIAGNOSTIC_NONCE']='b'*32
                acquired=[]
                def stop(*a,**kw):acquired.append('acquire');raise RuntimeError('Acquisition reached')
                ns=dict(phase_evidence=evidence,base=base,out=out,trace_profile=profile,env=env,peers=[],result={},
                    pty=types.SimpleNamespace(openpty=stop),rpc_helper_arguments=stop,ship=root,session=base/'game_session',__file__='inert')
                exec(compile(ast.Module(body=[init],type_ignores=[]),'<actual-init>','exec'),ns)
                with self.assertRaises(ValueError):ns['__init__'](types.SimpleNamespace(),name,[]) if cls=='Terminal' else ns['__init__'](types.SimpleNamespace())
                self.assertEqual(acquired,[])
    def test_actual_final_client_environment_is_checked_before_popen(self):
        fn=self.trace_admission();root,base,out,trace,marker,profile,env=self.trace_case()
        terminal=next(n for n in tree.body if isinstance(n,ast.ClassDef) and n.name=='Terminal');init=next(n for n in terminal.body if isinstance(n,ast.FunctionDef) and n.name=='__init__');body=next(n for n in init.body if isinstance(n,ast.Try)).body
        first=next(i for i,n in enumerate(body) if isinstance(n,ast.Assign) and any(isinstance(x,ast.Name) and x.id=='child_env' for x in n.targets));last=next(i for i,n in enumerate(body[first:],first) if isinstance(n,ast.Assign) and any(isinstance(x,ast.Attribute) and x.attr=='process' for x in n.targets))
        calls=[]
        def popen(*a,**kw):calls.append(kw);raise RuntimeError('Popen reached')
        def mutate(value):return dict(value,BOE_MAIN_STOP_DIAGNOSTIC_NONCE='b'*32)
        ns=dict(phase_evidence=evidence,base=base,out=out,trace_profile=profile,env=env,console_routes=types.SimpleNamespace(client_environment=mutate),name='client',self=types.SimpleNamespace(slave=11),args=[],ship=root,captured_output=False,own=lambda:None,subprocess=types.SimpleNamespace(Popen=popen,PIPE=1,STDOUT=2))
        with self.assertRaises(ValueError):exec(compile(ast.Module(body=body[first:last+1],type_ignores=[]),'<actual-final-child-env>','exec'),ns)
        self.assertEqual(calls,[])
    def test_actual_receipt_trace_refusal_precedes_run(self):
        fn=self.trace_admission();root,base,out,trace,marker,profile,env=self.trace_case();env['BOE_TEST_CANONICAL_LEASE_TRACE_ROUTE']=''
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
        def run(*a,**kw):calls.append(kw);raise RuntimeError('Run reached')
        pe=types.SimpleNamespace(require_physical_trace_admission=fn,receipt_package_proved=lambda *a:None)
        ns=dict(phase_evidence=pe,base=base,out=out,trace_profile=profile,env=env,receipt_helper=root/'helper.dll',package_manifest={'ReceiptHelperPins':{}},subprocess=types.SimpleNamespace(run=run),baseline=root,session=base/'game_session',receipt_check_budget=lambda *a:1,start=0,work_seconds=660,time=types.SimpleNamespace(monotonic=lambda:0))
        with self.assertRaises(ValueError):exec(compile(ast.Module(body=matches[0],type_ignores=[]),'<actual-receipt-prelaunch>','exec'),ns)
        self.assertEqual(calls,[])

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
if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(Fences);assert suite.countTestCases()==11
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() and result.testsRun==11 and not result.skipped else 1)
