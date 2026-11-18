import errno, os, pathlib, pty, select, shutil, subprocess, sys, time
repo, root, scenario = map(str, sys.argv[1:])
root=pathlib.Path(root); launcher=root/'BookOfEternityClient/Launcher';launcher.mkdir(parents=True)
bridge=root/'BookOfEternityGMBridge';bridge.mkdir();(bridge/'BookOfEternityGMBridge.dll').write_bytes(b'leaf-not-loaded')
fixture=launcher/'dispatch.ps1';shutil.copyfile(pathlib.Path(repo)/'tests/fixtures/BridgeRestart/dispatch.ps1',fixture)
master,slave=pty.openpty()
process=None
try:
    process=subprocess.Popen(['pwsh','-NoProfile','-File',str(fixture),'-Source',str(pathlib.Path(repo)/'BookOfEternityClient/Launcher/bookofeternity.ps1'),'-Session',str(root/'game_session'),'-Scenario',scenario],stdin=subprocess.DEVNULL if scenario=='redirected' else slave,stdout=slave,stderr=slave)
    os.close(slave);slave=None
    deadline=time.monotonic()+15
    with (root/'raw.log').open('wb') as raw:
        while True:
            if time.monotonic()>deadline:raise TimeoutError('Owned AST peer deadline')
            if not select.select([master],[],[],.1)[0]:continue
            try:data=os.read(master,65536)
            except OSError as error:
                if error.errno==errno.EIO:break
                raise
            if not data:break
            raw.write(data);raw.flush();sys.stdout.buffer.write(data);sys.stdout.buffer.flush()
    code=process.wait(timeout=2)
finally:
    if process is not None and process.poll() is None:process.kill();process.wait()
    if slave is not None:os.close(slave)
    os.close(master)
sys.exit(code)
